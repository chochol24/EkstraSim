using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Match.GET;


public class GetRoundMatches : Endpoint<GetMatchesByRoundRequest, EkstraSimResult<IEnumerable<MatchDTO>>>
{
    private readonly MatchService _matchService;

    public GetRoundMatches(MatchService matchService)
    {
        _matchService = matchService;
    }
    public override void Configure()
    {
        Get("api/matches/{LeagueId}/{SeasonId}/{Round}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetMatchesByRoundRequest req, CancellationToken ct)
    {
        var result = await _matchService.GetMatchesByRound(req);
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}

