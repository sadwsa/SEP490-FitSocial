using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Guards that the Staff/Admin management features really receive the shared
/// IAuditLogService at runtime. A missing registration would silently disable
/// every audit entry written by these services, so it must fail the build.
/// </summary>
public class AuditLogInjectionTests
{
    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=fitsocial;Username=postgres;Password=postgres",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["Encryption:Key"] = new string('0', 64)
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddHttpClient();
        services.AddScoped<IChatRealtimeNotifier, NoopChatRealtimeNotifier>();
        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    /// <summary>Stands in for the SignalR-backed notifier registered in Program.cs.</summary>
    private sealed class NoopChatRealtimeNotifier : IChatRealtimeNotifier
    {
        public Task BroadcastMessageAsync(List<Guid> participantUserIds, FitSocial.Application.DTOs.Conversations.MessageDto message)
            => Task.CompletedTask;

        public Task SendNotificationAsync(Guid recipientUserId, FitSocial.Application.DTOs.Notifications.NotificationDto notification)
            => Task.CompletedTask;
    }

    [Theory]
    [InlineData(typeof(IAuthService), "_auditLogService")]
    [InlineData(typeof(ICoachSubscriptionPlanService), "_auditLogService")]
    [InlineData(typeof(IPaymentGatewayConfigService), "_auditLogService")]
    public void ManagementServices_ShouldResolveWithAuditLogServiceInjected(Type serviceType, string fieldName)
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService(serviceType);
        var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

        var field = service!.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.Same(auditLogService, field!.GetValue(service));
    }

    [Fact]
    public void PayoutService_ShouldResolveWithCurrentUserContextInjected()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IPayoutService>();

        var field = typeof(PayoutService).GetField(
            "_currentUserContext", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.NotNull(field!.GetValue(service));
    }
}