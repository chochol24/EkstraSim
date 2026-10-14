using AutoMapper;
using EkstraSim.Backend.Database;
using EkstraSim.Backend.Database.Entities;
using EkstraSim.Backend.Database.Services;
using EkstraSim.Prediction.Models;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Tests;

public sealed class LegacyDatabase : IDbContextFactory<EkstraSimDbContext>
{
    public const int LeagueId = 1;
    public const int CurrentSeasonId = 6;
    public const int PreviousSeasonId = 1;
    private const int OtherSeasonOffset = 100;

    private static readonly IMapper Mapper = new MapperConfiguration(config => config.AddProfile<AutoMapperProfile>()).CreateMapper();

    private readonly DbContextOptions<EkstraSimDbContext> _options;

    private LegacyDatabase()
    {
        _options = new DbContextOptionsBuilder<EkstraSimDbContext>()
            .UseInMemoryDatabase($"legacy-{Guid.NewGuid():N}")
            .Options;
    }

    public EkstraSimDbContext CreateDbContext() => new(_options);

    public static IReadOnlyDictionary<int, int> SeasonIds(IReadOnlyList<int> chronology, int seasonId)
    {
        var index = chronology.ToList().IndexOf(seasonId);
        if (index < 1)
        {
            throw new ArgumentException($"Sezon {seasonId} nie ma sezonu poprzedniego w chronologii.", nameof(seasonId));
        }

        var previousSeasonId = chronology[index - 1];

        return chronology
            .Take(index + 1)
            .ToDictionary(
                id => id,
                id => id == seasonId ? CurrentSeasonId : id == previousSeasonId ? PreviousSeasonId : OtherSeasonOffset + id);
    }

    public static async Task<LegacyDatabase> SeedAsync(
        IReadOnlyList<MatchData> known,
        IReadOnlyList<MatchData> targets,
        IReadOnlyDictionary<int, int> seasonIds)
    {
        var database = new LegacyDatabase();
        await using var context = database.CreateDbContext();

        context.Leagues.Add(new League { Id = LeagueId, Name = "Ekstraklasa" });
        context.Seasons.AddRange(seasonIds.Values.Select(id => new Season { Id = id, Name = $"Sezon {id}", LeagueId = LeagueId }));
        context.Teams.AddRange(known
            .Concat(targets)
            .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
            .Distinct()
            .Select(id => new Team { Id = id, Name = $"Drużyna {id}", ELO = 1300 }));
        context.Matches.AddRange(known.Select(m => ToEntity(m, seasonIds, withScore: true)));
        context.Matches.AddRange(targets.Select(m => ToEntity(m, seasonIds, withScore: false)));

        await context.SaveChangesAsync();
        return database;
    }

    public async Task<IReadOnlyList<SimulatedMatchResult>> SimulateAsync(IReadOnlyCollection<int> targetIds, int simulations)
    {
        await new LeagueService(this, Mapper).UpdateAverageLeagueGoals(LeagueId);
        await new TeamService(this, Mapper).UpdateAverageTeamGoals();

        var engine = await SimulatingService.CreateAsync(this);

        await using var context = CreateDbContext();
        var targets = await context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => targetIds.Contains(m.Id))
            .OrderBy(m => m.Id)
            .ToListAsync();

        if (targets.Count != targetIds.Count)
        {
            throw new InvalidOperationException($"W bazie jest {targets.Count} z {targetIds.Count} meczów kolejki.");
        }

        return (await engine.SimulateRound(targets, LeagueId, simulations)).OrderBy(r => r.MatchId).ToList();
    }

    private static Match ToEntity(MatchData match, IReadOnlyDictionary<int, int> seasonIds, bool withScore)
    {
        if (match.LeagueId != LeagueId)
        {
            throw new ArgumentException($"Mecz {match.Id} nie należy do ligi {LeagueId}, a silnik z pracy inżynierskiej liczy formę bez filtra ligi.", nameof(match));
        }

        return new Match
        {
            Id = match.Id,
            Date = match.Date,
            SeasonId = seasonIds[match.SeasonId!.Value],
            LeagueId = LeagueId,
            Round = match.Round,
            HomeTeamId = match.HomeTeamId,
            AwayTeamId = match.AwayTeamId,
            HomeTeamScore = withScore ? match.HomeScore : null,
            AwayTeamScore = withScore ? match.AwayScore : null
        };
    }
}
