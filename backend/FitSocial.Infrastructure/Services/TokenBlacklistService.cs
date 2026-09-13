using FitSocial.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
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

        public TokenBlacklistService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task RevokeTokenAsync(string jti, TimeSpan expiresIn)
        {
            var cacheKey = $"blacklist:{jti}";
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiresIn
            };

            await _cache.SetStringAsync(cacheKey, "revoked", options);
        }

        public async Task<bool> IsTokenRevokedAsync(string jti)
        {
            var cacheKey = $"blacklist:{jti}";
            var value = await _cache.GetStringAsync(cacheKey);

            return !string.IsNullOrEmpty(value);
        }
    }
}
