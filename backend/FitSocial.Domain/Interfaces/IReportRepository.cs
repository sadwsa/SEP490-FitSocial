using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IReportRepository : IRepository<Report>
{
    Task<(List<Report> Items, int TotalCount)> GetPagedReportsAsync(
        string? status,
        string? targetType,
        Guid? reporterId,
        DateTime? fromDate,
        DateTime? toDate,
        string? searchTerm,
        int pageNumber,
        int pageSize,
        string? type = null,
        string? appealStatus = null,
        CancellationToken cancellationToken = default);

    Task<Report?> GetReportWithDetailsByIdAsync(Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-23.1 TH1: counts unresolved reports filed against a user (coach).
    /// </summary>
    Task<int> CountUnresolvedAgainstUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
