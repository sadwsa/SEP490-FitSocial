namespace FitSocial.Domain.Models;

public class FeedSessionSnapshot
{
    public string SessionId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string? PostType { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? AuthorId { get; set; }
    public string? SearchTerm { get; set; }
    public string? SortBy { get; set; }
    public int PageSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Guid> PostIds { get; set; } = new();
}
