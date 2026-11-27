namespace FitSocial.Application.Exceptions;

public class SessionExpiredException : Exception
{
    public SessionExpiredException(string message = "Feed session has expired. Please refresh your feed.") : base(message)
    {
    }
}
