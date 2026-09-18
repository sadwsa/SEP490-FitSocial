using FitSocial.Application.Interfaces;
using FitSocial.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using FitSocial.Domain.Interfaces;
using FitSocial.API.Hubs;
using FitSocial.API.Services;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Configure maximum request body size (100MB for video/media uploads)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
});

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5112",
                "https://localhost:7012",
                "http://localhost:5000",
                "https://localhost:5001"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(24));
    });
});

// Configure Rate Limiting against OTP spam
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("OtpPolicy", opt =>
    {
        opt.PermitLimit = 5; // Max requests
        opt.Window = TimeSpan.FromMinutes(1); // Time window
        opt.QueueLimit = 0;
    });

    // Custom message returned when blocked for spamming
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "You have sent too many requests. Please try again later.",
            data = false
        });
    };
});

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();
builder.Services.AddScoped<IChatRealtimeNotifier, ChatRealtimeNotifier>();

// Cloudinary Singleton service registration (reuse HttpClient / SocketsHttpHandler connection pool)
var cloudName = builder.Configuration["Cloudinary:CloudName"];
var apiKey = builder.Configuration["Cloudinary:ApiKey"];
var apiSecret = builder.Configuration["Cloudinary:ApiSecret"];
if (!string.IsNullOrWhiteSpace(cloudName) && !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret))
{
    var account = new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret);
    builder.Services.AddSingleton(new CloudinaryDotNet.Cloudinary(account));
}

// JWT Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "FitSocial_Super_Secret_Key_For_Jwt_2026_SecureAuthenticationKey_1234567890";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "FitSocial.API",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "FitSocial.Client",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
    };

    // Multi-layer check filter (Blacklist Jti & TokenVersion) for every request
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(FitSocial.Application.DTOs.Common.ApiResponseDto<object>.Fail(
                "Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Vui lòng đăng nhập lại."));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(FitSocial.Application.DTOs.Common.ApiResponseDto<object>.Fail(
                "Bạn không có quyền thực hiện thao tác này."));
        },
        OnTokenValidated = async context =>
        {
            var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var blacklistService = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklistService>();

            // 1. Get Jti and TokenVersion from the Access Token claims
            var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var userIdStr = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tokenVersionStr = context.Principal?.FindFirst("token_version")?.Value;

            // Check Layer 1: whether the token is in the Redis blacklist (logged out)
            if (!string.IsNullOrEmpty(jti) && await blacklistService.IsTokenRevokedAsync(jti))
            {
                context.Fail("Token has been revoked (signed out).");
                return;
            }

            // Check Layer 2: compare TokenVersion with the database (password change / locked account)
            if (Guid.TryParse(userIdStr, out var userId) && int.TryParse(tokenVersionStr, out var tokenVersion))
            {
                var user = await userRepository.GetByIdAsync(userId);
                if (user == null || user.IsLocked == true || user.TokenVersion != tokenVersion)
                {
                    context.Fail("Account has been locked, password changed, or the session is no longer valid.");
                }
            }
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Tắt auto-400 ProblemDetails của [ApiController] để mọi lỗi validation
        // đều trả về đúng dạng ApiResponseDto mà frontend có thể đọc được.
        options.SuppressModelStateInvalidFilter = true;
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FitSocial.API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<FitSocial.API.Middleware.ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors("AllowBlazorClient");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

// Ensure PostType column exists in PostgreSQL database
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<FitSocial.Infrastructure.Data.FitSocialDbContext>();
        Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRaw(
            dbContext.Database,
            @"ALTER TABLE ""Posts"" ADD COLUMN IF NOT EXISTS ""PostType"" VARCHAR(50) DEFAULT 'FEED';
              UPDATE ""Posts"" SET ""PostType"" = 'FEED' WHERE ""PostType"" IS NULL;"
        );
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DB Migration check: {ex.Message}");
    }
}

app.Run();
