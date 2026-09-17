namespace FitSocial.Application.Commands.Posts;

public record DeletePostCommand
{
    public Guid PostId { get; init; }
    public Guid CurrentUserId { get; init; }
    public string CurrentUserRole { get; init; } = string.Empty;
}
