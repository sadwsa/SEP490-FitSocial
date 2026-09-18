using System.Net;
using System.Text.Json;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Exceptions;

namespace FitSocial.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate _next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        this._next = _next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, message) = ex switch
        {
            ValidationException validationEx => (StatusCodes.Status400BadRequest, validationEx.Message),
            BusinessException businessEx => (StatusCodes.Status400BadRequest, businessEx.Message),
            ForbiddenException forbiddenEx => (StatusCodes.Status403Forbidden, forbiddenEx.Message),
            NotFoundException notFoundEx => (StatusCodes.Status404NotFound, notFoundEx.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(ex, "An unhandled server error occurred on path {Path}: {Message}", context.Request.Path, ex.Message);
        }
        else
        {
            _logger.LogWarning("Business error occurred on path {Path} [{StatusCode}]: {Message}", context.Request.Path, statusCode, ex.Message);
        }

        if (!context.Response.HasStarted)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var response = ApiResponseDto<object>.Fail(message);
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
