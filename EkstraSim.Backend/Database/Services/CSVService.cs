using EkstraSim.Backend.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database.Services;

public class CSVService
{
    private readonly IDbContextFactory<EkstraSimDbContext> _dbFactory;

    public CSVService(IDbContextFactory<EkstraSimDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }


    public async Task CSVImport()
	{
        //string filePath = "D:\\Inżynierka\\CSV\\Ekstraklasa_2024_2025.csv";
        await using var context = await _dbFactory.CreateDbContextAsync();

        string filePath = null;
		try
		{
			using (var reader = new StreamReader(filePath))
			{ 
				string line;
				List<Match> matches = [];
				Season? season = await context.Seasons.Where(s => s.LeagueId == 1 && s.Name == "2024/2025").FirstOrDefaultAsync();
				League? league = await context.Leagues.Where(l => l.Id == 1).FirstOrDefaultAsync();
				List<Team> teams = await context.Teams.ToListAsync();

				while ((line = reader.ReadLine()) != null)
				{
					var values = line.Split(',');

					DateTime matchDate = DateTime.Parse(values[1]);

					int round = int.Parse(values[2]);

					string homeTeam = values[3];
					int homeTeamId = teams.Where(t => t.Name == homeTeam).Select(t => t.Id).FirstOrDefault();

					int? homeGoals = string.IsNullOrEmpty(values[4]) ? (int?)null : int.Parse(values[4]);

					string awayTeam = values[5];
					int awayTeamId = teams.Where(t => t.Name == awayTeam).Select(t => t.Id).FirstOrDefault();

					int? awayGoals = string.IsNullOrEmpty(values[6]) ? (int?)null : int.Parse(values[6]);

					Match match = new Match
					{
						Date = matchDate,
						SeasonId = season?.Id,
						LeagueId = league?.Id,
						Round = round,
						HomeTeamId = homeTeamId,
						AwayTeamId = awayTeamId,
						HomeTeamScore = homeGoals ?? null,
						AwayTeamScore = awayGoals ?? null
					};

					matches.Add(match);
				}

				foreach (var match in matches)
				{
					if (!context.Teams.Any(t => t.Id == match.AwayTeamId))
					{
						throw new InvalidOperationException($"Team with ID {match.AwayTeamId} does not exist.");
					}
					await context.Matches.AddAsync(match);
				}
				await context.SaveChangesAsync();
			}
		}
		catch (Exception e)
		{
			
		}
	}
}
