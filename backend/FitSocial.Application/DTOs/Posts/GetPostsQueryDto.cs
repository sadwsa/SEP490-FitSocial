namespace FitSocial.Application.DTOs.Posts;

public class GetPostsQueryDto
{
    private const int MaxPageSize = 50;
    private int _pageSize = 10;
    private int _pageNumber = 1;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : (value < 1 ? 10 : value);
    }

    public string? PostType { get; set; }
    public Guid? SportId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? AuthorId { get; set; }
    public string? SearchTerm { get; set; }
}
