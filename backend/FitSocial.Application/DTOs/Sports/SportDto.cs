namespace FitSocial.Application.DTOs.Sports;

public class SportDto
{
    public Guid SportId { get; set; }
    public string SportName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Đang hoạt động";
    public int CoachCount { get; set; }
    public int UserCount { get; set; }
    public int OpenClassCount { get; set; }
}

