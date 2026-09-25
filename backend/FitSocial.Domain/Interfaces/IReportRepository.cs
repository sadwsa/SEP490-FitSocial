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
        CancellationToken cancellationToken = default);
}
