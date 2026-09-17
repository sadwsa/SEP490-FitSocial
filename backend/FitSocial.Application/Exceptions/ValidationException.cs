namespace FitSocial.Application.Exceptions;

public class ValidationException : AppException
{
    public ValidationException(string message) : base(message)
    {
    }
}
