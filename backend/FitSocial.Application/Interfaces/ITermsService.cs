using FitSocial.Application.DTOs.Common;

namespace FitSocial.Application.Interfaces;

public interface ITermsService
{
    Task<TermsDto?> GetLatestAsync(CancellationToken cancellationToken = default);
}
