using EkstraSim.Prediction.Models;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Research;

public class GetAvailableModels : EndpointWithoutRequest<EkstraSimResult<IEnumerable<string>>>
{
    public override void Configure()
    {
        Get("api/research/models");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = new EkstraSimResult<IEnumerable<string>>
        {
            Success = true,
            Data = PredictionModelFactory.AvailableModels
        };

        await SendAsync(result, 200, ct);
    }
}
