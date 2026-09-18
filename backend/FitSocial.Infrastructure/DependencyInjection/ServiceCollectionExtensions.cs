using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using FitSocial.Infrastructure.Repositories;
using FitSocial.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FitSocial.Infrastructure.DependencyInjection;

/// <summary>
/// Composition root: wires Application services + Infrastructure services in one place.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<ICoachService, CoachService>();
        services.AddScoped<ITrainingPackageService, TrainingPackageService>();
        return services;
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<FitSocialDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Configure Redis Distributed Cache
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            options.InstanceName = "FitSocial_";
        });

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<ITokenBlacklistService, TokenBlacklistService>();
        services.AddScoped<IPaymentGateway, PayOSGateway>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<INotificationService, NotificationService>();

        // Unit of Work + Repositories (data access layer for application services)
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOtpLogRepository, OtpLogRepository>();
        services.AddScoped<ISportRepository, SportRepository>();
        services.AddScoped<IPriceRepository, PriceRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ICoachProfileRepository, CoachProfileRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<ITrainingPackageRepository, TrainingPackageRepository>();

        return services;
    }
}
