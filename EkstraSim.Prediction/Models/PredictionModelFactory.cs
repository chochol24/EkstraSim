namespace EkstraSim.Prediction.Models;

public static class PredictionModelFactory
{
    public const string Poisson = "Poisson";
    public const string DixonColes = "DixonColes";
    public const string Elo = "Elo";

    public static IReadOnlyList<string> AvailableModels => [Poisson, DixonColes, Elo];

    public static bool IsKnown(string name) => AvailableModels.Contains(Canonical(name));

    public static IPredictionModel Create(string name) => Canonical(name) switch
    {
        Poisson => new PoissonModel(),
        DixonColes => new DixonColesModel(),
        Elo => new EloModel(),
        _ => throw new ArgumentException($"Nieznany model predykcyjny: '{name}'.", nameof(name))
    };

    private static string Canonical(string name)
    {
        return AvailableModels.FirstOrDefault(model => string.Equals(model, name?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
    }
}
