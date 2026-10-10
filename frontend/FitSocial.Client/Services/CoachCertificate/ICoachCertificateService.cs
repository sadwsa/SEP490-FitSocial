using System;
using System.Threading.Tasks;
using FitSocial.Client.Models.CoachCertificate;
using FitSocial.Client.Models.Common;

namespace FitSocial.Client.Services.CoachCertificate;

public interface ICoachCertificateService
{
    Task<ApiResponse<CoachCertificateListResponseModel>> GetCertificatesAsync(GetCoachCertificatesQueryModel query);
    Task<ApiResponse<CoachCertificateDetailModel>> GetCertificateByIdAsync(Guid certificateId);
    Task<ApiResponse<CoachCertificateDetailModel>> ApproveCertificateAsync(Guid certificateId);
    Task<ApiResponse<CoachCertificateDetailModel>> RejectCertificateAsync(Guid certificateId, RejectCoachCertificateModel request);
}
