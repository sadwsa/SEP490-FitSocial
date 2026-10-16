namespace FitSocial.Domain.Constants;

public static class PackageTypeConstants
{
    public const string Both = "BOTH";
    public const string Workout = "WORKOUT";
    public const string Meal = "MEAL";

    public static bool IsValid(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return false;
        var upper = type.Trim().ToUpperInvariant();
        return upper is Both or Workout or Meal;
    }

    public static string Normalize(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return Both;
        var upper = type.Trim().ToUpperInvariant();
        return upper switch
        {
            Workout => Workout,
            Meal => Meal,
            _ => Both
        };
    }
}
