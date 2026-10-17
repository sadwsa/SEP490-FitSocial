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
            ConflictException conflictEx => (StatusCodes.Status409Conflict, conflictEx.Message),
            ForbiddenException forbiddenEx => (StatusCodes.Status403Forbidden, forbiddenEx.Message),
            NotFoundException notFoundEx => (StatusCodes.Status404NotFound, notFoundEx.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(ex, "An unhandled server error occurred on path {Path}: {Message}", context.Request.Path, ex.Message);
            try
            {
                var auditLogs = context.RequestServices.GetService<FitSocial.Domain.Interfaces.IAuditLogRepository>();
                var uow = context.RequestServices.GetService<FitSocial.Domain.Interfaces.IUnitOfWork>();
                if (auditLogs != null && uow != null)
                {
                    var userIdClaim = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    Guid.TryParse(userIdClaim, out var actorId);

                    var auditLog = new FitSocial.Domain.Entities.AuditLog
                    {
                        ActorAccountId = actorId != Guid.Empty ? actorId : null,
                        Action = "SYSTEM_ERROR",
                        EntityType = "System",
                        EntityId = context.Request.Path.Value,
                        Ipaddress = context.Connection.RemoteIpAddress?.ToString(),
                        NewValue = JsonSerializer.Serialize(new
                        {
                            path = context.Request.Path.Value,
                            method = context.Request.Method,
                            statusCode = statusCode,
                            errorMessage = ex.Message,
                            exceptionType = ex.GetType().Name
                        }),
                        CreatedAt = DateTime.UtcNow
                    };
                    await auditLogs.AddAsync(auditLog);
                    await uow.SaveChangesAsync();
                }
            }
            catch
            {
                // Fail-safe: ensure exception handling never crashes if log writing fails
            }
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
