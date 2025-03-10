using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.Requests;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Match.PUT;

public class UpdateMatchById : Endpoint<UpdateMatchResultRequest>
{
    private readonly MatchService _matchService;

    public UpdateMatchById(MatchService matchService)
    {
        _matchService= matchService;
    }

    public override void Configure()
    {
        Put("api/matches/update-result");
        AllowAnonymous();
    }

    public override async Task HandleAsync(UpdateMatchResultRequest req ,CancellationToken ct)
    {
        await _matchService.UpdateMatchResultById(req);
    }
}