using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.API.Services;

/// <summary>
/// Nghị định 13/2023: eKYC bị từ chối/hủy -> xóa vĩnh viễn DB + Cloudinary sau 30 ngày.
/// Active coaches: giữ suốt thời gian hoạt động (+1-5 năm kế toán, xử lý thủ công).
/// </summary>
public class EkycRetentionService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<EkycRetentionService> _logger;

    public EkycRetentionService(IServiceProvider services, ILogger<EkycRetentionService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay first run 1h to avoid startup load, then daily
        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeExpiredAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EkycRetention purge failed");
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task PurgeExpiredAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FitSocialDbContext>();
        var cloudinary = scope.ServiceProvider.GetService<Cloudinary>();
        var cutoff = DateTime.UtcNow.AddDays(-30);

        var expired = await db.CoachEkycVerifications
            .Where(e => (e.VerificationStatus == "Failed" || e.VerificationStatus == "REJECTED" || e.VerificationStatus == "Failed")
                && e.UpdatedAt < cutoff)
            .Take(100)
            .ToListAsync(ct);

        if (expired.Count == 0) return;

        foreach (var e in expired)
        {
            try
            {
                // Delete Cloudinary assets best-effort (extract publicId from URL)
                if (cloudinary != null)
                {
                    foreach (var url in new[] { e.FrontCardUrl, e.BackCardUrl, e.FaceImageUrl })
                    {
                        var publicId = ExtractPublicId(url);
                        if (!string.IsNullOrWhiteSpace(publicId))
                        {
                            try { await cloudinary.DestroyAsync(new DeletionParams(publicId) { ResourceType = ResourceType.Image, Type = "authenticated" }); } catch { }
                        }
                    }
                }
                db.CoachEkycVerifications.Remove(e);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to purge eKYC {EkycId}", e.EkycId);
            }
        }
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("EkycRetention purged {Count} expired records", expired.Count);
    }

    private static string? ExtractPublicId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            // https://res.cloudinary.com/<cloud>/image/upload/v123/fitsocial/credentials/xxx.jpg -> fitsocial/credentials/xxx
            var uri = new Uri(url);
            var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var uploadIdx = Array.IndexOf(parts, "upload");
            if (uploadIdx < 0 || uploadIdx + 1 >= parts.Length) return null;
            var after = parts.Skip(uploadIdx + 1).ToList();
            // Skip version segment v123456
            if (after.Count > 0 && after[0].StartsWith("v") && long.TryParse(after[0].Substring(1), out _))
                after = after.Skip(1).ToList();
            var joined = string.Join("/", after);
            var dot = joined.LastIndexOf('.');
            if (dot > 0) joined = joined.Substring(0, dot);
            return joined;
        }
        catch { return null; }
    }
}
