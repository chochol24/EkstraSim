namespace EkstraSim.Shared.DTOs;

public class CsvImportResultDTO
{
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public bool SeasonCreated { get; set; }

    public int RowsRead { get; set; }
    public int MatchesInserted { get; set; }
    public int MatchesUpdated { get; set; }
    public int MatchesUnchanged { get; set; }

    public List<string> TeamsCreated { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}
