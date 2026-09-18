using FitSocial.Domain.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Domain.Interfaces
{
    public interface ITrainingPackageRepository : IRepository<TrainingPackage>
    {
        Task<IEnumerable<TrainingPackage>> GetPackagesByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default);
    }
}
