namespace FitSocial.Domain.Constants;

public static class OtpConstants
{
    public const string PurposeRegisterTrainee = "REGISTER_TRAINEE";
    public const string PurposeRegisterCoach = "REGISTER_COACH";
    public const string PurposeResetPassword = "RESET_PASSWORD";

    public const int ExpiryMinutes = 5;
    public const int MaxAttempts = 5;
}
