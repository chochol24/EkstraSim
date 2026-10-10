using EkstraSim.Shared;
using EkstraSim.Shared.Resources;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database.Services.Research;

public class ResearchRunRecovery
{
    private readonly IDbContextFactory<EkstraSimDbContext> _dbFactory;
    private readonly ILogger<ResearchRunRecovery> _logger;

    public ResearchRunRecovery(IDbContextFactory<EkstraSimDbContext> dbFactory, ILogger<ResearchRunRecovery> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task RecoverInterruptedRunsAsync()
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();

            var finishedAt = DateTime.UtcNow;
            var recovered = await context.ModelEvaluationRuns
                .Where(r => r.Status == EvaluationRunStatus.Pending || r.Status == EvaluationRunStatus.Running)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, EvaluationRunStatus.Failed)
                    .SetProperty(r => r.ErrorMessage, SnackbarMessages.Research_Run_Interrupted)
                    .SetProperty(r => r.FinishedAt, finishedAt));

            if (recovered > 0)
            {
                _logger.LogWarning("Odzyskiwanie badan: {Count} przerwanych badan oznaczono jako Failed.", recovered);
            }
            else
            {
                _logger.LogInformation("Odzyskiwanie badan: brak przerwanych badan.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Odzyskiwanie przerwanych badan nie powiodlo sie.");
        }
    }
}
