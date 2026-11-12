using FitSocial.Domain.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Domain.Interfaces
{
    public interface ITrainingPackageRepository : IRepository<TrainingPackage>
    {
        Task<IEnumerable<TrainingPackage>> GetPackagesByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default);
        Task<IEnumerable<TrainingPackage>> GetAllActivePackagesAsync(string? searchKeyword = null, decimal? maxPrice = null, Guid? coachId = null, CancellationToken cancellationToken = default);
        Task<List<TrainingPackage>> GetPurchasedPackagesAsync(Guid traineeId, CancellationToken cancellationToken = default);
        Task<(List<OrderDetail> Items, int TotalCount)> GetPurchasedOrderDetailsAsync(Guid traineeId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    }
}
