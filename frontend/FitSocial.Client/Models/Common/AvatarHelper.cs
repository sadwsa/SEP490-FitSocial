using System;

namespace FitSocial.Client.Models.Common;

/// <summary>
/// Helper for avatar URL validation and fallback initials across all components.
/// </summary>
public static class AvatarHelper
{
    public static bool HasValidAvatarUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var trimmed = url.Trim();
        if (trimmed.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uriResult))
        {
            return uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps;
        }

        return trimmed.StartsWith('/') && !trimmed.StartsWith("//");
    }

    public static string GetInitial(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "U";
        var trimmed = name.Trim();
        return trimmed.Length > 0 ? char.ToUpperInvariant(trimmed[0]).ToString() : "U";
    }

    public static string GetInitials(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "U";
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "U";
        if (parts.Length == 1)
        {
            return parts[0].Length >= 2
                ? parts[0].Substring(0, 2).ToUpperInvariant()
                : parts[0].ToUpperInvariant();
        }
        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }
}
