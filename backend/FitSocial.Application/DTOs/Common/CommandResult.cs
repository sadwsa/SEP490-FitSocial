namespace FitSocial.Application.DTOs.Common;

public class CommandResult<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public int StatusCode { get; set; }

    public static CommandResult<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data, StatusCode = 200 };

    public static CommandResult<T> BadRequest(string message) =>
        new() { Success = false, Message = message, StatusCode = 400 };

    public static CommandResult<T> Unauthorized(string message) =>
        new() { Success = false, Message = message, StatusCode = 401 };

    public static CommandResult<T> Forbidden(string message) =>
        new() { Success = false, Message = message, StatusCode = 403 };

    public static CommandResult<T> NotFound(string message) =>
        new() { Success = false, Message = message, StatusCode = 404 };

    public static CommandResult<T> ServerError(string message) =>
        new() { Success = false, Message = message, StatusCode = 500 };

    public ApiResponseDto<T> ToApiResponse() =>
        new() { Success = Success, Message = Message, Data = Data };
}
