namespace EkstraSim.Backend.Database.Services.Research;

public interface IResearchRunLauncher
{
    void Launch(int runId);
}

public class ResearchRunLauncher : IResearchRunLauncher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResearchRunLauncher> _logger;

    public ResearchRunLauncher(IServiceScopeFactory scopeFactory, ILogger<ResearchRunLauncher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Launch(int runId)
    {
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();

            try
            {
                var orchestrator = scope.ServiceProvider.GetRequiredService<ResearchOrchestrationService>();
                await orchestrator.ExecuteAsync(runId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieobsluzony blad w tle dla badania {RunId}.", runId);
            }
        });
    }
}
