using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.DTOs;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Season.GET;

public class GetSeasons : EndpointWithoutRequest<IEnumerable<SeasonDTO>>
{
    private readonly SeasonService _seasonService;

    public GetSeasons(SeasonService seasonService)
    {
        _seasonService = seasonService;
    }
    public override void Configure()
    {
        Get("api/seasons");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _seasonService.GetSeasonsAsync();
        if (result != null)
        {
            await SendAsync(result, cancellation: ct);
        }
        else
        {

            //obsluga bledu
        }
    }
}
