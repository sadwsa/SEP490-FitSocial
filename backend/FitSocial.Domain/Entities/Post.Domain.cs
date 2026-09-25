using FitSocial.Domain.Constants;
using FitSocial.Domain.Enums;

namespace FitSocial.Domain.Entities;

public partial class Post
{
    /// <summary>
    /// Verifies if the user is the author/owner of this post and that the post is active.
    /// </summary>
    public bool CanBeEditedBy(Guid userId)
    {
        return AuthorId == userId && (IsDeleted == null || IsDeleted == false);
    }

    /// <summary>
    /// Updates the main attributes of the post.
    /// </summary>
    public void UpdateDetails(string content, PostType postType, Guid locationId)
    {
        Content = content.Trim();
        PostType = postType.ToString();
        LocationId = locationId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates that total media items (kept existing + new additions) do not exceed maximum limit.
    /// </summary>
    public static bool ValidateTotalMediaLimit(int currentCount, int removeCount, int newCount, out int totalAfterEdit)
    {
        totalAfterEdit = (currentCount - removeCount) + newCount;
        return totalAfterEdit <= PostConstants.MaxMediaCount;
    }

    /// <summary>
    /// Checks if a user has permission to delete this post.
    /// The author (owner) of the post can delete it, or users with Admin/Staff privileges.
    /// </summary>
    public bool CanBeDeletedBy(Guid userId, string userRole)
    {
        if (AuthorId == userId) return true;
        if (RoleConstants.IsAdminOrStaff(userRole)) return true;
        return false;
    }

    /// <summary>
    /// Soft deletes the post by marking IsDeleted as true and setting UpdatedAt timestamp.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
