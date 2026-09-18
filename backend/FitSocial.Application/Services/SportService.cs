using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Sports;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class SportService : ISportService
{
    private readonly ISportRepository _sportRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SportService(ISportRepository sportRepository, IUnitOfWork unitOfWork)
    {
        _sportRepository = sportRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponseDto<List<SportDto>>> GetSportListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var sports = await _sportRepository.ListAllWithCountsAsync(cancellationToken);

            var dtos = sports.Select(s => MapToDto(s)).ToList();

            return ApiResponseDto<List<SportDto>>.Ok(dtos, "Lấy danh sách môn thể thao thành công.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<List<SportDto>>.Fail($"Lỗi khi tải danh sách môn thể thao: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<SportDto>> CreateSportAsync(CreateSportDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.SportName))
        {
            return ApiResponseDto<SportDto>.Fail("Tên môn thể thao không được để trống.");
        }

        var trimmedName = dto.SportName.Trim();
        if (trimmedName.Length > 100)
        {
            return ApiResponseDto<SportDto>.Fail("Tên môn thể thao không được vượt quá 100 ký tự.");
        }

        try
        {
            var existingSport = await _sportRepository.GetByNameAsync(trimmedName, cancellationToken);
            if (existingSport != null)
            {
                return ApiResponseDto<SportDto>.Fail($"Môn thể thao '{trimmedName}' đã tồn tại trong hệ thống.");
            }

            var newSport = new Sport
            {
                SportId = Guid.NewGuid(),
                SportName = trimmedName
            };

            await _sportRepository.AddAsync(newSport, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var resultDto = MapToDto(newSport);
            return ApiResponseDto<SportDto>.Ok(resultDto, "Tạo môn thể thao mới thành công.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<SportDto>.Fail($"Lỗi khi tạo môn thể thao: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<SportDto>> UpdateSportAsync(Guid sportId, UpdateSportDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.SportName))
        {
            return ApiResponseDto<SportDto>.Fail("Tên môn thể thao không được để trống.");
        }

        var trimmedName = dto.SportName.Trim();
        if (trimmedName.Length > 100)
        {
            return ApiResponseDto<SportDto>.Fail("Tên môn thể thao không được vượt quá 100 ký tự.");
        }

        try
        {
            var sport = await _sportRepository.GetByIdAsync(sportId, cancellationToken);
            if (sport == null)
            {
                return ApiResponseDto<SportDto>.Fail("Sport không tồn tại.");
            }

            var duplicate = await _sportRepository.GetByNameExcludingIdAsync(trimmedName, sportId, cancellationToken);
            if (duplicate != null)
            {
                return ApiResponseDto<SportDto>.Fail("Môn thể thao này đã tồn tại.");
            }

            sport.SportName = trimmedName;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var resultDto = MapToDto(sport);
            return ApiResponseDto<SportDto>.Ok(resultDto, "Cập nhật môn thể thao thành công.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<SportDto>.Fail($"Lỗi khi cập nhật môn thể thao: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> DeleteSportAsync(Guid sportId, CancellationToken cancellationToken = default)
    {
        try
        {
            var sport = await _sportRepository.GetByIdWithDetailsAsync(sportId, cancellationToken);
            if (sport == null)
            {
                return ApiResponseDto<bool>.Fail("Sport không tồn tại.");
            }

            // Check relationships before deleting
            if ((sport.Coaches != null && sport.Coaches.Count > 0) ||
                (sport.Users != null && sport.Users.Count > 0) ||
                (sport.Posts != null && sport.Posts.Count > 0))
            {
                return ApiResponseDto<bool>.Fail("Không thể xóa môn thể thao vì môn thể thao này đang được sử dụng.");
            }

            _sportRepository.Remove(sport);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponseDto<bool>.Ok(true, "Xóa môn thể thao thành công.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<bool>.Fail($"Lỗi khi xóa môn thể thao: {ex.Message}");
        }
    }

    private static SportDto MapToDto(Sport s)
    {
        return new SportDto
        {
            SportId = s.SportId,
            SportName = s.SportName,
            Category = DetermineCategory(s.SportName),
            Description = $"Môn thể thao {s.SportName} thuộc hệ thống FitSocial.",
            Status = "Đang hoạt động",
            CoachCount = s.Coaches?.Count ?? 0,
            UserCount = s.Users?.Count ?? 0,
            OpenClassCount = s.Posts?.Count ?? 0
        };
    }

    private static string DetermineCategory(string sportName)
    {
        if (string.IsNullOrWhiteSpace(sportName)) return "Thể thao tổng hợp";

        var name = sportName.ToLowerInvariant();
        if (name.Contains("chạy") || name.Contains("đạp") || name.Contains("marathon") || name.Contains("cardio"))
            return "Cardio & Sức bền";
        if (name.Contains("crossfit") || name.Contains("gym") || name.Contains("tạ") || name.Contains("thể hình"))
            return "Thể lực & Gym";
        if (name.Contains("boxing") || name.Contains("võ") || name.Contains("quyền") || name.Contains("karate") || name.Contains("judo"))
            return "Đối kháng & Võ thuật";
        if (name.Contains("yoga") || name.Contains("pilates") || name.Contains("dưỡng sinh") || name.Contains("phục hồi"))
            return "Thư giãn & Dưỡng sinh";
        if (name.Contains("bơi") || name.Contains("lặn") || name.Contains("chèo"))
            return "Sức bền & Kỹ thuật";
        if (name.Contains("bắn cung") || name.Contains("cầu lông") || name.Contains("tennis") || name.Contains("bóng đá") || name.Contains("bóng rổ"))
            return "Tập trung & Chính xác";

        return "Thể thao tổng hợp";
    }
}
