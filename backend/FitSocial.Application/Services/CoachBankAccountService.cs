using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class CoachBankAccountService : ICoachBankAccountService
{
    private readonly ICoachBankAccountRepository _bankRepository;
    private readonly ICoachProfileRepository _coachRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly IUnitOfWork _unitOfWork;

    public CoachBankAccountService(
        ICoachBankAccountRepository bankRepository,
        ICoachProfileRepository coachRepository,
        IEncryptionService encryptionService,
        IUnitOfWork unitOfWork)
    {
        _bankRepository = bankRepository;
        _coachRepository = coachRepository;
        _encryptionService = encryptionService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponseDto<List<CoachBankAccountDto>>> GetAccountsAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        var accounts = await _bankRepository.ListByCoachIdAsync(coachId, cancellationToken);
        var dtos = accounts.Select(MapToDto).ToList();
        return ApiResponseDto<List<CoachBankAccountDto>>.Ok(dtos, "Bank accounts retrieved successfully.");
    }

    public async Task<ApiResponseDto<CoachBankAccountDto>> AddAccountAsync(Guid coachId, CreateCoachBankAccountDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.AccountNumber))
            throw new ValidationException("Account number is required.");

        var coach = await _coachRepository.GetByIdAsync(coachId, cancellationToken);
        if (coach == null)
            throw new NotFoundException("Coach profile not found.");

        // Kiểm tra xem Coach đã có tài khoản chưa
        var existingAccounts = await _bankRepository.ListByCoachIdAsync(coachId, cancellationToken);
        var existingAccount = existingAccounts.FirstOrDefault();

        var now = DateTime.UtcNow;

        if (existingAccount != null)
        {
            // UPDATE: Đã có tài khoản thì cập nhật thông tin mới
            existingAccount.BankName = dto.BankName.Trim();
            existingAccount.BankCode = dto.BankCode.Trim().ToUpperInvariant();
            existingAccount.AccountName = dto.AccountName.Trim().ToUpperInvariant();
            existingAccount.EncryptedAccountNumber = _encryptionService.Encrypt(dto.AccountNumber.Trim());
            existingAccount.Branch = string.IsNullOrWhiteSpace(dto.Branch) ? null : dto.Branch.Trim();
            existingAccount.UpdatedAt = now;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponseDto<CoachBankAccountDto>.Ok(MapToDto(existingAccount), "Bank account updated successfully.");
        }
        else
        {
            // INSERT: Chưa có thì tạo mới
            var newAccount = new CoachBankAccount
            {
                BankId = Guid.NewGuid(),
                CoachId = coachId,
                BankName = dto.BankName.Trim(),
                BankCode = dto.BankCode.Trim().ToUpperInvariant(),
                AccountName = dto.AccountName.Trim().ToUpperInvariant(),
                EncryptedAccountNumber = _encryptionService.Encrypt(dto.AccountNumber.Trim()),
                Branch = string.IsNullOrWhiteSpace(dto.Branch) ? null : dto.Branch.Trim(),
                IsDefault = true, // Mặc định luôn là true vì chỉ có 1 tài khoản
                CreatedAt = now,
                UpdatedAt = now
            };

            await _bankRepository.AddAsync(newAccount, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponseDto<CoachBankAccountDto>.Ok(MapToDto(newAccount), "Bank account added successfully.");
        }
    }


    public async Task<ApiResponseDto<bool>> SetDefaultAsync(Guid coachId, Guid bankId, CancellationToken cancellationToken = default)
    {
        var accounts = await _bankRepository.ListByCoachIdAsync(coachId, cancellationToken);
        var targetAccount = accounts.FirstOrDefault(a => a.BankId == bankId);
        if (targetAccount == null)
        {
            throw new NotFoundException("Bank account not found.");
        }

        foreach (var acc in accounts)
        {
            acc.IsDefault = (acc.BankId == bankId);
            acc.UpdatedAt = DateTime.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponseDto<bool>.Ok(true, "Default bank account updated.");
    }

    public async Task<ApiResponseDto<bool>> DeleteAccountAsync(Guid coachId, Guid bankId, CancellationToken cancellationToken = default)
    {
        var accounts = await _bankRepository.ListByCoachIdAsync(coachId, cancellationToken);
        var targetAccount = accounts.FirstOrDefault(a => a.BankId == bankId);
        if (targetAccount == null)
        {
            throw new NotFoundException("Bank account not found.");
        }

        _bankRepository.Remove(targetAccount);

        // If deleted account was default and other accounts exist, make the first one default
        if (targetAccount.IsDefault == true)
        {
            var nextDefault = accounts.FirstOrDefault(a => a.BankId != bankId);
            if (nextDefault != null)
            {
                nextDefault.IsDefault = true;
                nextDefault.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponseDto<bool>.Ok(true, "Bank account deleted successfully.");
    }

    private CoachBankAccountDto MapToDto(CoachBankAccount b)
    {
        var plainAccountNumber = _encryptionService.Decrypt(b.EncryptedAccountNumber) ?? string.Empty;
        var masked = MaskAccountNumber(plainAccountNumber);

        return new CoachBankAccountDto
        {
            BankId = b.BankId,
            CoachId = b.CoachId,
            BankName = b.BankName,
            BankCode = b.BankCode,
            AccountName = b.AccountName,
            AccountNumber = plainAccountNumber,
            MaskedAccountNumber = masked,
            Branch = b.Branch,
            IsDefault = b.IsDefault,
            CreatedAt = b.CreatedAt,
            UpdatedAt = b.UpdatedAt
        };
    }

    private static string MaskAccountNumber(string number)
    {
        if (string.IsNullOrEmpty(number)) return string.Empty;
        if (number.Length <= 4) return new string('*', number.Length);
        return new string('*', number.Length - 4) + number[^4..];
    }
}
