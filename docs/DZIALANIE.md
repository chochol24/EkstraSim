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
| `EkstraSim.Backend.Tests` | **(nowy)** test zgodności portu Poissona z niezmienionym silnikiem z pracy inżynierskiej na EF Core InMemory (xUnit) — sekcja „Weryfikacja portu względem oryginału” |

Stary silnik (`SimulatingService`) pozostaje nietknięty — nowa warstwa powstaje obok niego.

## Rdzeń obliczeniowy — wspólne typy

`EkstraSim.Prediction/Models`:

- `MatchData` — rekord meczu (id, data, kolejka, sezon, liga, drużyny, wynik). `IsPlayed` = oba wyniki niepuste.
- `TrainingOptions` — liga, badany sezon, **`SeasonChronology`** (lista id sezonów od najstarszego), parametry modeli. `PreviousSeasonId` wyliczane z chronologii, bo `Season.Id` nie gwarantuje kolejności czasowej. Typ jest rekordem (`record`), więc wariant opcji powstaje przez `with` ([Analiza wrażliwości parametrów](#analiza-wrażliwości-parametrów)). Zamiana z klasy na rekord nie zmieniła serializacji: JSON z Newtonsoft jest znak w znak równy `EffectiveOptionsJson` runów 7, 9 i 10. Równość rekordu porównuje `SeasonChronology` po referencji listy, więc opcji zbudowanych z różnych list chronologii nie porównuje się przez `==` (dwie takie same chronologie w osobnych listach dają „różne" opcje). Wartość domyślna ξ (`TimeDecayXi`) pochodzi ze stałej `Constants.DixonColesTimeDecayXi` (`EkstraSim.Shared`), tej samej co w `CreateEvaluationRunRequest` i `PredictRoundRequest`, więc rdzeń, żądanie runu i `predict-round` mają jedną wartość domyślną (od wersji 6; test `DefaultOptionsTests` pilnuje bitowej równości trzech miejsc).
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

Składniki zwraca publiczna metoda `PoissonModel.FormComponents(mecz, drużyna, isHome)` jako rekord `FormFactors` (`Recent`, `Venue`, `HeadToHead`). `Predict` mnoży λ przez `FormFactors.Product`, czyli iloczyn składników w tej samej kolejności co wcześniej prywatny `FormMultiplier`. Wydzielenie nie zmieniło prognoz: prognozy przebiegów 13–16 z fixture (λ, P(1/X/2), skróty macierzy, dryf) i odpowiedzi `comparison` runów 13–16 są bitowo takie same jak przed nim. Kolejność mnożenia pilnuje test `FormProductMultipliesFromLeftToRight`, bo przy innej kolejności iloczyn różni się na ostatnim bicie.

**Charakter modelu.** To heurystyka ilorazu średnich z mnożnikami formy, a nie model Poissona dopasowywany metodą największej wiarygodności. Taki model opisał Maher (*Modelling association football scores*, Statistica Neerlandica 36(3), 1982, s. 109–118, DOI 10.1111/j.1467-9574.1982.tb00782.x; opis bibliograficzny sprawdzony w Crossref, treści nie czytano).
- **Siła drużyny** to jej średnia podzielona przez średnią ligi, z jej własnych meczów, bez korekty o siłę faktycznych rywali.
- **Rozkład Poissona** służy tu, tak jak w Dixonie-Colesie i Elo, tylko do zamiany λ na macierz wyników (`ScoreGrid`).
- **Wynik „Poisson jest najsłabszy”** dotyczy więc tej heurystyki, a nie rozkładu Poissona. Dixon-Coles też liczy wynik z rozkładu Poissona, a siły drużyn dopasowuje łącznie na całej historii i wypada wyraźnie lepiej.
- **Nazewnictwo.** W pracy model nazywa się „modelem Poissona z pracy inżynierskiej”. W kodzie i w bazie zostaje nazwa `Poisson`.

**Cztery świadome odstępstwa od oryginału** (istotne dla porównywalności modeli):

1. **Poprawiona waga historyczna dla gościa.** Oryginał (`SimulatingService.cs:190`) mnoży składnik historyczny `awayPred` przez `PreviousSeasonScale` (0.3) zamiast `HistoricalScale` (0.03) — wagi sumują się tam do 1.27, co systematycznie zawyża oczekiwane gole gościa. Port używa poprawnej wagi (suma = 1.0).
2. **Średnie liczone dynamicznie względem badanego sezonu**, nie z kolumn w bazie wypełnianych przez `TeamService.UpdateAverageTeamGoals` z zahardkodowanymi `SeasonId == 6` / `== 1`. Dzięki temu ten sam model działa dla każdego sezonu bez ręcznych przeliczeń.
3. **Fallback per strona.** Oryginał przy pustym koszyku podmienia wszystkie cztery średnie na ligowe (`if (homeMatches <= 0 || awayMatches <= 0)`). Port podmienia tylko brakującą stronę — identyczne zachowanie gdy oba koszyki niepuste, dokładniejsze gdy drużyna ma tylko mecze domowe (start sezonu, beniaminki).
4. **Mnożniki formy zawsze aktywne i filtrowane po dacie.** Oryginał liczył je tylko dla `numberOfSimulations > 1` (artefakt trybu MC) i po całym cache'u meczów. Port stosuje je zawsze i bierze wyłącznie mecze o dacie **wcześniejszej** niż mecz przewidywany — bez tego filtra badanie walk-forward miałoby wyciek danych z przyszłości.

Model trzyma własną listę wchłoniętych meczów (`HashSet` po id — powtórne podanie tej samej kolejki jest bezpieczne) i przelicza średnie po każdym `Train`/`UpdateWithRound`. Skutek uboczny: poprawiony wynik meczu o już znanym id jest ignorowany.

Średnia ligowa, która służy za normalizator sił i wartość awaryjną, jest **wspólna dla wszystkich wchłoniętych sezonów** — nie jest liczona osobno dla każdego horyzontu. Okna formy przekraczają granice sezonów.

#### Weryfikacja portu względem oryginału

Test `PoissonLegacyParityTests` w osobnym projekcie `EkstraSim.Backend.Tests` uruchamia **niezmieniony** kod z pracy inżynierskiej na tych samych meczach co port i porównuje prognozy mecz po meczu.

**Baza i zasiew** (`LegacyDatabase`):
- Każda kolejka wiosen 2024/25 i 2025/26 dostaje nową bazę EF Core InMemory.
- Zasiew: liga 1, drużyny, mecze znane przed kolejką (historia, jesień i kolejki już ocenione) z wynikami oraz mecze kolejki bez wyników. Późniejszych meczów w bazie nie ma.
- `TeamService.UpdateAverageTeamGoals` ma zahardkodowane id sezonów, więc baza je przenumerowuje: badany → 6, poprzedni według chronologii → 1, pozostałe → 100 + Id.

**Uruchomienie:**
- Łańcuch jak w aplikacji: `LeagueService.UpdateAverageLeagueGoals(1)` → `TeamService.UpdateAverageTeamGoals()` → `SimulatingService.CreateAsync` → `SimulateRound(mecze kolejki, 1, N)`, przy N = 100 000.
- Port to `PoissonModel` trenowany od zera na tym samym zbiorze meczów, z `MaxGoals` = 20, bo oryginał liczy 1X2 także z wyników powyżej 10 goli.
- Żeby odtworzyć błąd wagi gościa (odstępstwo 1), λ gospodarza pochodzi z opcji domyślnych, a λ gościa z `HistoricalScale = 0,3`.

**Asercja:**
- Kryterium: |p_oryginał − p_port| ≤ 5·√(p(1−p)/N) + 0,002 dla p₁, p_X i p₂.
- Margines 0,002 pokrywa zaokrąglanie średnich drużyn do 3 miejsc w `TeamService`.
- Asercja obejmuje mecze, przed którymi żaden znany mecz z datą ≥ daty meczu nie dotyczy jego drużyn. Pozostałe mecze różnią się przez odstępstwo 4 (oryginał nie filtruje form po dacie) i idą tylko do raportu.
- Oryginał losuje bez ziarna (`new Random()` przy każdym meczu), więc test jest statystyczny; przy ok. 800 porównaniach na 5 błędach standardowych fałszywy alarm zdarza się rzadziej niż raz na tysiąc przebiegów.

| | 2024/25 | 2025/26 |
| --- | --- | --- |
| mecze w asercji / tylko w raporcie | 144 / 0 | 118 / 26 |
| największa różnica P(1/X/2) w asercji | rzędu 0,004–0,006 | rzędu 0,004–0,006 |
| największa różnica w pozostałych meczach | — | ok. 0,04 |
| RPS oryginału | ok. 0,2556 | ok. 0,2589 |
| RPS portu z błędem wagi gościa (te same mecze) | 0,2555 | 0,2591 |
| RPS portu poprawionego (te same mecze) | 0,2518 | 0,2605 |
| RPS stałej predykcji | 0,2281 | 0,2270 |

- **Wartości oryginału** zmieniają się między przebiegami, bo symulacja losuje bez ziarna. W ośmiu przebiegach największa różnica w asercji wyniosła 0,0036–0,0053, a RPS oryginału zmieniał się o najwyżej ±0,0003.
- **Granica asercji.** Różnica nigdy nie przekracza progu asercji, czyli ok. 0,01 przy p = 0,5.
- **Wartości portu i stałej** są deterministyczne.

- **Port jest wierny.** Jedyna różnica merytoryczna to poprawiona waga gościa, a w 26 meczach 2025/26 dochodzi brak filtra daty w oryginale.
- **Skutek błędu wagi gościa.** Błąd zawyża λ gościa średnio o 0,291 gola (od 0,104 do 0,706). Prawdopodobieństwa 1X2 zmienia najwyżej o 0,138 w 2024/25 i 0,148 w 2025/26.
- **λ z macierzy oryginału.** Oryginał zapisuje macierz tylko dla 0–10 goli. Średnia z niej odbiega od λ portu o więcej niż 0,02 w kilku meczach porównywalnych na sezon, prawie zawsze przy λ gospodarza powyżej ok. 3,6. To skutek obcięcia, a nie różnica w modelu; pojedyncze przypadki przy niższym λ to szum symulacji.
- **Oryginał też przegrywa ze stałą predykcją.** Słabość modelu to cecha metody z pracy inżynierskiej, a nie artefakt portu. Poprawka wagi gościa obniża RPS w 2024/25 o 0,0037, a w 2025/26 podnosi go o 0,0014, więc wniosku nie zmienia.

Projekt jest osobny, bo test trwa ok. 20 s i potrzebuje referencji do backendu oraz EF Core InMemory. `EkstraSim.Tests` zostaje przy samym rdzeniu. Uruchomienie z raportem:

```bash
dotnet test EkstraSim.Backend.Tests/EkstraSim.Backend.Tests.csproj --logger "console;verbosity=detailed"
```

#### Kalibracja — prognozy zbyt pewne

`CalibrationSummary` na prognozach przebiegów 13–14, wiosny po 144 mecze. Wartości dla 2024/25 / 2025/26, a „bez form” to Poisson z `UseFormFactors = false`:

| | Poisson | Poisson bez form | Dixon-Coles | Elo |
| --- | --- | --- | --- | --- |
| średnie λ gospodarza (faktycznie 1,611 / 1,444 gola) | 1,739 / 1,899 | 1,544 / 1,677 | 1,502 / 1,551 | 1,470 / 1,491 |
| nachylenie: faktyczne gole gospodarza względem λ (idealnie 1) | 0,427 / 0,239 | 0,777 / 0,299 | 1,089 / 0,828 | 1,134 / 1,142 |
| to samo dla goli gościa | −0,052 / 0,011 | 0,110 / 0,174 | 0,393 / 0,360 | 0,881 / 0,974 |
| odchylenie standardowe λ − μ | 1,282 / 1,074 | 0,648 / 0,585 | 0,521 / 0,477 | 0,526 / 0,398 |
| największe λ gospodarza | 4,34 / 5,45 | 2,87 / 3,61 | 2,56 / 2,78 | 2,33 / 2,36 |
| mecze z λ gospodarza > 3 | 16 / 17 | 0 / 6 | 0 / 0 | 0 / 0 |
| średnie P(remis) (faktycznie 0,243 / 0,278) | 0,216 / 0,221 | 0,244 / 0,240 | 0,257 / 0,263 | 0,251 / 0,253 |
| RPS | 0,2518 / 0,2604 | 0,2253 / 0,2352 | 0,2186 / 0,2207 | 0,2189 / 0,2169 |

**Krzywa niezawodności** (obie wiosny razem, 288 meczów):
- **Poisson, wysokie P(gospodarz).** Przy P ≥ 0,70 (70 meczów, średnio 0,805) gospodarz wygrywa w 0,514 przypadków.
- **Poisson, niskie P(gospodarz).** Przy P < 0,25 (43 mecze, średnio 0,169) gospodarz wygrywa w 0,395.
- **Poisson, P(gość) 0,40–0,55** (40 meczów, średnio 0,480): gość wygrywa w 0,225.
- **Największe P(gospodarz):** Poisson 0,975, bez form 0,893, Dixon-Coles 0,782, Elo 0,707.
- **Dixon-Coles i Elo** odbiegają od rzeczywistości o kilka punktów, np. Dixon-Coles 0,470 → 0,448 i 0,617 → 0,592, Elo 0,614 → 0,638.

**Mechanizm: mnożnik dom/wyjazd drugi raz dolicza przewagę własnego boiska.**
- **Składniki form** (średnie z wiosny, 2024/25 / 2025/26):
  - gospodarz: forma z 10 meczów 0,9941 / 1,0101, dom 1,0576 / 1,0757, H2H 1,0003 / 0,9969, łącznie ×1,0739 / ×1,0976;
  - gość: forma z 10 meczów 0,9951 / 0,9944, wyjazd 0,9306 / 0,9250, H2H 0,9997 / 1,0031, łącznie ×0,9459 / ×0,9345.
- **Skąd się bierze.** Gospodarze w historii treningowej mają średnio +0,3199 / +0,3403 gola różnicy na mecz. Przeciętna drużyna dostaje więc z formy u siebie przed przycięciem ok. 1 + 0,32/2 ≈ 1,16, a na wyjeździe ok. 0,84. Przycięcie do [0,8; 1,2] obniża te średnie do ok. 1,06–1,08 i 0,93.
- **Dlaczego to podwójne liczenie.** λ już opiera się na średnich ligowych osobno u siebie i na wyjeździe, więc przewaga własnego boiska wchodzi do niego dwa razy. To przesunięcie systematyczne na korzyść gospodarza, a nie sam szum: zawyżone λ gospodarza i zaniżone P(remis).
- **Bez form zostaje nadmierny rozrzut.** Nachylenie dla goli gospodarza w 2025/26 wynosi 0,299, a w meczach z λ > 3 jest 6. Najbardziej prawdopodobna przyczyna to ilorazy liczone z ok. 9 meczów bieżącego sezonu u siebie i na wyjeździe, ważone 0,67. Tej części nie badano osobno.
- **RPS bez form** jest na poziomie stałej predykcji: 0,2253 / 0,2352 przy 0,2281 / 0,2270. Wilcoxon dla tej jednej pary daje p 0,7855 / 0,4612.

Liczby przypina `BaselineAndCalibrationRealDataTests` (`Category=RealData`). Kalibracja, przedziały, składniki form i ich zgodność z prognozami walk-forwardu: λ z formami = λ bez form × iloczyn składników, także dla 72 prognoz z filtrem daty.

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
- **Wygaszanie czasowe.** Waga meczu `φ(t) = exp(−ξ · Δdni)` względem najnowszego znanego meczu; ξ z `TrainingOptions.TimeDecayXi`, domyślnie (od wersji 6) 0,0065/3,5 ≈ 0,00186 na dzień, czyli półokres ok. 373 dni — stopa 0,0065 na pół tygodnia z literatury przeliczona na dni. Do wersji 5 domyślne było 0,0065 stosowane na dzień (półokres ok. 107 dni). Pochodzenie wartości, jednostkę i wpływ ξ na wyniki opisuje [Analiza wrażliwości parametrów](#analiza-wrażliwości-parametrów).
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

**Ustawienia.** `LimitedMemoryBfgsMinimizer(gradientTolerance: 1e-9, parameterTolerance: 0, functionProgressTolerance: 0, memory: 10, maximumIterations: 2000)`. Zera wyłączają wyjścia „po stagnacji", więc jedynym regularnym wyjściem jest kryterium gradientowe. Na prawdziwych danych dopasowanie zajmuje 122–196 iteracji przy ξ wersji 5 i 61–98 przy ξ wersji 6 (także przy odcięciu po kolejce 3), po kilkadziesiąt milisekund (Nelder-Mead: 13,7–18,2 tys. iteracji, ~1–2 s), więc limit 2000 daje co najmniej ~10× zapasu.

**Kryterium akceptacji.** Dopasowanie jest przyjęte, gdy spełnione są wszystkie cztery warunki:

1. punkt końcowy jest dopuszczalny: `f(x*) < 1e12` (nie jest karą za τ ≤ 0 albo λ, μ ≤ 0),
2. żaden parametr logarytmiczny (`a`, `d`, `g`) nie leży na przycięciu `|v| ≥ 20`,
3. `ReasonForExit` ∈ {`AbsoluteGradient`, `RelativeGradient`},
4. niezależnie policzone (z gradientu analitycznego w punkcie końcowym) `‖∇f(x*)‖∞ ≤ 1e-6 · max(1, |f(x*)|)`.

Warunki 1–2 są potrzebne, bo w obu sytuacjach gradient jest **zerowy z konstrukcji**, więc warunek 4 przechodzi trywialnie. Punkt niedopuszczalny dostaje karę `1e12` i gradient 0 — jeśli niedopuszczalny jest już **analityczny start**, Math.NET kończy w iteracji 0 z wyjściem gradientowym (przykład: 5× 30:0 i 1× 0:1 tych samych drużyn → λ_start ≈ 98, τ(0:1) = 1 − 0,03·98 < 0). Za przycięciem maska zeruje gradient współrzędnej, więc „rozwiązanie" z parametrem uciekającym do nieskończoności (MLE nie istnieje) wyglądało jak punkt stacjonarny. Oba przypadki znalazł przegląd implementacji (sondy, 2026-10-09): przed poprawką były po cichu przyjmowane — ta sama klasa błędu co dawny powrót do punktu startowego. Na prawdziwych danych nie występują (34/34 dopasowań wersji 5 i 98/98 wersji 6 — 34 przy odcięciu na przerwie zimowej i 64 przy odcięciu po kolejce 3: `f` ≈ 130–270, parametry logarytmiczne rzędu ±1).

**Punkt niedopuszczalny w trakcie optymalizacji.** Line search nie zawsze omija obszar kary. Na syntetycznej mini-lidze z testów widoczności (6 drużyn, dwa sezony, mecze przełożone) L-BFGS przy ξ = 0,0065/3,5 kończy w punkcie z karą: NLL 61,941 → 1e12 po 75 iteracjach, z wyjściem „AbsoluteGradient", bo gradient kary jest zerowy. Ta sama liga zbiega przy ξ = 0; 0,001; 0,002; 0,003; 0,004; 0,005; 0,0065 i 0,01, a bez meczów przełożonych przy każdym ξ. To przypadek numeryczny zależny od danych i ξ, a nie cecha wartości domyślnej. Warunek 1 odrzuca taki wynik głośnym błędem (run `Failed`, nie zła liczba). Optymalizator nie ma ścieżki wyjścia z takiego punktu (np. ponowienia z ostatniego punktu dopuszczalnego), co dotyczy każdego ξ. Na prawdziwych danych przypadek nie wystąpił w żadnym dopasowaniu. `WalkForwardVisibilityTests` liczą z jawnym ξ = 0,0065, bo sprawdzają widoczność meczów, a nie zbieżność. Sam przypadek przypina test `DixonColesEndsInAnInfeasiblePointOnThisLeagueAtTheDefaultTimeDecay`: przy wartości domyślnej ξ wymaga błędu „punkt niedopuszczalny" po kolejce 7, a przy 0,0065 przebiegu bez błędu. Po naprawie optymalizatora test zrobi się czerwony, a ten akapit trzeba będzie zaktualizować.

Warunek 4 jest konieczny, bo `ReasonForExit` z Math.NET 5.0 nie jest wiarygodny — zmierzone na dwa sposoby:

- kryterium `AbsoluteGradient` jest skalowane przez |f|: przy `gradientTolerance = 1e-6` przepuściło ‖∇f‖∞ ≈ 2·10⁻⁴;
- po wyczerpaniu limitu iteracji minimizer **nie rzuca** `MaximumIterationsException`, tylko zwraca wynik z `ReasonForExit = AbsoluteGradient` i `Iterations` = limit + 1 (limit 1 na danych syntetycznych: „AbsoluteGradient" przy ‖∇f‖∞ = 2,18). Bez niezależnego warunku taki punkt przeszedłby jako optimum.

Przy |f| ≈ 130–270 próg wynosi 1,3–2,7·10⁻⁴; obserwowane na prawdziwych danych ‖∇f‖∞ ≤ 2,4·10⁻⁷ (wersja 5) i ≤ 8,1·10⁻⁷ (wersja 6).

**Brak zbieżności = głośny błąd.** Każde odrzucone dopasowanie — wyjątek Math.NET (`OptimizationException`, np. line search: „Direction is not a descent direction"), punkt niedopuszczalny, parametr na granicy przycięcia, wyjście niegradientowe, niespełniony warunek stacjonarności, wyczerpany limit — kończy się `ModelConvergenceException` z polskim komunikatem, np. `Dixon-Coles: optymalizacja nie zbiegła (trening; 2 iteracji; powód: wyczerpany limit 1 iteracji; wyjście MathNet: AbsoluteGradient; NLL 352.675 → 352.651; ‖∇f‖∞ = 2.18E+000).` Pole `Reason` podaje faktyczną przyczynę odrzucenia, a nie etykietę z Math.NET (patrz wyżej). W kodzie nie ma `catch`, który zwraca punkt startowy. Po wyjątku model zachowuje poprzednie dopasowanie w spójnej postaci — indeks drużyn i parametry przypisywane są razem dopiero po akceptacji (test `FailedUpdateKeepsThePreviousFitConsistent`); mecze z odrzuconej kolejki zostają jednak wchłonięte. Orkiestrator ustawia wtedy run na `Failed` z tym komunikatem w `ErrorMessage`, a `predict-round` zwraca go w kopercie błędu — run `Completed` gwarantuje MLE we wszystkich kolejkach.

**Raporty dopasowań.** `DixonColesModel.FitReports` — po jednym `DixonColesFitReport` na każde przyjęte dopasowanie: `AfterRound` (`null` dla treningu), `Dimension`, `Iterations`, `ExitReason`, `StartObjective`, `FinalObjective`, `GradientNorm` (‖∇f‖∞). Lista jest czyszczona w `Train`. Raporty nie trafiają do bazy — polityka głośnego błędu i tak gwarantuje zbieżność w runach `Completed`; czyta je test na prawdziwych danych.

**Zimny start.** Każde dopasowanie startuje od analitycznego punktu startowego, nie od poprzedniego optimum. Wynik zależy wtedy tylko od zbioru meczów, a nie od ścieżki dopasowań: `Train` + kolejne `UpdateWithRound` dają te same parametry co jedno `Train` na tym samym zbiorze (tak liczy `predict-round`). Ciepły start by tę zgodność zepsuł, a przy kilkudziesięciu milisekundach na dopasowanie nie jest potrzebny.

**MLE nie zawsze istnieje.** Przy `RidgeLambda = 0` drużyna z jednym meczem 7:0 i zerem straconych goli nie ma skończonego optimum: wiarygodność rośnie monotonicznie, gdy jej obrona β → 0. Nelder-Mead zatrzymywał się „gdzieś" na tej dolinie, a L-BFGS kończy się wyjątkiem z line search. Podobnie drużyna, która nigdy nie strzela gola: przy ridge 0 jej atak α → 0 i parametr dochodzi do przycięcia ±20 — warunek 2 kryterium akceptacji odrzuca takie dopasowanie (test `ParameterDrivenToTheClampBoundaryThrows`; przy ridge 0,05 ta sama drużyna ma |log α| ≈ 5 i dopasowanie jest poprawne). Dlatego test `RidgeShrinksSparseTeamsTowardsLeagueAverage` porównuje `RidgeLambda` 0,05 z 5,0 (zamiast 0 z 5,0) — z ridge > 0 kara kwadratowa w `log β` daje skończone optimum. Domyślne opcje badań mają `RidgeLambda = 0.05`. Inne ustawienia (ridge 0, ξ = 0) mogą trafić na przypadki bez MLE — wtedy run kończy się błędem, nie złą liczbą (test `InfeasibleStartingPointThrowsInsteadOfFallingBack` pilnuje też niedopuszczalnego startu).

**Weryfikacja na prawdziwych danych.** Test `DixonColesRealDataTests` (`[Trait("Category", "RealData")]`) czyta fixture `EkstraSim.Tests/Data/ekstraklasa-liga1-mecze.csv` i odtwarza przez `WalkForwardEvaluator.Run` sekwencję dopasowań 2024/25 i 2025/26 w trzech wariantach: opcje wersji 5 (`RealDataFixture.OptionsVersion5`: formy włączone, ξ = 0,0065, ridge 0,05) z odcięciem 18 (runy 9–10), wartości domyślne wersji 6 z odcięciem 18 (runy 13–14) i z odcięciem 3 (runy 15–16). Wymaga:

- dokładnie 17 raportów na sezon przy odcięciu 18 (trening + 16 kolejek) i 32 przy odcięciu 3 (trening + 31 kolejek); modele tymczasowe filtra daty to osobne instancje i nie wchodzą do tej sekwencji, ale brak ich zbieżności przerwałby `Run` wyjątkiem,
- w każdym: wyjścia gradientowego, ‖∇f‖∞ w granicy akceptacji i `FinalObjective < StartObjective` (ruch ze startu),
- kotwicy na opcjach wersji 5: NLL(start) treningu 2024/25 = 248,939860312 (±1e-8), NLL końcowe ≤ 248,738 (wynik Neldera-Meada).

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

Ranking jest więc **zerosumowy** — suma ocen wszystkich drużyn nie zmienia się w trakcie replayu. Nowe drużyny startują z 1300. Wartość startowa nie wpływa na predykcje, bo liczą się tylko różnice ocen ([Analiza wrażliwości parametrów](#analiza-wrażliwości-parametrów)).

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
- **Jeden helper dla pętli i `predict-round`.** Część „dla każdego meczu: wyznacz ukryte, wybierz model bazowy albo tymczasowy, przewiduj" to publiczna metoda `WalkForwardEvaluator.PredictWithDateFilter`. Przyjmuje wytrenowany model bazowy, wchłonięte mecze, mecze do przewidzenia, opcje, fabrykę modeli i numer kolejki (do kontekstu błędu), a zwraca predykcje w kolejności wejścia i Id meczów zawężonych. Kesz modeli tymczasowych żyje w obrębie jednego wywołania, a pętla woła helper raz na kolejkę. Tego samego helpera używa `predict-round` (sekcja „Endpointy"), więc reguła „brak danych z przyszłości" jest w aplikacji jedna. Wydzielenie nie zmieniło wyników: testy widoczności i `RealData` (0 / 72 zawężenia, 0 / 9 modeli tymczasowych) przechodzą bez zmian, a run kontrolny z opcjami runu 10 dał predykcje, metryki kolejek i dryf bitowo równe runowi 10.
- **Dlaczego to poprawne.** Wszystkie trzy modele są wsadowe: stan to czysta funkcja zbioru wchłoniętych meczów. Poisson przelicza agregaty, Elo odtwarza replay od zera, a Dixon-Coles dopasowuje od analitycznego startu (zimny start). `Train(podzbiór)` daje więc dokładnie stan „gdyby znano tylko ten podzbiór", taki sam jak po `Train` i kolejnych `UpdateWithRound` na tych samych meczach.
- **Ścieżka bazowa nietknięta.** Model bazowy przechodzi przez te same wywołania `Train(historia)` i `UpdateWithRound` (po kolejkach, mecze w kolejności Id) co w wersji 4, a model tymczasowy to osobna instancja, która go nie modyfikuje. Dla zawężonych meczów `Predict` modelu bazowego jest pomijany, ale `Predict` nie zmienia stanu żadnego z trzech modeli, więc stan bazowy jest taki sam jak w wersji 4. Predykcje bez meczów ukrytych są więc bitowo identyczne z wersją 4, a filtr nie zmienia dryfu (liczonego ze snapshotów modelu bazowego). Porównanie predykcji z runami wersji 4 pokazuje wyłącznie skutek filtra. Dryf wersji 5 różni się od wersji 4 tylko przez zawężenie do drużyn aktywnych („Metryka dryfu parametrów"). Sprawdzone na fixture z prawdziwymi danymi dla każdego modelu: 2024/25 — 144 ze 144 predykcji identycznych bitowo; 2025/26 — 72 identyczne i 72 zmienione (dokładnie zawężone). Sam filtr daty nie zmienił dryfu w żadnej z 16 kolejek. Runy potwierdzają to samo: run 9 względem runu 7 — 432 z 432 predykcji identycznych bitowo (łącznie z macierzami wyników); run 10 względem runu 8 — różnice dokładnie w 216 wierszach (72 mecze × 3 modele), a zbiór meczów z różnicą równa się zbiorowi wyznaczonemu niezależnie z dat w tabeli `Matches`.
- **Wchłonięte = historia + ocenione już kolejki.** Mecz z późniejszej kolejki rozegrany wcześniej nie jest wchłaniany przed swoją kolejką. To ostrożne (model wie mniej, niż mógłby), ale nie wnosi danych z przyszłości. Pętla zostaje po kolejkach: pełny walk-forward po dniach, w którym mecz widzi wcześniejsze mecze tej samej kolejki, nie jest celem.
- **Błąd dopasowania.** Dopasowanie modelu tymczasowego może rzucić tak samo jak dopasowanie bazowe (Dixon-Coles: `ModelConvergenceException`). Run kończy się wtedy `Failed`, jak przy błędzie modelu bazowego. Ewaluator rzuca wyjątek ponownie z powodem uzupełnionym o kontekst (model tymczasowy filtra daty, kolejka, Id meczu, liczba ukrytych meczów), a oryginał zostaje jako `InnerException`. Bez tego komunikat wskazywałby trening początkowy: model tymczasowy powstaje przez `Train`, więc jego raport dopasowania ma `AfterRound = null` i opis „trening". Na danych 2025/26 wszystkie 9 dopasowań tymczasowych jest zbieżnych (w wersji 5 i 6).

Pomiar na fixture z prawdziwymi danymi i w runach 9–10 oraz 13–14 (odcięcie 18; log orkiestratora podaje te same liczby; zawężenia zależą tylko od dat, nie od ξ):

| Sezon | Mecze rozegrane po przewidywanych | Zawężone predykcje (na model) | Modele tymczasowe (na model) |
| --- | --- | --- | --- |
| 2024/25 | brak | 0 ze 144 | 0 |
| 2025/26 | 2 w treningu (z kolejek 4 i 16, rozegrane w lutym i marcu) i 3 w pętli (z kolejki 19 wchłonięty przed kolejkami 20–23, dwa z kolejki 31 wchłonięte przed kolejką 32) | 72 ze 144, w kolejkach 19–25 i 32 | 9 |

Mecze z treningu dotykają 63 predykcji, mecze z pętli 45; zbiory się pokrywają, razem 72.

W runach diagnostycznych z odcięciem po kolejce 3 (runy 11–12 i 15–16, do pytania nr 2 i do okien kolejek pytania nr 3) zawężonych jest 135 (2024/25) i 205 (2025/26) z 279 predykcji na model, bo do zbioru ewaluacyjnego wchodzą wtedy także jesienne mecze przełożone. Test `Version6RealDataTests.DateFilterNarrowingsDoNotDependOnTheTimeDecay` przypina 0 / 72 / 135 / 205 i równość zbiorów zawężonych meczów w wersji 5 i 6.

**Skutek dla wyników (2025/26, run 8 → run 10).** Filtr minimalnie poprawił metryki probabilistyczne wszystkich modeli:

- RPS: Poisson 0,2605 → 0,2604, Dixon-Coles 0,2326 → 0,2324, Elo 0,2170 → 0,2169; Brier i log-loss też zmieniają się o najwyżej 0,0003,
- trafność 1X2 bez zmian, top-1 i top-3 różnią się o najwyżej jeden mecz na model,
- p po Holmie z testu Wilcoxona (RPS): Dixon-Coles–Elo 0,0030 → 0,0035, Dixon-Coles–Poisson 0,0144 → 0,0129, Elo–Poisson 0,0028 → 0,0027.

Wszystkie trzy pary są nadal istotne, a ranking Elo < Dixon-Coles < Poisson i wnioski z pytania nr 1 się nie zmieniają. W 2024/25 predykcje, a więc i wszystkie testy, są identyczne z wersją 4.

**Testy.** `WalkForwardVisibilityTests` porównuje każdą predykcję pętli z modelem referencyjnym (`WalkForwardReference`), trenowanym od zera na dokładnie widocznym zbiorze, z tolerancją 1e-9. Obejmuje Poissona, Dixona-Colesa i Elo. Fixture ma 6 drużyn, pełny dwurundowy sezon poprzedni (30 meczów, żeby Elo miało ≥ 20 próbek do regresji) i sezon docelowy ze zmiennymi wynikami, z odcięciem po kolejce 4. Wariant „przełożony" przesuwa mecz z kolejki 2 za kolejkę 6 i mecz z kolejki 7 za kolejkę 8, co daje 9 dotkniętych predykcji w kolejkach 5, 6 i 8. Opcje testu mają jawne ξ = 0,0065: przy ξ wersji 6 Dixon-Coles kończy na tej lidze w punkcie niedopuszczalnym (sekcja Dixona-Colesa, „Punkt niedopuszczalny w trakcie optymalizacji"), a test dotyczy widoczności meczów, nie zbieżności.

Asercje mocy (`FixtureDetectsAbsorbingARoundBeforePredicting`, `FixtureDetectsMatchesPlayedAfterThePredictedOne`) sprawdzają, że dołożenie kolejki R albo meczu rozegranego później zmienia predykcję o więcej niż 1e-6. Bez nich zgodność z referencją niczego by nie dowodziła. Poprzednie testy pętli miały fixture z identycznymi wynikami w każdej kolejce, więc nie wykryłyby wchłaniania przed predykcją, i dotyczyły tylko Poissona.

`WalkForwardRealDataTests` (`Category=RealData`) robi to samo na fixture z prawdziwymi danymi:
- niezależnie wyznacza zawężenia (0 i 72, w kolejkach 19–25 i 32),
- sprawdza zgodność każdej predykcji z referencją i równość `DateFilteredMatchIds` z tym zbiorem,
- sprawdza, że liczba utworzonych modeli tymczasowych równa się liczbie różnych zbiorów ukrytych w kolejkach (0 i 9).

Na kodzie wersji 4 testy „po dacie" były czerwone (wszystkie trzy modele, sezon 2025/26), a testy „po kolejce" i asercje mocy zielone.

`BuildHistory` bierze rozegrane mecze z sezonów wcześniejszych w chronologii **oraz** kolejki ≤ odcięcie z sezonu badanego. `BuildEvaluationSet` bierze kolejki > odcięcie, **tylko rozegrane** — dzięki temu trwający sezon (np. 2026/27) ocenia się na tym, co już się odbyło, a nierozegrane kolejki są po prostu pomijane.

### Kolejka odcięcia i beniaminki

- **Odcięcie** domyślnie wykrywane automatycznie (`SeasonCalendar.DetectSplit`): największa przerwa między datami kolejnych kolejek = przerwa zimowa. Można nadpisać ręcznie w żądaniu. Wyjątek: w **2019/20** największa przerwa to pauza COVID po kolejce 26 (81 dni), nie zimowa po kolejce 20 (48 dni) — dla tego sezonu odcięcie trzeba podać ręcznie. Przełożony mecz z ostatniej kolejki jesiennej rozegrany wiosną też potrafi przesunąć wykryty podział. Tak jest w **2023/24**: mecze przełożone z kolejek 17 i 20 zasłaniają przerwę zimową i `DetectSplit` wskazuje kolejkę 25 zamiast 19 ([Analiza wrażliwości parametrów](#analiza-wrażliwości-parametrów)).
- **Beniaminki** (`PromotedTeamsService`): drużyny mające mecze w sezonie S i żadnego w S−1. Chronologia sezonów liczona z **najwcześniejszej daty meczu**, nie z `Season.Id` ani nazwy — Id nie gwarantuje kolejności czasowej. Lista jest zapisywana jako snapshot JSON na rekordzie badania, żeby wynik dał się odtworzyć nawet po dodaniu nowych sezonów. Orkiestrator czyta ze snapshotu wyłącznie `TeamId` (flaga `InvolvesPromotedTeam` predykcji), więc dodatkowe pola snapshotu nie wpływają na predykcje.
- **Historia beniaminka** (`PromotedTeamHistory.Compute`, `EkstraSim.Prediction/Evaluation`): `PriorMatchCount` to liczba **rozegranych** meczów **ligi badania** z udziałem drużyny w sezonach **wcześniejszych w chronologii** (tej samej, liczonej po dacie), a `LastPriorSeasonId` — najpóźniejszy w chronologii taki sezon, nie sezon o największym Id. Sezon badany i późniejsze się nie liczą, a sezon spoza chronologii to błąd. Kategoria (`PromotedTeamHistory.CategoryOf`): 0 meczów → **zimny start** (`ColdStart`), więcej → **powracający** (`Returning`). „0" znaczy „brak meczów od najstarszego sezonu w bazie" (2019/20), więc import starszych sezonów zmieniłby liczby runów liczonych przy odczycie. Reguła kategorii i liczenie historii istnieją tylko w rdzeniu — backend ich nie powiela.
- **Snapshot nowych runów a stare runy.** `GetPromotedTeamsAsync` zwraca beniaminków z historią (`PriorMatchCount`, `LastPriorSeasonId`, `LastPriorSeasonName`, `Category`), więc runy tworzone po tej zmianie zapisują ją w snapshocie. Snapshoty runów 1–12 mają same `TeamId` i `Name`. `PromotedTeamsService.CompleteHistoryAsync` uzupełnia przy odczycie wpisy bez `PriorMatchCount` tą samą funkcją rdzenia, na meczach ligi z bazy, i dla każdego wpisu przelicza kategorię z liczby meczów. Kategoria zapisana w snapshocie nie jest więc źródłem prawdy, a zbiór drużyn zawsze pochodzi ze snapshotu — beniaminki nie są wyznaczane od nowa. `comparison` czyta snapshot przy każdym odczycie, więc uszkodzony snapshot kończy błędem całą odpowiedź, a nie tylko stopniowanie. To świadome: run `Completed` ma snapshot już raz przeczytany przez orkiestrator tym samym deserializatorem (niepoprawny JSON skończyłby run jako `Failed`), a powtórzony `TeamId` `PromotedTeamsService.ReadSnapshot` pomija (zostaje pierwszy wpis), tak jak dawne `ToHashSet` w orkiestratorze.

### Stopniowanie beniaminków (pytanie nr 3)

Flaga binarna „mecz z beniaminkiem" zlepia prawdziwy zimny start (drużyna bez żadnej historii w bazie — modele znają ją wyłącznie z meczów badanego sezonu) z powrotem po przerwie (Lechia Gdańsk i Wisła Płock mają po 135 meczów historii). Porównanie stopniowane (`ModelComparison.PromotedByCategory`) stoi obok niezmienionego `PromotedVersusRest` i odpowiada na pytanie, czy kłopotem jest brak danych, czy sam awans. Reguły:

- **Testy kategorii.** Dla każdego modelu (porządek nazw jak w porównaniu par) i każdej kategorii obecnej wśród beniaminków runu (najpierw zimny start, potem powracający) — Mann-Whitney: mecze z udziałem **co najmniej jednej** drużyny tej kategorii vs mecze **bez żadnego beniaminka**. Grupa porównawcza to dokładnie „Pozostałe" z tabeli binarnej (ten sam zbiór meczów, ta sama średnia), bo oba zbiory wynikają z tego samego snapshotu.
- **Mecz dwóch beniaminków** należy do kategorii **każdej** drużyny-beniaminka, która w nim gra (np. GKS Katowice–Lechia Gdańsk jest i w zimnym starcie, i w powracających). To poprawne, bo każdy test porównuje kategorię z meczami bez beniaminka, a nie kategorie ze sobą. Mecz dwóch drużyn tej samej kategorii liczy się w jej grupie raz.
- **Jedna rodzina Holma na run** — wszystkie modele × kategorie obecne w runie. Kategoria bez żadnej drużyny (zimny start w 2025/26) **nie daje wierszy**, więc nie zawyża poprawki. Kategoria z drużynami, ale z mniej niż 4 meczami w grupie, daje wiersz nierozstrzygający z p = 1, który do rodziny wchodzi (jak w oknach binarnych).
- **Tylko cały zakres** — bez okien kolejek i bez bezpośredniego testu zimny start vs powracający.
- **Średnie per drużyna** — dla każdego beniaminka i modelu średnia metryki z meczów z udziałem drużyny i ich liczba, bez testów (przy 14–16 meczach na drużynę byłyby bez mocy).
- Mecz bez wpisu w mapie drużyn to błąd (`ArgumentException`) — backend przekazuje drużyny wszystkich predykcji runu.

Wszystko liczone jest przy odczycie `comparison`, więc działa dla każdego runu `Completed`, także sprzed zmiany, bez migracji i bez nowych runów. Wersja algorytmu się nie zmienia (5): predykcje, flaga i dryf są nietknięte. Run kontrolny 2025/26 z opcjami runu 10, policzony po zmianie, zapisał w snapshocie historię 37 / 34 / 135 i był bitowo równy runowi 10 (432 predykcje, 48 metryk kolejek, całe `comparison` dla czterech metryk łącznie ze stopniowaniem), czyli historia ze snapshotu = historia liczona przy odczycie.

W 2025/26 wszyscy beniaminki są powracający, więc wiersz „powracający vs reszta" jest z konstrukcji **identyczny** z wierszem binarnym „cały zakres" (te same 45 i 99 meczów, ta sama rodzina Holma 3 = 3). Informację dla tego sezonu wnosi tylko tabela per drużyna.

**Testy.** `PromotedTeamHistoryTests` przypina reguły historii (chronologia ≠ kolejność Id, ostatni sezon według chronologii, tylko rozegrane mecze ligi z sezonów wcześniejszych, sezon spoza chronologii → wyjątek) i kategorii. Testy stopniowania w `StatisticsTests` sprawdzają mecz mieszany w obu grupach i w średnich obu drużyn, pominięcie pustej kategorii, wiersz nierozstrzygający w rodzinie, rodzinę Holma (porównanie z `HolmCorrection.Adjust` na surowych p), resztę równą „Pozostałym" i wyjątek dla meczu bez drużyn. `PromotedTeamRealDataTests` (`Category=RealData`) na fixture z prawdziwymi danymi przypina beniaminków wyznaczonych tą samą definicją co `PromotedTeamsService` (drużyny z meczami w S bez meczów w S−1; sam kod serwisu, który testami nie jest objęty, sprawdzono przez API na runach 9–12) i ich historie (2024/25: GKS 0, Motor 0, Lechia 135 z ostatnim sezonem 2022/23; 2025/26: Arka 37 / 2019/20, Termalica 34 / 2021/22, Wisła Płock 135 / 2022/23), liczności wiosny (2024/25: zimny start 31, powracający 16, reszta 99; 2025/26: brak zimnego startu, powracający 45, reszta 99), równość reszty ze zbiorem `!InvolvesPromotedTeam` i równość wiersza powracających z wierszem binarnym w 2025/26 dla wszystkich czterech metryk. Wisła Płock wraca po **dwóch** sezonach przerwy (ostatnio 2022/23), nie po jednym.

#### Wyniki (wersja 6, runy 13–16, RPS)

Średnia per drużyna na wiośnie (runy 13–14; po 16 meczów na drużynę, w tym mecze z innymi beniaminkami), obok średniej meczów bez beniaminka. Elo i Poisson są bitowo takie same jak w wersji 5 (runy 9–10); w nawiasie Dixon-Coles wersji 5:

| Sezon | Beniaminek | Kategoria (meczów przed sezonem) | Dixon-Coles | Elo | Poisson |
| --- | --- | --- | --- | --- | --- |
| 2024/25 | GKS Katowice | zimny start (0) | 0,237 (0,250) | 0,237 | 0,231 |
| 2024/25 | Motor Lublin | zimny start (0) | 0,220 (0,234) | 0,215 | 0,259 |
| 2024/25 | Lechia Gdańsk | powracający (135) | 0,266 (0,285) | 0,259 | 0,301 |
| 2024/25 | mecze bez beniaminka (99) | — | 0,209 (0,217) | 0,210 | 0,247 |
| 2025/26 | Arka Gdynia | powracający (37) | 0,203 (0,215) | 0,194 | 0,233 |
| 2025/26 | Bruk-Bet Termalica Nieciecza | powracający (34) | 0,213 (0,226) | 0,211 | 0,244 |
| 2025/26 | Wisła Płock | powracający (135) | 0,303 (0,312) | 0,284 | 0,341 |
| 2025/26 | mecze bez beniaminka (99) | — | 0,216 (0,228) | 0,215 | 0,260 |

Testy kategorii (różnica średnich kategoria − reszta, p po Holmie w rodzinie runu):

| Run | Kategoria (n vs reszta) | Dixon-Coles | Elo | Poisson |
| --- | --- | --- | --- | --- |
| 13 (2024/25) | zimny start (31 vs 99) | +0,018 (0,97) | +0,016 (0,97) | −0,001 (0,97) |
| 13 | powracający (16 vs 99) | +0,057 (0,29) | +0,048 (0,37) | +0,054 (0,97) |
| 14 (2025/26) | powracający (45 vs 99) | +0,014 (1,0) | +0,006 (1,0) | +0,001 (1,0) |

Wersja 5 (runy 9–10) różniła się tylko Dixonem-Colesem: zimny start +0,022, powracający +0,068 (0,41), a Elo powracający miał po Holmie 0,41 (rodzina Holma obejmuje wszystkie modele runu). Binarnie (wszyscy beniaminki razem) w 2024/25 różnice wynoszą +0,016 (Poisson) … +0,031 (Dixon-Coles), p po Holmie 0,24–0,51 (wersja 5: Dixon-Coles +0,036, 0,32–0,51), a w 2025/26 +0,0006 … +0,0137, p po Holmie 1,0.

Żadna różnica nie jest istotna. Kierunek jest odwrotny niż intuicja „brak danych = gorsza predykcja": najgorzej wypadają drużyny z najdłuższą historią (Lechia i Wisła Płock, po 135 meczów), a zimny start jest prawie na poziomie meczów bez beniaminka (surowe p 0,30–0,85). Tak samo krótka historia po dłuższej przerwie (Arka, Termalica) nie przeszkadza. Przy 16 meczach na drużynę to poziom szumu, więc wniosek dla pracy brzmi: dane nie pokazują, żeby modele radziły sobie wiosną gorzej z drużynami bez historii, a binarną różnicę z 2024/25 w dużej mierze niesie powracająca Lechia. Wolniejsze wygaszanie (wersja 6) obniżyło RPS Dixona-Colesa we wszystkich grupach, a kary Lechii i Wisły Płock nie usunęło — zgodnie z analizą wrażliwości („Pytanie nr 3: kara powracających to cecha meczów").

W runach diagnostycznych 15–16 (kolejki 4–34; służą pytaniu nr 2 i oknom kolejek pytania nr 3, a porównanie na całym zakresie jest tu tylko uzupełnieniem, bo trening na trzech kolejkach to inny problem predykcyjny) zimny start odstaje mocniej (run 15: +0,038 / +0,026 / +0,045 dla Dixona-Colesa / Elo / Poissona, surowe p 0,040–0,096, po Holmie 0,24–0,38; wersja 5, run 11: +0,039 / +0,026 / +0,045, po Holmie 0,45), co zgadza się z oczekiwaniem, że brak historii szkodzi najbardziej na początku sezonu. Liczności: run 15 — 60 / 31 / 191, run 16 — 87 / 192.

**Okna kolejek** (`PromotedVersusRest` z `RoundWindows`, binarnie, RPS; wszyscy beniaminki razem). Różnica beniaminki − reszta w oknach 19–23 / 24–28 / 29–33 / 34, runy 13–14:

| Model | 2024/25 | 2025/26 |
| --- | --- | --- |
| Dixon-Coles | +0,057 / −0,045 / +0,058 / +0,177 | −0,051 / +0,033 / +0,059 / +0,033 |
| Elo | +0,050 / −0,039 / +0,050 / +0,153 | −0,058 / +0,022 / +0,053 / +0,031 |
| Poisson | +0,023 / −0,066 / +0,040 / +0,335 | −0,125 / +0,025 / +0,077 / +0,127 |

- **Liczności beniaminki / reszta:** 13 / 32, 15 / 30, 15 / 30, 2 / 7 w 2024/25 i 15 / 30, 14 / 31, 13 / 32, 3 / 6 w 2025/26.
- **Żaden wiersz nie jest istotny** (rodzina Holma 12 wierszy na run, najniższe p po Holmie 0,3972). Okno 34 ma 2–3 mecze z beniaminkiem, więc test jest tam nierozstrzygający (p = 1).
- **Znaki różnic zmieniają się** między oknami i sezonami. Wiosną nie widać więc, żeby trudność z beniaminkami zanikała z kolejnymi kolejkami.
- **Runy diagnostyczne 15–16** (okna od 4–8 do 34, rodzina 21 wierszy; liczby z `comparison`, nie przypięte testem) też nie mają istotnego wiersza (najniższe p po Holmie 0,6952 i 0,7829). W 2024/25 jesienne różnice Dixona-Colesa wynoszą +0,021 / +0,048 / +0,044, czyli nie maleją.

Wiersze runów 13–14 przypina `BaselineAndCalibrationRealDataTests.PromotedWindowsPinTheRows`.

### Encje i przepływ

Migracja `research_evaluation_runs` jest **wyłącznie addytywna** — trzy nowe tabele, zero zmian w istniejących:

| Tabela | Zawartość |
| --- | --- |
| `ModelEvaluationRuns` | parametry badania, lista modeli, opcje JSON (z żądania i efektywne), wersja algorytmu, snapshot beniaminków, status (`Pending`/`Running`/`Completed`/`Failed`), znaczniki czasu |
| `ModelPredictions` | jedna predykcja = model × mecz: λ, P(1/X/2), typowany wynik, macierz 11×11 jako JSON, faktyczny wynik i wszystkie metryki per mecz |
| `ModelRoundMetrics` | agregaty per model × kolejka + `ParameterDrift` (dla pytania nr 2) |

Uruchomienie jest **asynchroniczne**: endpoint tworzy rekord ze statusem `Pending`, zwraca jego Id i oddaje pracę `ResearchRunLauncher` (singleton), który w `Task.Run` (fire-and-forget, bez anulowania) otwiera świeży scope DI — bez tego scoped orkiestrator zniknąłby razem z zakresem żądania. Frontend **nie odpytuje statusu automatycznie** — strona runu `Pending` albo `Running` pokazuje przycisk „Odśwież".

**Cykl życia statusu:** `Pending` (rekord utworzony) → `Running` → `Completed` albo `Failed`. Niezmiennik: run `Completed` ma komplet wierszy wyników, a run `Failed` nie ma żadnego i ma komunikat w `ErrorMessage`. Pilnują tego trzy mechanizmy.

- **Atomowy zapis wyników.** Orkiestrator (`ResearchOrchestrationService.ExecuteAsync`) liczy wszystkie modele w pamięci. Metryki kolejek, wszystkie predykcje, liczniki (`EvaluatedMatchCount`, `EvaluatedRoundCount`), `EffectiveOptionsJson`, status `Completed` i `FinishedAt` zapisuje **jednym** `SaveChangesAsync`. Pojedynczy `SaveChanges` EF Core wykonuje się we własnej transakcji (`UseSqlServer` bez strategii ponawiania, więc jawna transakcja nie jest potrzebna): zapisuje się wszystko albo nic, czyli `Completed` ⇔ komplet wierszy z definicji. Wcześniej predykcje szły partiami po 500, każda partia w osobnej transakcji, a błąd w połowie zostawiał w bazie część wierszy. Jeden zapis ~430–840 predykcji z macierzami JSON trwa poniżej sekundy, bo EF i tak dzieli go na batche poleceń.
- **Ścieżka błędu.** Odczyt runu i zapis `Running` (razem z `StartedAt` i `AlgorithmVersion`) leżą wewnątrz `try`, więc błąd już na tym etapie też kończy się `Failed`, a nie runem zawieszonym w `Pending`. W `catch` orkiestrator najpierw czyści `ChangeTracker`, potem czyta run od nowa i zapisuje `Failed` z `FinishedAt`. Bez czyszczenia nieudane encje `Added` (metryki, predykcje) byłyby dalej śledzone, ponowny zapis rzuciłby ten sam wyjątek, a run zostałby w `Running` na zawsze. Tak działo się wcześniej np. przy duplikacie nazwy modelu `["Poisson","poisson"]`, który naruszał unikalny indeks `(ModelEvaluationRunId, ModelName, Round)` na `ModelRoundMetrics`. Run `Failed` zachowuje metadane: `StartedAt`, `AlgorithmVersion` i `EffectiveOptionsJson`, jeśli opcje zdążyły się zbudować (orkiestrator trzyma je w zmiennej, bo `Clear` je gubi). `ErrorMessage` to komunikat wyjątku, a dla `DbUpdateException` komunikat wyjątku wewnętrznego, czyli treść błędu SQL Servera zamiast ogólnika EF „See the inner exception". Pozostałych wyjątków orkiestrator nie rozpakowuje do `GetBaseException()`: błąd modelu tymczasowego filtra daty niesie kontekst (kolejka, mecz) w zewnętrznym komunikacie, a oryginał w `InnerException`. Jeśli świeży odczyt pokazuje już `Completed`, ścieżka błędu niczego nie zmienia: połączenie zerwało się po zatwierdzeniu zapisu wyników, więc klient dostał wyjątek mimo commitu, a run ma komplet wierszy. Bez tego warunku taki run zostałby oznaczony jako `Failed` z wierszami wyników. Jeśli zapis `Failed` też się nie uda (np. baza nieosiągalna), zostaje tylko log, a run domknie odzyskiwanie przy następnym starcie.
- **Odzyskiwanie przy starcie.** `ResearchRunRecovery.RecoverInterruptedRunsAsync` jednym `UPDATE` (`ExecuteUpdateAsync`) oznacza wszystkie runy `Pending` i `Running` jako `Failed`, z `FinishedAt` i komunikatem „Obliczenia przerwane restartem backendu — uruchom badanie ponownie.". Liczbę odzyskanych runów zapisuje w logu, jako ostrzeżenie, gdy jest większa od zera. Taki run nie ma wierszy wyników, bo wyniki zapisują się dopiero razem z `Completed`. Run przerwany przed zapisem `Running` nie ma `AlgorithmVersion`, a przerwany później nie ma `EffectiveOptionsJson`, bo opcje zapisują się z końcowym statusem. Błąd odzyskiwania jest logowany, a start backendu trwa dalej. Przerwane runy nie są wznawiane automatycznie — badanie trzeba uruchomić ponownie.

**Dlaczego odzyskiwanie siedzi w `Program.cs`, a nie w `IHostedService`.** Wywołanie stoi między `builder.Build()` a `app.Run()`, więc kończy się, zanim serwer przyjmie pierwsze żądanie. Żaden nowy run `Pending` nie może więc powstać w trakcie odzyskiwania i zostać przez nie oznaczony jako przerwany. Usługa hostowana takiej gwarancji nie daje, bo jej start względem serwera HTTP zależy od kolejności usług hosta.

**Założenie: jedna instancja backendu na bazę.** Odzyskiwanie zakłada, że w chwili startu nikt inny nie liczy runów na tej bazie. Drugi backend uruchomiony na tej samej bazie oznaczyłby run liczony przez pierwszy jako `Failed`. Lokalnie i przy jednej instancji App Service założenie jest spełnione.

Druga migracja badawcza, `research_run_algorithm_version`, też jest addytywna: dwie nullowalne kolumny w `ModelEvaluationRuns` (`AlgorithmVersion`, `EffectiveOptionsJson`), bez wpływu na istniejące wiersze — opis w [Wersjonowanie badań](#wersjonowanie-badań).

### Wersjonowanie badań

Run zapisuje, **który kod go policzył** i **z jakimi opcjami faktycznie**, żeby zmiany algorytmu (jak zmiana formuły dryfu czy optymalizatora Dixona-Colesa) były widoczne w danych, a nie tylko w ręcznym komentarzu.

- **`AlgorithmVersion`** — numer z `ResearchAlgorithm.Version` (`EkstraSim.Prediction/Evaluation`, jedyne źródło). Orkiestrator ustawia go razem ze statusem `Running`, więc to wersja kodu, który run **wykonał**, a nie tego, który utworzył rekord `Pending`. Run `Failed` też go ma: jeśli błąd wystąpił przed zapisem `Running`, ścieżka błędu uzupełnia wersję przy zapisie `Failed` (wyjątek: run przerwany restartem, zanim zapisał `Running` — „Encje i przepływ").
- **`EffectiveOptionsJson`** — zserializowane (Newtonsoft) `TrainingOptions` z `BuildOptions`, czyli opcje faktycznie przekazane modelom: łącznie z wartościami domyślnymi spoza żądania (`MaxGoals`, wagi horyzontów Poissona, parametry Elo) i `SeasonChronology`. Ustawiane przed liczeniem predykcji i zapisywane z końcowym statusem. Run `Failed` zachowuje je, jeśli błąd wystąpił po zbudowaniu opcji. Run przerwany restartem ich nie ma.
- **`OptionsJson` vs `EffectiveOptionsJson`.** `OptionsJson` to żądanie w postaci, w jakiej przyszło (`CreateEvaluationRunRequest`) — zawiera też `StabilityTolerance`/`StabilityWindow`, które czyta dopiero endpoint porównania przy każdym odczycie, a nie run. Starsze runy mają w `OptionsJson` dawne pole `StabilityThreshold` (próg absolutny): Newtonsoft pomija nieznane pola, więc deserializują się bez błędu i dostają domyślne δ = 0,25. `EffectiveOptionsJson` to to, co dostały modele. Pierwsze odpowiada na pytanie „o co poproszono", drugie — „co policzono".
- `NULL` w obu kolumnach znaczy „nieznane / sprzed wersjonowania". UI pokazuje wtedy „—": kolumna „Wersja" na `/research` i „Wersja algorytmu: N" w nagłówku `/research/{RunId}`.

| Wersja | Runy | Co się zmieniło |
| --- | --- | --- |
| 1 | 1–2 | dryf jako surowa norma L2 |
| 2 | 3–4 | dryf znormalizowany jednym, globalnym σ |
| 3 | 5–6 | dryf znormalizowany per rodzina parametrów |
| 4 | 7–8 | Dixon-Coles: L-BFGS z gradientem analitycznym i głośnym błędem zamiast cichego powrotu do punktu startowego |
| 5 | 9–12 (11–12: odcięcie po kolejce 3, tylko pytanie nr 2) | filtr daty w walk-forward (mecze przełożone); dryf liczony tylko po drużynach aktywnych w badanym sezonie |
| 6 | 13–16 (15–16: odcięcie po kolejce 3, tylko pytanie nr 2) | domyślne ξ Dixona-Colesa 0,0065/3,5 na dzień (wartość z literatury) zamiast 0,0065 na dzień |

Predykcje w wersjach 1–3 są w obrębie sezonu identyczne (zmieniała się tylko metryka dryfu); Dixon-Coles ma w nich cichy powrót do punktu startowego (sekcja Dixona-Colesa, „Historia"). Wersja 5 zmienia predykcje wyłącznie tam, gdzie działa filtr daty (2025/26: 72 mecze na model), a pozostałe są bitowo identyczne z wersją 4 (sekcja „Idea" wyżej). Zmienia też dryf każdego modelu przez zawężenie do drużyn aktywnych (sekcja „Metryka dryfu parametrów").

Wersja 6 zmienia wyłącznie wartość domyślną ξ (decyzja i jej uzasadnienie: [Analiza wrażliwości parametrów](#analiza-wrażliwości-parametrów)), więc wyłącznie predykcje i dryf Dixona-Colesa. Runy 13–16 względem runów 9–12 (te same sezony i odcięcia): Poisson i Elo bez jednej różnicy w predykcjach (wszystkie kolumny, także macierze wyników), metrykach kolejek i dryfie; Dixon-Coles różni się w każdym wierszu. `EffectiveOptionsJson` runów 13–16 różni się od runów 9–12 tylko polem `TimeDecayXi` (`0.0018571428571428571` zamiast `0.0065`). Fixture odtwarza runy 13–16 bitowo: 2538 z 2538 par (model, mecz) i 282 z 282 wartości dryfu. Kod modeli się nie zmienił, więc run z jawnie podanym ξ = 0,0065 liczy się jak w wersji 5 — kotwice runów 9–10 przechodzą na `RealDataFixture.OptionsVersion5` bez zmiany liczb.

**Reguła podbijania:** `ResearchAlgorithm.Version` rośnie przy każdej zmianie kodu, która zmienia predykcje lub dryf któregokolwiek modelu — także przy zmianie wartości domyślnej, która zmienia predykcje runu uruchomionego bez jawnych opcji (wersja 6). Zmiany samej prezentacji (UI, endpointy odczytu, testy statystyczne liczone przy odczycie) wersji nie zmieniają. Nie zmieniają jej też zmiany odporności przepływu: atomowy zapis wyników, ścieżka błędu, odzyskiwanie po restarcie, bramka statusu i walidacja wejść. Run kontrolny 2025/26 z opcjami runu 10, policzony po tych zmianach (z listą modeli podaną jako `["Poisson","poisson"," Elo ","DixonColes"]`), był bitowo równy runowi 10: 432 predykcje (wszystkie kolumny liczbowe i macierze wyników) oraz 48 metryk kolejek łącznie z dryfem.

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
| POST | `api/research/runs` | start badania; wejście walidowane przed zapisem rekordu (niżej) |
| GET | `api/research/runs` | lista badań (filtry: liga, sezon) |
| GET | `api/research/runs/{RunId}` | status i podsumowanie (dla każdego statusu) |
| GET | `api/research/runs/{RunId}/round-metrics` | metryki per kolejka (dane do wykresów); tylko run `Completed` |
| GET | `api/research/runs/{RunId}/predictions` | predykcje (filtry: model, kolejka); tylko run `Completed` |
| GET | `api/research/runs/{RunId}/comparison` | podsumowania, testy istotności, beniaminki, stabilność; tylko run `Completed`. Parametry zapytania: `metric` — nazwa członka `MetricKind` bez względu na wielkość liter (puste = `RankedProbability`; liczba albo nieznana nazwa → błąd z listą dostępnych), `tolerance` — δ analizy stabilności zamiast wartości z `OptionsJson` runu (ujemne albo nieskończone → błąd), `fromRound` i `toRound` — zakres kolejek **tylko dla analizy stabilności** (brakująca granica = bez ograniczenia; `fromRound > toRound` albo zakres bez ocenianych kolejek → błąd). Podsumowania, pary i beniaminki liczone są zawsze na całym runie. Stopniowanie beniaminków: `promotedTeams` (snapshot uzupełniony o historię i kategorię), `promotedByCategory` (testy kategorii, pole `category` = nazwa członka `PromotedCategory`; w wierszach binarnych (`promoted`) `category` jest `null`) i `promotedTeamMetrics` (średnia i liczba meczów per drużyna × model) — sekcja „Stopniowanie beniaminków" |
| PUT | `api/research/predict-round` | predykcja jednej kolejki wybranym modelem, bez zapisu — działa też dla kolejek nierozegranych. `Round` ≥ 1, odcięcie domyślnie `Round − 1`, a podane musi spełniać 0 ≤ odcięcie < `Round`. Ten sam filtr daty co w walk-forward (niżej). Jedno `Train`, więc dla kolejek dalszych niż odcięcie + 1 wynik nie odtwarza predykcji z runu; mecze nierozegrane mają w polach wyniku i metryk 0, nie `null` |

**Błędy walidacji i odczytu** wracają tak jak pozostałe błędy serwisów: kopertą `EkstraSimResult` z `Success = false`, polskim komunikatem z zasobów `SnackbarMessages` (klucze `Research_*`) i kodem HTTP 500. Kodów 400/404/409 celowo nie ma — frontend czyta kopertę przy każdym kodzie. DataAnnotations na requestach nie są egzekwowane (FastEndpoints działa tu bez walidatorów), więc o poprawności wejść decydują serwisy. Reguły niezależne od bazy siedzą w `EkstraSim.Shared/Requests/ResearchRequestRules.cs`, a nazwy modeli i metryk w `EkstraSim.Prediction` (`PredictionModelFactory.CanonicalName`, `MetricKindExtensions.TryParseName`), bo `Shared` nie referencjonuje `Prediction`. Dzięki temu wszystkie reguły są pod testami jednostkowymi, choć backend testów nie ma.

**Walidacja `POST runs`.** Żaden parametr badania nie jest po cichu przeinterpretowany, a błąd walidacji nie zapisuje rekordu:

- **Modele.** Każda nazwa przechodzi przez `CanonicalName` (trim, bez względu na wielkość liter → nazwa z `AvailableModels`). Nieznana nazwa → błąd z listą dostępnych modeli. Wcześniej nieznane nazwy znikały po cichu, a gdy znikały wszystkie, run liczył wszystkie modele. Duplikaty po kanonizacji są usuwane z zachowaniem kolejności pierwszego wystąpienia, więc `["Poisson","poisson"," Elo ","DixonColes"]` daje kolumnę `Models = Poisson,Elo,DixonColes`. **Pusta lista znaczy „wszystkie trzy modele"** — tak powstały runy 7–12 (`"Models":[]` w `OptionsJson`). `OptionsJson` zapisuje żądanie w postaci, w jakiej przyszło, a kolumna `Models` — nazwy kanoniczne.
- **Liczby.** `TrainingLastRound`, jeśli podane, ≥ 1. `TimeDecayXi`, `RidgeLambda` i `StabilityTolerance` skończone i ≥ 0. `StabilityWindow` ≥ 1, bo `StabilityAnalysis.RollingMean` traktuje mniejsze okno jak 1, a `OptionsJson` pokazywałby wartość, która nie została użyta. Regułę δ dzielą tworzenie runu i `comparison`.
- **Odcięcie z kolejkami do oceny.** Po wyznaczeniu odcięcia (podanego albo automatycznego) sezon musi mieć co najmniej jeden rozegrany mecz w kolejce powyżej odcięcia. Inaczej błąd od razu, a nie run `Failed` dopiero w tle.

**Deterministyczny odczyt `comparison`.** Predykcje runu czytane są w kolejności (`MatchId`, `Id`). Wcześniej zapytanie nie miało `ORDER BY`, a SQL Server zwracał wiersze raz w kolejności indeksu `(ModelEvaluationRunId, MatchId)`, a raz inaczej (zwykle przy pierwszym odczycie runu po starcie backendu). Średnie (podsumowania, beniaminki) sumują się w tej kolejności, więc dwa wywołania tego samego runu potrafiły różnić się na ostatnich cyfrach (względnie do 2·10⁻¹²); p-wartości i testy par od kolejności nie zależą. Obecna kolejność odpowiada dawnej „zwykłej", więc wszystkie stare pola odpowiedzi dla runów 1–12 i czterech metryk są bitowo równe odpowiedziom wcześniejszego kodu.

**Bramka statusu.** `round-metrics`, `predictions` i `comparison` przed odczytem wyników sprawdzają, czy run istnieje i jest `Completed`. Dla runu nieistniejącego, liczonego (`Pending`/`Running`) i zakończonego błędem zwracają osobny komunikat. Dzięki atomowemu zapisowi run `Failed` i tak nie ma wierszy, ale bramka sprawia, że API nie odpowiada sukcesem z pustą listą tam, gdzie wyników nie ma. `runs` i `runs/{RunId}` bramki nie mają, bo pokazują status.

**`predict-round` bez danych z przyszłości.** Wchłonięte mecze to `BuildHistory` (rozegrane mecze sezonów wcześniejszych i kolejek ≤ odcięcie badanego sezonu), model bazowy to `Train` na nich, a mecze kolejki przewiduje `PredictWithDateFilter` (sekcja „Idea"). Mecz rozegrany w dniu przewidywanego meczu albo później (np. przełożony z kolejki ≤ odcięcie) nie wchodzi więc do stanu modelu, którym liczona jest predykcja. Dla kolejki odcięcie + 1 wynik jest z konstrukcji równy predykcjom pierwszej ocenianej kolejki runu. Pilnuje tego test `PredictRoundPathEqualsTheFirstEvaluatedRound` na prawdziwych danych 2025/26 (kolejka 19, odcięcie 18, trzy modele, równość bitowa). Asercja mocy w tym teście wymaga, żeby zwykłe `Predict` modelu bazowego różniło się co najmniej na jednym meczu. W kolejce 19 zawężone są wszystkie 9 meczów, bo mecze z kolejek 4 i 16 rozegrano w lutym i marcu. Przez API to samo sprawdzono na bazie: odpowiedź `predict-round` dla kolejki 19 przy odcięciu 18 równa się predykcjom kolejki 19 z runu 10 dla każdego modelu (λ, P(1/X/2), typowany wynik, macierz). Wcześniej `predict-round` przyjmował też odcięcie ≥ `Round`, czyli trening na przewidywanej kolejce.

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
- **Mann-Whitney U** (`MannWhitneyUTest`) — pytanie nr 3. Test dla prób niezależnych (mecze z beniaminkiem vs pozostałe, a w porównaniu stopniowanym mecze z beniaminkiem danej kategorii vs mecze bez beniaminka), ta sama aproksymacja z korektami. Każda grupa musi mieć co najmniej 4 wartości, inaczej wynik jest nierozstrzygający (p = 1).
- **Holm-Bonferroni** (`HolmCorrection`) — poprawka na wielokrotne porównania (3 pary modeli, a przy podziale na okna kolejek więcej). Bez niej przy kilkunastu testach fałszywe „istotności" pojawiają się same. Testy nierozstrzygające wchodzą z p = 1 i zwiększają liczbę porównań m, co czyni korektę bardziej konserwatywną. Okna beniaminków to kolejne porcje po 5 ocenianych kolejek — dla kolejek 19–34 ostatnie okno ma tylko kolejkę 34. Buduje je `ModelComparison.RoundWindows` (przy mniej niż 4 kolejkach okien nie ma), wspólne dla `comparison` w backendzie i testów. Przeniesienie z prywatnej metody serwisu nie zmieniło odpowiedzi `comparison` runów 13–16 (porównane bitowo przed i po).
- `BetterModel` w porównaniu par wybierany jest po **średniej** metryki, a Wilcoxon testuje położenie rang — przy skośnych rozkładach kierunki mogą się różnić. Metryki trafności (1X2, top-1, top-3) nie są `MetricKind`, więc nie podlegają testom istotności.
- **`ModelComparison`** — składa całość: `Pairwise` (każda para modeli na wspólnym podzbiorze meczów), `PromotedVersusRest` (beniaminki vs reszta, opcjonalnie w oknach kolejek, żeby zobaczyć *kiedy* różnica zanika; całość i okna to dwie osobne rodziny Holma) i `PromotedByCategory` (zimny start / powracający vs mecze bez beniaminka z jedną rodziną Holma na run plus średnie per drużyna — sekcja „Stopniowanie beniaminków"). Wiersze binarne i stopniowane budują ten sam kod (`BuildGroups`, `ApplyHolm`), a stopniowane różnią się polem `Category`. `VersusBaseline` porównuje każdy model z jedną linią bazową testem Wilcoxona: jeden wiersz na model (`SecondModel` = nazwa linii bazowej), pary łączone po `MatchId` jak w `Pairwise`. Jedna rodzina Holma obejmuje modele wywołania i jest osobna od rodziny par modeli. Linia bazowa nie ma rozkładu wyniku, więc `ProbabilityOfActualScore` kończy się `ArgumentException`.
- **Punkty odniesienia i diagnostyka** (`EkstraSim.Prediction/Evaluation`, poza backendem i UI):
  - `ConstantBaseline` — stała predykcja 1X2, opisana w „Analiza wrażliwości parametrów”. Jej trafność 1X2 to reguła „zawsze najczęstszy rezultat”, w badanych sezonach „zawsze wygrywa gospodarz”.
  - `ConstantScoreBaseline` — reguła „zawsze najczęstszy wynik historii treningowej” dla trafności top-1. `Fit` bierze rozegrane mecze ligi i remisy liczności rozstrzyga jak `ScoreGrid.RankedScores` (mniej goli łącznie, potem mniej goli gospodarza). `Evaluate` zwraca `ExactScoreHit` dla rozegranych meczów w kolejności Id.
  - `CalibrationSummary` — dla listy prognoz z faktycznymi wynikami (po `MatchId`):
    - średnie λ, μ i faktycznych goli;
    - nachylenie regresji faktycznych goli na λ i na μ (najmniejsze kwadraty z wyrazem wolnym);
    - populacyjne odchylenie standardowe λ − μ, największe λ i liczba meczów z λ > 3;
    - średnie P(1/X/2) i częstości rezultatów;
    - przedziały niezawodności dla P(gospodarz) i P(gość). Granice domyślnie 0; 0,25; 0,40; 0,55; 0,70; 1, przedział [od, do), ostatni domknięty, pusty z NaN.
    - Nachylenie przy zerowej wariancji λ to NaN.
    - `ArgumentException`: pusta lista prognoz, rosnące granice w liczbie mniejszej niż dwie, mecz bez rozegranego wyniku, dwie prognozy jednego meczu, prognoza spoza granic przedziałów albo NaN.
  - `MonteCarloNoise` — z dokładnej macierzy każdej prognozy losuje N wyników (jedno `Random` z podanym ziarnem, modele w porządku nazw, mecze po Id) i liczy RPS z częstości 1/X/2. Po R powtórzeniach podaje przesunięcie i odchylenie średniego RPS oraz odsetek powtórzeń z odwróconą kolejnością pary modeli. Mierzy, co wniosłaby do badania symulacja Monte Carlo z pracy inżynierskiej (N = 1000 na mecz).
    - Prognoza bez macierzy albo z macierzą o zerowej sumie kończy się `ArgumentException`.
    - Odsetek odwróceń dla pary o równych średnich dokładnych też kończy się `ArgumentException`, bo bez kolejności nie ma czego odwracać.
  - `VersusBaseline` odrzuca pustą linię bazową, a `PoissonModel.FormComponents` drużynę, która nie gra w meczu po wskazanej stronie (`ArgumentException`).
- **`StabilityAnalysis`** — pytanie nr 2. Dla każdego modelu liczy średnią kroczącą metryki i dryfu parametrów (okno `StabilityWindow`, domyślnie 3) oraz:
  - **poziom dryfu L** (`DriftLevel`) — średni surowy dryf z ostatnich k kolejek, k = min(n, max(okno, ⌈n/3⌉)); dla 16 kolejek wiosny k = 6, dla 31 kolejek (odcięcie po 3.) k = 11,
  - **średni dryf** (`MeanDrift`) ze wszystkich kolejek,
  - **próg względny** `Threshold` = (1+δ)·L, gdzie δ to tolerancja (`StabilityAnalysis.DefaultTolerance` = 0,25),
  - **`StabilisedFromRound`** — pierwsza kolejka, **od której do końca** krocząca średnia dryfu nie przekracza progu. Świadomie nie jest to „pierwszy spadek poniżej progu" — chwilowe wyciszenie, po którym parametry znów skaczą, nie jest stabilnością,
  - **trend** (`Trend`) — test Spearmana dryfu surowego względem numeru kolejki.

  Od zmiany kryterium próg jest względny wobec własnego poziomu modelu, a nie wspólny i absolutny — powód w „Konsekwencja dla pytania badawczego nr 2". Całość liczy się przy odczycie, więc działa też dla starych runów i nie zmienia wersji algorytmu.

  `StabilityAnalysis.DetectAll` składa analizę dla wszystkich modeli runu, tak jak `ModelComparison` dla par: zawęża obserwacje do zakresu kolejek (`fromRound`/`toRound`), pomija modele bez kolejek w zakresie, woła `Detect` dla każdego modelu (kolejność nazw porządkowa) i poprawia p trendu Holmem między modelami (`TrendAdjustedPValue`). Serwis backendu tylko waliduje żądanie i mapuje wynik na DTO, więc cała statystyka stabilności jest w testowanym projekcie `EkstraSim.Prediction`.
- **Spearman** (`SpearmanTrendTest`) — test trendu dla pytania nr 2: ρ to korelacja Pearsona na rangach średnich (`Ranking.AverageRanks`, zgodna z `MathNet.Numerics.Statistics.Correlation.Spearman` także przy wiązaniach). Statystyka t = ρ·√((n−2)/(1−ρ²)) (mianownik podłogowany na 1e-12, żeby przy |ρ| = 1 nie było nieskończoności), p dwustronne z rozkładu t-Studenta o n−2 stopniach swobody. Poniżej 6 obserwacji albo przy zerowej wariancji rang (np. stały dryf) wynik jest nierozstrzygający (p = 1). p trendu poprawiane jest Holmem między modelami runu; nierozstrzygające wchodzą z p = 1.

### Wyniki pytań nr 1 i 4 (wersja 6, runy 13–14)

Runda wiosenna, 144 mecze w każdym sezonie, odcięcie po kolejce 18. Liczby z `comparison` runów 13–14 (bitowo równe fixture, przypięte w `Version6RealDataTests`):

| Model | Sezon | RPS | Brier | Log-loss | 1X2 | top-1 | top-3 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Dixon-Coles | 2024/25 | 0,2186 | 0,6215 | 1,0339 | 44,4% | 11,1% | 30,6% |
| Elo | 2024/25 | 0,2189 | 0,6211 | 1,0333 | 45,1% | 16,0% | 31,9% |
| Poisson | 2024/25 | 0,2518 | 0,6941 | 1,1493 | 38,9% | 8,3% | 20,8% |
| Elo | 2025/26 | 0,2169 | 0,6345 | 1,0534 | 45,1% | 12,5% | 34,0% |
| Dixon-Coles | 2025/26 | 0,2207 | 0,6457 | 1,0699 | 44,4% | 11,1% | 35,4% |
| Poisson | 2025/26 | 0,2604 | 0,7324 | 1,2145 | 40,3% | 11,8% | 27,8% |

Test Wilcoxona (RPS), p / p po Holmie: 2024/25 — DC–Elo 0,7870 / 0,7870, DC–Poisson 0,0166 / **0,0497**, Elo–Poisson 0,0278 / 0,0555; 2025/26 — DC–Elo 0,2219 / 0,2219, DC–Poisson 0,0008 / **0,0023**, Elo–Poisson 0,0009 / **0,0023**.

- **Pytanie nr 1.** Poisson (port z pracy inżynierskiej) jest najsłabszy w obu sezonach; w 2025/26 obie pary z nim są istotne, w 2024/25 tylko DC–Poisson i na granicy. Dixon-Coles i Elo są nierozróżnialni: kolejność odwraca się między sezonami, a para nie jest istotna. Odporność tych wniosków na pozostałe parametry: [Decyzja: wersja 6](#decyzja-wersja-6).
- **Pytanie nr 4.** Trafność dokładnego wyniku top-1 (8–16%, czyli 12–23 mecze) jest za mała, żeby porządkować modele — kolejność top-1 zmieniała się w historii runów przez różnice 1–3 trafień (naprawa optymalizatora, filtr daty). Top-3 oddziela Poissona (20,8% i 27,8%) od pozostałych, ale Dixon-Coles i Elo różnią się o 2 mecze w każdym sezonie, w przeciwne strony (Elo 46 vs 44, DC 51 vs 49).
- **Wersja 5 (runy 9–10, ξ 0,0065 na dzień)** różniła się tylko Dixonem-Colesem (RPS 0,2287 / 0,2324, top-3 26,4% / 31,9%): ranking E < D < P w obu sezonach i w 2025/26 wszystkie trzy pary istotne (DC–Elo 0,0035). Zmiana ξ przestawiła więc wniosek o Dixonie-Colesie i Elo, a wniosek o Poissonie się utrzymał.

**Modele a stała predykcja.** Test Wilcoxona każdego modelu wobec stałej predykcji (`VersusBaseline`, rodzina Holma trzech modeli w sezonie). RPS modelu vs stałej, p / p po Holmie:

| Sezon | Dixon-Coles | Elo | Poisson |
| --- | --- | --- | --- |
| 2024/25 (stała 0,2281) | 0,2186; 0,1818 / 0,4574 | 0,2189; 0,1525 / 0,4574 | 0,2518; 0,2303 / 0,4574 |
| 2025/26 (stała 0,2270) | 0,2207; 0,2008 / 0,2008 | 0,2169; 0,0280 / 0,0841 | 0,2604; 0,0588 / 0,1176 |

- **Wynik:** żaden model nie różni się od stałej predykcji istotnie po Holmie, w żadnym sezonie. Dixon-Coles i Elo są od niej lepsi tylko opisowo, a Poisson tylko opisowo gorszy.
- **Brier i log-loss** dają ten sam obraz. Stała predykcja ma Brier 0,6408 / 0,6548 i log-loss 1,0613 / 1,0813. W 2025/26 surowe p Elo wynosi 0,0334 (Brier) i 0,0501 (log-loss), a Poissona 0,0382 i 0,0615; po Holmie najniższe jest 0,1003. W 2024/25 wszystkie p po Holmie są ≥ 0,4489.
- **Rodzina testów** na sezon to trzy modele, osobno od rodziny trzech par modeli. Test samego Poissona bez rodziny daje p 0,2303 / 0,0588. Wniosek się nie zmienia.

**Reguły odniesienia trafności** (te same 144 mecze):
- **„Zawsze wygrywa gospodarz”** (trafność 1X2 stałej predykcji) trafia rezultat 67 / 61 razy. Modele trafiają: Elo 65 / 65, Dixon-Coles 64 / 64, Poisson 56 / 58.
- **„Zawsze 1:1”** (1:1 to najczęstszy wynik historii treningowej w obu sezonach) trafia dokładny wynik 20 / 20 razy. Top-1 modeli: Elo 23 / 18, Dixon-Coles 16 / 16, Poisson 12 / 17.
- **Wniosek:** liczba trafień nie odróżnia modeli od reguł, które nic nie wiedzą o drużynach. RPS pokazuje różnicę, której trafność nie widzi, bo nie uwzględnia pewności prognozy. To argument za RPS jako miarą główną.
- **Poisson bez form** trafia 62 / 63 rezultaty, 19 / 18 wyników top-1 i 41 / 43 wyniki top-3.

**Szum symulacji Monte Carlo** (`MonteCarloNoise`, N = 1000 wyników na mecz jak w pracy inżynierskiej, 200 powtórzeń, ziarno 20261014):
- **Rozrzut średniego RPS sezonu:** odchylenie standardowe 0,00088–0,00098 dla każdego modelu.
- **Przesunięcie średnio:** +0,00012…+0,00025, bo częstości z próby są gorszą prognozą niż dokładne prawdopodobieństwa.
- **Kolejność Dixon-Coles / Elo** (różnica 0,0003 w 2024/25) odwraca się w 42% powtórzeń w 2024/25 i w 0,5% w 2025/26.
- **Kolejność z Poissonem** nie odwraca się nigdy.

Badanie oparte na symulacji nie rozstrzygnęłoby więc kolejności Dixona-Colesa i Elo w 2024/25. Liczby przypina `BaselineAndCalibrationRealDataTests`.

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
| dryf tylko po drużynach aktywnych (wersja algorytmu 5) | 3,3× (2025/26: 2,8×) |
| **ξ Dixona-Colesa z literatury (wersja algorytmu 6)** | **2,1×** (2025/26: 1,7×) |

Zmiana metryki dotyczy wyłącznie dryfu — metryki predykcyjne (RPS, Brier, log-loss, trafności) są po niej **bitowo identyczne**, co potwierdzono porównaniem badań na tych samych danych. Wiersz wersji 4 to ta sama metryka po naprawie optymalizatora Dixona-Colesa (runy 7–8): zmienił się wyłącznie dryf DC. Wiersz wersji 5 to runy 9–10: zawężenie do 18 drużyn aktywnych (filtr daty wersji 5 dryfu nie zmienia) — patrz „Nieaktywne drużyny — hipoteza obalona". Ostatni wiersz to runy 13–14: metryka bez zmian, a dryf Dixona-Colesa spadł o połowę przez wolniejsze wygaszanie czasowe; rozrzut wyznacza teraz para Poisson / Elo (dokładnie 2,06× i 1,68×).

### Pozostały rozrzut — częściowo sygnał, częściowo artefakt

Średni dryf (średnia z kroczącej średniej okna 3; 2024/25 → 2025/26):

| Model | Wersja 6 (runy 13–14) | Wersja 5 (runy 9–10) | Wersja 4 (runy 7–8, 28 drużyn) | Wersja 3 (runy 5–6) |
| --- | --- | --- | --- | --- |
| Elo | 0,086 → 0,104 | 0,086 → 0,104 | 0,065 → 0,072 | jak w wersji 4 |
| Poisson | 0,178 → 0,174 | 0,178 → 0,174 | 0,178 → 0,173 | jak w wersji 4 |
| Dixon-Coles | 0,136 → 0,133 | 0,282 → 0,293 | 0,223 → 0,232 | 0,308 → 0,350 (przed naprawą optymalizatora) |

Powtarzalność między sezonami jest wysoka. Do wersji 5 uporządkowanie było we wszystkich wersjach to samo (Elo < Poisson < Dixon-Coles). W wersji 6 Dixon-Coles schodzi poniżej Poissona (Elo < Dixon-Coles < Poisson), a Elo i Poisson są bitowo takie same jak w wersji 5. Uporządkowanie wynika z mechaniki aktualizacji. Trajektorie niżej to średnia krocząca okna 3 z wersji 6: wiosna z runów 13–14, jesień (od kolejki 4) z runów diagnostycznych 15–16 (dla Elo i Poissona identyczne z runami 9–12).

- **Elo** zmienia oceny przyrostowo o `K·G·(W−W_e)` przy K=10, więc rusza się najmniej, a jego trajektoria jest **płaska** od pierwszej ocenianej kolejki: jesienią 0,078–0,121, wiosną 0,071–0,097 (2024/25) i 0,093–0,109 (2025/26; w pierwszej kolejce wiosny 0,135). Model jest w stanie ustalonym od początku.
- **Poisson** przelicza średnie kroczące; im więcej meczów w koszyku, tym mniejszy wpływ kolejnego. Stąd **wyraźne wygaszanie jesienią** (kolejki 4 → 18: 0,489 → 0,212 i 0,509 → 0,240), a wiosną już tylko łagodny spadek (0,220 → 0,131 w 2024/25) albo płaski przebieg (0,161–0,187 w 2025/26).
- **Dixon-Coles** (wersja 6) — dryf leży między Elo a Poissonem i jest **bez oscylacji**. Jesienią wyraźnie opada: 2024/25 0,471 → 0,119 (z lokalnym garbem 0,383 w kolejce 13), 2025/26 0,321 (kolejka 5) → 0,150. Wiosną trzyma się w przedziale 0,109–0,163 w 2024/25 (wzrost do 0,163 w kolejce 32) i 0,115–0,142 w 2025/26, bez trendu. Każda kolejka to pełne MLE od zimnego startu, a wygaszanie czasowe przesuwa przy tym wagi wszystkich meczów (punkt odniesienia to najnowszy mecz). Przy półokresie ok. 373 dni nowa kolejka zmienia rozkład wag i efektywną próbę mniej niż przy ok. 107 dniach, więc parametry ruszają się mniej.
- **Dixon-Coles w wersji 5** (ξ = 0,0065 na dzień) miał dryf najwyższy z trzech modeli i słabiej wygasający: jesienią 2024/25 0,358 → 0,213 z garbem 0,460 w kolejce 13, 2025/26 0,333 → 0,284 z minimum 0,234 w kolejce 13; wiosną 0,217–0,342 (2024/25) i 0,258–0,326 (2025/26). Hipoteza, że ten wysoki, niewygasający poziom napędzają drużyny bez nowych meczów (w tym 10 nieaktywnych w badanym sezonie), które co kolejkę przesuwają się w stronę α = β = 1, nie potwierdziła się — patrz „Nieaktywne drużyny — hipoteza obalona". Napędzało go szybkie wygaszanie czasowe.
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

Nieaktywne drużyny ruszały się mniej niż aktywne, więc dryf **rozcieńczały**, a nie zawyżały. Po zawężeniu dryf Dixona-Colesa rośnie o ~26%, Elo o 32–44%, a Poissona praktycznie się nie zmienia. Rozrzut między modelami spada tylko nieznacznie, więc wysoki poziom Dixona-Colesa i rozrzut wynikają głównie z mechaniki aktualizacji (punkty wyżej), a nie z nieaktywnych drużyn. Tabela dotyczy wersji 4–5; w wersji 6 poziom Dixona-Colesa spadł poniżej Poissona przez wolniejsze wygaszanie czasowe.

Od wersji 5 dryf liczony jest tylko po drużynach aktywnych jako **poprawna definicja** „ruchu parametrów modelu w badanym sezonie", a nie jako poprawka rozrzutu. Testy: `RestrictToTeams` zachowuje skalary i drużyny aktywne we wszystkich trzech formatach kluczy, zmiana parametrów wyłącznie nieaktywnej drużyny nie zmienia dryfu (`ModelSnapshotDriftTests`). Pętla na fixture z drużyną obecną tylko w sezonie poprzednim daje dryf równy ręcznemu `NormalisedDistance` na zawężonych snapshotach, różny od dryfu po pełnych (`WalkForwardEvaluatorTests`).

### Konsekwencja dla pytania badawczego nr 2

Przy **wspólnym progu absolutnym** `StabilisedFromRound` nie odpowiada na pytanie „kiedy model się stabilizuje", bo zlepia dwie różne rzeczy: poziom dryfu w stanie ustalonym (który różni się między modelami mechanicznie) i moment wygaszenia dryfu (o który pytanie faktycznie chodzi). Widać to w przemiataniu progów na 2024/25:

| Próg | Elo | Poisson | Dixon-Coles (wersja 4) | Dixon-Coles (wersja 3, artefakt) |
| --- | --- | --- | --- | --- |
| 0,05 | nigdy | nigdy | nigdy | nigdy |
| 0,10 | od 19 | nigdy | nigdy | nigdy |
| 0,20 | od 19 | od 20 | od 34 | nigdy |
| 0,25 | od 19 | od 19 | od 33 | od 32 |

W 2025/26 Dixon-Coles (wersja 4) daje „nigdy" przy 0,05–0,20 i „od 19" przy 0,25 — jego płaski poziom ~0,23 leży między tymi progami. Wynik każdego modelu zależy więc głównie od tego, czy próg leży nad, czy pod jego własnym poziomem dryfu: poniżej — „nigdy", powyżej — „od razu", a próg przecinający łagodny spadek daje kolejkę z końca rundy (Dixon-Coles 2024/25: 33–34). „Kolejka stabilizacji" Dixona-Colesa skacze przez to między 19 a 34 zależnie od sezonu i progu. Ówczesne domyślne 0,05 było nieosiągalne dla każdego modelu. Wniosek, który dane rzeczywiście uzasadniają, to **różnica w charakterze aktualizacji parametrów** (Elo płaski na niskim poziomie, Poisson łagodnie opadający, Dixon-Coles płaski lub łagodnie opadający na najwyższym poziomie — do wersji 5), a nie jedna liczba „kolejka stabilizacji". Tabela wyżej zostaje jako historia: liczby dotyczą wersji 3–4 i progu absolutnego.

**Obecne kryterium: poziom, trend i osiadanie względne.** Pytanie „kiedy model się stabilizuje" rozbite jest na trzy liczby, liczone osobno dla każdego modelu:

1. **Poziom dryfu L** — jak dużo parametry ruszają się w stanie ustalonym (średni dryf z ostatniej 1/3 kolejek). To cecha mechaniki aktualizacji, a nie moment stabilizacji, więc porównuje się go między modelami osobno.
2. **Trend** — czy dryf w ogóle maleje: test Spearmana dryfu względem numeru kolejki, z poprawką Holma między modelami. ρ < 0 przy istotnym p = wygaszanie.
3. **Osiadanie względne** — pierwsza kolejka, od której krocząca średnia dryfu do końca nie przekracza (1+δ)·L, czyli „od kiedy model jest blisko własnego poziomu końcowego". Domyślnie δ = 0,25; wrażliwość sprawdza się parametrem `?tolerance=` (np. 0,10 i 0,50).

Próg względny usuwa zlepienie poziomu z momentem wygaszenia: każdy model jest mierzony swoją miarą, więc „nigdy" przy niskim progu i „od razu" przy wysokim przestają zależeć od tego, gdzie leży wspólna liczba.

**Osiadanie wymaga trendu.** Kryterium względne zawsze coś zwróci: płaski ciąg „osiada" od pierwszej kolejki, a ciąg łagodnie rosnący zwykle też, bo L jest wtedy wysokie. Liczba z osiadania znaczy „stabilizację" dopiero przy ujemnym (albo nieistotnym) trendzie. Przy istotnym trendzie dodatnim dryf rośnie i osiadanie nie oznacza stabilności. Dlatego tabela „Stabilność" pokazuje obie wielkości obok siebie.

**Runy diagnostyczne z wczesnym odcięciem.** Na samej rundzie wiosennej (kolejki 19–34) dryf wszystkich modeli jest już w okolicy poziomu końcowego, więc osiadanie daje 19–20 dla każdego modelu i nic nie rozróżnia. Wygaszanie widać dopiero, gdy ewaluacja zaczyna się wcześnie: runy z ręcznym odcięciem po kolejce 3 obejmują kolejki 4–34, a parametry `?fromRound=` i `?toRound=` endpointu porównania pozwalają policzyć trend osobno dla jesieni (4–18) i wiosny (19–34). Takie runy służą pytaniu nr 2 i oknom kolejek pytania nr 3 (sekcja „Stopniowanie beniaminków”, akapit o oknach kolejek). Do porównania trafności modeli (pytanie nr 1) obowiązują runy z odcięciem na przerwie zimowej, bo trening na trzech kolejkach to inny problem predykcyjny.

#### Wyniki (wersja 6)

Runy diagnostyczne 15 (2024/25) i 16 (2025/26), kolejki 4–34. L, średni dryf i trend przy δ = 0,25; osiadanie dla trzech wartości δ:

| Model | Sezon | L | Średni dryf | Trend ρ | p (Holm) | Osiada od: δ = 0,10 / 0,25 / 0,50 |
| --- | --- | --- | --- | --- | --- | --- |
| Poisson | 2024/25 | 0,163 | 0,259 | −0,874 | < 0,0001 | 33 / 20 / 16 |
| Dixon-Coles | 2024/25 | 0,131 | 0,207 | −0,768 | < 0,0001 | 33 / 18 / 16 |
| Elo | 2024/25 | 0,084 | 0,088 | −0,200 | 0,28 | 24 / 14 / 4 |
| Poisson | 2025/26 | 0,172 | 0,243 | −0,847 | < 0,0001 | 20 / 20 / 18 |
| Dixon-Coles | 2025/26 | 0,138 | 0,163 | −0,585 | 0,0011 | 18 / 13 / 13 |
| Elo | 2025/26 | 0,100 | 0,098 | +0,104 | 0,58 | 20 / 4 / 4 |

Trend osobno dla jesieni i wiosny (`?toRound=18` i `?fromRound=19`), ρ i p po Holmie między modelami:

| Model | Jesień 4–18: 2024/25 | Jesień: 2025/26 | Wiosna 19–34: 2024/25 | Wiosna: 2025/26 |
| --- | --- | --- | --- | --- |
| Poisson | −0,807 (0,0008) | −0,821 (0,0005) | −0,494 (0,16) | −0,147 (0,79) |
| Dixon-Coles | −0,679 (0,011) | −0,529 (0,086; surowe p 0,043) | −0,326 (0,43) | +0,382 (0,43) |
| Elo | −0,236 (0,40) | +0,246 (0,38) | −0,171 (0,53) | −0,229 (0,79) |

Poisson i Elo mają dryf bitowo równy wersji 5, ale p po Holmie trendu liczone jest w rodzinie wszystkich modeli runu, więc zmiana p Dixona-Colesa przesunęła niektóre z nich (Elo 2025/26, kolejki 4–34: 1,0 → 0,58; wiosna 2025/26: 1,0 → 0,79). Wersja 5 (runy 11–12) dla Dixona-Colesa: L 0,264 i 0,304, średni dryf 0,306 i 0,295, trend 4–34 −0,536 (0,0037) i −0,015 (1,0), jesień −0,525 (0,089) i −0,407 (0,26), wiosna −0,424 (0,20) i +0,156 (1,0), osiadanie 33 / 16 / 14 i 7 / 6 / 4.

Dryf w kolejkach 19–34 jest w runach 15–16 bitowo równy dryfowi runów 13–14 (tak samo w wersji 5: runy 11–12 i 9–10): ten sam zbiór wchłoniętych meczów daje ten sam stan modelu bazowego, niezależnie od tego, czy kolejki 4–18 weszły treningiem, czy pętlą. Trend wiosny jest więc trendem obowiązujących runów 13–14. Na samej wiośnie (runy 13–14) osiadanie przy δ = 0,25 wypada w kolejce 19 albo 20 dla każdego modelu.

Odpowiedź na pytanie nr 2:

- **Poisson** — parametry wyraźnie wygasają w rundzie jesiennej (istotny trend w obu sezonach, także po Holmie) i osiadają pod koniec rundy jesiennej albo na przełomie rund: kolejka 20 przy δ = 0,25, 16–18 przy δ = 0,50. Wiosną dryf jest już na poziomie końcowym.
- **Dixon-Coles** też wygasa w rundzie jesiennej, w obu sezonach: istotny spadek na całym zakresie 4–34 (ρ −0,768 i −0,585), jesienią istotny w 2024/25 i na granicy w 2025/26 (po Holmie 0,086, surowe p 0,043). Osiada w drugiej połowie rundy jesiennej: kolejka 18 (2024/25) i 13 (2025/26) przy δ = 0,25, 16 i 13 przy δ = 0,50 — wcześniej niż Poisson, a w 2024/25 przy δ = 0,50 w tej samej kolejce. Poziom końcowy L jest o połowę niższy niż w wersji 5 i leży między Elo a Poissonem. W wersji 5 (ξ = 0,0065 na dzień) obraz zależał od sezonu: osiadanie od kolejki 16 przy istotnym spadku (2024/25) i od kolejki 6 bez żadnego trendu (2025/26), bo szybkie wygaszanie czasowe trzymało dryf wysoko przez cały sezon i zasłaniało jesienne wygaszanie.
- **Elo** jest w stanie ustalonym od pierwszej ocenianej kolejki: nie ma trendu w żadnym zakresie, a kolejki osiadania (14 i 4) wyznaczają lokalne wahania rzędu progu, a nie wygaszanie.
- **Wiosną żaden model nie ma istotnego trendu** (najniższe p po Holmie 0,16). Runy z odcięciem na przerwie zimowej nie odpowiadają więc na pytanie nr 2 — do tego służą runy diagnostyczne.
- **Wrażliwość na δ.** Przy δ = 0,10 próg mieści się w wahaniach stanu ustalonego, bo wiosną krocząca średnia dryfu odchyla się od L nawet o 8–38%, zależnie od modelu i sezonu (runy 13–14; tyle samo w runach 9–10). Osiadanie ucieka wtedy na koniec sezonu (kolejka 33) albo zależy od pojedynczych garbów. Przy δ = 0,25 i 0,50 kolejność jest w obu sezonach ta sama: Elo ≤ Dixon-Coles ≤ Poisson.

Liczby z obu tabel zwraca endpoint porównania (`GET …/runs/{RunId}/comparison` z `tolerance`, `fromRound`, `toRound`), więc da się je odtworzyć z bazy.

Ranking z wiązaniami (`Ranking.AverageRanks`) zwraca rangi średnie i sumę `t³−t` potrzebną do korekty wariancji w obu testach.

## Analiza wrażliwości parametrów

Wnioski z pytań nr 1 i 3 w wersji 5 (runy 9–10) pochodziły z jednego zestawu wartości domyślnych: mnożniki formy Poissona włączone, wagi horyzontów 0,67 / 0,3 / 0,03, ξ = 0,0065, `RidgeLambda` = 0,05, Elo K = 10, start 1300, przewaga gospodarza 100. Analiza wrażliwości sprawdza, które wnioski przetrwają zmianę tych wartości i czy któraś wartość domyślna jest gorsza od alternatyw także poza badanymi sezonami. Ma być podstawą decyzji o wartościach domyślnych (korekta albo wnioski warunkowe), a nie wyborem „najlepszego" zestawu. Liczy się w rdzeniu na fixture z prawdziwymi danymi (`EkstraSim.Tests/Data/ekstraklasa-liga1-mecze.csv`), bez bazy i bez nowych runów. Pytania nr 2 i 4 są poza zakresem.

Analiza doprowadziła do wersji 6 algorytmu: domyślne ξ = 0,0065/3,5 na dzień (wartość z literatury), formy i K bez zmian (uzasadnienie w punkcie „Decyzja" na końcu sekcji). Raport nadal liczy się od wartości wersji 5 (`RealDataFixture.OptionsVersion5`), bo dokumentuje, skąd wzięła się decyzja, więc tabele niżej się nie zmieniły. „Domyślne" w tabelach to wartości wersji 5, a wiersz „ξ z literatury" to wartość domyślna wersji 6. Odporność wniosków wersji 6 opisuje osobny punkt na końcu sekcji.

### Metoda

**Sezony.** Tylko runda wiosenna, odcięcie na przerwie zimowej:

| Sezon | Rola | Odcięcie | Mecze wiosny |
| --- | --- | --- | --- |
| 2021/22 | walidacja | po kolejce 19 (`DetectSplit`) | 135 |
| 2022/23 | walidacja | po kolejce 17 (`DetectSplit`, przerwa na mundial) | 153 |
| 2023/24 | walidacja | po kolejce 19 (**ręcznie**, `DetectSplit` daje 25) | 135 |
| 2024/25 | ewaluacja (run 9; wersja 6: run 13) | po kolejce 18 | 144 |
| 2025/26 | ewaluacja (run 10; wersja 6: run 14) | po kolejce 18 | 144 |

Sezony ewaluacyjne to te, na których praca porównuje modele. Trzy wcześniejsze wiosny z fixture w porównaniu nie biorą udziału i służą za zbiór walidacyjny. Wariant lepszy od domyślnego także na walidacji jest kandydatem na korektę wartości domyślnej, a wariant lepszy tylko na ewaluacji byłby strojeniem na zbiorze testowym. Tak też dobiera się ξ w literaturze: przez wiarygodność predykcyjną na danych spoza ewaluacji.

**Defekt `DetectSplit` w 2023/24.** Dwa mecze kolejki 17 rozegrano 28.02 i 12.03.2024, a jeden mecz kolejki 20 już 30.08.2023. Przedziały dat kolejek 17–20 nakładają się, więc przerwa zimowa (18.12.2023 → 9.02.2024) nie jest luką między kolejnymi kolejkami, a największą luką zostaje 13 dni po kolejce 25. Raport podaje dla tego sezonu odcięcie ręcznie. Kodu `DetectSplit` nie poprawiano.

**Siatka** (`SensitivityGrid.Default`, 24 warianty, w tym jeden wariant domyślny na model):

| Model | Parametr | Wartości (w nawiasie domyślna) |
| --- | --- | --- |
| Dixon-Coles | `TimeDecayXi` (ξ, na dzień) | 0; 0,001; 0,00186 = 0,0065/3,5 (literatura, domyślne od wersji 6); 0,003; 0,004; 0,01; 0,015 (0,0065 — domyślne wersji 5, baza raportu) |
| Dixon-Coles | `RidgeLambda` | 0; 0,5; 5; 50 (0,05) |
| Poisson | `UseFormFactors` | wyłączone (włączone) |
| Poisson | wagi horyzontów bieżący / poprzedni / historia | 0,7 / 0,3 / 0; 0,6 / 0,3 / 0,1 (0,67 / 0,3 / 0,03) |
| Elo | `EloK` | 5; 15; 20; 30 (10) |
| Elo | `EloHomeAdvantage` | 0; 200 (100) |
| Elo | `EloInitialRating` | 1500 (1300) |

**Jeden parametr naraz.** Każdy parametr dotyka dokładnie jednego modelu, więc wariant modelu m liczy się przy pozostałych modelach na wartościach domyślnych: testy dostają zestaw {m: wariant, pozostałe: domyślne}. Kombinacje między modelami nie wymagają nowych obliczeń, bo scenariusz to ten sam zestaw z wariantem wybranym dla każdego modelu. Siatki ξ × ridge (oba parametry Dixona-Colesa) nie liczono. Ridge sprawdzono przy domyślnym ξ, a przy mniejszym ξ efektywna liczba meczów drużyny rośnie, więc waga kary `1/(1 + efektywna liczba meczów)` i dźwignia ridge jeszcze maleją.

**Testy.** Każdy wariant i scenariusz dostaje dokładnie to, co `comparison` runu: test Wilcoxona dla trzech par (RPS) z jedną rodziną Holma, beniaminki binarnie (`PromotedVersusRest`, cały zakres) i kategoriami (`PromotedByCategory`) z rodziną Holma kategorii. Siatka jest opisowa: raport podaje ΔRPS, ranking i to, czy wniosek z testu po Holmie się przestawia. Nie ma korekty między wariantami ani testu „najlepszy wariant vs domyślny". **Zastrzeżenie dla tekstu pracy:** 24 warianty w pięciu sezonach to ponad sto rodzin testów, więc pojedyncze p < 0,05 w siatce nie jest wynikiem. Liczy się kierunek, który powtarza się w sezonach, zwłaszcza walidacyjnych. Wyższość wariantu nad domyślnym opiera się na pięciu wiosnach (135–153 mecze) i nie była testowana.

**Scenariusze.** Cztery: domyślne; ξ z literatury; formy wyłączone; oba naraz.

**Stała linia bazowa** (`ConstantBaseline`). P(1/X/2) równe częstościom zwycięstw gospodarzy, remisów i zwycięstw gości wśród rozegranych meczów ligi w historii treningowej (sezony wcześniejsze i runda jesienna), takie samo dla każdego meczu wiosny. Linia bazowa nie ma parametrów i nie wie nic o drużynach. Jeśli grupa meczów jest trudna także dla niej, nadwyżka modeli w tej grupie jest po części cechą meczów, a nie modeli. Raport liczy dla niej osobno beniaminków binarnie i kategoriami (z własną rodziną Holma).

**Implementacja** (`EkstraSim.Prediction/Evaluation`):

- `SensitivitySeason.Build` składa sezon tak jak run: mecze ligi, `BuildHistory`, `BuildEvaluationSet`, opcje domyślne z chronologią (od wersji 6 z ξ z literatury; raport wersji 5 nadpisuje je przez `with { Options = RealDataFixture.OptionsVersion5(…) }`) i historia beniaminków (`PromotedTeamHistory.Compute`). Sezon bez rozegranych meczów po odcięciu odrzuca od razu (`ArgumentException`), zamiast dawać puste warianty.
- `SensitivitySweep.Run` liczy walk-forward każdego wariantu (`WalkForwardEvaluator.Run` z filtrem daty, flagą beniaminka i fabryką modeli tymczasowych) równolegle. Wynik nie zależy od stopnia równoległości. Ewaluacje są sortowane po `MatchId`, tak jak `comparison` czyta predykcje z bazy. Bez tego średnie grup beniaminków różniłyby się na ostatnich bitach, bo pętla zwraca mecze w kolejności (kolejka, Id), a średnie sumują się w kolejności wejścia. `ModelConvergenceException` jest wynikiem wariantu („nie zbiega" z komunikatem). Każdy inny wyjątek to błąd kodu i przerywa przemiatanie. Z `Run` wychodzi wtedy jako `AggregateException` (`Parallel.For`) z oryginałem w `InnerExceptions`.
- `SensitivitySummary.Summarise` składa dla każdego wariantu zestaw modeli i woła na nim `ModelComparison.Pairwise`, `PromotedVersusRest` i `PromotedByCategory`. `Scenario` robi to samo dla wariantów wybranych etykietą per model; model bez etykiety dostaje wariant domyślny. Brak albo błąd wariantu domyślnego, nieznana albo powtórzona etykieta i wariant z błędem w scenariuszu kończą się `ArgumentException`. Wiersz podsumowania jest udany wtedy i tylko wtedy, gdy ma średnie — wynik wariantu bez ewaluacji nigdy nie daje „udanego" wiersza bez statystyk.
- `TrainingOptions` jest rekordem, więc wariant to `opcje with { TimeDecayXi = … }`. Test sprawdza refleksją, że każdy wariant zmienia dokładnie deklarowane właściwości, na dwóch różnych zestawach opcji bazowych. Na samych wartościach domyślnych wariant, który przy okazji ustawia drugi parametr na jego wartość domyślną, byłby niewidoczny.

**Uruchomienie.** Raport drukuje test `SensitivityRealDataTests` (`Category=RealData`):

```bash
dotnet test EkstraSim.Tests/EkstraSim.Tests.csproj --filter "FullyQualifiedName~SensitivityRealDataTests" --logger "console;verbosity=detailed"
```

Raport ma sześć części:

1. tabelę wariantów dla każdego sezonu: RPS, ΔRPS, Brier, log-loss, ranking i p po Holmie trzech par, a w sezonach ewaluacyjnych także porównanie binarne (różnica beniaminki − reszta i p po Holmie), średnie reszta / zimny start / powracający, p Holma kategorii i średnie per drużyna,
2. przekrój RPS wszystkich wariantów w pięciu sezonach,
3. scenariusze,
4. linię bazową z nadwyżkami grup beniaminków,
5. dowód niezmienniczości startu Elo (w teście `EloInitialRatingDoesNotChangeEvaluations`),
6. odporność wartości wersji 6: drugie przemiatanie dla 2024/25 i 2025/26 od wartości wersji 6, siatka bez wariantów ξ (17 wariantów), ta sama tabela co w części 1.

Oba przemiatania liczą się raz na klasę testów (sezony równolegle); główne trwa ok. 15 s, cała klasa ok. 20 s. `Version6RealDataTests` trwa ok. 5 s.

**Kotwice.** Test przypina:

- wariant domyślny bitowo równy bezpośredniemu `WalkForwardEvaluator.Run` (każdy sezon i model),
- średni RPS, p po Holmie par, średnie per beniaminek, średnie meczów bez beniaminka i p Holma kategorii powracających z runów 9–10,
- liczności wiosen i odcięcia sezonów walidacyjnych,
- zbieżność wariantów domyślnych,
- liczby z wyników, na których opiera się wniosek: RPS ξ z literatury i form wyłączonych w pięciu sezonach, jedyny brak zbieżności (ξ = 0,015 w 2023/24) oraz ranking i p po Holmie trzech scenariuszy w sezonach ewaluacyjnych,
- wartości domyślne wersji 6 bitowo równe wariantowi „ξ z literatury" (Dixon-Coles) i wariantom domyślnym (Elo, Poisson) raportu wersji 5 (`Version6DefaultsEqualTheLiteratureVariant`), wersję 6 bez form równą scenariuszowi „oba", wybrane liczby tabeli odporności wersji 6 (7 wierszy, `Version6RobustnessReproducesTheDocumentedNumbers`) i wszystkie wnioski spod tabeli (`Version6ConclusionsHoldAcrossVariants`: DC–Elo nieistotne w każdym wariancie, Poisson ostatni w każdym, w 2025/26 obie pary z Poissonem istotne w każdym, ridge bez dźwigni, najniższe p po Holmie kategorii beniaminków 0,2890).

Liczby runów 13–16 (metryki, trafienia, pary, beniaminki, stabilność i trendy runów diagnostycznych, niezmienność Poissona i Elo względem wersji 5, zawężenia filtra daty) przypina osobna klasa `Version6RealDataTests` (`Category=RealData`), która liczy walk-forward na `RealDataFixture.Options` i `OptionsVersion5` dla odcięć 18 i 3 i drukuje te same tabele.

Fixture odtwarza runy 9–10 **bitowo**. Warianty domyślne zestawione z `ModelPredictions` runów 9 i 10 (żywa i testowa baza) dają 864 z 864 par (model, mecz) równych we wszystkich polach metryk: RPS, Brier, log-loss, P wyniku, P rezultatu, kolejka, flaga beniaminka i trafienia. Tolerancje kotwic nie są więc potrzebne do zgodności z bazą. To samo dla runów 13–16: 2538 z 2538 par i 282 z 282 wartości dryfu.

### Wyniki

Liczby z raportu: RPS (niższy = lepszy) i p po Holmie w rodzinie wariantu. Kody rankingu: E = Elo, D = Dixon-Coles, P = Poisson.

**Start Elo nic nie zmienia.** Start 1500 zamiast 1300 daje predykcje identyczne co do bitu we wszystkich pięciu sezonach (maks. |ΔRPS|, |ΔBrier|, |ΔLogLoss| = 0). To własność matematyczna. Replay jest zerosumowy, wszystkie drużyny startują z tej samej wartości, a oczekiwany wynik i cecha regresji biorą tylko różnice ocen, więc przesunięcie wszystkich ocen o stałą niczego nie zmienia. Drużyna powracająca wraca z oceną zamrożoną w chwili spadku, bo replay nie resetuje ocen między sezonami. Parametru nie ma w żądaniu badania.

**Ridge wiosną nie ma dźwigni.** `RidgeLambda` od 0 do 5 zmienia RPS Dixona-Colesa najwyżej o 0,0010 w każdym sezonie. Przy λ = 0 zmiana jest poniżej 0,0001, a dopasowania zbiegają we wszystkich pięciu sezonach. Dopiero λ = 50, tysiąc razy więcej niż domyślne, daje −0,0010…−0,0067, a ranking zmienia tylko w 2023/24 (D < E < P). Po 17–19 meczach jesieni waga kary `1/(1 + efektywna liczba meczów)` jest mała dla każdej drużyny, także beniaminka. Hipoteza, że domyślne 0,05 jest za słabe, by pomóc beniaminkom, nie wyjaśnia kary Dixona-Colesa w pytaniu nr 3.

**ξ przestawia pytanie nr 1.** RPS Dixona-Colesa (półokres = ln 2 / ξ):

| ξ (na dzień) | Półokres | 2021/22 | 2022/23 | 2023/24 | 2024/25 | 2025/26 |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | ∞ | 0,2115 | 0,2065 | **0,2306** | **0,2161** | **0,2145** |
| 0,001 | 693 dni | 0,2105 | **0,2064** | 0,2316 | 0,2166 | 0,2174 |
| **0,00186 (literatura; domyślne od wersji 6)** | 373 dni | **0,2103** | 0,2067 | 0,2318 | 0,2186 | 0,2207 |
| 0,003 | 231 dni | 0,2110 | 0,2073 | 0,2318 | 0,2216 | 0,2247 |
| 0,004 | 173 dni | 0,2121 | 0,2077 | 0,2320 | 0,2238 | 0,2274 |
| 0,0065 (domyślne do wersji 5) | 107 dni | 0,2154 | 0,2082 | 0,2340 | 0,2287 | 0,2324 |
| 0,01 | 69 dni | 0,2192 | 0,2091 | 0,2383 | 0,2353 | 0,2373 |
| 0,015 | 46 dni | 0,2237 | 0,2118 | nie zbiega | 0,2443 | 0,2424 |

- Każde ξ ≤ 0,004 jest lepsze od 0,0065 w każdym z pięciu sezonów, a minimum leży między 0 a 0,00186. Na walidacji minimum wypada kolejno przy 0,00186, 0,001 i 0. ξ z literatury poprawia RPS o 0,0050 / 0,0014 / 0,0022 na walidacji i o 0,0101 / 0,0117 na ewaluacji.
- ξ = 0,015 nie zbiega w 2023/24: L-BFGS wyczerpuje limit 2000 iteracji po kolejce 26. Przy szybkim wygaszaniu efektywna próba jest mała.

Ranking i p po Holmie w sezonach ewaluacyjnych:

| ξ | 2024/25: ranking; p DC–E; p DC–P | 2025/26: ranking; p DC–E; p DC–P |
| --- | --- | --- |
| 0 | D < E < P; 0,2758; 0,0557 | D < E < P; 0,1884; **0,0017** |
| 0,001 | D < E < P; 0,1463; **0,0412** | E < D < P; 0,6174; **0,0017** |
| 0,00186 | D < E < P; 0,7870; **0,0497** | E < D < P; 0,2219; **0,0023** |
| 0,003 | E < D < P; 0,3884; 0,0833 | E < D < P; **0,0173**; **0,0036** |
| 0,004 | E < D < P; 0,1932; 0,0833 | E < D < P; **0,0076**; **0,0075** |
| 0,0065 | E < D < P; 0,0833; 0,1403 | E < D < P; **0,0035**; **0,0129** |
| 0,01 | E < D < P; 0,0522; 0,4539 | E < D < P; **0,0027**; 0,0552 |
| 0,015 | E < D < P; **0,0201**; 0,9928 | E < D < P; **0,0028**; 0,2397 |

Przy ξ z literatury Dixon-Coles wyprzedza Elo w 2024/25 (0,2186 vs 0,2189), a w 2025/26 różnica DC–Elo traci istotność (0,0035 → 0,2219). Istotność DC–Elo w 2025/26 trzyma się tylko dla ξ ≥ 0,003. Para Elo–Poisson od ξ nie zależy: w 2025/26 jest istotna przy każdym ξ, w 2024/25 przy żadnym.

**Jednostka ξ.** Kod liczy wagę meczu `exp(−ξ · Δdni)` (`DixonColesModel`), więc dawne domyślne 0,0065 (do wersji 5) działało na dzień, z półokresem ok. 107 dni. W literaturze ta sama liczba występuje jako stopa na **pół tygodnia**. Dixon i Coles (1997) mierzą czas w półtygodniach, więc ich 0,0065 odpowiada 0,0065/3,5 ≈ 0,00186 na dzień i półokresowi ok. 373 dni, czyli ok. roku. Wartość weszła do kodu w pierwszej wersji rdzenia bez opisu jednostki. Od wersji 6 wartością domyślną jest 0,0065/3,5.

- **Źródło pierwotne niesprawdzone.** Oryginału (M. J. Dixon, S. G. Coles, *Modelling Association Football Scores and Inefficiencies in the Football Betting Market*, Journal of the Royal Statistical Society: Series C 46(2), 1997, s. 265–280, DOI 10.1111/1467-9876.00065) nie udało się przeczytać: 2026-10-11 był dostępny tylko w subskrypcji wydawcy, a repozytorium Uniwersytetu w Lancasterze ma sam opis bibliograficzny. Ponowna próba 2026-10-12: Unpaywall oznacza DOI jako zamknięty (bez legalnej kopii w otwartym dostępie), a repozytoria uniwersytetów w Lancasterze i Bristolu mają same opisy bibliograficzne.
- **Źródła wtórne są zgodne co do jednostki.** Clegg, Song i Cartlidge (2026, arXiv:2605.16066, rozdz. 5.1, wzór 6) ważą mecze przez exp(−ξ·d/3,5), gdzie d to dni, a dzielenie przez 3,5 zamienia je na półtygodnie. Biorą ξ = 0,0065 na pół tygodnia za Dixonem i Colesem i podają półokres ok. roku. Kharrat, López Peña i McHale (2017, arXiv:1706.04943) też skalują odległości czasowe w półtygodniach i powołują się przy tym na Dixona i Colesa.
- **Niezależny punkt odniesienia.** Pitcan (2026, arXiv:2608.11505) dobrał ξ siatką na sezonach walidacyjnych Serie A i dostał 0,002 na dzień (półokres 347 dni), czyli prawie tyle, co 0,00186.
- **Sformułowanie w pracy.** Do czasu sprawdzenia oryginału pisać o „wartości z literatury podawanej na pół tygodnia, stosowanej w kodzie do wersji 5 na dzień", a nie o błędzie. Niezależnie od źródła dane z walidacji same wskazują wolniejsze wygaszanie.

**Formy pogarszają Poissona w każdym sezonie.** Wyłączenie mnożników formy obniża RPS Poissona o 0,0098 / 0,0042 / 0,0283 / 0,0266 / 0,0252 (od 2021/22 do 2025/26). Ranking z Poissonem bez form to E < P < D w 2021/22, 2023/24 i 2024/25 oraz E < D < P w 2022/23 i 2025/26. W 2024/25 żadna para nie jest wtedy istotna (DC–E 0,1213, DC–P 0,6529, E–P 0,6442). W 2025/26 istotne zostają Elo–Poisson (0,0096) i DC–Elo (0,0053), a DC–Poisson przestaje być istotne (0,0129 → 0,5597). Poisson z formami jest na meczach bez beniaminka gorszy od stałej predykcji: 0,2469 vs 0,2231 (2024/25) i 0,2602 vs 0,2247 (2025/26). Mnożniki formy nie tylko dodają szum, lecz szkodzą systematycznie: składnik dom/wyjazd drugi raz dolicza przewagę własnego boiska. Mechanizm opisuje sekcja Poissona, „Kalibracja — prognozy zbyt pewne”.

**K i przewaga Elo.** RPS Elo:

| K | 2021/22 | 2022/23 | 2023/24 | 2024/25 | 2025/26 |
| --- | --- | --- | --- | --- | --- |
| 5 | **0,2051** | **0,2054** | 0,2325 | **0,2177** | **0,2150** |
| 10 (domyślne) | 0,2070 | 0,2067 | 0,2302 | 0,2189 | 0,2169 |
| 15 | 0,2091 | 0,2075 | 0,2285 | 0,2203 | 0,2190 |
| 20 | 0,2111 | 0,2080 | 0,2274 | 0,2215 | 0,2209 |
| 30 | 0,2143 | 0,2083 | **0,2261** | 0,2233 | 0,2235 |

K = 5 jest lepsze od domyślnego w czterech sezonach (o 0,0012–0,0019), a w 2023/24 kierunek jest odwrotny i najlepsze jest K = 30. Ranking E < D < P trzyma się dla każdego K poza K = 30 w 2022/23 (Dixon-Coles lepszy o 0,0001). W 2025/26 wszystkie trzy pary są istotne przy każdym K (najwyższe p 0,0344 przy K = 30), w 2024/25 żadna (najniższe p 0,0505 przy K = 5). Przewaga gospodarza 0 albo 200 zmienia RPS Elo najwyżej o 0,0010 i nie zmienia rankingu.

**Wagi horyzontów Poissona.** Więcej wagi dla historii (0,6 / 0,3 / 0,1) obniża RPS o 0,0018–0,0029, a jej usunięcie (0,7 / 0,3 / 0) podnosi go o 0,0008–0,0013. Ranking nie zmienia się w żadnym sezonie. Efekt jest o rząd mniejszy niż efekt form.

**Scenariusze** (ranking; p po Holmie DC–E / DC–P / E–P):

| Scenariusz | 2024/25 | 2025/26 |
| --- | --- | --- |
| domyślne (wersja 5) | E < D < P; 0,0833 / 0,1403 / 0,0833 | E < D < P; **0,0035 / 0,0129 / 0,0027** |
| ξ z literatury (= wersja 6) | D < E < P; 0,7870 / **0,0497** / 0,0555 | E < D < P; 0,2219 / **0,0023 / 0,0023** |
| formy wyłączone | E < P < D; 0,1213 / 0,6529 / 0,6442 | E < D < P; **0,0053** / 0,5597 / **0,0096** |
| oba | D < E < P; 0,7870 / 0,7240 / 0,7240 | E < D < P; 0,2219 / **0,0121 / 0,0121** |

- W sezonach walidacyjnych żaden scenariusz nie daje istotnej pary (najniższe p 0,1476: 2023/24, domyślne). W całej siatce walidacji istotna jest tylko para DC–Poisson przy λ = 50 w 2023/24 (0,0447).
- Wniosek „Elo najlepszy" trzyma się w 2025/26 w każdym scenariuszu (w siatce wyjątkiem jest ξ = 0), ale w 2024/25 zależy od ξ i form.
- Wniosek „w 2025/26 istotne są wszystkie trzy różnice" wymaga form włączonych i ξ między 0,003 a 0,0065 (przy 0,01 przestaje być istotna para DC–Poisson). Przy ξ z literatury albo bez form jedna para przestaje być istotna.

**Stała linia bazowa a modele.** RPS stałej predykcji: 0,2315 / 0,2219 / 0,2217 / 0,2281 / 0,2270 (od 2021/22 do 2025/26). W siatce porównania ze stałą są opisowe, bez testów. Test Wilcoxona model − stała przy wartościach wersji 6 opisuje sekcja „Wyniki pytań nr 1 i 4” (po Holmie nic istotnego).

- Dixon-Coles na wartościach wersji 5 jest od niej gorszy w trzech sezonach, w tym w obu ewaluacyjnych (0,2287 vs 0,2281 i 0,2324 vs 0,2270). Z ξ z literatury (wersja 6) jest gorszy tylko w 2023/24, a w sezonach ewaluacyjnych lepszy (0,2186 vs 0,2281 i 0,2207 vs 0,2270).
- Poisson domyślny jest gorszy w 2023/24, 2024/25 i 2025/26 (o 0,024–0,039), Elo tylko w 2023/24.
- W 2023/24 stała predykcja wygrywa z każdym wariantem każdego modelu (najlepszy wariant: Elo K = 30, 0,2261).

**Pytanie nr 3: kara powracających to cecha meczów, nie parametrów.**

- **Kara trwa w każdym wariancie.** W 2024/25 średnia meczów powracającej Lechii jest wyższa niż meczów bez beniaminka o +0,039…+0,074 we wszystkich 24 wariantach, a w 2025/26 kategoria powracających odstaje o 0,000…+0,016. Drużyna ze 135 meczami historii (Lechia, Wisła Płock) ma najwyższy RPS wśród beniaminków w każdym wariancie obu sezonów. Żaden test kategorii nie jest istotny w żadnym wariancie (najniższe p po Holmie 0,29). Zimny start odstaje od reszty o −0,003…+0,040.
- **Ani ξ, ani ridge kary nie usuwa.** Niższe ξ obniża RPS wszystkich grup. Przy ξ = 0 Lechia ma 0,247 przy reszcie 0,2076 (przy 0,0065: 0,285 przy 0,2175), a Wisła Płock 0,279 przy 0,2121 (przy 0,0065: 0,312 przy 0,2281). Przy λ = 50 Lechia ma 0,266 przy 0,2151, a Wisła Płock 0,301 przy 0,2281. Elo, które nie ma wygaszania czasowego, ma podobną nadwyżkę (+0,048 i +0,069).
- **Punkty odniesienia bez parametrów odtwarzają część kary.** Stała linia bazowa też ma nadwyżkę na tych drużynach, tylko mniejszą: Lechia +0,015 (modele +0,048…+0,068), Wisła Płock +0,045 (modele +0,069…+0,084). Na meczach zimnego startu w 2024/25 stała ma nadwyżkę +0,017, tyle co modele (Elo +0,016, Dixon-Coles +0,022, Poisson −0,001). Jednorazowy pomiar prostym modelem formy (probit na punktach na mecz z bieżącego sezonu, bez historii i bez parametrów do strojenia; kodu nie ma w repozytorium) odtworzył +0,070…+0,073 z nadwyżki Wisły Płock, czyli 83–106%, i +0,033…+0,049 z nadwyżki Lechii. Korelacja nadwyżek modeli i tego modelu formy na sześciu beniaminkach wyniosła 0,91–0,98. Wisła Płock była w tym pomiarze najtrudniejszym z 72 drużyno-sezonów: spadła z 2. miejsca w tabeli jesieni na 15. w tabeli wiosny, 11 z 16 meczów wypadło poniżej oczekiwań. Lechia poszła w drugą stronę, z 17. na 8. miejsce.
- **Wniosek.** Kategoria „135 meczów historii" to w każdym sezonie jedna drużyna, akurat z największą zmianą formy między jesienią a wiosną, więc długość historii jest pomieszana z drużyną. Kary powracających nie da się przypisać wygaszaniu historii, ridge'owi ani startowi Elo. Werdykt pytania nr 3 („żadna różnica nie jest istotna") trzyma się w każdym wariancie.

### Decyzja: wersja 6

Decyzja z 2026-10-12, po analizie wyżej:

- **ξ: korekta na 0,0065/3,5 na dzień.** To powrót do wartości z publikacji przeliczonej na jednostkę, w której liczy kod, a nie strojenie. Wartość nie jest dobrana do danych, a jest lepsza od 0,0065 we wszystkich pięciu sezonach, także w trzech walidacyjnych. Wartości z minimum na walidacji (0 albo 0,001) nie przyjęto, bo byłaby wyborem dokonanym na tych samych danych.
- **Formy Poissona: bez zmian.** Poisson ma być wiernym portem modelu z pracy inżynierskiej, czyli punktem odniesienia porównania. To, że mnożniki formy pogarszają go we wszystkich pięciu sezonach, jest wynikiem analizy, a nie powodem zmiany modelu.
- **K Elo: bez zmian.** K = 5 jest lepsze w czterech sezonach, ale w 2023/24 kierunek jest odwrotny, a w analizie od wartości wersji 5 ranking i istotność par na K prawie nie reagowały. Przy wartościach wersji 6 K przestawia kolejność Dixona-Colesa i Elo, ale ta para i tak nie jest istotna (punkt niżej).

Zmiana wartości domyślnej zmienia predykcje, więc podbija wersję algorytmu do 6. Obowiązujące runy to 13–16 („Wersjonowanie badań"). Wnioski z pytania nr 1 nadal zależą od form Poissona: bez nich w 2024/25 żadna para nie jest istotna (scenariusz „oba").

**Odporność wartości wersji 6.** Drugie przemiatanie od wartości wersji 6 (sezony ewaluacyjne; zmienia się jeden parametr jednego modelu, pozostałe modele na wartościach wersji 6). Ranking; p po Holmie DC–E / DC–P / E–P:

| Wariant | 2024/25 | 2025/26 |
| --- | --- | --- |
| wersja 6 | D < E < P; 0,7870 / **0,0497** / 0,0555 | E < D < P; 0,2219 / **0,0023 / 0,0023** |
| formy wyłączone | D < E < P; 0,7870 / 0,7240 / 0,7240 | E < D < P; 0,2219 / **0,0121 / 0,0121** |
| horyzonty 0,7 / 0,3 / 0 | D < E < P; 0,7870 / **0,0383 / 0,0478** | E < D < P; 0,2219 / **0,0022 / 0,0022** |
| horyzonty 0,6 / 0,3 / 0,1 | D < E < P; 0,7870 / 0,0729 / 0,0729 | E < D < P; 0,2219 / **0,0025 / 0,0025** |
| K = 5 | E < D < P; 0,6645 / **0,0497 / 0,0497** | E < D < P; 0,1014 / **0,0020 / 0,0020** |
| K = 15 | D < E < P; 0,2319 / **0,0497** / 0,0706 | E < D < P; 0,6703 / **0,0023 / 0,0025** |
| K = 20 | D < E < P; 0,1186 / **0,0497** / 0,0942 | D < E < P; 0,3221 / **0,0023 / 0,0047** |
| K = 30 | D < E < P; 0,0790 / **0,0497** / 0,0790 | D < E < P; 0,1376 / **0,0023 / 0,0119** |
| przewaga 0 | D < E < P; 0,5678 / **0,0497** / 0,0544 | E < D < P; 0,4210 / **0,0023 / 0,0023** |
| przewaga 200 | E < D < P; 0,9849 / **0,0497 / 0,0497** | E < D < P; 0,1117 / **0,0023 / 0,0023** |
| λ = 0 | D < E < P; 0,7885 / **0,0497** / 0,0555 | E < D < P; 0,2204 / **0,0023 / 0,0023** |
| λ = 0,5 | D < E < P; 0,7809 / **0,0484** / 0,0555 | E < D < P; 0,2226 / **0,0022 / 0,0022** |
| λ = 5 | D < E < P; 0,8271 / **0,0494** / 0,0555 | E < D < P; 0,2130 / **0,0021 / 0,0021** |
| λ = 50 | D < E < P; 0,6819 / **0,0441** / 0,0555 | E < D < P; 0,1654 / **0,0017 / 0,0018** |

Start Elo 1500 daje predykcje identyczne z wersją 6, a wiersz „formy wyłączone" jest równy scenariuszowi „oba" wyżej.

- **Dixon-Coles i Elo są nierozróżnialne.** Para DC–E nie jest istotna w żadnym wariancie obu sezonów (najniższe p po Holmie 0,0790 i 0,1014). Ich kolejność zależy od Elo: w 2024/25 Elo wyprzedza Dixona-Colesa przy K = 5 (0,2177 vs 0,2186) i przewadze 200 (0,2185), w 2025/26 Dixon-Coles wyprzedza Elo przy K = 20 (0,2207 vs 0,2209) i K = 30 (0,2235).
- **Poisson jest ostatni w każdym wariancie obu sezonów.** W 2025/26 obie pary z Poissonem są istotne w każdym wariancie (najwyższe p po Holmie 0,0121, bez form). W 2024/25 p tych par krąży wokół 0,05 (przy formach włączonych DC–P 0,0383–0,0729, E–P 0,0478–0,0942), więc istotność w 2024/25 nie jest odporna.
- **Ridge nadal bez dźwigni:** λ od 0 do 50 zmienia RPS Dixona-Colesa najwyżej o 0,0003.
- **Pytanie nr 3:** żaden test kategorii beniaminków nie jest istotny w żadnym wariancie (najniższe p po Holmie 0,29).

## Interfejs badawczy

Nowa sekcja w nawigacji, obok istniejących stron symulacji MC (te działają bez zmian):

| Strona | Trasa | Zawartość |
| --- | --- | --- |
| `ResearchRunsPage` | `/research` | lista badań ze statusem + formularz nowego badania |
| `ResearchRunDetailsPage` | `/research/{RunId}` | wykresy, podsumowania, testy istotności, stabilność, beniaminki, predykcje |
| `ModelRoundPredictionPage` | `/model-prediction` | predykcja jednej kolejki wybranym modelem |

`EvaluationRunForm` po wyborze sezonu odpytuje `season-structure` i pokazuje wykrytą przerwę zimową oraz beniaminków z liczbą meczów w bazie przed sezonem (np. „GKS Katowice (0 meczów)", „Lechia Gdańsk (135 meczów, ostatnio 2022/2023)") — kolejkę odcięcia można zostawić puste (auto) albo nadpisać. Parametry modeli (mnożniki formy, ξ, ridge, tolerancja δ i okno stabilności) siedzą w zwiniętym panelu, żeby nie zaśmiecać formularza. Pole ξ pokazuje 6 miejsc po przecinku (`0.001857`), a nieedytowane wysyła pełną wartość domyślną 0,0065/3,5: run 13 utworzony z formularza ma w `OptionsJson` `"TimeDecayXi":0.0018571428571428571`. Wartość wpisana ręcznie trafia do żądania tak, jak ją wpisano.

Strona szczegółów ma sześć zakładek: **Przebieg w sezonie** (dwa wykresy `MudChart` — wybrana metryka po kolejkach i dryf parametrów, po jednej serii na model), **Podsumowanie**, **Istotność różnic**, **Stabilność**, **Beniaminki**, **Predykcje** (z filtrem modelu i kolejki; kliknięcie wiersza pokazuje pod tabelą macierz wyników 0–6 × 0–6, obramowana komórka = faktyczny wynik). Selektor metryki przełącza wykres (przeliczany po stronie przeglądarki z metryk per kolejka) i testy istotności par oraz beniaminków (przeliczane przez backend); zakładki „Podsumowanie" i „Stabilność" od metryki nie zależą — werdykt stabilności liczony jest wyłącznie z dryfu. Selektor i zakładki pojawiają się dopiero dla runu `Completed`. Run `Failed` pokazuje pod nagłówkiem tylko czerwony alert z `ErrorMessage` (np. komunikat o restarcie backendu), a run `Pending` albo `Running` — informację „Obliczenia jeszcze trwają." z przyciskiem „Odśwież". Strona woła endpointy wyników tylko dla runu `Completed`, więc bramka statusu w backendzie niczego w niej nie zmienia.

`ModelRoundPredictionPage` pilnuje reguły odcięcia `predict-round` już w formularzu. Pole „Trening do kolejki" ma maksimum `Round − 1`, a przycisk „Przewiduj" jest nieaktywny, gdy podane odcięcie jest ≥ `Round`. Drugi warunek jest potrzebny, bo pole przycina tylko własne zmiany: po zmniejszeniu kolejki wpisane wcześniej odcięcie by zostało. Pole pokazuje wtedy błąd „Odcięcie musi być wcześniejsze niż przewidywana kolejka.", więc przycisk nie gaśnie bez wyjaśnienia. Podpis pod formularzem mówi, że mecze rozegrane w dniu przewidywanego meczu lub później nie wchodzą do treningu jego predykcji.

Zakładka **Beniaminki** ma pod niezmienioną tabelą binarną (całość i okna) dwie tabele stopniowania dla wybranej metryki: per drużyna (beniaminek, kategoria „zimny start" / „powracający", mecze w bazie przed sezonem, ostatni sezon w lidze — „—" dla zimnego startu — i „średnia (n)" dla każdego modelu) oraz testy kategorii (model, kategoria, średnia kategorii (n), średnia pozostałych (n), różnica, p po Holmie z tym samym kolorem istotności co tabela binarna). Opisy nad tabelami podają definicję kategorii, regułę meczu dwóch beniaminków, grupę porównawczą, jedną poprawkę Holma na run i pomijanie pustej kategorii. Nagłówek runu pokazuje beniaminków z liczbą meczów z `comparison.promotedTeams`, a przed wczytaniem porównania — ze snapshotu (stare snapshoty dają same nazwy). Etykiety i odmianę „mecz / mecze / meczów" ma `PromotedTeamLabels`, wspólny dla strony runu i formularza.

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
| 2026/27 | 80 / 306 | 80 / 306 | sezon w toku; eksport z 2026-10-10 — kolejki 1–9 bez trzech meczów przełożonych (3. kolejka: 12.10, 4. kolejka: grudzień) i dwa mecze 10. kolejki |

Dwa wnioski, oba ważne:

- **2024/25 jest gotowe pod walk-forward**, bo baza ma wszystkie 306 wyników. Sam plik CSV by na to nie pozwolił — gdyby ten sezon odtwarzać z pliku, runda wiosenna wyszłaby pusta. Kierunek jest tu jednoznaczny: baza jest źródłem prawdy, plik nie.
- **2026/27 jeszcze nie**. Odcięcie wypadnie ~kolejka 17, a `BuildEvaluationSet` bierze kolejki powyżej odcięcia i tylko rozegrane — przy wynikach z pierwszych ~10 kolejek zbiór ewaluacyjny jest **pusty**. Ten sezon obsługuje na razie wyłącznie `predict-round`.

### Konwersja eksportu z TheSportsDB

Pliki 2025/26 i 2026/27 przyszły w innym formacie niż pozostałe i wymagały konwersji przed importem. Eksport z TheSportsDB różni się od formatu docelowego na cztery sposoby:

1. Ma nagłówek `idEvent,strTimestamp,Round,Home Team,Home Score,Away Team,Away Score,Poster,Thumb` — 9 kolumn, doszedł `Poster` (w praktyce pusty).
2. **Każdy wiersz danych jest owinięty w dodatkową parę cudzysłowów** z podwojonymi cudzysłowami wewnątrz (podwójnie zakodowany CSV). `ParseRow` robi naiwne `line.Split(',')`, więc `fields[1]` wychodzi jako `""2025-07-18 16:00:00""` i `DateTime.TryParse` odrzuca **każdy** wiersz.
3. Kolejka to tekst `"Round 1"`, nie liczba — `int.TryParse` odrzuca ją nawet po naprawieniu cudzysłowów.
4. Kodowanie to **cp1250 z CRLF**, nie UTF-8. Czytane jako UTF-8 daje `Bia<?>ystok` i zakłada śmieciowe rekordy `Team`.

Skąd ten format: link „Download as CSV" na stronie sezonu w TheSportsDB (`/season/4422-polish-ekstraklasa/<sezon>?csv=1&all=1`) nie zwraca pliku, tylko stronę HTML z CSV w polu `<textarea>`. Każde pole jest tam w cudzysłowach, zapisanych jako `&quot;`, a znaki są w UTF-8. Owinięcie wierszy (punkt 2) i cp1250 (punkt 4) dokłada dopiero arkusz kalkulacyjny przy zapisie skopiowanego pola.

Konwersję robi `scripts/Convert-SportsDbCsv.ps1`. Przyjmuje oba źródła — plik zapisany z arkusza albo, z przełącznikiem `-FromHtml`, wprost pobraną stronę:

```powershell
.\scripts\Convert-SportsDbCsv.ps1 -Path Database\CSV\Ekstraklasa_2026_2027.csv -InPlace
.\scripts\Convert-SportsDbCsv.ps1 -Path strona.html -FromHtml -OutPath Database\CSV\Ekstraklasa_2026_2027.csv
```

Z `-FromHtml` skrypt czyta plik jako UTF-8 (ścisłe dekodowanie — plik w innym kodowaniu jest odrzucany, zamiast dać zniekształcone nazwy drużyn), wycina zawartość `<textarea id='myInput'>`, dekoduje encje HTML i parsuje wiersze jak zwykły CSV z polami w cudzysłowach, bez zdejmowania owinięcia. To rozróżnienie jest konieczne: wiersz wzięty wprost z pola zaczyna się i kończy cudzysłowem, więc tryb domyślny potraktowałby go jak owinięty i rozjechał pola. Dalej obie ścieżki są wspólne — ten sam nagłówek, te same kontrole wierszy i ten sam zapis. Na stronie z 2026-10-10 tryb `-FromHtml` daje bajtowo ten sam plik co tryb domyślny na wersji przepuszczonej przez format arkusza. `-FromHtml` wymaga `-OutPath` (strona nie jest nadpisywana plikiem CSV), a strona HTML podana bez przełącznika jest odrzucana z podpowiedzią.

Skrypt wyciąga numer z `Round N`, obcina znacznik czasu do daty, przepisuje `Thumb` na ósme pole i zapisuje w UTF-8; w trybie domyślnym zdejmuje też owinięcie wierszy. **Nazw drużyn nie rusza** — kanonizuje je mapa aliasów w warstwie importu, dzięki czemu każdy kolejny download wymaga tylko konwersji formatu.

Dwa zabezpieczenia: nic nie jest zapisywane, jeśli **którykolwiek** wiersz okaże się niepoprawny (żadnych plików w połowie skonwertowanych), a plik bez nagłówka `idEvent,...` jest odrzucany — więc powtórne uruchomienie na już skonwertowanym pliku nie zepsuje go. `-InPlace` odkłada oryginał do `*.csv.orig` (ignorowane przez git).

Dotyczy to zwłaszcza **sezonu w toku**: 2026/27 trzeba re-importować po każdej kolejce, a każdy kolejny eksport przyjdzie w tym samym formacie. Parser świadomie nie został rozszerzony o ten format — konwersja jest krokiem ręcznym, więc pominięcie jej skończy się odrzuceniem wszystkich wierszy (błąd „Plik nie zawiera żadnego poprawnego wiersza"), a nie cichym zepsuciem danych.
