using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IAppealRepository : IRepository<Appeal>
{
    Task<Appeal?> GetByIdWithDetailsAsync(Guid appealId, CancellationToken cancellationToken = default);
    Task<Appeal?> GetByReportIdAsync(Guid reportId, CancellationToken cancellationToken = default);
    Task<List<Appeal>> GetAppealsByAppealerIdAsync(Guid appealerId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveAppealForReportAsync(Guid reportId, CancellationToken cancellationToken = default);
    Task<(List<Appeal> Items, int TotalCount)> GetPagedAppealsAsync(
        string? status,
        string? searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
