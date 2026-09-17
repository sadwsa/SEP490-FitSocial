using FitSocial.Application.Commands.Posts;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Policies;

namespace FitSocial.Application.Validation;

public static class EditPostCommandValidator
{
    public static (bool IsValid, string? ErrorMessage) Validate(EditPostCommand command)
    {
        // 1. PostId
        if (command.PostId == Guid.Empty)
        {
            return (false, "PostId is required and cannot be empty.");
        }

        // 2. CurrentUserId
        if (command.CurrentUserId == Guid.Empty)
        {
            return (false, "User is not authenticated or UserId is invalid.");
        }

        // 3. PostType: không null, không empty, không whitespace
        if (string.IsNullOrWhiteSpace(command.PostType))
        {
            return (false, "PostType is required and cannot be empty or whitespace.");
        }

        if (!PostRolePolicy.TryParsePostType(command.PostType, out _))
        {
            return (false, $"Invalid PostType '{command.PostType}'. Allowed types: Normal, FindCoach, FindTrainee.");
        }

        // 4. Content: không null, không empty, không whitespace
        if (string.IsNullOrWhiteSpace(command.Content))
        {
            return (false, "Content is required and cannot be empty or whitespace.");
        }

        if (command.Content.Length > PostConstants.MaxContentLength)
        {
            return (false, $"Content must not exceed {PostConstants.MaxContentLength} characters.");
        }

        // 5. SportId & LocationId: không empty
        if (command.SportId == Guid.Empty)
        {
            return (false, "SportId is required and cannot be empty.");
        }

        if (command.LocationId == Guid.Empty)
        {
            return (false, "LocationId is required and cannot be empty.");
        }

        // 6. RemoveMediaIds: không chứa Guid.Empty
        if (command.RemoveMediaIds != null && command.RemoveMediaIds.Count > 0)
        {
            if (command.RemoveMediaIds.Any(id => id == Guid.Empty))
            {
                return (false, "RemoveMediaIds cannot contain empty GUID values.");
            }
        }

        // 7. NewMedia Items validation
        if (command.NewMedia != null && command.NewMedia.Count > 0)
        {
            for (int i = 0; i < command.NewMedia.Count; i++)
            {
                var item = command.NewMedia[i];
                if (item == null)
                {
                    return (false, $"NewMedia item at index {i} cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(item.MediaUrl))
                {
                    return (false, $"NewMedia item #{i + 1} is missing MediaUrl.");
                }

                if (string.IsNullOrWhiteSpace(item.MediaType))
                {
                    return (false, $"NewMedia item #{i + 1} is missing MediaType.");
                }

                var normalizedMediaType = item.MediaType.Trim().ToUpperInvariant();
                if (normalizedMediaType != PostConstants.MediaTypes.Image && normalizedMediaType != PostConstants.MediaTypes.Video)
                {
                    return (false, $"NewMedia item #{i + 1} has an invalid MediaType '{item.MediaType}'. Allowed: IMAGE, VIDEO.");
                }
            }
        }

        return (true, null);
    }
}
