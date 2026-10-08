namespace EkstraSim.Shared;

public static class TeamNameAliases
{
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["legia warsaw"] = "Legia Warszawa",
        ["cracovia kraków"] = "Cracovia",
        ["wisla plock"] = "Wisła Płock"
    };

    public static string Normalise(string name)
    {
        return name.Trim().ToLowerInvariant();
    }

    public static string Canonicalise(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var trimmed = name.Trim();

        if (Aliases.TryGetValue(Normalise(trimmed), out var canonical))
        {
            return canonical;
        }

        return trimmed;
    }
}
