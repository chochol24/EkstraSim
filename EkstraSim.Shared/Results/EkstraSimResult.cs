namespace EkstraSim.Shared.Results;

public class EkstraSimResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
}
