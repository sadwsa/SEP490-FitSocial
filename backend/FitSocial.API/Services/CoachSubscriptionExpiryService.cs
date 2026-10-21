using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.API.Services;

/// <summary>
/// Marks coach subscriptions as EXPIRED when EndDay has passed.
/// Does NOT lock the user account (expired coaches can still log in).
/// </summary>
public class CoachSubscriptionExpiryService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CoachSubscriptionExpiryService> _logger;

    public CoachSubscriptionExpiryService(IServiceProvider services, ILogger<CoachSubscriptionExpiryService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CoachSubscriptionExpiry check failed");
            }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task ExpireAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FitSocialDbContext>();
        var now = DateTime.UtcNow;

        var expired = await db.CoachUpgrades
            .Where(u => u.Status == "ACTIVE" && u.EndDay.HasValue && u.EndDay.Value < now)
            .ToListAsync(ct);

        if (expired.Count == 0) return;

        foreach (var u in expired)
        {
            u.Status = "EXPIRED";
        }
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Marked {Count} coach subscriptions as EXPIRED", expired.Count);
    }
}