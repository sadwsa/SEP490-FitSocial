using FitSocial.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Domain.Interfaces
{
    public interface ICartRepository : IRepository<Cart>
    {
        Task<List<Cart>> GetCartByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<Cart?> GetCartItemAsync(Guid userId, Guid packageId, CancellationToken cancellationToken = default);
        Task<int> GetCartCountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task ClearCartAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
