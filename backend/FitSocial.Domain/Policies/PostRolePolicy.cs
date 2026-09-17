using FitSocial.Domain.Constants;
using FitSocial.Domain.Enums;

namespace FitSocial.Domain.Policies;

public static class PostRolePolicy
{
    /// <summary>
    /// Checks if a user role is permitted to select a given PostType.
    /// Trainee: Normal, FindCoach.
    /// Coach: Normal, FindCoach, FindTrainee.
    /// </summary>
    public static bool IsPostTypeAllowedForRole(string? roleCode, PostType postType)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
        {
            return false;
        }

        var normalizedRole = roleCode.Trim().ToUpperInvariant();

        return normalizedRole switch
        {
            RoleConstants.Trainee => postType is PostType.Normal or PostType.FindCoach,
            RoleConstants.Coach   => postType is PostType.Normal or PostType.FindCoach or PostType.FindTrainee,
            RoleConstants.Admin or RoleConstants.Staff => true,
            _ => false
        };
    }

    /// <summary>
    /// Parses case-insensitive string into PostType enum.
    /// </summary>
    public static bool TryParsePostType(string? postTypeStr, out PostType postType)
    {
        postType = default;
        if (string.IsNullOrWhiteSpace(postTypeStr))
        {
            return false;
        }

        return Enum.TryParse(postTypeStr.Trim(), ignoreCase: true, out postType) 
               && Enum.IsDefined(typeof(PostType), postType);
    }
}
