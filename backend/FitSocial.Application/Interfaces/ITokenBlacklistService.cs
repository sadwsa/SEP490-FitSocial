using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FitSocial.Application.Interfaces
{
    public interface ITokenBlacklistService
    {
        Task RevokeTokenAsync(string jti, TimeSpan expiresIn);
        Task<bool> IsTokenRevokedAsync(string jti);
    }
}
