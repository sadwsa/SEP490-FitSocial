using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Application.Services;

public class CoachBankAccountService : ICoachBankAccountService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> UserBankAccountLocks = new();

    private readonly ICoachBankAccountRepository _bankRepository;
    private readonly ICoachProfileRepository _coachRepository;
    private readonly IUserRepository? _userRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration? _configuration;

    public CoachBankAccountService(
        ICoachBankAccountRepository bankRepository,
        ICoachProfileRepository coachRepository,
        IEncryptionService encryptionService,
        IUnitOfWork unitOfWork,
        IHttpClientFactory httpClientFactory,
        IConfiguration? configuration = null,
        IUserRepository? userRepository = null)
    {
        _bankRepository = bankRepository;
        _coachRepository = coachRepository;
        _encryptionService = encryptionService;
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _userRepository = userRepository;
    }

    public async Task<ApiResponseDto<List<CoachBankAccountDto>>> GetAccountsAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        var accounts = await _bankRepository.ListByCoachIdAsync(coachId, cancellationToken);
        var dtos = accounts.Select(MapToDto).ToList();
        return ApiResponseDto<List<CoachBankAccountDto>>.Ok(dtos, "Bank accounts retrieved successfully.");
    }

    public async Task<ApiResponseDto<CoachBankAccountDto>> AddAccountAsync(Guid coachId, CreateCoachBankAccountDto dto, CancellationToken cancellationToken = default)
    {
        var (cleanAccountNumber, cleanBankName, cleanBankCode, cleanAccountName, cleanBranch) =
            ValidateAndSanitize(dto?.AccountNumber, dto?.BankName, dto?.BankCode, dto?.AccountName, dto?.Branch);

        if (_userRepository != null)
        {
            var user = await _userRepository.GetByIdAsync(coachId, cancellationToken);
            if (user == null)
            {
                throw new NotFoundException("User not found.");
            }
        }
        else
        {
            var coach = await _coachRepository.GetByIdAsync(coachId, cancellationToken);
            if (coach == null)
            {
                throw new NotFoundException("Coach profile not found.");
            }
        }

        var userLock = UserBankAccountLocks.GetOrAdd(coachId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync(cancellationToken);
        try
        {
            var existingAccounts = await _bankRepository.ListByCoachIdAsync(coachId, cancellationToken);

            // Prevent duplicate bank account for the same coach
            var isDuplicate = existingAccounts.Any(a =>
                string.Equals(a.BankCode, cleanBankCode, StringComparison.OrdinalIgnoreCase) &&
                _encryptionService.Decrypt(a.EncryptedAccountNumber) == cleanAccountNumber);

            if (isDuplicate)
            {
                throw new ValidationException("This bank account number already exists in your account list.");
            }

            // If this is the first account or marked as default, make it default
            var isDefault = dto!.IsDefault || !existingAccounts.Any();
            var now = DateTime.UtcNow;

            if (isDefault)
            {
                foreach (var acc in existingAccounts.Where(a => a.IsDefault == true))
                {
                    acc.IsDefault = false;
                    acc.UpdatedAt = now;
                }
            }

            var newAccount = new CoachBankAccount
            {
                BankId = Guid.NewGuid(),
                CoachId = coachId,
                BankName = cleanBankName,
                BankCode = cleanBankCode,
                AccountName = cleanAccountName,
                EncryptedAccountNumber = _encryptionService.Encrypt(cleanAccountNumber),
                Branch = cleanBranch,
                IsDefault = isDefault,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _bankRepository.AddAsync(newAccount, cancellationToken);
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (
                string.Equals(ex.InnerException?.GetType().GetProperty("SqlState")?.GetValue(ex.InnerException) as string, "23503", StringComparison.Ordinal) &&
                ex.InnerException?.Message?.Contains("CoachBankAccounts_CoachID_fkey", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new BusinessException("Bank account registration is currently restricted to accounts with an active coach profile.");
            }

            return ApiResponseDto<CoachBankAccountDto>.Ok(MapToDto(newAccount), "Bank account added successfully.");
        }
        finally
        {
            userLock.Release();
        }
    }

    public async Task<ApiResponseDto<CoachBankAccountDto>> UpdateAccountAsync(Guid coachId, Guid bankId, UpdateCoachBankAccountDto dto, CancellationToken cancellationToken = default)
    {
        var (cleanAccountNumber, cleanBankName, cleanBankCode, cleanAccountName, cleanBranch) =
            ValidateAndSanitize(dto?.AccountNumber, dto?.BankName, dto?.BankCode, dto?.AccountName, dto?.Branch);

        var userLock = UserBankAccountLocks.GetOrAdd(coachId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync(cancellationToken);
        try
        {
            var accounts = await _bankRepository.ListByCoachIdAsync(coachId, cancellationToken);
            var targetAccount = accounts.FirstOrDefault(a => a.BankId == bankId);
            if (targetAccount == null)
            {
                throw new NotFoundException("Bank account not found.");
            }

            // Check duplicate against other accounts of the same coach (excluding targetAccount)
            var isDuplicate = accounts.Any(a => a.BankId != bankId &&
                string.Equals(a.BankCode, cleanBankCode, StringComparison.OrdinalIgnoreCase) &&
                _encryptionService.Decrypt(a.EncryptedAccountNumber) == cleanAccountNumber);

            if (isDuplicate)
            {
                throw new ValidationException("This bank account number already exists in your account list.");
            }

            if (dto!.IsDefault && targetAccount.IsActive == false)
            {
                throw new ValidationException("An inactive bank account cannot be set as default.");
            }

            var now = DateTime.UtcNow;

            // Update account details
            targetAccount.BankName = cleanBankName;
            targetAccount.BankCode = cleanBankCode;
            targetAccount.AccountName = cleanAccountName;
            targetAccount.EncryptedAccountNumber = _encryptionService.Encrypt(cleanAccountNumber);
            targetAccount.Branch = cleanBranch;
            targetAccount.UpdatedAt = now;

            if (dto.IsDefault)
            {
                foreach (var acc in accounts.Where(a => a.BankId != bankId && a.IsDefault == true))
                {
                    acc.IsDefault = false;
                    acc.UpdatedAt = now;
                }
                targetAccount.IsDefault = true;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponseDto<CoachBankAccountDto>.Ok(MapToDto(targetAccount), "Bank account updated successfully.");
        }
        finally
        {
            userLock.Release();
        }
    }

    private static (string AccountNumber, string BankName, string BankCode, string AccountName, string? Branch)
        ValidateAndSanitize(string? accountNumber, string? bankName, string? bankCode, string? accountName, string? branch)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            throw new ValidationException("Account number is required.");
        }

        var cleanAccountNumber = SanitizeAccountNumber(accountNumber);
        if (string.IsNullOrWhiteSpace(cleanAccountNumber) || !System.Text.RegularExpressions.Regex.IsMatch(cleanAccountNumber, @"^\d{6,20}$"))
        {
            throw new ValidationException("Account number must contain only digits (6-20 characters).");
        }

        if (string.IsNullOrWhiteSpace(bankName))
        {
            throw new ValidationException("Bank name is required.");
        }

        if (string.IsNullOrWhiteSpace(bankCode))
        {
            throw new ValidationException("Bank code is required.");
        }

        var cleanBankName = bankName.Trim();
        var cleanBankCode = bankCode.Trim().ToUpperInvariant();
        var cleanAccountName = NormalizeVietnameseAccountName(accountName ?? string.Empty);
        var cleanBranch = string.IsNullOrWhiteSpace(branch) ? null : branch.Trim();

        return (cleanAccountNumber, cleanBankName, cleanBankCode, cleanAccountName, cleanBranch);
    }

    private static string SanitizeAccountNumber(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return raw.Replace(" ", "").Replace("-", "").Trim();
    }

    private static string NormalizeVietnameseAccountName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var normalized = raw.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();

        foreach (var c in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString()
            .Normalize(System.Text.NormalizationForm.FormC)
            .Replace("đ", "d", StringComparison.OrdinalIgnoreCase)
            .Trim()
            .ToUpperInvariant();
    }


    public async Task<ApiResponseDto<bool>> SetDefaultAsync(Guid coachId, Guid bankId, CancellationToken cancellationToken = default)
    {
        var userLock = UserBankAccountLocks.GetOrAdd(coachId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync(cancellationToken);
        try
        {
            var accounts = await _bankRepository.ListByCoachIdAsync(coachId, cancellationToken);
            var targetAccount = accounts.FirstOrDefault(a => a.BankId == bankId);
            if (targetAccount == null)
            {
                throw new NotFoundException("Bank account not found.");
            }

            if (targetAccount.IsActive == false)
            {
                throw new ValidationException("An inactive bank account cannot be set as default.");
            }

            var now = DateTime.UtcNow;
            foreach (var acc in accounts)
            {
                acc.IsDefault = (acc.BankId == bankId);
                acc.UpdatedAt = now;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponseDto<bool>.Ok(true, "Default bank account updated.");
        }
        finally
        {
            userLock.Release();
        }
    }

    public async Task<ApiResponseDto<bool>> DeleteAccountAsync(Guid coachId, Guid bankId, CancellationToken cancellationToken = default)
    {
        var userLock = UserBankAccountLocks.GetOrAdd(coachId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync(cancellationToken);
        try
        {
            var accounts = await _bankRepository.ListByCoachIdAsync(coachId, cancellationToken);
            var targetAccount = accounts.FirstOrDefault(a => a.BankId == bankId);
            if (targetAccount == null)
            {
                throw new NotFoundException("Bank account not found.");
            }

            _bankRepository.Remove(targetAccount);

            // If deleted account was default and other active accounts exist, make the first active one default
            if (targetAccount.IsDefault == true)
            {
                var nextDefault = accounts.FirstOrDefault(a => a.BankId != bankId && a.IsActive != false);
                if (nextDefault != null)
                {
                    nextDefault.IsDefault = true;
                    nextDefault.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponseDto<bool>.Ok(true, "Bank account deleted successfully.");
        }
        finally
        {
            userLock.Release();
        }
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
            IsActive = b.IsActive,
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

    public async Task<ApiResponseDto<string?>> LookupAccountNameAsync(string bin, string accountNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bin) || string.IsNullOrWhiteSpace(accountNumber))
            return ApiResponseDto<string?>.Fail("Please provide bank BIN and account number.");

        var clientId = _configuration?["VietQr:ClientId"];
        var apiKey = _configuration?["VietQr:ApiKey"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(apiKey))
        {
            return ApiResponseDto<string?>.Fail("Automated lookup is unavailable without VietQR API keys. Please enter account holder name manually.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(6);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.vietqr.io/v2/lookup");
            request.Headers.Add("x-client-id", clientId.Trim());
            request.Headers.Add("x-api-key", apiKey.Trim());
            request.Content = JsonContent.Create(new
            {
                bin = bin.Trim(),
                accountNumber = accountNumber.Trim()
            });

            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiResponseDto<string?>.Fail("Automated lookup service is temporarily unavailable. Please enter account holder name manually.");
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = doc.RootElement;

            if (root.TryGetProperty("code", out var code) && code.GetString() == "00")
            {
                if (root.TryGetProperty("data", out var data) && data.TryGetProperty("accountName", out var name))
                {
                    var accountName = name.GetString();
                    if (!string.IsNullOrWhiteSpace(accountName))
                        return ApiResponseDto<string?>.Ok(accountName.ToUpperInvariant(), "Account verified successfully.");
                }
            }

            var desc = root.TryGetProperty("desc", out var descProp) ? descProp.GetString() : "Bank account not found or invalid account number.";
            return ApiResponseDto<string?>.Fail(desc ?? "Bank account not found or invalid account number.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return ApiResponseDto<string?>.Fail("Automated lookup service is temporarily unavailable. Please enter account holder name manually.");
        }
    }



}
