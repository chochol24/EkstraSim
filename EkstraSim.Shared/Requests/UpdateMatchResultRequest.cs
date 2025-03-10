namespace EkstraSim.Shared.Requests;

public record UpdateMatchResultRequest(int MatchId, int HomeTeamScore, int AwayTeamScore);
