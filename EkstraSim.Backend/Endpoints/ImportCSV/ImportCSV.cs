using EkstraSim.Backend.Database;
using EkstraSim.Backend.Database.Services;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.ImportCSV;


public class ImportCSV : EndpointWithoutRequest
{
	private readonly EkstraSimDbContext _context;

	public ImportCSV(EkstraSimDbContext context)
	{
		_context = context;
	}
	public override void Configure()
	{
		Get("api/importcsv");
		AllowAnonymous();
	}

	public override async Task HandleAsync(CancellationToken ct)
	{
		await CSVService.CSVImport(_context);
	}
}
