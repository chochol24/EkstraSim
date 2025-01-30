SELECT TOP (1000) [Id]
      ,[SeasonId]
      ,[LeagueId]
      ,[RoundBeforeSimulation]
      ,[NumberOfSimulations]
  FROM [EkstraSimDB].[dbo].[SimulatedFinalLeagues]


Select t.Name AS Nazwa, s.* from
SimulatedTeamInFinalTables as s 
inner join Teams as t on s.TeamId = t.Id
Where SimulatedFinalLeagueId = 3
order by AveragePoints desc

Select t.Name AS Nazwa, s.* from
SimulatedTeamInFinalTables as s 
inner join Teams as t on s.TeamId = t.Id
Where SimulatedFinalLeagueId = 2
order by AveragePoints desc

order by FirstPlaceProbability desc, SecondPlaceProbability desc, ThirdPlaceProbability desc, FourthPlaceProbability desc, FifthPlaceProbability desc,
		 SixthPlaceProbability desc, SeventhPlaceProbability desc, EighthPlaceProbability desc, NinthPlaceProbability desc, TenthPlaceProbability desc,
		 EleventhPlaceProbability desc, TwelfthPlaceProbability desc, ThirteenthPlaceProbability desc, FourteenthPlaceProbability desc, FifteenthPlaceProbability desc,
		 SixteenthPlaceProbability desc, SeventeenthPlaceProbability desc, EighteenthPlaceProbability desc