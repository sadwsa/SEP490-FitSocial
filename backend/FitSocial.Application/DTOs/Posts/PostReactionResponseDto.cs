namespace FitSocial.Application.DTOs.Posts;

public class PostReactionResponseDto
{
    public Guid PostId { get; set; }
    public bool IsLiked { get; set; }
    public int LikeCount { get; set; }
}
