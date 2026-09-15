using FitSocial.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FitSocial.Infrastructure.Services
{
    public class TokenBlacklistService : ITokenBlacklistService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<TokenBlacklistService> _logger;

        public TokenBlacklistService(IDistributedCache cache, ILogger<TokenBlacklistService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task RevokeTokenAsync(string jti, TimeSpan expiresIn)
        {
            var cacheKey = $"blacklist:{jti}";
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiresIn
            };

            try
            {
                await _cache.SetStringAsync(cacheKey, "revoked", options);
            }
            catch (Exception ex)
            {
                // Redis outage must not break logout; the access token simply keeps
                // its natural expiry. Logged for ops visibility.
                _logger.LogWarning(ex, "Could not write token blacklist entry for {Jti}.", jti);
            }
        }

        public async Task<bool> IsTokenRevokedAsync(string jti)
        {
            var cacheKey = $"blacklist:{jti}";
            try
            {
                var value = await _cache.GetStringAsync(cacheKey);
                return !string.IsNullOrEmpty(value);
            }
            catch (Exception ex)
            {
                // Fail-open: a Redis outage must not take down every authorized endpoint.
                // Tokens keep their JWT lifetime check; revocation resumes when Redis recovers.
                _logger.LogWarning(ex, "Could not read token blacklist entry for {Jti}. Assuming not revoked.", jti);
                return false;
            }
        }
    }
}
