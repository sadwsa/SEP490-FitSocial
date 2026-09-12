using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FitSocial.Infrastructure.Services;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAt) GenerateToken(User user)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "FitSocial_Secret_Super_Secure_Key_2026_KeyForAuth_987654321";
        var issuer = _configuration["Jwt:Issuer"] ?? "FitSocial.API";
        var audience = _configuration["Jwt:Audience"] ?? "FitSocial.Client";
        var expiryMinutesStr = _configuration["Jwt:ExpiryMinutes"] ?? "1440"; // 1 day default

        if (!int.TryParse(expiryMinutesStr, out var expiryMinutes))
        {
            expiryMinutes = 1440;
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName ?? user.Email),
            new(ClaimTypes.Role, user.RoleCode ?? "TRAINEE"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("token_version", user.TokenVersion.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return (tokenHandler.WriteToken(token), expiresAt);
    }
}
