using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;

namespace FitSocial.Application.Interfaces;

public interface IPostReportService
{
    Task<ApiResponseDto<PostReportResponseDto>> ReportPostAsync(
        Guid postId,
        Guid reporterId,
        CreatePostReportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<bool>> HasUserReportedPostAsync(
        Guid postId,
        Guid reporterId,
        CancellationToken cancellationToken = default);
}
