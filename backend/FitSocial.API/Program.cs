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
                "Your login session has expired or is invalid. Please log in again."));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(FitSocial.Application.DTOs.Common.ApiResponseDto<object>.Fail(
                "You do not have permission to perform this action."));
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
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All);
    })
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
    c.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
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

// Ensure PostType column and Roles table exist in PostgreSQL database safely
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<FitSocial.Infrastructure.Data.FitSocialDbContext>();
        Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRaw(
            dbContext.Database,
            @"DO $$
            DECLARE
                v_csp_table text := NULL;
                v_price_table text := NULL;
                v_has_plans boolean := false;
                v_price_id UUID;
            BEGIN
                IF to_regclass('public.posts') IS NOT NULL THEN
                    ALTER TABLE posts ADD COLUMN IF NOT EXISTS posttype VARCHAR(50) DEFAULT 'FEED';
                    UPDATE posts SET posttype = 'FEED' WHERE posttype IS NULL;
                ELSIF to_regclass('public.""Posts""') IS NOT NULL THEN
                    ALTER TABLE ""Posts"" ADD COLUMN IF NOT EXISTS ""posttype"" VARCHAR(50) DEFAULT 'FEED';
                    UPDATE ""Posts"" SET ""posttype"" = 'FEED' WHERE ""posttype"" IS NULL;
                END IF;

                IF to_regclass('public.""Roles""') IS NOT NULL THEN
                    EXECUTE 'INSERT INTO ""Roles"" (""RoleCode"", ""RoleName"") VALUES
                        (''ADMIN'', ''Administrator''),
                        (''STAFF'', ''Staff''),
                        (''COACH'', ''Coach''),
                        (''TRAINEE'', ''Trainee'')
                        ON CONFLICT (""RoleCode"") DO NOTHING';
                ELSIF to_regclass('public.roles') IS NOT NULL THEN
                    EXECUTE 'INSERT INTO roles (rolecode, rolename) VALUES
                        (''ADMIN'', ''Administrator''),
                        (''STAFF'', ''Staff''),
                        (''COACH'', ''Coach''),
                        (''TRAINEE'', ''Trainee'')
                        ON CONFLICT (rolecode) DO NOTHING';
                ELSE
                    CREATE TABLE IF NOT EXISTS roles (
                        rolecode VARCHAR(8) PRIMARY KEY,
                        rolename VARCHAR(50) NOT NULL
                    );
                    INSERT INTO roles (rolecode, rolename) VALUES
                        ('ADMIN', 'Administrator'),
                        ('STAFF', 'Staff'),
                        ('COACH', 'Coach'),
                        ('TRAINEE', 'Trainee')
                        ON CONFLICT (rolecode) DO NOTHING;
                END IF;

                IF to_regclass('public.""TermsAndPolicies""') IS NOT NULL THEN
                    EXECUTE 'INSERT INTO ""TermsAndPolicies"" (""TermID"", ""Version"", ""Title"", ""Content"", ""EffectiveDate"")
                        SELECT gen_random_uuid(), ''v1.0'', ''FitSocial Terms of Service & Privacy Policy'',
                               ''Welcome to FitSocial sports platform. By accessing or using our services, you agree to be bound by these Terms of Service and Privacy Policy.'',
                               CURRENT_TIMESTAMP
                        WHERE NOT EXISTS (SELECT 1 FROM ""TermsAndPolicies"")';
                ELSIF to_regclass('public.termsandpolicies') IS NOT NULL THEN
                    EXECUTE 'INSERT INTO termsandpolicies (termid, version, title, content, effectivedate)
                        SELECT gen_random_uuid(), ''v1.0'', ''FitSocial Terms of Service & Privacy Policy'',
                               ''Welcome to FitSocial sports platform. By accessing or using our services, you agree to be bound by these Terms of Service and Privacy Policy.'',
                               CURRENT_TIMESTAMP
                        WHERE NOT EXISTS (SELECT 1 FROM termsandpolicies)';
                ELSE
                    CREATE TABLE IF NOT EXISTS termsandpolicies (
                        termid UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                        version VARCHAR(50) NOT NULL,
                        title VARCHAR(255) NOT NULL,
                        content TEXT NOT NULL,
                        effectivedate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        createdat TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );
                    INSERT INTO termsandpolicies (termid, version, title, content, effectivedate)
                    SELECT 
                        gen_random_uuid(),
                        'v1.0',
                        'FitSocial Terms of Service & Privacy Policy',
                        'Welcome to FitSocial sports platform. By accessing or using our services, you agree to be bound by these Terms of Service and Privacy Policy.',
                        CURRENT_TIMESTAMP
                    WHERE NOT EXISTS (SELECT 1 FROM termsandpolicies);
                END IF;

                IF to_regclass('public.""Price""') IS NOT NULL THEN
                    v_price_table := '""Price""';
                ELSIF to_regclass('public.price') IS NOT NULL THEN
                    v_price_table := 'price';
                END IF;

                IF to_regclass('public.""CoachSubscriptionPlans""') IS NOT NULL THEN
                    v_csp_table := '""CoachSubscriptionPlans""';
                ELSIF to_regclass('public.coachsubscriptionplans') IS NOT NULL THEN
                    v_csp_table := 'coachsubscriptionplans';
                END IF;

                IF v_csp_table IS NOT NULL AND v_price_table IS NOT NULL THEN
                    EXECUTE format('SELECT EXISTS (SELECT 1 FROM %s)', v_csp_table) INTO v_has_plans;
                    IF NOT v_has_plans THEN
                        v_price_id := gen_random_uuid();
                        IF v_price_table = '""Price""' THEN
                            EXECUTE 'INSERT INTO ""Price"" (""PriceID"", ""Amount"", ""CreatedAt"") VALUES ($1, 299000, CURRENT_TIMESTAMP) ON CONFLICT DO NOTHING' USING v_price_id;
                        ELSE
                            EXECUTE 'INSERT INTO price (priceid, amount, createdat) VALUES ($1, 299000, CURRENT_TIMESTAMP) ON CONFLICT DO NOTHING' USING v_price_id;
                        END IF;

                        IF v_csp_table = '""CoachSubscriptionPlans""' THEN
                            EXECUTE 'INSERT INTO ""CoachSubscriptionPlans"" (
                                ""CoachSubscriptionPlansID"", ""PriceID"", ""Amount"", ""Currency"",
                                ""Description"", ""SubscriptionDuration"", ""TrainingPackageDuration"",
                                ""IsActive"", ""CreatedAt""
                            ) VALUES (
                                gen_random_uuid(), $1, 299000, ''VND'',
                                ''Standard Coach Monthly Subscription Plan'', 30, 30,
                                TRUE, CURRENT_TIMESTAMP
                            )' USING v_price_id;
                        ELSE
                            EXECUTE 'INSERT INTO coachsubscriptionplans (
                                coachsubscriptionplansid, priceid, amount, currency,
                                description, subscriptionduration, trainingpackageduration,
                                isactive, createdat
                            ) VALUES (
                                gen_random_uuid(), $1, 299000, ''VND'',
                                ''Standard Coach Monthly Subscription Plan'', 30, 30,
                                TRUE, CURRENT_TIMESTAMP
                            )' USING v_price_id;
                        END IF;
                    END IF;
                END IF;
            END $$;"
        );
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DB Migration check: {ex.Message}");
    }
}

app.Run();
