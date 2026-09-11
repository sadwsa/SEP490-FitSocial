namespace FitSocial.Client.Models.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ApiResponse
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
}
