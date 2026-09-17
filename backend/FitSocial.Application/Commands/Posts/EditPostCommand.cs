using FitSocial.Application.DTOs.Posts;

namespace FitSocial.Application.Commands.Posts;

public class EditPostCommand
{
    public Guid PostId { get; set; }
    public Guid CurrentUserId { get; set; }
    public string CurrentUserRole { get; set; } = string.Empty;
    public string PostType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid SportId { get; set; }
    public Guid LocationId { get; set; }
    public List<Guid> RemoveMediaIds { get; set; } = new();
    public List<CreatePostMediaDto> NewMedia { get; set; } = new();
}
