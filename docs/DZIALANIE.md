# EkstraSim — opis działania

Dokumentacja techniczna rozbudowy systemu o porównanie modeli predykcyjnych
(praca magisterska: *"Analiza porównawcza skuteczności wybranych modeli predykcyjnych dla rozgrywek piłkarskich"*).

Zamiast komentarzy w kodzie — wyjaśnienia trafiają tutaj. Każda zmiana zachowania systemu powinna aktualizować ten plik.

## Struktura rozwiązania

| Projekt | Rola |
| --- | --- |
| `EkstraSim.Backend` | API (FastEndpoints + EF Core/SQL Server); stary silnik MC + nowa warstwa badawcza |
| `EkstraSim.Frontend` | Blazor Server + MudBlazor (UI po polsku) |
| `EkstraSim.Shared` | DTO, requesty, koperta `EkstraSimResult<T>`, stałe |
| `EkstraSim.Prediction` | **(nowy)** czysty rdzeń obliczeniowy: modele predykcyjne, metryki, statystyka — bez EF/HTTP |
| `EkstraSim.Tests` | **(nowy)** testy jednostkowe rdzenia (xUnit) |

Stary silnik (`SimulatingService`) pozostaje nietknięty — nowa warstwa powstaje obok niego.

## Rdzeń obliczeniowy — wspólne typy

`EkstraSim.Prediction/Models`:

- `MatchData` — rekord meczu (id, data, kolejka, sezon, liga, drużyny, wynik). `IsPlayed` = oba wyniki niepuste.
- `TrainingOptions` — liga, badany sezon, **`SeasonChronology`** (lista id sezonów od najstarszego), parametry modeli. `PreviousSeasonId` wyliczane z chronologii, bo `Season.Id` nie gwarantuje kolejności czasowej.
- `MatchPrediction` — λ dla obu stron, P(1/X/2), typowany wynik, macierz wyników.
- `ScoreGrid` — macierz prawdopodobieństw wyników liczona **analitycznie** (iloczyn dwóch rozkładów Poissona, `MathNet.Numerics.Distributions.Poisson`), domyślnie 11×11 (0–10 goli, jak w starym silniku). Po obcięciu przy 10 golach macierz jest **normalizowana**, więc sumuje się dokładnie do 1. Brak Monte Carlo = brak szumu losowego w badaniach.
- `ModelSnapshot` — parametry modelu w danym momencie + `Distance()` (norma L2 po wspólnych kluczach) do mierzenia dryfu parametrów między kolejkami (pytanie badawcze nr 2).
- `GoalAverages` — średnie bramkowe (dom/wyjazd × zdobyte/stracone) dla ligi lub drużyny, z fallbackiem.

Interfejs `IPredictionModel`: `Train(history, options)` → `Predict(match)` → `UpdateWithRound(playedRound)` → `GetParametersSnapshot()`.

Modele nie dotykają EF ani starego singletona `SimulatingService` — dane wstrzykuje orkiestrator.

## Modele predykcyjne

### 1. Poisson (port modelu z pracy inżynierskiej)

Matematyka jak w `SimulatingService.SimulateRound`: siła ataku i obrony drużyny liczona względem średnich ligowych, mieszana z trzech horyzontów czasowych z wagami z `Constants` (bieżący sezon 0.67, poprzedni 0.3, historia 0.03).

```
λ_gospodarza = Σ_horyzont ( (śr. gole zdob. w domu gospodarza / śr. ligowa dom)
                          × (śr. gole strac. na wyjeździe gościa / śr. ligowa wyjazd-stracone)
                          × śr. ligowa dom ) × waga_horyzontu
```

Mnożniki formy (gdy `UseFormFactors`, domyślnie włączone): forma z 10 ostatnich meczów, forma dom/wyjazd z 5 ostatnich, H2H z 5 ostatnich. Każdy liczony jako `(śr. zdobyte − śr. stracone) / 2 + 1` i przycięty: forma i forma dom/wyjazd do [0.8, 1.2], H2H do [0.95, 1.05].

**Cztery świadome odstępstwa od oryginału** (istotne dla porównywalności modeli):

1. **Poprawiona waga historyczna dla gościa.** Oryginał (`SimulatingService.cs:190`) mnoży składnik historyczny `awayPred` przez `PreviousSeasonScale` (0.3) zamiast `HistoricalScale` (0.03) — wagi sumują się tam do 1.27, co systematycznie zawyża oczekiwane gole gościa. Port używa poprawnej wagi (suma = 1.0).
2. **Średnie liczone dynamicznie względem badanego sezonu**, nie z kolumn w bazie wypełnianych przez `TeamService.UpdateAverageTeamGoals` z zahardkodowanymi `SeasonId == 6` / `== 1`. Dzięki temu ten sam model działa dla każdego sezonu bez ręcznych przeliczeń.
3. **Fallback per strona.** Oryginał przy pustym koszyku podmienia wszystkie cztery średnie na ligowe (`if (homeMatches <= 0 || awayMatches <= 0)`). Port podmienia tylko brakującą stronę — identyczne zachowanie gdy oba koszyki niepuste, dokładniejsze gdy drużyna ma tylko mecze domowe (start sezonu, beniaminki).
4. **Mnożniki formy zawsze aktywne i filtrowane po dacie.** Oryginał liczył je tylko dla `numberOfSimulations > 1` (artefakt trybu MC) i po całym cache'u meczów. Port stosuje je zawsze i bierze wyłącznie mecze o dacie **wcześniejszej** niż mecz przewidywany — bez tego filtra badanie walk-forward miałoby wyciek danych z przyszłości.

Model trzyma własną listę wchłoniętych meczów (`HashSet` po id — powtórne podanie tej samej kolejki jest bezpieczne) i przelicza średnie po każdym `Train`/`UpdateWithRound`.

### 2. Dixon-Coles (1997)

Parametry: siła ataku αᵢ i obrony βᵢ dla każdej drużyny, przewaga gospodarzy γ, korekta remisów ρ.

```
λ_gospodarza = α_gospodarza × β_gościa × γ
λ_gościa     = α_gościa     × β_gospodarza
```

Dopasowanie metodą największej wiarygodności (`NelderMeadSimplex` z Math.NET) na logarytmicznej wiarygodności z korektą τ dla czterech niskich wyników (0:0, 0:1, 1:0, 1:1) — to ona odwzorowuje nadwyżkę remisów, której czysty Poisson nie widzi.

Szczegóły implementacyjne:

- **Identyfikowalność.** Likelihood ma jedną redundancję: α→cα przy β→β/c nie zmienia λ. Rozwiązywana normalizacją średniej ataków do 1 wewnątrz funkcji celu, z jednoczesnym przemnożeniem obron przez tę samą stałą (inaczej λ gościa by się przeskalowało). Poziom bramkowy gościa siedzi więc w β, a stosunek dom/wyjazd w γ.
- **Parametryzacja.** α, β, γ optymalizowane w logarytmach (dodatniość gwarantowana), ρ jako `0.3 · tanh(r)` — trzyma korektę w rozsądnym zakresie bez twardych więzów. Punkty, w których τ ≤ 0 lub λ ≤ 0, dostają karę `1e12`.
- **Wygaszanie czasowe.** Waga meczu `φ(t) = exp(−ξ · Δdni)` względem najnowszego znanego meczu; ξ z `TrainingOptions.TimeDecayXi` (domyślnie 0.0065 ≈ półokres ~107 dni).
- **Regularyzacja ridge.** Kara `RidgeLambda · wᵢ · (log²αᵢ + log²βᵢ)`, gdzie `wᵢ = 1/(1 + efektywna liczba meczów drużyny)`. Ściąga do średniej ligowej (α=β=1) tym mocniej, im mniej danych ma drużyna — to obsługuje beniaminków na starcie sezonu.
- **Punkt startowy** liczony analitycznie ze średnich bramkowych (α ze zdobytych, β ze straconych przeskalowanych do poziomu goli gościa, γ ze stosunku dom/wyjazd, ρ = −0.03). Bez dobrego startu Nelder-Mead w ~38 wymiarach nie zbiega sensownie.
- `UpdateWithRound` = pełne ponowne dopasowanie (przy tej liczbie parametrów jest tanie).
- Drużyny nieobecne w treningu dostają α = β = 1.

### 3. ELO → gole

Dwa etapy, bo sam ELO daje tylko oczekiwany „wynik punktowy", a badania wymagają rozkładu wyników bramkowych.

**Etap 1 — ranking.** Chronologiczny replay wszystkich znanych meczów tą samą formułą co `TeamService.BaseRecalculateEloRankingAllTeamsAsync`:

```
dr        = ELO_gosp − ELO_gość + 100
W_e       = 1 / (10^(−dr/400) + 1)
G         = 1.0 (różnica 1 bramki), 1.5 (różnica 2), w przeciwnym razie (11 + różnica) / 8
ELO_gosp += K · G · (W − W_e)        K = 10
ELO_gość -= K · G · (W − W_e)
```

Ranking jest więc **zerosumowy** — suma ocen wszystkich drużyn nie zmienia się w trakcie replayu. Nowe drużyny startują z 1300.

Zachowana dziwność oryginału: dla remisu (różnica 0 bramek) `G` wpada w gałąź `(11 + 0) / 8 = 1.375`, czyli remis waży więcej niż zwycięstwo jedną bramką. Nie poprawiam tego, bo porównanie ma dotyczyć modelu faktycznie użytego w pracy inżynierskiej — ale warto to opisać w tekście pracy.

**Etap 2 — mapowanie na gole.** Regresja Poissona (log-link) z różnicy rankingów na oczekiwane bramki, dopasowana metodą Newtona-Raphsona (`PoissonRegression`, IRLS na układzie 2×2):

```
x       = (ELO_gosp − ELO_gość) / 400
λ_gosp  = exp(a₀ + a₁·x)
λ_gość  = exp(b₀ + b₁·x)
```

Kluczowe: cechą `x` jest różnica rankingów **przed** meczem, zbierana w trakcie replayu — nigdy po aktualizacji. Bez tego model uczyłby się na wyniku, który ma przewidzieć.

Do regresji wchodzą wyłącznie mecze badanej ligi; ranking aktualizują wszystkie wchłonięte mecze (także z innych rozgrywek, jeśli takie trafią do danych). Przy mniej niż 20 próbkach regresja degeneruje się do samego wyrazu wolnego (średnia bramkowa), co chroni start sezonu.

`UpdateWithRound` przelicza replay od zera — deterministycznie, niezależnie od kolejności wywołań.

## Warstwa badawcza (walk-forward)

### Idea

Trening: historia sprzed badanego sezonu + runda jesienna. Ewaluacja: kolejne kolejki rundy wiosennej, przy czym **po każdej rozegranej kolejce modele dotrenowują się jej wynikami** i dopiero potem przewidują następną.

Czysta pętla siedzi w `EkstraSim.Prediction/Evaluation/WalkForwardEvaluator.cs` (bez EF, testowalna):

```
model.Train(historia, opcje)
dla każdej kolejki R rundy wiosennej:
    predykcje  = mecze(R).Select(model.Predict)      ← model nie zna jeszcze wyników R
    oceny      = metryki(predykcje, faktyczne wyniki)
    model.UpdateWithRound(mecze(R))                  ← dopiero teraz wchłania wyniki
    dryf       = ||parametry_po − parametry_przed||
```

**Brak wycieku danych z przyszłości jest własnością konstrukcji**, nie sprawdzeniem: model widzi wyniki kolejki R wyłącznie po tym, jak wszystkie predykcje dla R zostały już policzone. Testy `FirstRoundPredictionUsesOnlyTrainingHistory` i `SecondRoundPredictionSeesOnlyTheFirstEvaluatedRound` porównują wynik pętli z modelem trenowanym ręcznie na dokładnie tym zakresie danych.

`BuildHistory` bierze rozegrane mecze z sezonów wcześniejszych w chronologii **oraz** kolejki ≤ odcięcie z sezonu badanego. `BuildEvaluationSet` bierze kolejki > odcięcie, **tylko rozegrane** — dzięki temu trwający sezon (np. 2026/27) ocenia się na tym, co już się odbyło, a nierozegrane kolejki są po prostu pomijane.

### Kolejka odcięcia i beniaminki

- **Odcięcie** domyślnie wykrywane automatycznie (`SeasonCalendar.DetectSplit`): największa przerwa między datami kolejnych kolejek = przerwa zimowa. Można nadpisać ręcznie w żądaniu.
- **Beniaminki** (`PromotedTeamsService`): drużyny mające mecze w sezonie S i żadnego w S−1. Chronologia sezonów liczona z **najwcześniejszej daty meczu**, nie z `Season.Id` ani nazwy — Id nie gwarantuje kolejności czasowej. Lista jest zapisywana jako snapshot JSON na rekordzie badania, żeby wynik dał się odtworzyć nawet po dodaniu nowych sezonów.

### Encje i przepływ

Migracja `research_evaluation_runs` jest **wyłącznie addytywna** — trzy nowe tabele, zero zmian w istniejących:

| Tabela | Zawartość |
| --- | --- |
| `ModelEvaluationRuns` | parametry badania, lista modeli, opcje JSON, snapshot beniaminków, status (`Pending`/`Running`/`Completed`/`Failed`), znaczniki czasu |
| `ModelPredictions` | jedna predykcja = model × mecz: λ, P(1/X/2), typowany wynik, macierz 11×11 jako JSON, faktyczny wynik i wszystkie metryki per mecz |
| `ModelRoundMetrics` | agregaty per model × kolejka + `ParameterDrift` (dla pytania nr 2) |

Uruchomienie jest **asynchroniczne**: endpoint tworzy rekord ze statusem `Pending`, zwraca jego Id i oddaje pracę `ResearchRunLauncher` (singleton), który otwiera świeży scope DI — bez tego scoped `DbContext` zniknąłby razem z żądaniem. Frontend odpytuje status. Predykcje zapisywane są partiami po 500 wierszy.

### Endpointy

| Metoda | Trasa (bez prefiksu `/v1`) | Rola |
| --- | --- | --- |
| POST | `api/research/import-csv` | import sezonu z CSV; auto-tworzy `Season` i brakujące `Team` (ELO 1300), idempotentny po (kolejka, gospodarz, gość), **uzupełnia wyniki** przy ponownym imporcie trwającego sezonu |
| GET | `api/research/season-structure/{SeasonId}/{LeagueId}` | liczba drużyn/kolejek, podział jesień-wiosna, beniaminki — zasila domyślne wartości formularza |
| GET | `api/research/models` | lista dostępnych modeli |
| POST | `api/research/runs` | start badania |
| GET | `api/research/runs` | lista badań (filtry: liga, sezon) |
| GET | `api/research/runs/{RunId}` | status i podsumowanie |
| GET | `api/research/runs/{RunId}/round-metrics` | metryki per kolejka (dane do wykresów) |
| GET | `api/research/runs/{RunId}/predictions` | predykcje (filtry: model, kolejka) |
| GET | `api/research/runs/{RunId}/comparison` | podsumowania, testy istotności, beniaminki, stabilność |
| PUT | `api/research/predict-round` | predykcja jednej kolejki wybranym modelem, bez zapisu — działa też dla kolejek nierozegranych |

Import CSV czyta plik jawnie jako **UTF-8** (pliki w `Database/CSV/` są w UTF-8; domyślne kodowanie konsoli Windows psuje polskie znaki w nazwach drużyn). Przyjmuje albo ścieżkę serwerową, albo treść pliku w `CsvContent`.

Stary, zepsuty `CSVService` i endpoint `api/importcsv` pozostają nietknięte.

## Metryki i testy statystyczne

`EkstraSim.Prediction/Metrics` — liczone per mecz, agregowane średnią (`MetricSummary`):

| Metryka | Zakres | Interpretacja |
| --- | --- | --- |
| **Brier** (3-klasowy) | 0–2 | suma kwadratów błędów po 1/X/2; 0 = pewna trafna predykcja |
| **RPS** (Ranked Probability Score) | 0–1 | jak Brier, ale karze *odległość* pomyłki — pomylenie zwycięstwa gospodarza z wyjazdowym boli bardziej niż z remisem. Standard w literaturze piłkarskiej |
| **Log-loss** | 0–∞ | −ln(P przypisanego faktycznemu wynikowi); prawdopodobieństwa podłogowane na 1e-15, żeby nie było nieskończoności |
| **Trafność 1X2** | 0–1 | czy argmax rozkładu = faktyczny rezultat |
| **Trafność dokładnego wyniku (top-1 / top-3)** | 0–1 | czy faktyczny wynik był najbardziej prawdopodobny / w trzech najbardziej prawdopodobnych — to odpowiedź na pytanie badawcze nr 4 |
| **Średnie P dokładnego wyniku** | 0–1 | ile prawdopodobieństwa model przypisał temu, co faktycznie padło (wyższe = lepsze) |

`MetricKind.LowerIsBetter()` rozstrzyga kierunek — bez tego porównania modeli myliłyby zwycięzcę dla metryk „im więcej tym lepiej".

`EkstraSim.Prediction/Statistics`:

- **Wilcoxon signed-rank** (`WilcoxonSignedRankTest`) — pytanie nr 1. Test sparowany na różnicach metryk **na tych samych meczach**, aproksymacja normalna z korektą na wiązania i korektą ciągłości. Różnice zerowe odrzucane; poniżej 6 par wynik oznaczany jako `IsConclusive = false` (nie udajemy istotności na małej próbie).
- **Mann-Whitney U** (`MannWhitneyUTest`) — pytanie nr 3. Test dla prób niezależnych (mecze z beniaminkiem vs pozostałe), ta sama aproksymacja z korektami.
- **Holm-Bonferroni** (`HolmCorrection`) — poprawka na wielokrotne porównania (3 pary modeli, a przy podziale na okna kolejek więcej). Bez niej przy kilkunastu testach fałszywe „istotności" pojawiają się same.
- **`ModelComparison`** — składa całość: `Pairwise` (każda para modeli na wspólnym podzbiorze meczów) i `PromotedVersusRest` (beniaminki vs reszta, opcjonalnie w oknach kolejek, żeby zobaczyć *kiedy* różnica zanika).
- **`StabilityAnalysis`** — pytanie nr 2. Średnia krocząca metryki i dryfu parametrów, oraz `StabilisedFromRound`: pierwsza kolejka, **od której do końca** kroczący dryf nie przekracza progu. Świadomie nie jest to „pierwszy spadek poniżej progu" — chwilowe wyciszenie, po którym parametry znów skaczą, nie jest stabilnością.

Ranking z wiązaniami (`Ranking.AverageRanks`) zwraca rangi średnie i sumę `t³−t` potrzebną do korekty wariancji w obu testach.

## Interfejs badawczy

Nowa sekcja w nawigacji, obok istniejących stron symulacji MC (te działają bez zmian):

| Strona | Trasa | Zawartość |
| --- | --- | --- |
| `ResearchRunsPage` | `/research` | lista badań ze statusem + formularz nowego badania |
| `ResearchRunDetailsPage` | `/research/{RunId}` | wykresy, podsumowania, testy istotności, stabilność, beniaminki, predykcje |
| `ModelRoundPredictionPage` | `/model-prediction` | predykcja jednej kolejki wybranym modelem |

`EvaluationRunForm` po wyborze sezonu odpytuje `season-structure` i pokazuje wykrytą przerwę zimową oraz beniaminków — kolejkę odcięcia można zostawić puste (auto) albo nadpisać. Parametry modeli (mnożniki formy, ξ, ridge, próg i okno stabilności) siedzą w zwiniętym panelu, żeby nie zaśmiecać formularza.

Strona szczegółów ma sześć zakładek: **Przebieg w sezonie** (dwa wykresy `MudChart` — wybrana metryka po kolejkach i dryf parametrów, po jednej serii na model), **Podsumowanie**, **Istotność różnic**, **Stabilność**, **Beniaminki**, **Predykcje** (z filtrem modelu i kolejki oraz rozwijaną macierzą wyników; obramowana komórka = faktyczny wynik). Selektor metryki przełącza jednocześnie wykres i wszystkie testy — backend przelicza je pod wybraną metrykę.

### Dwie poprawki w istniejącym kodzie frontendu

1. **`HttpServiceHelper` nie obsługiwał koperty w POST/PUT.** `SendPostAsync<T>` deserializował ciało odpowiedzi jako `T`, choć backend zwraca `EkstraSimResult<T>`, a `SendPutAsync<T>` w ogóle odrzucał ciało (`Data = default`). Dotąd nie miało to znaczenia, bo istniejące PUT-y są typu „odpal i zapomnij". Dodane `SendPostEnvelopeAsync<T>` / `SendPutEnvelopeAsync<T>` czytają kopertę tak samo jak `SendGetAsync`. Stare metody nietknięte, żeby nie ruszać istniejących wywołań.
2. **Adres API z konfiguracji.** `EkstraSim.Frontend/Program.cs` czyta klucz `ApiBaseAddress`, z produkcyjnym URL-em Azure jako wartością domyślną. Bez tego front lokalnie zawsze strzelał w chmurę.

## Dane

- CSV: `Database/CSV/*.csv`, format `id,data,kolejka,gospodarz,gole,gość,gole,url_obrazka`.
- Sezony 2019/20–2020/21: 16 drużyn, format z podziałem na grupy (~37 kolejek). Od 2021/22: 18 drużyn, 34 kolejki.
- Beniaminki sezonu S = drużyny mające mecze w S, bez meczów ligowych w S−1.
- Podział jesień/wiosna wykrywany po największej przerwie między datami kolejek (przerwa zimowa).
