using FitSocial.Application;
using FitSocial.Application.Interfaces;
using FitSocial.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

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
            .AllowCredentials();
    });
});

// Cấu hình Rate Limiting chống spam OTP
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("OtpPolicy", opt =>
    {
        opt.PermitLimit = 5; // Số request tối đa
        opt.Window = TimeSpan.FromMinutes(1); // Khoảng thời gian
        opt.QueueLimit = 0;
    });

    // Tùy chọn message trả về khi bị chặn do spam
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "Bạn đã gửi yêu cầu quá nhiều lần. Vui lòng thử lại sau.",
            data = false
        });
    };
});

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

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

    // Thêm bộ lọc kiểm tra đa lớp (Blacklist Jti & TokenVersion) cho mọi request
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var dbContext = context.HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();
            var blacklistService = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklistService>();

            // 1. Lấy Jti và TokenVersion từ Claims của Access Token
            var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var userIdStr = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tokenVersionStr = context.Principal?.FindFirst("token_version")?.Value;

            // Kiểm tra Lớp 1: Token có nằm trong danh sách đen Redis không (Đăng xuất)
            if (!string.IsNullOrEmpty(jti) && await blacklistService.IsTokenRevokedAsync(jti))
            {
                context.Fail("Token đã bị thu hồi (Đăng xuất).");
                return;
            }

            // Kiểm tra Lớp 2: Đối chiếu TokenVersion với Database (Đổi mật khẩu / Khóa tài khoản)
            if (Guid.TryParse(userIdStr, out var userId) && int.TryParse(tokenVersionStr, out var tokenVersion))
            {
                var user = await dbContext.Users.FindAsync(userId);
                if (user == null || user.IsLocked == true || user.TokenVersion != tokenVersion)
                {
                    context.Fail("Tài khoản đã bị khóa, đổi mật khẩu hoặc phiên làm việc không còn hiệu lực.");
                }
            }
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddControllers();
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

app.UseHttpsRedirection();

app.UseCors("AllowBlazorClient");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.Run();
