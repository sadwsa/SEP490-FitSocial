namespace FitSocial.Domain.Constants;

public static class RoleConstants
{
    public const string Trainee = "TRAINEE";
    public const string Coach = "COACH";
    public const string Staff = "STAFF";
    public const string Admin = "ADMIN";

    public static bool IsAdminOrStaff(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim().ToUpperInvariant();
        return r == Admin || r == Staff;
    }
}
