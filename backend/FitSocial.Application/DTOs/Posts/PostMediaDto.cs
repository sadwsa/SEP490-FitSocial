namespace FitSocial.Application.DTOs.Posts;

public class PostMediaDto
{
    public Guid Id { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}
