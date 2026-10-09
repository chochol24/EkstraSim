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

Dopasowanie metodą największej wiarygodności na logarytmicznej wiarygodności z korektą τ dla czterech niskich wyników (0:0, 0:1, 1:0, 1:1) — to ona odwzorowuje nadwyżkę remisów, której czysty Poisson nie widzi. Minimalizacja: L-BFGS z analitycznym gradientem (`LimitedMemoryBfgsMinimizer` z Math.NET) — podsekcja [Minimalizacja](#minimalizacja-l-bfgs-z-gradientem-analitycznym).

Szczegóły implementacyjne:

- **Identyfikowalność.** Likelihood ma jedną redundancję: α→cα przy β→β/c nie zmienia λ. Rozwiązywana normalizacją średniej ataków do 1 wewnątrz funkcji celu, z jednoczesnym przemnożeniem obron przez tę samą stałą (inaczej λ gościa by się przeskalowało). Poziom bramkowy gościa siedzi więc w β, a stosunek dom/wyjazd w γ.
- **Parametryzacja.** α, β, γ optymalizowane w logarytmach (dodatniość gwarantowana), ρ jako `0.3 · tanh(r)` — trzyma korektę w rozsądnym zakresie bez twardych więzów. Punkty, w których τ ≤ 0 lub λ ≤ 0, dostają karę `1e12`.
- **Wygaszanie czasowe.** Waga meczu `φ(t) = exp(−ξ · Δdni)` względem najnowszego znanego meczu; ξ z `TrainingOptions.TimeDecayXi` (domyślnie 0.0065 ≈ półokres ~107 dni).
- **Regularyzacja ridge.** Kara `RidgeLambda · wᵢ · (log²αᵢ + log²βᵢ)`, gdzie `wᵢ = 1/(1 + efektywna liczba meczów drużyny)` (efektywna = suma wag czasowych), domyślnie `RidgeLambda = 0.05`. Ściąga do α = β = 1 tym mocniej, im mniej danych ma drużyna — to obsługuje beniaminków na starcie sezonu. Uwaga: przy normalizacji średniej ataku do 1 poziom goli gościa siedzi w β (typowo ~1,1–1,2), więc **β = 1 nie jest średnią ligową**, tylko obroną lepszą niż przeciętna — dotyczy to zarówno celu ridge, jak i drużyn nieobecnych w treningu.
- **Punkt startowy** liczony analitycznie ze średnich bramkowych (α ze zdobytych, β ze straconych przeskalowanych do poziomu goli gościa, γ ze stosunku dom/wyjazd, ρ = −0.03) — ten sam co przed zmianą optymalizatora. Wymiar problemu to 2N + 2, gdzie N to **wszystkie drużyny z historii**, nie tylko z badanego sezonu — dla 2024/25 i 2025/26 N = 28, czyli **58 wymiarów**. Logarytmy parametrów są przycinane do ±20.
- `UpdateWithRound` = pełne ponowne dopasowanie, za każdym razem od analitycznego punktu startowego (nie od poprzedniego optimum) — powód w punkcie „Zimny start" niżej.
- Drużyny nieobecne w treningu dostają α = β = 1.
- **Kolejność meczów** w modelu to `(Date, Id)`. Wcześniej sortowanie było po samej dacie, a `List.Sort` jest niestabilne i zapytanie orkiestratora nie ma `ORDER BY` — kolejność meczów z tego samego dnia zależała od bazy. Suma w funkcji celu jest matematycznie ta sama, ale zaokrąglenia (i ścieżka optymalizatora) już nie; teraz wejście jest deterministyczne.
- **Historia — cichy powrót do punktu startowego (runy 1–6, wersje algorytmu 1–3).** Do 2026-10 dopasowanie robił `NelderMeadSimplex(1e-7, 20000)`, a `catch (Exception)` w `Fit` po `MaximumIterationsException` bez śladu podstawiał punkt startowy. Pomiar (audyt 2026-10-05, odtworzony testem `RealData` na starym kodzie): **13 z 17** dopasowań w 2024/25 (kolejki 19–25, 27, 29–33) i **7 z 17** w 2025/26 (20, 21, 23, 26, 27, 28, 33) kończyło się w punkcie startowym; udane potrzebowały 13,7–18,2 tys. iteracji. W tych kolejkach model był estymatorem momentów, nie MLE. Tam, gdzie Nelder-Mead „zbiegł", L-BFGS schodzi niżej: trening 2024/25 248,738 → 248,734786, k.34 2024/25 262,182 → 262,173636, k.34 2025/26 269,551 → 269,543276. Ponowne runy (7–8, wersja 4) dały RPS Dixona-Colesa 0,2290 → 0,2287 (2024/25) i 0,2332 → 0,2326 (2025/26) — mała poprawa, ranking modeli i wnioski z testów istotności bez zmian; ρ ma prawdziwą dynamikę (w 2024/25 od −0,10 do +0,05), a fallback trzymał ją na −0,03. Predykcje Poissona i Elo w runach 7–8 są bitowo identyczne z runami 5–6. Wpływ na dryf — sekcja [Metryka dryfu parametrów](#metryka-dryfu-parametrów).

#### Minimalizacja: L-BFGS z gradientem analitycznym

Funkcja celu `f = −Σₖ wₖ·ℓₖ + P` (ujemna, ważona czasowo log-wiarygodność plus kara ridge) żyje w `DixonColesObjective` razem z punktem startowym i rozpakowaniem parametrów (`Unpack`); `DixonColesModel` tylko ją minimalizuje. Wartość liczy **ten sam kod** co przed zmianą optymalizatora (kara `1e12`, przycięcie ±20, normalizacja ataku) — zmieniła się metoda minimalizacji, nie definicja estymatora. Kotwica: NLL w punkcie startowym treningu 2024/25 = 248,939860312, identycznie jak w starym kodzie.

Wektor parametrów `v = (a₁…a_N, d₁…d_N, g, r)`; `c(x)` to przycięcie do ±20, maska `D(x) = 1` dla `|x| < 20`, inaczej 0 (poza przedziałem funkcja jest płaska).

```
Aᵢ = e^{c(aᵢ)},  m = średnia(A),  αᵢ = Aᵢ/m,  βᵢ = e^{c(dᵢ)}·m,  γ = e^{c(g)},  ρ = 0,3·tanh r

λ = α_h·β_a·γ = A_h·e^{c(d_a)}·γ        μ = α_a·β_h = A_a·e^{c(d_h)}        (m się skraca)
```

Dla meczu x:y z wagą w:

```
s_λ = x − λ + λ·τ_λ/τ        s_μ = y − μ + μ·τ_μ/τ        s_ρ = τ_ρ/τ

∂f/∂a_h, ∂f/∂d_a, ∂f/∂g  −= w·s_λ
∂f/∂a_a, ∂f/∂d_h         −= w·s_μ
∂f/∂r                    −= w·s_ρ·0,3·(1 − tanh² r)
```

| Wynik | τ_λ | τ_μ | τ_ρ |
| --- | --- | --- | --- |
| 0:0 | −μρ | −λρ | −λμ |
| 0:1 | ρ | 0 | λ |
| 1:0 | 0 | ρ | μ |
| 1:1 | 0 | 0 | −1 |
| pozostałe | 0 | 0 | 0 |

Ridge `P = κ·Σᵢ ωᵢ·(log² αᵢ + log² βᵢ)`, gdzie `log αᵢ = c(aᵢ) − ln m`, `log βᵢ = c(dᵢ) + ln m`:

```
∂P/∂dⱼ = 2κωⱼ·log βⱼ
∂P/∂aⱼ = 2κωⱼ·log αⱼ + (αⱼ/N)·Σᵢ 2κωᵢ·(log βᵢ − log αᵢ)        (bo Aⱼ/ΣA = αⱼ/N)
```

Każdy składnik mnożony jest przez maskę swojej współrzędnej. Punkt niedopuszczalny (τ ≤ 0, λ lub μ ≤ 0 albo nieskończone) daje wartość `1e12` i gradient zerowy — line search traktuje to jak nieudany krok i go skraca. Zgodność gradientu z centralnymi różnicami skończonymi sprawdza `DixonColesObjectiveTests` (dane ze wszystkimi czterema niskimi wynikami, ridge > 0, ξ > 0; w punkcie startowym i w punkcie zaburzonym).

**Płaski kierunek.** Średnia ataków `m` skraca się w λ i μ, a kara zależy od `a − ln m` i `d + ln m`, więc f jest **dokładnie stała** wzdłuż kierunku `(a + c, d − c)` (wszystkie ataki w górę, wszystkie obrony w dół o tę samą stałą). Hesjan jest w tym kierunku osobliwy. Nelder-Mead czołgał się po tej dolinie (jeden z powodów 13–18 tys. iteracji). Gradient ma w tym kierunku zerową składową, więc L-BFGS się po nim nie przesuwa, a normalizacja w `Unpack` i tak daje jednoznaczne α i β. Pilnuje tego test `ObjectiveIsFlatAlongTheAttackDefenceShift`.

**Ustawienia.** `LimitedMemoryBfgsMinimizer(gradientTolerance: 1e-9, parameterTolerance: 0, functionProgressTolerance: 0, memory: 10, maximumIterations: 2000)`. Zera wyłączają wyjścia „po stagnacji", więc jedynym regularnym wyjściem jest kryterium gradientowe. Na prawdziwych danych dopasowanie zajmuje 122–196 iteracji i kilkadziesiąt milisekund (Nelder-Mead: 13,7–18,2 tys. iteracji, ~1–2 s), więc limit 2000 daje ~10× zapasu.

**Kryterium akceptacji.** Dopasowanie jest przyjęte, gdy spełnione są wszystkie cztery warunki:

1. punkt końcowy jest dopuszczalny: `f(x*) < 1e12` (nie jest karą za τ ≤ 0 albo λ, μ ≤ 0),
2. żaden parametr logarytmiczny (`a`, `d`, `g`) nie leży na przycięciu `|v| ≥ 20`,
3. `ReasonForExit` ∈ {`AbsoluteGradient`, `RelativeGradient`},
4. niezależnie policzone (z gradientu analitycznego w punkcie końcowym) `‖∇f(x*)‖∞ ≤ 1e-6 · max(1, |f(x*)|)`.

Warunki 1–2 są potrzebne, bo w obu sytuacjach gradient jest **zerowy z konstrukcji**, więc warunek 4 przechodzi trywialnie. Punkt niedopuszczalny dostaje karę `1e12` i gradient 0 — jeśli niedopuszczalny jest już **analityczny start**, Math.NET kończy w iteracji 0 z wyjściem gradientowym (przykład: 5× 30:0 i 1× 0:1 tych samych drużyn → λ_start ≈ 98, τ(0:1) = 1 − 0,03·98 < 0). Za przycięciem maska zeruje gradient współrzędnej, więc „rozwiązanie" z parametrem uciekającym do nieskończoności (MLE nie istnieje) wyglądało jak punkt stacjonarny. Oba przypadki znalazł przegląd implementacji (sondy, 2026-10-09): przed poprawką były po cichu przyjmowane — ta sama klasa błędu co dawny powrót do punktu startowego. Na prawdziwych danych nie występują (34/34 dopasowań: `f` ≈ 130–270, parametry logarytmiczne rzędu ±1).

Warunek 4 jest konieczny, bo `ReasonForExit` z Math.NET 5.0 nie jest wiarygodny — zmierzone na dwa sposoby:

- kryterium `AbsoluteGradient` jest skalowane przez |f|: przy `gradientTolerance = 1e-6` przepuściło ‖∇f‖∞ ≈ 2·10⁻⁴;
- po wyczerpaniu limitu iteracji minimizer **nie rzuca** `MaximumIterationsException`, tylko zwraca wynik z `ReasonForExit = AbsoluteGradient` i `Iterations` = limit + 1 (limit 1 na danych syntetycznych: „AbsoluteGradient" przy ‖∇f‖∞ = 2,18). Bez niezależnego warunku taki punkt przeszedłby jako optimum.

Przy |f| ≈ 130–270 próg wynosi 1,3–2,7·10⁻⁴; obserwowane na prawdziwych danych ‖∇f‖∞ ≤ 2,4·10⁻⁷.

**Brak zbieżności = głośny błąd.** Każde odrzucone dopasowanie — wyjątek Math.NET (`OptimizationException`, np. line search: „Direction is not a descent direction"), punkt niedopuszczalny, parametr na granicy przycięcia, wyjście niegradientowe, niespełniony warunek stacjonarności, wyczerpany limit — kończy się `ModelConvergenceException` z polskim komunikatem, np. `Dixon-Coles: optymalizacja nie zbiegła (trening; 2 iteracji; powód: wyczerpany limit 1 iteracji; wyjście MathNet: AbsoluteGradient; NLL 352.675 → 352.651; ‖∇f‖∞ = 2.18E+000).` Pole `Reason` podaje faktyczną przyczynę odrzucenia, a nie etykietę z Math.NET (patrz wyżej). W kodzie nie ma `catch`, który zwraca punkt startowy. Po wyjątku model zachowuje poprzednie dopasowanie w spójnej postaci — indeks drużyn i parametry przypisywane są razem dopiero po akceptacji (test `FailedUpdateKeepsThePreviousFitConsistent`); mecze z odrzuconej kolejki zostają jednak wchłonięte. Orkiestrator ustawia wtedy run na `Failed` z tym komunikatem w `ErrorMessage`, a `predict-round` zwraca go w kopercie błędu — run `Completed` gwarantuje MLE we wszystkich kolejkach.

**Raporty dopasowań.** `DixonColesModel.FitReports` — po jednym `DixonColesFitReport` na każde przyjęte dopasowanie: `AfterRound` (`null` dla treningu), `Dimension`, `Iterations`, `ExitReason`, `StartObjective`, `FinalObjective`, `GradientNorm` (‖∇f‖∞). Lista jest czyszczona w `Train`. Raporty nie trafiają do bazy — polityka głośnego błędu i tak gwarantuje zbieżność w runach `Completed`; czyta je test na prawdziwych danych.

**Zimny start.** Każde dopasowanie startuje od analitycznego punktu startowego, nie od poprzedniego optimum. Wynik zależy wtedy tylko od zbioru meczów, a nie od ścieżki dopasowań: `Train` + kolejne `UpdateWithRound` dają te same parametry co jedno `Train` na tym samym zbiorze (tak liczy `predict-round`). Ciepły start by tę zgodność zepsuł, a przy kilkudziesięciu milisekundach na dopasowanie nie jest potrzebny.

**MLE nie zawsze istnieje.** Przy `RidgeLambda = 0` drużyna z jednym meczem 7:0 i zerem straconych goli nie ma skończonego optimum: wiarygodność rośnie monotonicznie, gdy jej obrona β → 0. Nelder-Mead zatrzymywał się „gdzieś" na tej dolinie, a L-BFGS kończy się wyjątkiem z line search. Podobnie drużyna, która nigdy nie strzela gola: przy ridge 0 jej atak α → 0 i parametr dochodzi do przycięcia ±20 — warunek 2 kryterium akceptacji odrzuca takie dopasowanie (test `ParameterDrivenToTheClampBoundaryThrows`; przy ridge 0,05 ta sama drużyna ma |log α| ≈ 5 i dopasowanie jest poprawne). Dlatego test `RidgeShrinksSparseTeamsTowardsLeagueAverage` porównuje `RidgeLambda` 0,05 z 5,0 (zamiast 0 z 5,0) — z ridge > 0 kara kwadratowa w `log β` daje skończone optimum. Domyślne opcje badań mają `RidgeLambda = 0.05`. Inne ustawienia (ridge 0, ξ = 0) mogą trafić na przypadki bez MLE — wtedy run kończy się błędem, nie złą liczbą (test `InfeasibleStartingPointThrowsInsteadOfFallingBack` pilnuje też niedopuszczalnego startu).

**Weryfikacja na prawdziwych danych.** Test `DixonColesRealDataTests` (`[Trait("Category", "RealData")]`) czyta fixture `EkstraSim.Tests/Data/ekstraklasa-liga1-mecze.csv`, odtwarza przez `WalkForwardEvaluator.Run` sekwencję dopasowań z runów 2024/25 i 2025/26 (odcięcie 18, opcje domyślne: formy włączone, ξ = 0,0065, ridge 0,05) i wymaga:

- dokładnie 17 raportów na sezon (trening + 16 kolejek; modele tymczasowe filtra daty to osobne instancje i nie wchodzą do tej sekwencji),
- w każdym: wyjścia gradientowego, ‖∇f‖∞ w granicy akceptacji i `FinalObjective < StartObjective` (ruch ze startu),
- kotwicy: NLL(start) treningu 2024/25 = 248,939860312 (±1e-8), NLL końcowe ≤ 248,738 (wynik Neldera-Meada).

Tabelę dopasowań (iteracje, wyjście, NLL start → koniec, ‖∇f‖∞, ρ, γ) wypisuje `dotnet test EkstraSim.Tests/EkstraSim.Tests.csproj --filter "Category=RealData" --logger "console;verbosity=detailed"`; resztę testów uruchamia filtr `"Category!=RealData"`.

Fixture powstaje skryptem `scripts/Export-ResearchFixture.ps1` (domyślnie serwer `.\SQLEXPRESS`, baza `EkstraSimDB`, plik jak wyżej — ścieżka domyślna liczona od korzenia repo, więc skrypt działa z dowolnego katalogu; `-Server`, `-Database`, `-OutPath` do zmiany, jawna względna `-OutPath` liczy się od bieżącego katalogu). Skrypt tylko czyta bazę (`sqlcmd -E`) i eksportuje mecze ligi 1 z sezonów, w których **każdy** mecz jest rozegrany — trwający sezon wypada sam, więc fixture się nie starzeje (dziś 7 sezonów, 2066 meczów). Format: nagłówek `Id;Date;Round;SeasonId;LeagueId;HomeTeamId;AwayTeamId;HomeScore;AwayScore`, wiersze w kolejności Id, data `yyyy-MM-dd`, UTF-8 bez BOM — same identyfikatory i wyniki, bez nazw drużyn. Nic nie jest zapisywane, gdy którykolwiek wiersz się nie parsuje, data ma składnik godzinowy (format by go obciął) albo liczba wierszy różni się od `COUNT(*)` z bazy. Kolejność po Id plus stabilne `OrderBy(Date)` w `BuildHistory` dają deterministyczne wejście do modelu, więc na starym kodzie test odtwarzał pomiar z audytu co do kolejki (punkt „Historia" wyżej).

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
wchłonięte = rozegrane mecze historii
dla każdej kolejki R rundy wiosennej:
    dla każdego meczu m z R (po Id):
        ukryte = wchłonięte z Date >= m.Date
        jeśli ukryte puste:   predykcja = model.Predict(m)        ← model nie zna jeszcze wyników R
        w przeciwnym razie:   predykcja = tymczasowy.Predict(m)   ← świeży model po Train(wchłonięte \ ukryte)
    oceny      = metryki(predykcje, faktyczne wyniki)
    model.UpdateWithRound(mecze(R))                  ← dopiero teraz wchłania wyniki
    wchłonięte += mecze(R)
    dryf       = NormalisedDistance(parametry_przed, parametry_po)   ← patrz „Metryka dryfu parametrów"
```

**Brak danych z przyszłości — po kolejce i po dacie (od wersji 5).** Predykcja meczu m powstaje ze stanu modelu, który zna wyłącznie mecze spełniające jednocześnie dwa warunki: (a) są w treningu albo w kolejkach ocenionych przed kolejką m, (b) mają datę wcześniejszą niż `m.Date`. Warunek (a) daje sama kolejność pętli: model widzi wyniki kolejki R dopiero po policzeniu wszystkich jej predykcji. Warunek (b) jest potrzebny przez **mecze przełożone**. Kolejki idą w kolejności numerów, a historia to kolejki ≤ odcięcie, więc np. mecz z kolejki 16 rozegrany w marcu trafia do treningu, choć odbył się po meczach, które model jeszcze przewiduje. Do wersji 4 włącznie gwarancja działała tylko po numerze kolejki; po dacie filtrowały jedynie mnożniki formy Poissona.

Filtr daty działa tylko tam, gdzie jest potrzebny:

- **Warunek ukrycia.** Mecz a jest ukryty przed meczem m, gdy `a.Date >= m.Date`, czyli mecze z tego samego dnia też są ukryte (ostrożnie). Daty w bazie nie mają godzin, a w danych nie ma przypadku, w którym ukryty byłby wyłącznie mecz z tego samego dnia.
- **Model tymczasowy.** Gdy zbiór ukrytych jest niepusty, predykcję liczy świeża instancja modelu (`createModel`, domyślnie `PredictionModelFactory.Create(model.Name)`) po `Train(wchłonięte \ ukryte)`. W obrębie kolejki jest keszowana po posortowanych Id ukrytych, więc mecze z tym samym zbiorem ukrytych dzielą jedno dopasowanie. Id meczów przewidzianych stanem zawężonym trafiają do `RoundEvaluation.DateFilteredMatchIds`, a orkiestrator loguje ich liczbę na model. W bazie nie ma flagi „zawężone po dacie".
- **Dlaczego to poprawne.** Wszystkie trzy modele są wsadowe: stan to czysta funkcja zbioru wchłoniętych meczów. Poisson przelicza agregaty, Elo odtwarza replay od zera, a Dixon-Coles dopasowuje od analitycznego startu (zimny start). `Train(podzbiór)` daje więc dokładnie stan „gdyby znano tylko ten podzbiór", taki sam jak po `Train` i kolejnych `UpdateWithRound` na tych samych meczach.
- **Ścieżka bazowa nietknięta.** Model bazowy przechodzi przez te same wywołania co w wersji 4 (`Train(historia)`, potem `Predict` i `UpdateWithRound` w kolejności Id), a model tymczasowy to osobna instancja, która go nie modyfikuje. Predykcje bez meczów ukrytych są więc bitowo identyczne z wersją 4, a filtr nie zmienia dryfu (liczonego ze snapshotów modelu bazowego). Porównanie predykcji z runami wersji 4 pokazuje wyłącznie skutek filtra. Dryf wersji 5 różni się od wersji 4 tylko przez zawężenie do drużyn aktywnych („Metryka dryfu parametrów"). Sprawdzone na fixture z prawdziwymi danymi dla każdego modelu: 2024/25 — 144 ze 144 predykcji identycznych bitowo; 2025/26 — 72 identyczne i 72 zmienione (dokładnie zawężone). Sam filtr daty nie zmienił dryfu w żadnej z 16 kolejek. Runy potwierdzają to samo: run 9 względem runu 7 — 432 z 432 predykcji identycznych bitowo (łącznie z macierzami wyników); run 10 względem runu 8 — różnice dokładnie w 216 wierszach (72 mecze × 3 modele), a zbiór meczów z różnicą równa się zbiorowi wyznaczonemu niezależnie z dat w tabeli `Matches`.
- **Wchłonięte = historia + ocenione już kolejki.** Mecz z późniejszej kolejki rozegrany wcześniej nie jest wchłaniany przed swoją kolejką. To ostrożne (model wie mniej, niż mógłby), ale nie wnosi danych z przyszłości. Pętla zostaje po kolejkach: pełny walk-forward po dniach, w którym mecz widzi wcześniejsze mecze tej samej kolejki, nie jest celem.
- **Błąd dopasowania.** Dopasowanie modelu tymczasowego może rzucić tak samo jak dopasowanie bazowe (Dixon-Coles: `ModelConvergenceException`). Run kończy się wtedy `Failed`, jak przy błędzie modelu bazowego. Na danych 2025/26 wszystkie 9 dopasowań tymczasowych jest zbieżnych.

Pomiar na fixture z prawdziwymi danymi i w runach 9–10 (odcięcie 18; log orkiestratora podaje te same liczby):

| Sezon | Mecze rozegrane po przewidywanych | Zawężone predykcje (na model) | Modele tymczasowe (na model) |
| --- | --- | --- | --- |
| 2024/25 | brak | 0 ze 144 | 0 |
| 2025/26 | 2 w treningu (z kolejek 4 i 16, rozegrane w lutym i marcu) i 3 w pętli (z kolejki 19 wchłonięty przed kolejkami 20–23, dwa z kolejki 31 wchłonięte przed kolejką 32) | 72 ze 144, w kolejkach 19–25 i 32 | 9 |

Mecze z treningu dotykają 63 predykcji, mecze z pętli 45; zbiory się pokrywają, razem 72.

W runach diagnostycznych z odcięciem po kolejce 3 (runy 11–12, tylko do pytania nr 2) zawężonych jest 135 (2024/25) i 205 (2025/26) z 279 predykcji na model, bo do zbioru ewaluacyjnego wchodzą wtedy także jesienne mecze przełożone.

**Skutek dla wyników (2025/26, run 8 → run 10).** Filtr minimalnie poprawił metryki probabilistyczne wszystkich modeli:

- RPS: Poisson 0,2605 → 0,2604, Dixon-Coles 0,2326 → 0,2324, Elo 0,2170 → 0,2169; Brier i log-loss też zmieniają się o najwyżej 0,0003,
- trafność 1X2 bez zmian, top-1 i top-3 różnią się o najwyżej jeden mecz na model,
- p po Holmie z testu Wilcoxona (RPS): Dixon-Coles–Elo 0,0030 → 0,0035, Dixon-Coles–Poisson 0,0144 → 0,0129, Elo–Poisson 0,0028 → 0,0027.

Wszystkie trzy pary są nadal istotne, a ranking Elo < Dixon-Coles < Poisson i wnioski z pytania nr 1 się nie zmieniają. W 2024/25 predykcje, a więc i wszystkie testy, są identyczne z wersją 4.

**Testy.** `WalkForwardVisibilityTests` porównuje każdą predykcję pętli z modelem referencyjnym (`WalkForwardReference`), trenowanym od zera na dokładnie widocznym zbiorze, z tolerancją 1e-9. Obejmuje Poissona, Dixona-Colesa i Elo. Fixture ma 6 drużyn, pełny dwurundowy sezon poprzedni (30 meczów, żeby Elo miało ≥ 20 próbek do regresji) i sezon docelowy ze zmiennymi wynikami, z odcięciem po kolejce 4. Wariant „przełożony" przesuwa mecz z kolejki 2 za kolejkę 6 i mecz z kolejki 7 za kolejkę 8, co daje 9 dotkniętych predykcji w kolejkach 5, 6 i 8.

Asercje mocy (`FixtureDetectsAbsorbingARoundBeforePredicting`, `FixtureDetectsMatchesPlayedAfterThePredictedOne`) sprawdzają, że dołożenie kolejki R albo meczu rozegranego później zmienia predykcję o więcej niż 1e-6. Bez nich zgodność z referencją niczego by nie dowodziła. Poprzednie testy pętli miały fixture z identycznymi wynikami w każdej kolejce, więc nie wykryłyby wchłaniania przed predykcją, i dotyczyły tylko Poissona.

`WalkForwardRealDataTests` (`Category=RealData`) robi to samo na fixture z prawdziwymi danymi:
- niezależnie wyznacza zawężenia (0 i 72, w kolejkach 19–25 i 32),
- sprawdza zgodność każdej predykcji z referencją i równość `DateFilteredMatchIds` z tym zbiorem,
- sprawdza, że liczba utworzonych modeli tymczasowych równa się liczbie różnych zbiorów ukrytych w kolejkach (0 i 9).

Na kodzie wersji 4 testy „po dacie" były czerwone (wszystkie trzy modele, sezon 2025/26), a testy „po kolejce" i asercje mocy zielone.

`BuildHistory` bierze rozegrane mecze z sezonów wcześniejszych w chronologii **oraz** kolejki ≤ odcięcie z sezonu badanego. `BuildEvaluationSet` bierze kolejki > odcięcie, **tylko rozegrane** — dzięki temu trwający sezon (np. 2026/27) ocenia się na tym, co już się odbyło, a nierozegrane kolejki są po prostu pomijane.

### Kolejka odcięcia i beniaminki

- **Odcięcie** domyślnie wykrywane automatycznie (`SeasonCalendar.DetectSplit`): największa przerwa między datami kolejnych kolejek = przerwa zimowa. Można nadpisać ręcznie w żądaniu. Wyjątek: w **2019/20** największa przerwa to pauza COVID po kolejce 26 (81 dni), nie zimowa po kolejce 20 (48 dni) — dla tego sezonu odcięcie trzeba podać ręcznie. Przełożony mecz z ostatniej kolejki jesiennej rozegrany wiosną też potrafi przesunąć wykryty podział.
- **Beniaminki** (`PromotedTeamsService`): drużyny mające mecze w sezonie S i żadnego w S−1. Chronologia sezonów liczona z **najwcześniejszej daty meczu**, nie z `Season.Id` ani nazwy — Id nie gwarantuje kolejności czasowej. Lista jest zapisywana jako snapshot JSON na rekordzie badania, żeby wynik dał się odtworzyć nawet po dodaniu nowych sezonów.

### Encje i przepływ

Migracja `research_evaluation_runs` jest **wyłącznie addytywna** — trzy nowe tabele, zero zmian w istniejących:

| Tabela | Zawartość |
| --- | --- |
| `ModelEvaluationRuns` | parametry badania, lista modeli, opcje JSON (z żądania i efektywne), wersja algorytmu, snapshot beniaminków, status (`Pending`/`Running`/`Completed`/`Failed`), znaczniki czasu |
| `ModelPredictions` | jedna predykcja = model × mecz: λ, P(1/X/2), typowany wynik, macierz 11×11 jako JSON, faktyczny wynik i wszystkie metryki per mecz |
| `ModelRoundMetrics` | agregaty per model × kolejka + `ParameterDrift` (dla pytania nr 2) |

Uruchomienie jest **asynchroniczne**: endpoint tworzy rekord ze statusem `Pending`, zwraca jego Id i oddaje pracę `ResearchRunLauncher` (singleton), który w `Task.Run` (fire-and-forget, bez anulowania) otwiera świeży scope DI — bez tego scoped orkiestrator zniknąłby razem z zakresem żądania. Frontend **nie odpytuje statusu automatycznie** — strony pokazują przycisk „Odśwież", dopóki run nie jest `Completed`. Predykcje zapisywane są partiami po 500 wierszy, każda partia w osobnej transakcji.

Znane słabości tego przepływu: status `Running` jest zapisywany poza `try`, a `catch` ponawia zapis na tym samym kontekście, więc błąd zapisu (np. duplikat nazwy modelu `["Poisson","poisson"]` naruszający unikalny indeks) zostawia run w `Running` na zawsze, z wyjątkiem tylko w logu. Nie ma odzyskiwania po restarcie. Endpointy wyników nie sprawdzają statusu, więc częściowo zapisany run jest widoczny przez API.

Druga migracja badawcza, `research_run_algorithm_version`, też jest addytywna: dwie nullowalne kolumny w `ModelEvaluationRuns` (`AlgorithmVersion`, `EffectiveOptionsJson`), bez wpływu na istniejące wiersze — opis w [Wersjonowanie badań](#wersjonowanie-badań).

### Wersjonowanie badań

Run zapisuje, **który kod go policzył** i **z jakimi opcjami faktycznie**, żeby zmiany algorytmu (jak zmiana formuły dryfu czy optymalizatora Dixona-Colesa) były widoczne w danych, a nie tylko w ręcznym komentarzu.

- **`AlgorithmVersion`** — numer z `ResearchAlgorithm.Version` (`EkstraSim.Prediction/Evaluation`, jedyne źródło). Orkiestrator ustawia go razem ze statusem `Running`, więc to wersja kodu, który run **wykonał**, a nie tego, który utworzył rekord `Pending`.
- **`EffectiveOptionsJson`** — zserializowane (Newtonsoft) `TrainingOptions` z `BuildOptions`, czyli opcje faktycznie przekazane modelom: łącznie z wartościami domyślnymi spoza żądania (`MaxGoals`, wagi horyzontów Poissona, parametry Elo) i `SeasonChronology`. Ustawiane przed liczeniem predykcji i zapisywane z końcowym statusem (także `Failed`, jeśli błąd wystąpił po zbudowaniu opcji).
- **`OptionsJson` vs `EffectiveOptionsJson`.** `OptionsJson` to żądanie w postaci, w jakiej przyszło (`CreateEvaluationRunRequest`) — zawiera też `StabilityTolerance`/`StabilityWindow`, które czyta dopiero endpoint porównania przy każdym odczycie, a nie run. Starsze runy mają w `OptionsJson` dawne pole `StabilityThreshold` (próg absolutny): Newtonsoft pomija nieznane pola, więc deserializują się bez błędu i dostają domyślne δ = 0,25. `EffectiveOptionsJson` to to, co dostały modele. Pierwsze odpowiada na pytanie „o co poproszono", drugie — „co policzono".
- `NULL` w obu kolumnach znaczy „nieznane / sprzed wersjonowania". UI pokazuje wtedy „—": kolumna „Wersja" na `/research` i „Wersja algorytmu: N" w nagłówku `/research/{RunId}`.

| Wersja | Runy | Co się zmieniło |
| --- | --- | --- |
| 1 | 1–2 | dryf jako surowa norma L2 |
| 2 | 3–4 | dryf znormalizowany jednym, globalnym σ |
| 3 | 5–6 | dryf znormalizowany per rodzina parametrów |
| 4 | 7–8 | Dixon-Coles: L-BFGS z gradientem analitycznym i głośnym błędem zamiast cichego powrotu do punktu startowego |
| 5 | 9–12 (11–12: odcięcie po kolejce 3, tylko pytanie nr 2) | filtr daty w walk-forward (mecze przełożone); dryf liczony tylko po drużynach aktywnych w badanym sezonie |

Predykcje w wersjach 1–3 są w obrębie sezonu identyczne (zmieniała się tylko metryka dryfu); Dixon-Coles ma w nich cichy powrót do punktu startowego (sekcja Dixona-Colesa, „Historia"). Wersja 5 zmienia predykcje wyłącznie tam, gdzie działa filtr daty (2025/26: 72 mecze na model), a pozostałe są bitowo identyczne z wersją 4 (sekcja „Idea" wyżej). Zmienia też dryf każdego modelu przez zawężenie do drużyn aktywnych (sekcja „Metryka dryfu parametrów").

**Reguła podbijania:** `ResearchAlgorithm.Version` rośnie przy każdej zmianie kodu, która zmienia predykcje lub dryf któregokolwiek modelu. Zmiany samej prezentacji (UI, endpointy odczytu, testy statystyczne liczone przy odczycie) wersji nie zmieniają.

**Runy sprzed wersjonowania (1–6)** dostają wersje historyczne 1–3 jednorazowym backfillem SQL (transakcja z kontrolą liczby wierszy). `EffectiveOptionsJson` zostaje dla nich `NULL` — nie odtwarzamy opcji z domysłu; ich żądania są w `OptionsJson`. Na lokalnej bazie wykonany 2026-10-09; na każdej innej bazie z runami 1–6 (`sqlcmd … -I -b`):

```sql
SET XACT_ABORT ON;
BEGIN TRAN;
UPDATE ModelEvaluationRuns
SET AlgorithmVersion = CASE WHEN Id IN (1,2) THEN 1 WHEN Id IN (3,4) THEN 2 ELSE 3 END
WHERE Id BETWEEN 1 AND 6 AND AlgorithmVersion IS NULL;
IF @@ROWCOUNT <> 6 BEGIN ROLLBACK; THROW 50000, 'Backfill: oczekiwano 6 wierszy', 1; END
COMMIT;
```

**Kolejność wdrożenia.** Backend od wersji 4 czyta kolumny `AlgorithmVersion` i `EffectiveOptionsJson`, więc migrację `research_run_algorithm_version` trzeba zaaplikować **przed** uruchomieniem nowego backendu na danej bazie — inaczej każdy endpoint `research/runs` kończy się `Invalid column name`. Wycofanie: `dotnet ef database update 20260803092357_research_evaluation_runs` usuwa tylko te dwie kolumny.

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
| GET | `api/research/runs/{RunId}/comparison` | podsumowania, testy istotności, beniaminki, stabilność. Parametry zapytania: `metric` (domyślnie `RankedProbability`), `tolerance` — δ analizy stabilności zamiast wartości z `OptionsJson` runu (ujemne → błąd), `fromRound` i `toRound` — zakres kolejek **tylko dla analizy stabilności** (brakująca granica = bez ograniczenia; `fromRound > toRound` albo zakres bez ocenianych kolejek → błąd). Podsumowania, pary i beniaminki liczone są zawsze na całym runie |
| PUT | `api/research/predict-round` | predykcja jednej kolejki wybranym modelem, bez zapisu — działa też dla kolejek nierozegranych. Domyślne odcięcie = `Round − 1`; **odcięcie ≥ `Round` nie jest odrzucane** (wyciek wprost). Jedno `Train` zamiast `Train` + `UpdateWithRound`, więc wynik nie odtwarza predykcji z runu; mecze nierozegrane mają w polach wyniku i metryk 0, nie `null` |

**Liczby w parametrach zapytania i trasy** (np. `tolerance=0.5`) parsowane są z kropką dziesiętną niezależnie od kultury serwera. `Program.cs` rejestruje w FastEndpoints parser `double` i `double?` z `CultureInfo.InvariantCulture`. Domyślny parser FastEndpoints używa kultury bieżącej, więc na polskim Windowsie przyjmował `0,5`, a odrzucał `0.5` (HTTP 400) — to samo zapytanie zachowywałoby się inaczej lokalnie i na serwerze. Ciała JSON (System.Text.Json) to nie dotyczy.

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
- **`StabilityAnalysis`** — pytanie nr 2. Dla każdego modelu liczy średnią kroczącą metryki i dryfu parametrów (okno `StabilityWindow`, domyślnie 3) oraz:
  - **poziom dryfu L** (`DriftLevel`) — średni surowy dryf z ostatnich k kolejek, k = min(n, max(okno, ⌈n/3⌉)); dla 16 kolejek wiosny k = 6, dla 31 kolejek (odcięcie po 3.) k = 11,
  - **średni dryf** (`MeanDrift`) ze wszystkich kolejek,
  - **próg względny** `Threshold` = (1+δ)·L, gdzie δ to tolerancja (`StabilityAnalysis.DefaultTolerance` = 0,25),
  - **`StabilisedFromRound`** — pierwsza kolejka, **od której do końca** krocząca średnia dryfu nie przekracza progu. Świadomie nie jest to „pierwszy spadek poniżej progu" — chwilowe wyciszenie, po którym parametry znów skaczą, nie jest stabilnością,
  - **trend** (`Trend`) — test Spearmana dryfu surowego względem numeru kolejki.

  Od zmiany kryterium próg jest względny wobec własnego poziomu modelu, a nie wspólny i absolutny — powód w „Konsekwencja dla pytania badawczego nr 2". Całość liczy się przy odczycie, więc działa też dla starych runów i nie zmienia wersji algorytmu.
- **Spearman** (`SpearmanTrendTest`) — test trendu dla pytania nr 2: ρ to korelacja Pearsona na rangach średnich (`Ranking.AverageRanks`, zgodna z `MathNet.Numerics.Statistics.Correlation.Spearman` także przy wiązaniach). Statystyka t = ρ·√((n−2)/(1−ρ²)) (mianownik podłogowany na 1e-12, żeby przy |ρ| = 1 nie było nieskończoności), p dwustronne z rozkładu t-Studenta o n−2 stopniach swobody. Poniżej 6 obserwacji albo przy zerowej wariancji rang (np. stały dryf) wynik jest nierozstrzygający (p = 1). p trendu poprawiane jest Holmem między modelami runu; nierozstrzygające wchodzą z p = 1.

## Metryka dryfu parametrów

`ModelSnapshot.NormalisedDistance()` liczy dryf jako **średniokwadratową zmianę parametrów wyrażoną w jednostkach ich własnego rozrzutu**, osobno w każdej rodzinie parametrów: dla rodziny f — `r_f = RMS(zmian) / σ_f`, gdzie σ_f to odchylenie standardowe (populacyjne) wartości rodziny w **poprzednim** snapshocie. Wyniki rodzin składa jako ważony RMS: `√(Σ n_f·r_f² / Σ n_f)`, gdzie n_f to liczba parametrów rodziny.

Rodzina to klucz z usuniętymi segmentami liczbowymi: `rating_5` → `rating`, `team_5_home_scored` → `team_home_scored`. Brane są tylko rodziny liczące **więcej niż jeden** parametr — czyli rodziny „per drużyna". Gdy takich nie ma, wszystkie klucze traktowane są jako jedna grupa.

**Tylko drużyny aktywne (od wersji 5).** Snapshot modelu zawiera wszystkie drużyny, które pojawiły się w historii (28), ale dryf liczony jest dopiero po zawężeniu do drużyn grających w badanym sezonie (18):

- **Zbiór aktywnych** `WalkForwardEvaluator` wyznacza raz na run: drużyny z meczów sezonu zbioru ewaluacyjnego, obecnych w historii (runda jesienna) albo w zbiorze ewaluacyjnym.
- **`ModelSnapshot.RestrictToTeams`** zostawia klucze bez segmentu liczbowego (skalary) i klucze, których wszystkie segmenty liczbowe są Id drużyn aktywnych. To ta sama reguła segmentów co przy rodzinach.
- **Liczba parametrów po zawężeniu:** Poisson 76 (4 + 4·18), Dixon-Coles 38 (2 + 2·18), Elo 22 (4 + 18).
- **Snapshot modelu zostaje pełny** — zawężenie dzieje się tylko przy liczeniu `ParameterDrift`, a `NormalisedDistance` i `Distance` się nie zmieniły.

### Dlaczego nie surowa norma L2

Poprzednia miara (`Distance()`, sama norma L2 po różnicach) była **nieporównywalna między modelami** z trzech niezależnych powodów:

1. **Skale między modelami.** Parametry Elo to oceny rankingowe rzędu 1300, Dixona-Colesa — siły w okolicy 1,0. Dryf Elo wychodził 30–50× większy niezależnie od tego, co się faktycznie zmieniło.
2. **Skale wewnątrz modelu.** Snapshot Elo miesza 28 ocen (wszystkie drużyny z historii, ~1300) z 4 współczynnikami regresji (~0,3). W normie L2 współczynniki były niewidoczne, a przy normalizacji globalnym σ było odwrotnie — te 4 wartości leżące ~1000 poniżej średniej zawyżały σ i **zaniżały dryf Elo ~5×**.
3. **Liczba parametrów.** Suma kwadratów rośnie z liczbą parametrów, więc Poisson (116 = 4 + 4·28) miał z definicji większy dryf niż Elo (32 = 4 + 28); Dixon-Coles ma 58 (2 + 2·28).

Liczby w punktach 1–3 dotyczą pełnych snapshotów (28 drużyn), na których dryf liczono do wersji 4.

Normalizacja w obrębie rodzin usuwa wszystkie trzy: dzielenie przez rozrzut rodziny znosi skalę i przesunięcie, uśrednianie znosi liczebność, a rozbicie na rodziny nie pozwala jednej skali zdominować drugiej. Rodziny jednoelementowe (średnie ligowe, `home_advantage`, ρ, współczynniki regresji) są pomijane, gdy istnieją rodziny per-drużyna — ich liczba i charakter różnią się między modelami, więc włączanie ich czyniłoby porównanie arbitralnym. Dodatkowo chroni to ρ, którego wartość ~−0,03 przy normalizacji własną wielkością generowałaby pozorne skoki.

Niezmienniki są w testach (`ModelSnapshotDriftTests`): niewrażliwość na skalę, na stałe przesunięcie, na liczbę parametrów, brak `NaN` przy zerowym rozrzucie oraz to, że skok pojedynczego parametru skalarnego nie dominuje wyniku.

`Distance()` **pozostaje nietknięte** — modele używają go w testach jako sondy równości snapshotów (`Assert.Equal(0, ...)` przy sprawdzaniu determinizmu i idempotencji `UpdateWithRound`), gdzie potrzebna jest odległość absolutna, a nie znormalizowana.

### Zmierzony efekt

Rozrzut średniego dryfu między modelami na sezonie 2024/2025:

| Wersja metryki | Rozrzut |
| --- | --- |
| surowa norma L2 | 40,6× |
| normalizacja globalnym σ | 27,7× |
| normalizacja w obrębie rodzin | 4,7× |
| ta sama metryka, Dixon-Coles z L-BFGS (wersja algorytmu 4) | 3,4× (2025/26: 3,2×) |
| **dryf tylko po drużynach aktywnych (wersja algorytmu 5)** | **3,3×** (2025/26: 2,8×) |

Zmiana metryki dotyczy wyłącznie dryfu — metryki predykcyjne (RPS, Brier, log-loss, trafności) są po niej **bitowo identyczne**, co potwierdzono porównaniem badań na tych samych danych. Wiersz wersji 4 to ta sama metryka po naprawie optymalizatora Dixona-Colesa (runy 7–8): zmienił się wyłącznie dryf DC. Ostatni wiersz to runy 9–10: zawężenie do 18 drużyn aktywnych (filtr daty wersji 5 dryfu nie zmienia) — patrz „Nieaktywne drużyny — hipoteza obalona".

### Pozostały rozrzut — częściowo sygnał, częściowo artefakt

Średni dryf (średnia z kroczącej średniej okna 3; 2024/25 → 2025/26):

| Model | Wersja 5 (runy 9–10) | Wersja 4 (runy 7–8, 28 drużyn) | Wersja 3 (runy 5–6) |
| --- | --- | --- | --- |
| Elo | 0,086 → 0,104 | 0,065 → 0,072 | jak w wersji 4 |
| Poisson | 0,178 → 0,174 | 0,178 → 0,173 | jak w wersji 4 |
| Dixon-Coles | 0,282 → 0,293 | 0,223 → 0,232 | 0,308 → 0,350 (przed naprawą optymalizatora) |

Powtarzalność między sezonami jest wysoka, a uporządkowanie modeli jest to samo we wszystkich wersjach. Wynika ono z mechaniki aktualizacji. Trajektorie niżej to średnia krocząca okna 3 z wersji 5: wiosna z runów 9–10, jesień (od kolejki 4) z runów diagnostycznych 11–12.

- **Elo** zmienia oceny przyrostowo o `K·G·(W−W_e)` przy K=10, więc rusza się najmniej, a jego trajektoria jest **płaska** od pierwszej ocenianej kolejki: jesienią 0,078–0,121, wiosną 0,071–0,097 (2024/25) i 0,093–0,109 (2025/26; w pierwszej kolejce wiosny 0,135). Model jest w stanie ustalonym od początku.
- **Poisson** przelicza średnie kroczące; im więcej meczów w koszyku, tym mniejszy wpływ kolejnego. Stąd **wyraźne wygaszanie jesienią** (kolejki 4 → 18: 0,489 → 0,212 i 0,509 → 0,240), a wiosną już tylko łagodny spadek (0,220 → 0,131 w 2024/25) albo płaski przebieg (0,161–0,187 w 2025/26).
- **Dixon-Coles** — dryf jest najwyższy, ale **bez oscylacji**. Jesienią opada słabiej niż Poisson: 2024/25 0,358 → 0,213 z garbem 0,460 w kolejce 13, 2025/26 0,333 → 0,284 z minimum 0,234 w kolejce 13. Wiosną w 2024/25 łagodnie spada (0,342 → 0,217, z ponownym wzrostem do 0,326 w kolejkach 30–32), a w 2025/26 trzyma się w przedziale 0,258–0,326 bez trendu. Każda kolejka to pełne MLE od zimnego startu, a wygaszanie czasowe przesuwa przy tym wagi wszystkich meczów (punkt odniesienia to najnowszy mecz). Hipoteza, że wysoki, niewygasający poziom napędzają drużyny bez nowych meczów (w tym 10 nieaktywnych w badanym sezonie), które co kolejkę przesuwają się w stronę α = β = 1, nie potwierdziła się — patrz „Nieaktywne drużyny — hipoteza obalona".
- **Historia (wersje 1–3, runy 1–6).** Dryf DC **oscylował** (0,455 → 0,235 z garbem 0,449 w kolejce 29). Był to **artefakt**: wzór pokrywał się co do kolejki z przełączaniem między optimum a analitycznym punktem startowym przy przekroczeniu limitu iteracji (2024/25: trening optimum, 19–25 start, 26 optimum, 27 start, 28 optimum, 29–33 start, 34 optimum — patrz „Historia — cichy powrót do punktu startowego" w sekcji Dixona-Colesa). Pierwotnie przypisano go pełnemu ponownemu MLE co kolejkę; ta interpretacja była błędna. Po naprawie oscylacja zniknęła, a średni dryf DC spadł o ~30% (0,308 → 0,223 i 0,350 → 0,232).

Ograniczenie, przez które rozrzut nie jest czystym sygnałem — **ślepe pola**: rodziny jednoelementowe wypadają, więc dryf nie widzi γ i ρ Dixona-Colesa ani czterech współczynników regresji Elo (etap 2). Snapshot Poissona wystawia tylko horyzont bieżącego sezonu — dryf nie widzi horyzontów poprzedniego i historycznego, wspólnej średniej ligowej ani mnożników formy. Zostaje to opisanym ograniczeniem, bo rodziny jednoelementowe różnią się liczbą i charakterem między modelami.

### Nieaktywne drużyny — hipoteza obalona

10 z 28 drużyn w snapshocie nie gra w badanym sezonie, a każdy model traktuje je inaczej:

- Elo zamraża ich oceny — zerowe zmiany rozcieńczają RMS, a mimo to wchodzą do σ.
- Poisson trzyma dla nich wartości awaryjne ze średnich ligowych.
- Dixon-Coles dopasowuje je co kolejkę, choć nie mają nowych meczów, więc są słabo zidentyfikowane.

Hipoteza brzmiała: przeoptymalizowywane nieaktywne drużyny zawyżają dryf Dixona-Colesa i część rozrzutu między modelami jest ich artefaktem. Pomiar mówi coś przeciwnego. Poniżej średnia z kroczącej średniej dryfu (okno 3), 28 → 18 drużyn, czyli runy 7–8 (wersja 4) → 9–10 (wersja 5). Predykcje 2024/25 są w obu wersjach identyczne, a w 2025/26 filtr daty dryfu nie zmienia, więc różnica to wyłącznie zawężenie. Te same liczby dało wcześniej przeliczenie na fixture z prawdziwymi danymi.

| Model | 2024/25 | 2025/26 |
| --- | --- | --- |
| Poisson | 0,178 → 0,178 | 0,173 → 0,174 |
| Dixon-Coles | 0,223 → 0,282 | 0,232 → 0,293 |
| Elo | 0,065 → 0,086 | 0,072 → 0,104 |
| rozrzut | 3,4× → 3,3× | 3,2× → 2,8× |

Nieaktywne drużyny ruszały się mniej niż aktywne, więc dryf **rozcieńczały**, a nie zawyżały. Po zawężeniu dryf Dixona-Colesa rośnie o ~26%, Elo o 32–44%, a Poissona praktycznie się nie zmienia. Rozrzut między modelami spada tylko nieznacznie, więc wysoki poziom Dixona-Colesa i rozrzut wynikają głównie z mechaniki aktualizacji (punkty wyżej), a nie z nieaktywnych drużyn.

Od wersji 5 dryf liczony jest tylko po drużynach aktywnych jako **poprawna definicja** „ruchu parametrów modelu w badanym sezonie", a nie jako poprawka rozrzutu. Testy: `RestrictToTeams` zachowuje skalary i drużyny aktywne we wszystkich trzech formatach kluczy, zmiana parametrów wyłącznie nieaktywnej drużyny nie zmienia dryfu (`ModelSnapshotDriftTests`). Pętla na fixture z drużyną obecną tylko w sezonie poprzednim daje dryf równy ręcznemu `NormalisedDistance` na zawężonych snapshotach, różny od dryfu po pełnych (`WalkForwardEvaluatorTests`).

### Konsekwencja dla pytania badawczego nr 2

Przy **wspólnym progu absolutnym** `StabilisedFromRound` nie odpowiada na pytanie „kiedy model się stabilizuje", bo zlepia dwie różne rzeczy: poziom dryfu w stanie ustalonym (który różni się między modelami mechanicznie) i moment wygaszenia dryfu (o który pytanie faktycznie chodzi). Widać to w przemiataniu progów na 2024/25:

| Próg | Elo | Poisson | Dixon-Coles (wersja 4) | Dixon-Coles (wersja 3, artefakt) |
| --- | --- | --- | --- | --- |
| 0,05 | nigdy | nigdy | nigdy | nigdy |
| 0,10 | od 19 | nigdy | nigdy | nigdy |
| 0,20 | od 19 | od 20 | od 34 | nigdy |
| 0,25 | od 19 | od 19 | od 33 | od 32 |

W 2025/26 Dixon-Coles (wersja 4) daje „nigdy" przy 0,05–0,20 i „od 19" przy 0,25 — jego płaski poziom ~0,23 leży między tymi progami. Wynik każdego modelu zależy więc głównie od tego, czy próg leży nad, czy pod jego własnym poziomem dryfu: poniżej — „nigdy", powyżej — „od razu", a próg przecinający łagodny spadek daje kolejkę z końca rundy (Dixon-Coles 2024/25: 33–34). „Kolejka stabilizacji" Dixona-Colesa skacze przez to między 19 a 34 zależnie od sezonu i progu. Ówczesne domyślne 0,05 było nieosiągalne dla każdego modelu. Wniosek, który dane rzeczywiście uzasadniają, to **różnica w charakterze aktualizacji parametrów** (Elo płaski na niskim poziomie, Poisson łagodnie opadający, Dixon-Coles płaski lub łagodnie opadający na najwyższym poziomie), a nie jedna liczba „kolejka stabilizacji". Tabela wyżej zostaje jako historia: liczby dotyczą wersji 3–4 i progu absolutnego.

**Obecne kryterium: poziom, trend i osiadanie względne.** Pytanie „kiedy model się stabilizuje" rozbite jest na trzy liczby, liczone osobno dla każdego modelu:

1. **Poziom dryfu L** — jak dużo parametry ruszają się w stanie ustalonym (średni dryf z ostatniej 1/3 kolejek). To cecha mechaniki aktualizacji, a nie moment stabilizacji, więc porównuje się go między modelami osobno.
2. **Trend** — czy dryf w ogóle maleje: test Spearmana dryfu względem numeru kolejki, z poprawką Holma między modelami. ρ < 0 przy istotnym p = wygaszanie.
3. **Osiadanie względne** — pierwsza kolejka, od której krocząca średnia dryfu do końca nie przekracza (1+δ)·L, czyli „od kiedy model jest blisko własnego poziomu końcowego". Domyślnie δ = 0,25; wrażliwość sprawdza się parametrem `?tolerance=` (np. 0,10 i 0,50).

Próg względny usuwa zlepienie poziomu z momentem wygaszenia: każdy model jest mierzony swoją miarą, więc „nigdy" przy niskim progu i „od razu" przy wysokim przestają zależeć od tego, gdzie leży wspólna liczba.

**Osiadanie wymaga trendu.** Kryterium względne zawsze coś zwróci: płaski ciąg „osiada" od pierwszej kolejki, a ciąg łagodnie rosnący zwykle też, bo L jest wtedy wysokie. Liczba z osiadania znaczy „stabilizację" dopiero przy ujemnym (albo nieistotnym) trendzie. Przy istotnym trendzie dodatnim dryf rośnie i osiadanie nie oznacza stabilności. Dlatego tabela „Stabilność" pokazuje obie wielkości obok siebie.

**Runy diagnostyczne z wczesnym odcięciem.** Na samej rundzie wiosennej (kolejki 19–34) dryf wszystkich modeli jest już w okolicy poziomu końcowego, więc osiadanie daje 19–20 dla każdego modelu i nic nie rozróżnia. Wygaszanie widać dopiero, gdy ewaluacja zaczyna się wcześnie: runy z ręcznym odcięciem po kolejce 3 obejmują kolejki 4–34, a parametry `?fromRound=` i `?toRound=` endpointu porównania pozwalają policzyć trend osobno dla jesieni (4–18) i wiosny (19–34). Takie runy służą **wyłącznie pytaniu nr 2**. Do porównania trafności modeli (pytanie nr 1) obowiązują runy z odcięciem na przerwie zimowej, bo trening na trzech kolejkach to inny problem predykcyjny.

#### Wyniki (wersja 5)

Runy diagnostyczne 11 (2024/25) i 12 (2025/26), kolejki 4–34. L, średni dryf i trend przy δ = 0,25; osiadanie dla trzech wartości δ:

| Model | Sezon | L | Średni dryf | Trend ρ | p (Holm) | Osiada od: δ = 0,10 / 0,25 / 0,50 |
| --- | --- | --- | --- | --- | --- | --- |
| Poisson | 2024/25 | 0,163 | 0,259 | −0,874 | < 0,0001 | 33 / 20 / 16 |
| Dixon-Coles | 2024/25 | 0,264 | 0,306 | −0,536 | 0,0037 | 33 / 16 / 14 |
| Elo | 2024/25 | 0,084 | 0,088 | −0,200 | 0,28 | 24 / 14 / 4 |
| Poisson | 2025/26 | 0,172 | 0,243 | −0,847 | < 0,0001 | 20 / 20 / 18 |
| Dixon-Coles | 2025/26 | 0,304 | 0,295 | −0,015 | 1,0 | 7 / 6 / 4 |
| Elo | 2025/26 | 0,100 | 0,098 | +0,104 | 1,0 | 20 / 4 / 4 |

Trend osobno dla jesieni i wiosny (`?toRound=18` i `?fromRound=19`), ρ i p po Holmie między modelami:

| Model | Jesień 4–18: 2024/25 | Jesień: 2025/26 | Wiosna 19–34: 2024/25 | Wiosna: 2025/26 |
| --- | --- | --- | --- | --- |
| Poisson | −0,807 (0,0008) | −0,821 (0,0005) | −0,494 (0,16) | −0,147 (1,0) |
| Dixon-Coles | −0,525 (0,089; surowe p 0,045) | −0,407 (0,26) | −0,424 (0,20) | +0,156 (1,0) |
| Elo | −0,236 (0,40) | +0,246 (0,38) | −0,171 (0,53) | −0,229 (1,0) |

Dryf w kolejkach 19–34 jest w runach 11–12 bitowo równy dryfowi runów 9–10: ten sam zbiór wchłoniętych meczów daje ten sam stan modelu bazowego, niezależnie od tego, czy kolejki 4–18 weszły treningiem, czy pętlą. Trend wiosny jest więc trendem obowiązujących runów 9–10. Na samej wiośnie (runy 9–10) osiadanie przy δ = 0,25 wypada w kolejce 19 albo 20 dla każdego modelu.

Odpowiedź na pytanie nr 2:

- **Poisson** — parametry wyraźnie wygasają w rundzie jesiennej (istotny trend w obu sezonach, także po Holmie) i osiadają pod koniec rundy jesiennej albo na przełomie rund: kolejka 20 przy δ = 0,25, 16–18 przy δ = 0,50. Wiosną dryf jest już na poziomie końcowym.
- **Dixon-Coles** osiada wcześniej i zależnie od sezonu. W 2024/25 od kolejki 16, przy istotnym spadku na całym zakresie 4–34. W 2025/26 od kolejki 6, bez żadnego trendu, czyli dryf od początku jest blisko swojego (najwyższego) poziomu. Jesienny spadek jest słabszy niż u Poissona i po Holmie nieistotny.
- **Elo** jest w stanie ustalonym od pierwszej ocenianej kolejki: nie ma trendu w żadnym zakresie, a kolejki osiadania (14 i 4) wyznaczają lokalne wahania rzędu progu, a nie wygaszanie.
- **Wiosną żaden model nie ma istotnego trendu** (najniższe p po Holmie 0,16). Runy z odcięciem na przerwie zimowej nie odpowiadają więc na pytanie nr 2 — do tego służą runy diagnostyczne.
- **Wrażliwość na δ.** Przy δ = 0,10 próg mieści się w wahaniach stanu ustalonego, bo wiosną krocząca średnia dryfu odchyla się od L nawet o 8–38%, zależnie od modelu i sezonu (runy 9–10). Osiadanie ucieka wtedy na koniec sezonu (kolejka 33) albo zależy od pojedynczych garbów. Przy δ = 0,25 i 0,50 kolejność jest w obu sezonach ta sama: Elo ≤ Dixon-Coles ≤ Poisson.

Liczby z obu tabel zwraca endpoint porównania (`GET …/runs/{RunId}/comparison` z `tolerance`, `fromRound`, `toRound`), więc da się je odtworzyć z bazy.

Ranking z wiązaniami (`Ranking.AverageRanks`) zwraca rangi średnie i sumę `t³−t` potrzebną do korekty wariancji w obu testach.

## Interfejs badawczy

Nowa sekcja w nawigacji, obok istniejących stron symulacji MC (te działają bez zmian):

| Strona | Trasa | Zawartość |
| --- | --- | --- |
| `ResearchRunsPage` | `/research` | lista badań ze statusem + formularz nowego badania |
| `ResearchRunDetailsPage` | `/research/{RunId}` | wykresy, podsumowania, testy istotności, stabilność, beniaminki, predykcje |
| `ModelRoundPredictionPage` | `/model-prediction` | predykcja jednej kolejki wybranym modelem |

`EvaluationRunForm` po wyborze sezonu odpytuje `season-structure` i pokazuje wykrytą przerwę zimową oraz beniaminków — kolejkę odcięcia można zostawić puste (auto) albo nadpisać. Parametry modeli (mnożniki formy, ξ, ridge, tolerancja δ i okno stabilności) siedzą w zwiniętym panelu, żeby nie zaśmiecać formularza.

Strona szczegółów ma sześć zakładek: **Przebieg w sezonie** (dwa wykresy `MudChart` — wybrana metryka po kolejkach i dryf parametrów, po jednej serii na model), **Podsumowanie**, **Istotność różnic**, **Stabilność**, **Beniaminki**, **Predykcje** (z filtrem modelu i kolejki; kliknięcie wiersza pokazuje pod tabelą macierz wyników 0–6 × 0–6, obramowana komórka = faktyczny wynik). Selektor metryki przełącza wykres (przeliczany po stronie przeglądarki z metryk per kolejka) i testy istotności par oraz beniaminków (przeliczane przez backend); zakładki „Podsumowanie" i „Stabilność" od metryki nie zależą — werdykt stabilności liczony jest wyłącznie z dryfu. Selektor i zakładki pojawiają się dopiero dla runu `Completed`.

Zakładka **Stabilność** ma tabelę: Model | Osiada od kolejki | Poziom dryfu L | Próg (1+δ)·L | Średni dryf | Trend ρ | p | p (Holm) | Okno. Brak osiadania pokazuje „nie osiągnięto", a trend nierozstrzygający — „—". p po Holmie jest zielone przy α = 0,05. Podpis pod tabelą definiuje L, próg (z faktycznym δ) i osiadanie oraz przypomina, że osiadanie czyta się razem z trendem. Strona używa δ i okna z `OptionsJson` runu; inne δ i zakres kolejek są dostępne przez parametry endpointu porównania.

**Oś Y wykresów.** `MudChart` (MudBlazor 8.2) ma krok osi Y jako liczbę całkowitą (`ChartOptions.YAxisTicks`, domyślnie 20), a oś biegnie od `floor(min/krok)·krok` do `ceil(max/krok)·krok`. Bez ustawień oś miała więc zakres 0–20, a metryki i dryf (wartości 0,05–1,2) leżały płasko przy zerze — kształtu dryfu nie dało się odczytać. Dlatego oba wykresy dostają serie przemnożone przez 1000, krok osi dobierany z zakresu danych („ładny" krok 1/2/5·10ᵏ, ok. 6 linii siatki) i format etykiet `0,.000`, który dzieli wartość z powrotem przez 1000 (specyfikator skalowania .NET). Na osi widać więc oryginalne jednostki z trzema miejscami po przecinku; wykres liniowy w tej wersji nie pokazuje wartości punktów, więc przeskalowane liczby nigdzie się nie wyświetlają.

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
