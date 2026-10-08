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
- `ModelSnapshot` — parametry modelu w danym momencie. Dwie miary odległości: `Distance()` (surowa norma L2 po wspólnych kluczach) i **`NormalisedDistance()`**, która mierzy dryf parametrów między kolejkami (pytanie badawcze nr 2). Szczegóły i powód rozdzielenia — sekcja [Metryka dryfu parametrów](#metryka-dryfu-parametrów).
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

Model trzyma własną listę wchłoniętych meczów (`HashSet` po id — powtórne podanie tej samej kolejki jest bezpieczne) i przelicza średnie po każdym `Train`/`UpdateWithRound`. Skutek uboczny: poprawiony wynik meczu o już znanym id jest ignorowany.

Średnia ligowa, która służy za normalizator sił i wartość awaryjną, jest **wspólna dla wszystkich wchłoniętych sezonów** — nie jest liczona osobno dla każdego horyzontu. Okna formy przekraczają granice sezonów.

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
- **Regularyzacja ridge.** Kara `RidgeLambda · wᵢ · (log²αᵢ + log²βᵢ)`, gdzie `wᵢ = 1/(1 + efektywna liczba meczów drużyny)` (efektywna = suma wag czasowych), domyślnie `RidgeLambda = 0.05`. Ściąga do α = β = 1 tym mocniej, im mniej danych ma drużyna — to obsługuje beniaminków na starcie sezonu. Uwaga: przy normalizacji średniej ataku do 1 poziom goli gościa siedzi w β (typowo ~1,1–1,2), więc **β = 1 nie jest średnią ligową**, tylko obroną lepszą niż przeciętna — dotyczy to zarówno celu ridge, jak i drużyn nieobecnych w treningu.
- **Punkt startowy** liczony analitycznie ze średnich bramkowych (α ze zdobytych, β ze straconych przeskalowanych do poziomu goli gościa, γ ze stosunku dom/wyjazd, ρ = −0.03). Bez dobrego startu Nelder-Mead nie zbiega sensownie. Wymiar problemu to 2N + 2, gdzie N to **wszystkie drużyny z historii**, nie tylko z badanego sezonu — dla 2024/25 i 2025/26 N = 28, czyli **58 wymiarów**. Logarytmy parametrów są przycinane do ±20.
- `UpdateWithRound` = pełne ponowne dopasowanie, za każdym razem od analitycznego punktu startowego (nie od poprzedniego optimum).
- Drużyny nieobecne w treningu dostają α = β = 1.
- **Znane ograniczenie — cichy powrót do punktu startowego.** `NelderMeadSimplex(1e-7, 20000)` po przekroczeniu limitu iteracji rzuca `MaximumIterationsException`, a `catch (Exception)` w `Fit` bez śladu podstawia punkt startowy. Pomiar z 2026-10-05 na danych z bazy (te same opcje co w badaniach): **13 z 17** dopasowań w 2024/25 i **7 z 17** w 2025/26 kończy się w punkcie startowym; udane dopasowania potrzebowały 13,7–18,2 tys. iteracji. W tych kolejkach model jest estymatorem momentów, nie MLE. Zysk log-likelihood optimum nad startem jest mały, ale wpływ na wyniki trzeba zmierzyć po naprawie (stan i plan: `STAN_PROJEKTU.md`).

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
    dryf       = NormalisedDistance(parametry_przed, parametry_po)   ← patrz „Metryka dryfu parametrów"
```

**Brak wycieku danych z przyszłości jest własnością konstrukcji** — ale **po numerze kolejki, nie po dacie**: model widzi wyniki kolejki R wyłącznie po tym, jak wszystkie predykcje dla R zostały już policzone. Kolejki są przetwarzane w kolejności numerów, a historia to kolejki ≤ odcięcie, więc **mecz przełożony** (np. z kolejki 16 rozegrany w marcu) trafia do treningu albo jest wchłaniany razem ze swoją kolejką, choć odbył się po meczach, które model jeszcze przewiduje. Pomiar w bazie: 2024/25 — żadnego takiego meczu; 2025/26 — 2 mecze w treningu (wpływ na 63 ze 144 predykcji) i 3 w pętli (45 predykcji). Jedynie mnożniki formy Poissona filtrują po dacie. Wariant w pełni czasowy wymagałby historii ograniczonej do `Date < mecz.Date` dla każdego przewidywanego meczu.

Testy `FirstRoundPredictionUsesOnlyTrainingHistory` i `SecondRoundPredictionSeesOnlyTheFirstEvaluatedRound` porównują wynik pętli z modelem trenowanym ręcznie na dokładnie tym zakresie danych — ale ich fixture powtarza w każdej kolejce identyczne wyniki, więc wchłonięcie kolejki niczego w modelu nie zmienia. **Nie wykryłyby wycieku** (pętla wchłaniająca wyniki przed predykcją też by przeszła); dotyczą tylko Poissona.

`BuildHistory` bierze rozegrane mecze z sezonów wcześniejszych w chronologii **oraz** kolejki ≤ odcięcie z sezonu badanego. `BuildEvaluationSet` bierze kolejki > odcięcie, **tylko rozegrane** — dzięki temu trwający sezon (np. 2026/27) ocenia się na tym, co już się odbyło, a nierozegrane kolejki są po prostu pomijane.

### Kolejka odcięcia i beniaminki

- **Odcięcie** domyślnie wykrywane automatycznie (`SeasonCalendar.DetectSplit`): największa przerwa między datami kolejnych kolejek = przerwa zimowa. Można nadpisać ręcznie w żądaniu. Wyjątek: w **2019/20** największa przerwa to pauza COVID po kolejce 26 (81 dni), nie zimowa po kolejce 20 (48 dni) — dla tego sezonu odcięcie trzeba podać ręcznie. Przełożony mecz z ostatniej kolejki jesiennej rozegrany wiosną też potrafi przesunąć wykryty podział.
- **Beniaminki** (`PromotedTeamsService`): drużyny mające mecze w sezonie S i żadnego w S−1. Chronologia sezonów liczona z **najwcześniejszej daty meczu**, nie z `Season.Id` ani nazwy — Id nie gwarantuje kolejności czasowej. Lista jest zapisywana jako snapshot JSON na rekordzie badania, żeby wynik dał się odtworzyć nawet po dodaniu nowych sezonów.

### Encje i przepływ

Migracja `research_evaluation_runs` jest **wyłącznie addytywna** — trzy nowe tabele, zero zmian w istniejących:

| Tabela | Zawartość |
| --- | --- |
| `ModelEvaluationRuns` | parametry badania, lista modeli, opcje JSON, snapshot beniaminków, status (`Pending`/`Running`/`Completed`/`Failed`), znaczniki czasu |
| `ModelPredictions` | jedna predykcja = model × mecz: λ, P(1/X/2), typowany wynik, macierz 11×11 jako JSON, faktyczny wynik i wszystkie metryki per mecz |
| `ModelRoundMetrics` | agregaty per model × kolejka + `ParameterDrift` (dla pytania nr 2) |

Uruchomienie jest **asynchroniczne**: endpoint tworzy rekord ze statusem `Pending`, zwraca jego Id i oddaje pracę `ResearchRunLauncher` (singleton), który w `Task.Run` (fire-and-forget, bez anulowania) otwiera świeży scope DI — bez tego scoped orkiestrator zniknąłby razem z zakresem żądania. Frontend **nie odpytuje statusu automatycznie** — strony pokazują przycisk „Odśwież", dopóki run nie jest `Completed`. Predykcje zapisywane są partiami po 500 wierszy, każda partia w osobnej transakcji.

Znane słabości tego przepływu: status `Running` jest zapisywany poza `try`, a `catch` ponawia zapis na tym samym kontekście, więc błąd zapisu (np. duplikat nazwy modelu `["Poisson","poisson"]` naruszający unikalny indeks) zostawia run w `Running` na zawsze, z wyjątkiem tylko w logu. Nie ma odzyskiwania po restarcie. Endpointy wyników nie sprawdzają statusu, więc częściowo zapisany run jest widoczny przez API. Run zapisuje opcje z żądania i snapshot beniaminków, ale **nie wersję algorytmu** — zmiany kodu (jak zmiana formuły dryfu) są w danych niewidoczne.

### Endpointy

| Metoda | Trasa (bez prefiksu `/v1`) | Rola |
| --- | --- | --- |
| POST | `api/research/import-csv` | import sezonu z CSV; auto-tworzy `Season` i brakujące `Team` (ELO 1300), idempotentny po (kolejka, gospodarz, gość), **uzupełnia wyniki** przy ponownym imporcie trwającego sezonu |
| GET | `api/research/season-structure/{SeasonId}/{LeagueId}` | liczba drużyn/kolejek (`roundCount` = najwyższy numer kolejki), podział jesień-wiosna, beniaminki — formularz je wyświetla, a odcięcie „auto" liczy backend tą samą metodą |
| GET | `api/research/models` | lista dostępnych modeli |
| POST | `api/research/runs` | start badania |
| GET | `api/research/runs` | lista badań (filtry: liga, sezon) |
| GET | `api/research/runs/{RunId}` | status i podsumowanie |
| GET | `api/research/runs/{RunId}/round-metrics` | metryki per kolejka (dane do wykresów) |
| GET | `api/research/runs/{RunId}/predictions` | predykcje (filtry: model, kolejka) |
| GET | `api/research/runs/{RunId}/comparison` | podsumowania, testy istotności, beniaminki, stabilność |
| PUT | `api/research/predict-round` | predykcja jednej kolejki wybranym modelem, bez zapisu — działa też dla kolejek nierozegranych. Domyślne odcięcie = `Round − 1`; **odcięcie ≥ `Round` nie jest odrzucane** (wyciek wprost). Jedno `Train` zamiast `Train` + `UpdateWithRound`, więc wynik nie odtwarza predykcji z runu; mecze nierozegrane mają w polach wyniku i metryk 0, nie `null` |

Import CSV czyta plik jawnie jako **UTF-8** (pliki w `Database/CSV/` są w UTF-8; domyślne kodowanie konsoli Windows psuje polskie znaki w nazwach drużyn). Przyjmuje albo ścieżkę serwerową, albo treść pliku w `CsvContent`.

### Kanonizacja nazw drużyn

`EkstraSim.Shared/TeamNameAliases.cs` mapuje nazwy z plików źródłowych na nazwy **faktycznie występujące w tabeli `Teams`**. Powód: TheSportsDB podaje część klubów po angielsku (`Legia Warsaw`), a część bez znaków diakrytycznych (`Wisla Plock`) — i robi to **niespójnie między plikami** (`Wisla Plock` w eksporcie 2025/26, `Wisła Płock` w 2026/27). `Normalise` to tylko `Trim().ToLowerInvariant()`, więc bez mapy każdy taki rozjazd tworzyłby drugą drużynę z ELO 1300 obok istniejącej i rozrywał historię meczów na dwa byty.

Obowiązujące mapowania:

| Z pliku | Na nazwę z bazy | Skąd rozjazd |
| --- | --- | --- |
| `Legia Warsaw` | `Legia Warszawa` | angielska nazwa w obu nowych eksportach |
| `Wisla Plock` | `Wisła Płock` | eksport 2025/26 bez diakrytyków |
| `Cracovia Kraków` | `Cracovia` | **stare** pliki CSV 2019/20–2024/25 |

Ostatni wiersz jest kontrintuicyjny i dlatego wart uwagi: nazwą kanoniczną jest krótkie `Cracovia`, bo tak klub nazywa się w bazie (`Teams.Id = 14`). To **stare** pliki CSV mają rozjazd (`Cracovia Kraków`), a nowe eksporty z TheSportsDB trafiają w bazę bezpośrednio. Kierunek aliasu ustalono zapytaniem do bazy, nie z plików — odwrotne mapowanie zakładałoby duplikat `Cracovia Kraków` obok istniejącej drużyny z ELO 1335.

Mapa jest świadomie **jawną listą aliasów**, nie dopasowaniem z pominięciem diakrytyków — to drugie załatwiłoby `Wisla Plock`, ale nie `Legia Warsaw` ani `Cracovia Kraków`, a przy okazji groziłoby scaleniem `Wisła Kraków` z `Wisła Płock`.

Stan sprawdzony: po kanonizacji wszystkie nazwy z ośmiu plików CSV trafiają w istniejące drużyny. Jedyny wyjątek to `Wieczysta Kraków` w 2026/27 — faktyczny beniaminek, którego utworzenie jest prawidłowe.

`Canonicalise` jest stosowana w obu miejscach, gdzie nazwa z pliku staje się kluczem drużyny: przy zakładaniu brakujących drużyn i przy wyszukiwaniu drużyny dla wiersza. `CsvImportService.Normalise` deleguje do `TeamNameAliases.Normalise`, żeby klucze mapy i klucze wyszukiwania nie mogły się rozjechać.

Siatką bezpieczeństwa dla nazw jeszcze nieznanych jest ostrzeżenie: **każde** utworzenie drużyny dopisuje wpis do `CsvImportResultDTO.Warnings`. Utworzenie jest prawidłowe dla faktycznego beniaminka (`Wieczysta Kraków` w 2026/27), ale wygląda identycznie jak rozjazd nazwy — więc import to zgłasza zamiast decydować po cichu. Nowy alias dopisuje się wtedy do `TeamNameAliases`.

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
- **Mann-Whitney U** (`MannWhitneyUTest`) — pytanie nr 3. Test dla prób niezależnych (mecze z beniaminkiem vs pozostałe), ta sama aproksymacja z korektami. Każda grupa musi mieć co najmniej 4 wartości, inaczej wynik jest nierozstrzygający (p = 1).
- **Holm-Bonferroni** (`HolmCorrection`) — poprawka na wielokrotne porównania (3 pary modeli, a przy podziale na okna kolejek więcej). Bez niej przy kilkunastu testach fałszywe „istotności" pojawiają się same. Testy nierozstrzygające wchodzą z p = 1 i zwiększają liczbę porównań m, co czyni korektę bardziej konserwatywną. Okna beniaminków to kolejne porcje po 5 ocenianych kolejek — dla kolejek 19–34 ostatnie okno ma tylko kolejkę 34.
- `BetterModel` w porównaniu par wybierany jest po **średniej** metryki, a Wilcoxon testuje położenie rang — przy skośnych rozkładach kierunki mogą się różnić. Metryki trafności (1X2, top-1, top-3) nie są `MetricKind`, więc nie podlegają testom istotności.
- **`ModelComparison`** — składa całość: `Pairwise` (każda para modeli na wspólnym podzbiorze meczów) i `PromotedVersusRest` (beniaminki vs reszta, opcjonalnie w oknach kolejek, żeby zobaczyć *kiedy* różnica zanika).
- **`StabilityAnalysis`** — pytanie nr 2. Średnia krocząca metryki i dryfu parametrów, oraz `StabilisedFromRound`: pierwsza kolejka, **od której do końca** kroczący dryf nie przekracza progu. Świadomie nie jest to „pierwszy spadek poniżej progu" — chwilowe wyciszenie, po którym parametry znów skaczą, nie jest stabilnością. Ograniczenie tej konstrukcji przy wspólnym progu — patrz niżej.

## Metryka dryfu parametrów

`ModelSnapshot.NormalisedDistance()` liczy dryf jako **średniokwadratową zmianę parametrów wyrażoną w jednostkach ich własnego rozrzutu**, osobno w każdej rodzinie parametrów: dla rodziny f — `r_f = RMS(zmian) / σ_f`, gdzie σ_f to odchylenie standardowe (populacyjne) wartości rodziny w **poprzednim** snapshocie. Wyniki rodzin składa jako ważony RMS: `√(Σ n_f·r_f² / Σ n_f)`, gdzie n_f to liczba parametrów rodziny.

Rodzina to klucz z usuniętymi segmentami liczbowymi: `rating_5` → `rating`, `team_5_home_scored` → `team_home_scored`. Brane są tylko rodziny liczące **więcej niż jeden** parametr — czyli rodziny „per drużyna". Gdy takich nie ma, wszystkie klucze traktowane są jako jedna grupa.

### Dlaczego nie surowa norma L2

Poprzednia miara (`Distance()`, sama norma L2 po różnicach) była **nieporównywalna między modelami** z trzech niezależnych powodów:

1. **Skale między modelami.** Parametry Elo to oceny rankingowe rzędu 1300, Dixona-Colesa — siły w okolicy 1,0. Dryf Elo wychodził 30–50× większy niezależnie od tego, co się faktycznie zmieniło.
2. **Skale wewnątrz modelu.** Snapshot Elo miesza 28 ocen (wszystkie drużyny z historii, ~1300) z 4 współczynnikami regresji (~0,3). W normie L2 współczynniki były niewidoczne, a przy normalizacji globalnym σ było odwrotnie — te 4 wartości leżące ~1000 poniżej średniej zawyżały σ i **zaniżały dryf Elo ~5×**.
3. **Liczba parametrów.** Suma kwadratów rośnie z liczbą parametrów, więc Poisson (116 = 4 + 4·28) miał z definicji większy dryf niż Elo (32 = 4 + 28); Dixon-Coles ma 58 (2 + 2·28).

Snapshoty zawierają **wszystkie drużyny, które pojawiły się w historii** (28), a nie tylko 18 z badanego sezonu — liczby w tej sekcji to uwzględniają.

Normalizacja w obrębie rodzin usuwa wszystkie trzy: dzielenie przez rozrzut rodziny znosi skalę i przesunięcie, uśrednianie znosi liczebność, a rozbicie na rodziny nie pozwala jednej skali zdominować drugiej. Rodziny jednoelementowe (średnie ligowe, `home_advantage`, ρ, współczynniki regresji) są pomijane, gdy istnieją rodziny per-drużyna — ich liczba i charakter różnią się między modelami, więc włączanie ich czyniłoby porównanie arbitralnym. Dodatkowo chroni to ρ, którego wartość ~−0,03 przy normalizacji własną wielkością generowałaby pozorne skoki.

Niezmienniki są w testach (`ModelSnapshotDriftTests`): niewrażliwość na skalę, na stałe przesunięcie, na liczbę parametrów, brak `NaN` przy zerowym rozrzucie oraz to, że skok pojedynczego parametru skalarnego nie dominuje wyniku.

`Distance()` **pozostaje nietknięte** — modele używają go w testach jako sondy równości snapshotów (`Assert.Equal(0, ...)` przy sprawdzaniu determinizmu i idempotencji `UpdateWithRound`), gdzie potrzebna jest odległość absolutna, a nie znormalizowana.

### Zmierzony efekt

Rozrzut średniego dryfu między modelami na sezonie 2024/2025:

| Wersja metryki | Rozrzut |
| --- | --- |
| surowa norma L2 | 40,6× |
| normalizacja globalnym σ | 27,7× |
| **normalizacja w obrębie rodzin** | **4,7×** |

Zmiana dotyczy wyłącznie dryfu — metryki predykcyjne (RPS, Brier, log-loss, trafności) są po niej **bitowo identyczne**, co potwierdzono porównaniem badań na tych samych danych.

### Pozostałe 4,7× — częściowo sygnał, częściowo artefakt

Średni dryf (2024/25 → 2025/26): Elo 0,065 → 0,072, Poisson 0,178 → 0,173, DixonColes 0,308 → 0,350. Powtarzalność między sezonami jest wysoka. Dla Elo i Poissona uporządkowanie wynika z mechaniki aktualizacji:

- **Elo** zmienia oceny przyrostowo o `K·G·(W−W_e)` przy K=10, więc rusza się najmniej i jego trajektoria jest **płaska** (0,053 → 0,060) — model jest w stanie ustalonym od pierwszej ocenianej kolejki.
- **Poisson** przelicza średnie kroczące; im więcej meczów w koszyku, tym mniejszy wpływ kolejnego, stąd **łagodny spadek** (0,220 → 0,130).
- **Dixon-Coles** — dryf jest najwyższy i **oscyluje** (0,455 → 0,235 z garbem 0,449 w kolejce 29), ale to **artefakt**, nie cecha modelu: wzór pokrywa się co do kolejki z sekwencją przełączeń między optimum a analitycznym punktem startowym przy przekroczeniu limitu iteracji (2024/25: trening optimum, 19–25 start, 26 optimum, 27 start, 28 optimum, 29–33 start, 34 optimum — patrz „Znane ograniczenie" w sekcji Dixona-Colesa). Pierwotnie przypisano go pełnemu ponownemu MLE co kolejkę; ta interpretacja była błędna. Do ponownego pomiaru po naprawie optymalizatora.

Dwa dalsze ograniczenia, przez które rozrzut nie jest czystym sygnałem:

- **Nieaktywne drużyny.** 10 z 28 drużyn w snapshocie nie gra w badanym sezonie: w Elo mają zamrożone oceny (zerowe zmiany rozcieńczają RMS, a mimo to wchodzą do σ), w Poissonie trzymają wartości awaryjne ze średnich ligowych (obniżają σ rodziny), w Dixonie-Colesie są słabo zidentyfikowane i przeoptymalizowywane co kolejkę. Każdy model traktuje je inaczej, więc zaburzają porównanie między modelami. Kierunek naprawy: liczyć dryf tylko po drużynach aktywnych w badanym sezonie.
- **Ślepe pola.** Rodziny jednoelementowe wypadają, więc dryf nie widzi γ i ρ Dixona-Colesa ani czterech współczynników regresji Elo (etap 2). Snapshot Poissona wystawia tylko horyzont bieżącego sezonu — dryf nie widzi horyzontów poprzedniego i historycznego, wspólnej średniej ligowej ani mnożników formy.

### Konsekwencja dla pytania badawczego nr 2

Przy **wspólnym progu absolutnym** `StabilisedFromRound` nie odpowiada na pytanie „kiedy model się stabilizuje", bo zlepia dwie różne rzeczy: poziom dryfu w stanie ustalonym (który różni się między modelami mechanicznie) i moment wygaszenia dryfu (o który pytanie faktycznie chodzi). Widać to w przemiataniu progów na 2024/25:

| Próg | Elo | Poisson | DixonColes |
| --- | --- | --- | --- |
| 0,05 | nigdy | nigdy | nigdy |
| 0,10 | od 19 | nigdy | nigdy |
| 0,20 | od 19 | od 20 | nigdy |
| 0,25 | od 19 | od 19 | od 32 |

Każdy próg daje albo „Elo od razu, reszta nigdy", albo „wszyscy od razu". Domyślne 0,05 jest nieosiągalne dla każdego modelu. Wniosek, który dane rzeczywiście uzasadniają, to **różnica w charakterze aktualizacji parametrów** (Elo płaski, Poisson opadający; obraz Dixona-Colesa do ponownego pomiaru po naprawie optymalizatora), a nie jedna liczba „kolejka stabilizacji". Alternatywa, gdyby pojedyncza liczba była potrzebna: próg relatywny względem własnego poziomu dryfu modelu.

Ranking z wiązaniami (`Ranking.AverageRanks`) zwraca rangi średnie i sumę `t³−t` potrzebną do korekty wariancji w obu testach.

## Interfejs badawczy

Nowa sekcja w nawigacji, obok istniejących stron symulacji MC (te działają bez zmian):

| Strona | Trasa | Zawartość |
| --- | --- | --- |
| `ResearchRunsPage` | `/research` | lista badań ze statusem + formularz nowego badania |
| `ResearchRunDetailsPage` | `/research/{RunId}` | wykresy, podsumowania, testy istotności, stabilność, beniaminki, predykcje |
| `ModelRoundPredictionPage` | `/model-prediction` | predykcja jednej kolejki wybranym modelem |

`EvaluationRunForm` po wyborze sezonu odpytuje `season-structure` i pokazuje wykrytą przerwę zimową oraz beniaminków — kolejkę odcięcia można zostawić puste (auto) albo nadpisać. Parametry modeli (mnożniki formy, ξ, ridge, próg i okno stabilności) siedzą w zwiniętym panelu, żeby nie zaśmiecać formularza.

Strona szczegółów ma sześć zakładek: **Przebieg w sezonie** (dwa wykresy `MudChart` — wybrana metryka po kolejkach i dryf parametrów, po jednej serii na model), **Podsumowanie**, **Istotność różnic**, **Stabilność**, **Beniaminki**, **Predykcje** (z filtrem modelu i kolejki; kliknięcie wiersza pokazuje pod tabelą macierz wyników 0–6 × 0–6, obramowana komórka = faktyczny wynik). Selektor metryki przełącza wykres (przeliczany po stronie przeglądarki z metryk per kolejka) i testy istotności par oraz beniaminków (przeliczane przez backend); zakładki „Podsumowanie" i „Stabilność" od metryki nie zależą — werdykt stabilności liczony jest wyłącznie z dryfu. Selektor i zakładki pojawiają się dopiero dla runu `Completed`.

### Dwie poprawki w istniejącym kodzie frontendu

1. **`HttpServiceHelper` nie obsługiwał koperty w POST/PUT.** `SendPostAsync<T>` deserializował ciało odpowiedzi jako `T`, choć backend zwraca `EkstraSimResult<T>`, a `SendPutAsync<T>` w ogóle odrzucał ciało (`Data = default`). Dotąd nie miało to znaczenia, bo istniejące PUT-y są typu „odpal i zapomnij". Dodane `SendPostEnvelopeAsync<T>` / `SendPutEnvelopeAsync<T>` czytają kopertę tak samo jak `SendGetAsync`. Stare metody nietknięte, żeby nie ruszać istniejących wywołań.
2. **Adres API z konfiguracji.** `EkstraSim.Frontend/Program.cs` czyta klucz `ApiBaseAddress`, z produkcyjnym URL-em Azure jako wartością domyślną. Bez tego front lokalnie zawsze strzelał w chmurę.

## Dane

- CSV: `Database/CSV/*.csv`, format `id,data,kolejka,gospodarz,gole,gość,gole,url_obrazka` — 8 pól, **bez nagłówka**, bez cudzysłowów, UTF-8 bez BOM, CRLF, data jako `yyyy-MM-dd`, kolejka jako liczba. Puste pola goli = mecz nierozegrany.
- Sezony 2019/20–2020/21: 16 drużyn, format z podziałem na grupy (~37 kolejek). Od 2021/22: 18 drużyn, 34 kolejki.
- Beniaminki sezonu S = drużyny mające mecze w S, bez meczów ligowych w S−1.
- Podział jesień/wiosna wykrywany po największej przerwie między datami kolejek (przerwa zimowa).

### Stan kompletności sezonów

Pliki CSV i baza **nie są tym samym źródłem** — badania czytają bazę, a nie pliki. Kompletność trzeba więc czytać dwukolumnowo:

| Sezon | CSV: rozegrane | Baza: rozegrane | Uwaga |
| --- | --- | --- | --- |
| 2019/20 | 296 / 296 | 296 / 296 | 37 kolejek, format grupowy |
| 2020/21 | 240 / 240 | 240 / 240 | 30 kolejek, format grupowy |
| 2021/22 – 2023/24 | 306 / 306 | 306 / 306 | — |
| 2024/25 | **162 / 306** | **306 / 306** | plik CSV jest urwany na kolejce 18, ale **baza ma pełny sezon** — nie odtwarzać go z CSV |
| 2025/26 | 306 / 306 | 306 / 306 | zaimportowane 2026-08-04 — uzupełniło 168 wyników; konflikt boisk Jagiellonia ↔ Wisła Płock rozstrzygnięty na korzyść pliku (zweryfikowane w źródłach) |
| 2026/27 | 16 / 306 | 16 / 306 | sezon w toku; dane z eksportu 2026-08-03, do odświeżenia |

Dwa wnioski, oba ważne:

- **2024/25 jest gotowe pod walk-forward**, bo baza ma wszystkie 306 wyników. Sam plik CSV by na to nie pozwolił — gdyby ten sezon odtwarzać z pliku, runda wiosenna wyszłaby pusta. Kierunek jest tu jednoznaczny: baza jest źródłem prawdy, plik nie.
- **2026/27 jeszcze nie**. Odcięcie wypadnie ~kolejka 17, a `BuildEvaluationSet` bierze kolejki powyżej odcięcia i tylko rozegrane — przy wynikach z ~2 pierwszych kolejek zbiór ewaluacyjny jest **pusty**. Ten sezon obsługuje na razie wyłącznie `predict-round`.

### Konwersja eksportu z TheSportsDB

Pliki 2025/26 i 2026/27 przyszły w innym formacie niż pozostałe i wymagały konwersji przed importem. Eksport z TheSportsDB różni się od formatu docelowego na cztery sposoby:

1. Ma nagłówek `idEvent,strTimestamp,Round,Home Team,Home Score,Away Team,Away Score,Poster,Thumb` — 9 kolumn, doszedł `Poster` (w praktyce pusty).
2. **Każdy wiersz danych jest owinięty w dodatkową parę cudzysłowów** z podwojonymi cudzysłowami wewnątrz (podwójnie zakodowany CSV). `ParseRow` robi naiwne `line.Split(',')`, więc `fields[1]` wychodzi jako `""2025-07-18 16:00:00""` i `DateTime.TryParse` odrzuca **każdy** wiersz.
3. Kolejka to tekst `"Round 1"`, nie liczba — `int.TryParse` odrzuca ją nawet po naprawieniu cudzysłowów.
4. Kodowanie to **cp1250 z CRLF**, nie UTF-8. Czytane jako UTF-8 daje `Bia<?>ystok` i zakłada śmieciowe rekordy `Team`.

Konwersję robi `scripts/Convert-SportsDbCsv.ps1`:

```powershell
.\scripts\Convert-SportsDbCsv.ps1 -Path Database\CSV\Ekstraklasa_2026_2027.csv -InPlace
```

Skrypt zdejmuje owinięcie, wyciąga numer z `Round N`, obcina znacznik czasu do daty, przepisuje `Thumb` na ósme pole i przekodowuje na UTF-8. **Nazw drużyn nie rusza** — kanonizuje je mapa aliasów w warstwie importu, dzięki czemu każdy kolejny download wymaga tylko konwersji formatu.

Dwa zabezpieczenia: nic nie jest zapisywane, jeśli **którykolwiek** wiersz okaże się niepoprawny (żadnych plików w połowie skonwertowanych), a plik bez nagłówka `idEvent,...` jest odrzucany — więc powtórne uruchomienie na już skonwertowanym pliku nie zepsuje go. `-InPlace` odkłada oryginał do `*.csv.orig` (ignorowane przez git).

Dotyczy to zwłaszcza **sezonu w toku**: 2026/27 trzeba re-importować po każdej kolejce, a każdy kolejny eksport przyjdzie w tym samym formacie. Parser świadomie nie został rozszerzony o ten format — konwersja jest krokiem ręcznym, więc pominięcie jej skończy się odrzuceniem wszystkich wierszy (błąd „Plik nie zawiera żadnego poprawnego wiersza"), a nie cichym zepsuciem danych.
