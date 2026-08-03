using EkstraSim.Backend.Database.Entities;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Resources;
using EkstraSim.Shared.Results;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace EkstraSim.Backend.Database.Services.Research;

public class CsvImportService
{
    private const int ExpectedColumnCount = 7;
    private const decimal NewTeamElo = 1300;

    private readonly IDbContextFactory<EkstraSimDbContext> _dbFactory;

    public CsvImportService(IDbContextFactory<EkstraSimDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<EkstraSimResult<CsvImportResultDTO>> ImportAsync(ImportSeasonCsvRequest request)
    {
        try
        {
            var lines = await ReadLinesAsync(request);
            if (lines == null)
            {
                return Failure("Nie podano ani ścieżki do pliku, ani treści CSV.");
            }

            await using var context = await _dbFactory.CreateDbContextAsync();

            if (!await context.Leagues.AnyAsync(l => l.Id == request.LeagueId))
            {
                return Failure($"Liga o Id {request.LeagueId} nie istnieje.");
            }

            var result = new CsvImportResultDTO { SeasonName = request.SeasonName.Trim() };
            var rows = ParseRows(lines, result);
            result.RowsRead = rows.Count;

            if (rows.Count == 0)
            {
                return Failure("Plik nie zawiera żadnego poprawnego wiersza.");
            }

            var season = await EnsureSeasonAsync(context, request.LeagueId, result);
            var teamsByName = await EnsureTeamsAsync(context, rows, result);

            var matchLookup = (await context.Matches
                    .Where(m => m.SeasonId == season.Id && m.LeagueId == request.LeagueId)
                    .ToListAsync())
                .GroupBy(m => MatchKey(m.Round, m.HomeTeamId, m.AwayTeamId))
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var row in rows)
            {
                var homeTeam = teamsByName[Normalise(row.HomeTeamName)];
                var awayTeam = teamsByName[Normalise(row.AwayTeamName)];
                var key = MatchKey(row.Round, homeTeam.Id, awayTeam.Id);

                if (matchLookup.TryGetValue(key, out var existing))
                {
                    if (UpdateExisting(existing, row))
                    {
                        result.MatchesUpdated++;
                    }
                    else
                    {
                        result.MatchesUnchanged++;
                    }

                    continue;
                }

                var match = new Match
                {
                    Date = row.Date,
                    Round = row.Round,
                    SeasonId = season.Id,
                    LeagueId = request.LeagueId,
                    HomeTeamId = homeTeam.Id,
                    AwayTeamId = awayTeam.Id,
                    HomeTeamScore = row.HomeScore,
                    AwayTeamScore = row.AwayScore
                };

                context.Matches.Add(match);
                matchLookup[key] = match;
                result.MatchesInserted++;
            }

            await context.SaveChangesAsync();

            return new EkstraSimResult<CsvImportResultDTO>
            {
                Success = true,
                Data = result
            };
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<CsvImportResultDTO>
            {
                Success = false,
                ErrorMessage = $"{SnackbarMessages.Error_Base} {ex.Message}"
            };
        }
    }

    private async Task<Season> EnsureSeasonAsync(EkstraSimDbContext context, int leagueId, CsvImportResultDTO result)
    {
        var season = await context.Seasons
            .FirstOrDefaultAsync(s => s.LeagueId == leagueId && s.Name == result.SeasonName);

        if (season == null)
        {
            season = new Season { Name = result.SeasonName, LeagueId = leagueId };
            context.Seasons.Add(season);
            await context.SaveChangesAsync();
            result.SeasonCreated = true;
        }

        result.SeasonId = season.Id;
        return season;
    }

    private static async Task<Dictionary<string, Team>> EnsureTeamsAsync(
        EkstraSimDbContext context,
        List<CsvRow> rows,
        CsvImportResultDTO result)
    {
        var teamsByName = (await context.Teams.ToListAsync())
            .GroupBy(t => Normalise(t.Name))
            .ToDictionary(g => g.Key, g => g.First());

        var namesInFile = rows
            .SelectMany(r => new[] { r.HomeTeamName, r.AwayTeamName })
            .GroupBy(Normalise)
            .Select(g => g.First())
            .ToList();

        var created = false;

        foreach (var name in namesInFile)
        {
            if (teamsByName.ContainsKey(Normalise(name)))
            {
                continue;
            }

            var team = new Team { Name = name, ELO = NewTeamElo };
            context.Teams.Add(team);
            teamsByName[Normalise(name)] = team;
            result.TeamsCreated.Add(name);
            created = true;
        }

        if (created)
        {
            await context.SaveChangesAsync();
        }

        return teamsByName;
    }

    private static async Task<List<string>?> ReadLinesAsync(ImportSeasonCsvRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.CsvContent))
        {
            return request.CsvContent
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.FilePath) && File.Exists(request.FilePath))
        {
            var content = await File.ReadAllLinesAsync(request.FilePath, Encoding.UTF8);
            return content.ToList();
        }

        return null;
    }

    private sealed record CsvRow(DateTime Date, int Round, string HomeTeamName, int? HomeScore, string AwayTeamName, int? AwayScore);

    private static List<CsvRow> ParseRows(List<string> lines, CsvImportResultDTO result)
    {
        var rows = new List<CsvRow>();

        for (var lineNumber = 1; lineNumber <= lines.Count; lineNumber++)
        {
            var line = lines[lineNumber - 1];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var row = ParseRow(line, lineNumber, result.Warnings);
            if (row != null)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static CsvRow? ParseRow(string line, int lineNumber, List<string> warnings)
    {
        var fields = line.Split(',');

        if (fields.Length < ExpectedColumnCount)
        {
            warnings.Add($"Linia {lineNumber}: oczekiwano co najmniej {ExpectedColumnCount} kolumn, jest {fields.Length}.");
            return null;
        }

        if (!DateTime.TryParse(fields[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            warnings.Add($"Linia {lineNumber}: nieprawidłowa data '{fields[1]}'.");
            return null;
        }

        if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var round))
        {
            warnings.Add($"Linia {lineNumber}: nieprawidłowa kolejka '{fields[2]}'.");
            return null;
        }

        var homeName = fields[3].Trim();
        var awayName = fields[5].Trim();

        if (string.IsNullOrWhiteSpace(homeName) || string.IsNullOrWhiteSpace(awayName))
        {
            warnings.Add($"Linia {lineNumber}: brak nazwy drużyny.");
            return null;
        }

        if (Normalise(homeName) == Normalise(awayName))
        {
            warnings.Add($"Linia {lineNumber}: gospodarz i gość to ta sama drużyna ('{homeName}').");
            return null;
        }

        return new CsvRow(date, round, homeName, ParseScore(fields[4]), awayName, ParseScore(fields[6]));
    }

    private static int? ParseScore(string field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return null;
        }

        return int.TryParse(field.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var score) ? score : null;
    }

    private static bool UpdateExisting(Match existing, CsvRow row)
    {
        var changed = false;

        if (row.HomeScore.HasValue && existing.HomeTeamScore != row.HomeScore)
        {
            existing.HomeTeamScore = row.HomeScore;
            changed = true;
        }

        if (row.AwayScore.HasValue && existing.AwayTeamScore != row.AwayScore)
        {
            existing.AwayTeamScore = row.AwayScore;
            changed = true;
        }

        if (existing.Date.Date != row.Date.Date)
        {
            existing.Date = row.Date;
            changed = true;
        }

        return changed;
    }

    private static string MatchKey(int? round, int homeTeamId, int awayTeamId) => $"{round}|{homeTeamId}|{awayTeamId}";

    private static string Normalise(string name) => name.Trim().ToLowerInvariant();

    private static EkstraSimResult<CsvImportResultDTO> Failure(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
}
