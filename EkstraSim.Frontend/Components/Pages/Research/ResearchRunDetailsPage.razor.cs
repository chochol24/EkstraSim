using EkstraSim.Shared;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Resources;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Newtonsoft.Json;

namespace EkstraSim.Frontend.Components.Pages.Research;

public partial class ResearchRunDetailsPage
{
    [Parameter]
    public int RunId { get; set; }

    private static readonly (string Key, string Label)[] Metrics =
    [
        ("RankedProbability", "RPS"),
        ("Brier", "Brier"),
        ("LogLoss", "Log-loss"),
        ("ProbabilityOfActualScore", "P dokładnego wyniku")
    ];

    private ModelEvaluationRunDTO? run;
    private List<ModelRoundMetricDTO> roundMetrics = [];
    private ModelComparisonDTO? comparison;
    private List<ModelPredictionDTO> predictions = [];

    private string selectedMetric = "RankedProbability";
    private string? predictionModelFilter;
    private int? predictionRoundFilter;
    private ModelPredictionDTO? selectedPrediction;
    private double[,]? selectedMatrix;

    private bool isLoading = true;
    private bool isLoadingPredictions;

    private List<string> ModelNames => run?.ModelNames ?? [];

    private bool IsFinished => run?.Status == EvaluationRunStatus.Completed;

    private List<int> ChartRounds => roundMetrics
        .Select(m => m.Round)
        .Distinct()
        .OrderBy(round => round)
        .ToList();

    protected override async Task OnInitializedAsync()
    {
        await LoadAllAsync();
        isLoading = false;
    }

    private async Task LoadAllAsync()
    {
        var runResult = await _researchService.GetRunAsync(RunId);

        if (!runResult.Success || runResult.Data == null)
        {
            Snackbar.Add(runResult.ErrorMessage ?? SnackbarMessages.Research_Run_Get_Failed, Severity.Error);
            return;
        }

        run = runResult.Data;

        if (!IsFinished)
        {
            return;
        }

        var metricsResult = await _researchService.GetRoundMetricsAsync(RunId);
        if (metricsResult.Success && metricsResult.Data != null)
        {
            roundMetrics = metricsResult.Data;
        }
        else
        {
            Snackbar.Add(metricsResult.ErrorMessage ?? SnackbarMessages.Research_Metrics_Get_Failed, Severity.Error);
        }

        await LoadComparisonAsync();
    }

    private async Task LoadComparisonAsync()
    {
        var result = await _researchService.GetComparisonAsync(RunId, selectedMetric);

        if (result.Success && result.Data != null)
        {
            comparison = result.Data;
        }
        else
        {
            Snackbar.Add(result.ErrorMessage ?? SnackbarMessages.Research_Comparison_Get_Failed, Severity.Error);
        }
    }

    private async Task OnMetricChangedAsync(string metric)
    {
        selectedMetric = metric;
        await LoadComparisonAsync();
    }

    private async Task LoadPredictionsAsync()
    {
        isLoadingPredictions = true;
        selectedPrediction = null;
        selectedMatrix = null;

        var result = await _researchService.GetPredictionsAsync(RunId, predictionModelFilter, predictionRoundFilter);

        isLoadingPredictions = false;

        if (result.Success && result.Data != null)
        {
            predictions = result.Data;
        }
        else
        {
            Snackbar.Add(result.ErrorMessage ?? SnackbarMessages.Research_Predictions_Get_Failed, Severity.Error);
        }
    }

    private void SelectPrediction(DataGridRowClickEventArgs<ModelPredictionDTO> args)
    {
        selectedPrediction = args.Item;
        selectedMatrix = null;

        if (string.IsNullOrWhiteSpace(args.Item.ResultProbabilityMatrixJson))
        {
            return;
        }

        selectedMatrix = JsonConvert.DeserializeObject<double[,]>(args.Item.ResultProbabilityMatrixJson);
    }

    private List<ChartSeries> BuildMetricSeries()
    {
        var rounds = ChartRounds;

        return roundMetrics
            .GroupBy(m => m.ModelName)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(group => new ChartSeries
            {
                Name = group.Key,
                Data = rounds
                    .Select(round =>
                    {
                        var metric = group.FirstOrDefault(m => m.Round == round);
                        return metric == null ? 0 : MetricValue(metric, selectedMetric);
                    })
                    .ToArray()
            })
            .ToList();
    }

    private List<ChartSeries> BuildDriftSeries()
    {
        var rounds = ChartRounds;

        return roundMetrics
            .GroupBy(m => m.ModelName)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(group => new ChartSeries
            {
                Name = group.Key,
                Data = rounds
                    .Select(round => group.FirstOrDefault(m => m.Round == round)?.ParameterDrift ?? 0)
                    .ToArray()
            })
            .ToList();
    }

    private static double MetricValue(ModelRoundMetricDTO metric, string key) => key switch
    {
        "Brier" => metric.Brier,
        "LogLoss" => metric.LogLoss,
        "ProbabilityOfActualScore" => metric.MeanProbabilityOfActualScore,
        _ => metric.RankedProbabilityScore
    };

    private static string MetricLabel(string key) => Metrics.FirstOrDefault(m => m.Key == key).Label ?? key;

    private static string Percent(double value) => $"{value * 100:F1}%";

    private static string Fixed3(double value) => value.ToString("F3");

    private static string PValueLabel(double pValue, bool isConclusive)
    {
        return isConclusive ? pValue.ToString("F4") : "—";
    }

    private static Color SignificanceColour(bool isSignificant) => isSignificant ? Color.Success : Color.Default;

    private static string StatusLabel(EvaluationRunStatus status) => status switch
    {
        EvaluationRunStatus.Pending => "Oczekuje w kolejce",
        EvaluationRunStatus.Running => "Obliczenia w toku",
        EvaluationRunStatus.Completed => "Gotowe",
        _ => "Zakończone błędem"
    };

    private static Color StatusColour(EvaluationRunStatus status) => status switch
    {
        EvaluationRunStatus.Completed => Color.Success,
        EvaluationRunStatus.Running => Color.Info,
        EvaluationRunStatus.Failed => Color.Error,
        _ => Color.Default
    };

    private List<PromotedTeamDTO> PromotedTeams()
    {
        if (string.IsNullOrWhiteSpace(run?.PromotedTeamsJson))
        {
            return [];
        }

        return JsonConvert.DeserializeObject<List<PromotedTeamDTO>>(run.PromotedTeamsJson) ?? [];
    }
}
