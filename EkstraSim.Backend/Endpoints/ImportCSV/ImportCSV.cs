using EkstraSim.Backend.Database;
using EkstraSim.Backend.Database.Services;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.ImportCSV;


public class ImportCSV : EndpointWithoutRequest
{
	private readonly CSVService _csvService;

	public ImportCSV(CSVService csvService)
	{
        _csvService = csvService;
	}
	public override void Configure()
	{
		Get("api/importcsv");
		AllowAnonymous();
	}

	public override async Task HandleAsync(CancellationToken ct)
	{
		await _csvService.CSVImport();
	}
}
