using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FitSocial.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
