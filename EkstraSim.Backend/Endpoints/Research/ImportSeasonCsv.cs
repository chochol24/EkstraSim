using EkstraSim.Backend.Database.Services.Research;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Research;

public class ImportSeasonCsv : Endpoint<ImportSeasonCsvRequest, EkstraSimResult<CsvImportResultDTO>>
{
    private readonly CsvImportService _csvImportService;

    public ImportSeasonCsv(CsvImportService csvImportService)
    {
        _csvImportService = csvImportService;
    }

    public override void Configure()
    {
        Post("api/research/import-csv");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ImportSeasonCsvRequest request, CancellationToken ct)
    {
        var result = await _csvImportService.ImportAsync(request);
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}
