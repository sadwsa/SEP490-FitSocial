using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.CoachCertificate;
using FitSocial.Application.DTOs.Common;

namespace FitSocial.Application.Interfaces;

public interface ICoachCertificateService
{
    Task<ApiResponseDto<CoachCertificateAdminListResponseDto>> GetCertificatesAsync(
        GetCoachCertificatesAdminQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<CoachCertificateAdminDetailDto>> GetCertificateByIdAsync(
        Guid certificateId,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<CoachCertificateAdminDetailDto>> ApproveCertificateAsync(
        Guid certificateId,
        Guid reviewerId,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<CoachCertificateAdminDetailDto>> RejectCertificateAsync(
        Guid certificateId,
        Guid reviewerId,
        RejectCoachCertificateDto request,
        CancellationToken cancellationToken = default);
}
