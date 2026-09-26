namespace FitSocial.Domain.Entities;

public partial class PostInteraction
{
    /// <summary>
    /// Factory method to create a new post Like interaction.
    /// </summary>
    public static PostInteraction Create(Guid postId, Guid userId)
    {
        return new PostInteraction
        {
            PostId = postId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
