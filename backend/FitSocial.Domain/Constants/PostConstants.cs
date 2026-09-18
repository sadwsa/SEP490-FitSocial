namespace FitSocial.Domain.Constants;

public static class PostConstants
{
    public const int MaxMediaCount = 10;
    public const int MaxContentLength = 5000;

    public static class PostTypes
    {
        public const string General = "GENERAL";
        public const string Workout = "WORKOUT";
        public const string FindBuddy = "FIND_BUDDY";
        public const string Event = "EVENT";
    }

    public static class MediaTypes
    {
        public const string Image = "IMAGE";
        public const string Video = "VIDEO";
    }
}
