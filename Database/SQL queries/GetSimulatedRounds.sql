select homeTeam.Name AS 'Gospodarz', sm.PredictedHomeScore as ' ', sm.PredictedAwayScore as ' ', awayTeam.Name as 'Goœæ', 
r.[Round] as 'Kolejka', l.Name as 'Liga' ,s.Name As 'Sezon', r.NumberOfSimulations as 'Iloœæ symulacji'
from [SimulatedRounds] as r
inner join SimulatedMatchResults as sm on r.Id = sm.SimulatedRoundId
inner join Matches as m on sm.MatchId = m.Id
LEFT JOIN Teams AS homeTeam ON m.HomeTeamId = homeTeam.Id
LEFT JOIN Teams AS awayTeam ON m.AwayTeamId = awayTeam.Id
inner join Leagues as l on r.LeagueId = l.Id
inner join Seasons as s on s.Id = r.SeasonId
Where sm.MatchId = 2377