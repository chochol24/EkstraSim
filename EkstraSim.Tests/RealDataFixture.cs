using System.Globalization;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public static class RealDataFixture
{
    public const int LeagueId = 1;
    public const int Season2425 = 6;
    public const int Season2526 = 7;
    public const int TrainingLastRound = 18;

    private const string FileName = "ekstraklasa-liga1-mecze.csv";
    private const string Header = "Id;Date;Round;SeasonId;LeagueId;HomeTeamId;AwayTeamId;HomeScore;AwayScore";

    private static readonly Lazy<List<MatchData>> LoadedMatches = new(Load);
    private static readonly Lazy<List<int>> LoadedChronology = new(BuildChronology);

    public static IReadOnlyList<MatchData> Matches => LoadedMatches.Value;

    public static IReadOnlyList<int> SeasonChronology => LoadedChronology.Value;

    public static TrainingOptions Options(int seasonId)
    {
        return new TrainingOptions
        {
            LeagueId = LeagueId,
            SeasonId = seasonId,
            SeasonChronology = SeasonChronology,
            UseFormFactors = true,
            TimeDecayXi = 0.0065,
            RidgeLambda = 0.05
        };
    }

    private static List<MatchData> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", FileName);
        var lines = File.ReadAllLines(path);

        if (lines.Length == 0 || lines[0] != Header)
        {
            throw new InvalidDataException($"{FileName}: nieoczekiwany nagłówek.");
        }

        return lines
            .Skip(1)
            .Where(line => line.Length > 0)
            .Select(Parse)
            .ToList();
    }

    private static MatchData Parse(string line)
    {
        var fields = line.Split(';');
        if (fields.Length != 9)
        {
            throw new InvalidDataException($"{FileName}: wiersz '{line}' ma {fields.Length} pól, oczekiwano 9.");
        }

        return new MatchData(
            Int(fields[0]),
            DateTime.ParseExact(fields[1], "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Int(fields[2]),
            Int(fields[3]),
            Int(fields[4]),
            Int(fields[5]),
            Int(fields[6]),
            Int(fields[7]),
            Int(fields[8]));
    }

    private static int Int(string value) => int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);

    private static List<int> BuildChronology()
    {
        return Matches
            .Where(m => m.LeagueId == LeagueId && m.SeasonId.HasValue)
            .GroupBy(m => m.SeasonId!.Value)
            .OrderBy(g => g.Min(m => m.Date))
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .ToList();
    }
}
