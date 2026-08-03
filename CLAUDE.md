# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Working conventions

- **No code comments** beyond the kind already present in the codebase (short, single-line, sparse — e.g. `//current season strengths`). Do not add XML doc comments, explanatory blocks, or narrated steps.
- **All "how it works" documentation goes to [docs/DZIALANIE.md](docs/DZIALANIE.md)** (in Polish) — model math, research pipeline, data flow, design decisions. Update it whenever behavior is added or changed instead of commenting code.
- **No git operations** — the user manages branches and commits themselves. Never commit, push, or stage anything here.
- **`bin/` and `obj/` are tracked** in this repo despite being in `.gitignore` (committed before the ignore rule). A build dirties ~55 of them, so `git status` is always noisy — that noise is not your change.

## Solution layout

Five projects (`EkstraSim.sln` at the repo root), `net9.0` throughout, file-scoped namespaces:

| Project | Role |
| --- | --- |
| `EkstraSim.Backend` | ASP.NET Core Web API — FastEndpoints + EF Core (SQL Server). Owns the legacy Monte Carlo engine and the research layer. |
| `EkstraSim.Frontend` | Blazor **Server** app (interactive server render mode) with MudBlazor. Talks to the backend over HTTP only. UI is in Polish. |
| `EkstraSim.Shared` | DTOs, request records, the `EkstraSimResult<T>` envelope, `Constants`, and the `SnackbarMessages` resx. Referenced by everything. |
| `EkstraSim.Prediction` | Pure computational core for the master's thesis: prediction models, metrics, statistics. **No EF, no HTTP** — depends only on `Shared` + MathNet.Numerics. |
| `EkstraSim.Tests` | xUnit tests for `EkstraSim.Prediction`. |

## Commands

```bash
dotnet build EkstraSim.sln
```

```bash
dotnet test EkstraSim.Tests/EkstraSim.Tests.csproj
```

```bash
dotnet test EkstraSim.Tests/EkstraSim.Tests.csproj --filter FullyQualifiedName~DixonColesModelTests
```

```bash
dotnet run --project EkstraSim.Backend --launch-profile https
```

```bash
dotnet run --project EkstraSim.Frontend --launch-profile https
```

Local ports: backend `https://localhost:7050` / `http://localhost:5274`, frontend `https://localhost:7079` / `http://localhost:5285`. Swagger UI is served at the backend root in all environments.

EF Core migrations (run from `EkstraSim.Backend`):

```bash
dotnet ef migrations add <Name>
```

Connection string key is `ConnectionStrings:DefaultConnection`. `appsettings.Development.json` is gitignored; `appsettings.json` is not — never put a real connection string in it. **Do not run `dotnet ef database update`** without being asked; applying migrations is the user's call.

## Backend architecture

**Endpoints** (`Endpoints/<Area>/<VERB>/<Name>.cs`) are FastEndpoints classes that do nothing but resolve a service, call one method, and `SendAsync`. Business logic belongs in `Database/Services`.

Route versioning is configured in `Program.cs` with `Versioning.PrependToRoute = true` and default version 1. **Endpoints declare `Get("api/teams")` but the live route is `/v1/api/teams`** — the frontend services must include the `v1` prefix, the endpoint definitions must not.

**Services** (`Database/Services`) all follow the same shape:
- inject `IDbContextFactory<EkstraSimDbContext>` (never a scoped `DbContext` — the simulation code runs many concurrent contexts) plus `IMapper`;
- create a context per operation with `await using var context = await _dbFactory.CreateDbContextAsync();`
- map entities to DTOs via AutoMapper and return `EkstraSimResult<T>` (`Success` / `Data` / `ErrorMessage`), catching exceptions into the envelope rather than throwing. Error text comes from `SnackbarMessages` resources.

Endpoints translate the envelope to a status code with `await SendAsync(result, result.Success ? 200 : 500, ct);`.

**Entity configuration** uses `IEntityTypeConfiguration` classes in `Database/Configurations`, auto-discovered via `ApplyConfigurationsFromAssembly`. Add new DTO mappings to `Database/Entities/AutoMapperProfile.cs`.

## The legacy Monte Carlo engine

`SimulatingService` ([Database/Services/SimulatingService.cs](EkstraSim.Backend/Database/Services/SimulatingService.cs)) is the engineering-thesis simulator. **It is frozen** — the research layer was deliberately built alongside it rather than refactoring it. Do not change its behavior without being asked.

Things about it that surprise people:

- It is registered as a **singleton built via `SimulatingService.CreateAsync(...)` at startup**, which eagerly loads *all* leagues, matches, and teams into in-memory lists that are never refreshed. **Any data change requires a backend restart before the simulator sees it.**
- Scores are drawn from a Poisson distribution using expected goals blended across three horizons weighted by `Constants.CurrentSeasonScale` (0.67), `PreviousSeasonScale` (0.3) and `HistoricalScale` (0.03), with recent-form/home-away/head-to-head multipliers layered on when `numberOfSimulations > 1`.
- Final tables are ordered by `TeamStatsComparer.SortSeason`: points → head-to-head mini-table → goal difference/goals/wins/away wins → random `Guid` tiebreak.
- It assumes an 18-team, 34-round Ekstraklasa (`Constants.NumberOfRoundsEkstaklasa` plus the hardcoded 1st–18th place properties on `SimulatedTeamInFinalTable`).

Known defects, documented rather than fixed (the research layer routes around them):

- [CSVService.cs](EkstraSim.Backend/Database/Services/CSVService.cs) has `filePath = null` (path commented out) and an empty `catch`, so `GET /v1/api/importcsv` silently does nothing.
- [TeamService.cs](EkstraSim.Backend/Database/Services/TeamService.cs) `UpdateAverageTeamGoals` hardcodes `LeagueId == 1`, current `SeasonId == 6`, previous `SeasonId == 1`.
- `SimulatingService.cs:190` applies `PreviousSeasonScale` (0.3) instead of `HistoricalScale` (0.03) to the away historical term, so away weights sum to 1.27. The `EkstraSim.Prediction` port fixes this — see `docs/DZIALANIE.md`.

Data-prep order for the legacy engine (then restart the backend): `importcsv` → `league/goals` → `team/goals` → `rebase-elo`.

## Research layer (`EkstraSim.Prediction`)

Three models behind `IPredictionModel` (`Train` → `Predict` → `UpdateWithRound` → `GetParametersSnapshot`): `PoissonModel` (port of the legacy math), `DixonColesModel` (MLE with tau correction, time decay, ridge), `EloModel` (zero-sum rating replay → Poisson regression onto expected goals).

Score matrices are computed **analytically** (`ScoreGrid`), not by Monte Carlo, so research results carry no sampling noise.

Models never touch EF or `SimulatingService` — the orchestrator hands them `MatchData` records. Read `docs/DZIALANIE.md` before changing model math; it records the deliberate deviations from the legacy engine and why each one matters for the thesis.

## Frontend architecture

- Pages live in `Components/Pages/<Area>/`, with markup in `.razor` and logic in a partial `.razor.cs` for the larger ones. Services are injected with `@inject` in the markup and used from the code-behind.
- Every backend call goes through a thin service in `Components/Services/` wrapping `HttpServiceHelper`, which deserialises into `EkstraSimResult<T>` and converts exceptions into a failed result. Pages surface `result.ErrorMessage` through MudBlazor `ISnackbar`, falling back to `SnackbarMessages.Error_Base`.
- **The API base address is hardcoded to the Azure production URL in [EkstraSim.Frontend/Program.cs:27](EkstraSim.Frontend/Program.cs:27).** Change it there to point at a local backend.
- Keep user-facing strings (and `SnackbarMessages.resx` entries) in Polish.

## Deployment

Both web projects publish to Azure App Service (Poland Central) via Web Deploy profiles in `Properties/PublishProfiles/`: `jw-ekstrasim-api` (backend) and `jw-ekstrasim` (frontend).
