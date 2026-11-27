using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using FitSocial.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit test suite for Update Payout Account Info feature:
/// 1. CoachesController.UpdateBankAccount(Guid bankId, UpdateCoachBankAccountDto dto, CancellationToken cancellationToken)
///    - Normal (UTCID01 - UTCID04): NameIdentifier 200 OK, "sub" fallback 200 OK, TRAINEE role allowed 200 OK, CancellationToken forwarding.
///    - Boundary (UTCID05 - UTCID06): Empty/whitespace NameIdentifier -> 401, Guid.Empty bankId forwarded to service.
///    - Abnormal (UTCID07 - UTCID10): Unauthenticated 401, invalid GUID NameIdentifier/"sub" 401, NotFoundException propagation, ValidationException propagation.
/// 2. CoachBankAccountService.UpdateAccountAsync(Guid coachId, Guid bankId, UpdateCoachBankAccountDto dto, CancellationToken cancellationToken)
///    - Normal (UTCID01 - UTCID06): Valid update with sanitization/Vietnamese normalization/encryption/masking, updating same account without false duplicate, promoting IsDefault & demoting old default, same account number in different bank, null/whitespace branch -> null, CancellationToken forwarding.
///    - Boundary (UTCID07 - UTCID12): Min 6-digit account number, max 20-digit account number, 5-digit under-min validation error, 21-digit over-max validation error, IsDefault=false keeps existing default true, whitespace AccountName -> empty string.
///    - Abnormal (UTCID13 - UTCID18): Null dto, empty/whitespace AccountNumber, non-digit AccountNumber, bank account not found, duplicate account on another BankId, SaveChanges/cancellation exception propagation.
/// 3. CoachBankAccountRepository.ListByCoachIdAsync(Guid coachId, CancellationToken cancellationToken)
///    - Normal (UTCID01 - UTCID02): Filters strictly by coachId, orders IsDefault descending then CreatedAt descending.
///    - Boundary (UTCID03 - UTCID04): Empty list when no accounts, Guid.Empty & null IsDefault/CreatedAt handling.
///    - Abnormal (UTCID05): Cancelled CancellationToken throws OperationCanceledException.
/// </summary>
public class UpdatePayoutAccountInfoServiceTests
{
    private readonly Mock<ICoachBankAccountRepository> _mockBankRepo;
    private readonly Mock<ICoachProfileRepository> _mockCoachRepo;
    private readonly Mock<IEncryptionService> _mockEncryption;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly CoachBankAccountService _service;

    public UpdatePayoutAccountInfoServiceTests()
    {
        _mockBankRepo = new Mock<ICoachBankAccountRepository>();
        _mockCoachRepo = new Mock<ICoachProfileRepository>();
        _mockEncryption = new Mock<IEncryptionService>();
        _mockUow = new Mock<IUnitOfWork>();

        _service = new CoachBankAccountService(
            _mockBankRepo.Object,
            _mockCoachRepo.Object,
            _mockEncryption.Object,
            _mockUow.Object,
            new Mock<IHttpClientFactory>().Object);
    }

    private static UpdateCoachBankAccountDto CreateValidUpdateDto(
        string bankName = "Vietcombank",
        string bankCode = "vcb",
        string accountName = "Đặng Văn Hùng",
        string accountNumber = "1012345678",
        string? branch = "Ho Chi Minh",
        bool isDefault = false)
    {
        return new UpdateCoachBankAccountDto
        {
            BankName = bankName,
            BankCode = bankCode,
            AccountName = accountName,
            AccountNumber = accountNumber,
            Branch = branch,
            IsDefault = isDefault
        };
    }

    #region CoachesController.UpdateBankAccount (UTCID01 - UTCID10)

    private static CoachesController CreateControllerWithClaims(
        Mock<ICoachBankAccountService> mockBankService,
        params Claim[] claims)
    {
        var mockCoachService = new Mock<ICoachService>();
        var controller = new CoachesController(mockCoachService.Object, mockBankService.Object);
        var identity = claims.Length > 0
            ? new ClaimsIdentity(claims, "TestAuth")
            : new ClaimsIdentity();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task CoachesController_UpdateBankAccount_WhenCoachUsesNameIdentifier_ShouldReturn200Ok()
    {
        // Controller UTCID01 - Normal: Coach authenticated with NameIdentifier updates bank account -> 200 OK
        var mockBankService = new Mock<ICoachBankAccountService>();
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockBankService,
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Coach));

        var dto = CreateValidUpdateDto();
        var expected = new CoachBankAccountDto
        {
            BankId = bankId, CoachId = coachId, BankCode = "VCB",
            AccountName = "DANG VAN HUNG", AccountNumber = "1012345678", MaskedAccountNumber = "******5678"
        };
        mockBankService.Setup(s => s.UpdateAccountAsync(coachId, bankId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<CoachBankAccountDto>.Ok(expected, "Bank account updated successfully."));

        var actionResult = await controller.UpdateBankAccount(bankId, dto, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<CoachBankAccountDto>>(okResult.Value);
        Assert.True(body.Success);
        Assert.Equal("Bank account updated successfully.", body.Message);
        Assert.Equal(bankId, body.Data!.BankId);
        mockBankService.Verify(s => s.UpdateAccountAsync(coachId, bankId, dto, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CoachesController_UpdateBankAccount_WhenSubClaimUsed_ShouldReturn200Ok()
    {
        // Controller UTCID02 - Normal: Falls back to "sub" claim when NameIdentifier is absent
        var mockBankService = new Mock<ICoachBankAccountService>();
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockBankService,
            new Claim("sub", coachId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Coach));

        var dto = CreateValidUpdateDto();
        mockBankService.Setup(s => s.UpdateAccountAsync(coachId, bankId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<CoachBankAccountDto>.Ok(new CoachBankAccountDto { BankId = bankId }));

        var actionResult = await controller.UpdateBankAccount(bankId, dto, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponseDto<CoachBankAccountDto>>(okResult.Value);
        Assert.True(body.Success);
        mockBankService.Verify(s => s.UpdateAccountAsync(coachId, bankId, dto, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CoachesController_UpdateBankAccount_WhenTraineeAuthenticated_ShouldReturn200Ok()
    {
        // Controller UTCID03 - Normal: Endpoint allows any [Authorize] user (coach or trainee for refund payouts)
        var mockBankService = new Mock<ICoachBankAccountService>();
        var traineeId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockBankService,
            new Claim(ClaimTypes.NameIdentifier, traineeId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Trainee));

        var dto = CreateValidUpdateDto();
        mockBankService.Setup(s => s.UpdateAccountAsync(traineeId, bankId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<CoachBankAccountDto>.Ok(new CoachBankAccountDto { BankId = bankId, CoachId = traineeId }));

        var actionResult = await controller.UpdateBankAccount(bankId, dto, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.True(Assert.IsType<ApiResponseDto<CoachBankAccountDto>>(okResult.Value).Success);
        mockBankService.Verify(s => s.UpdateAccountAsync(traineeId, bankId, dto, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CoachesController_UpdateBankAccount_ShouldForwardCancellationTokenToService()
    {
        // Controller UTCID04 - Normal: Explicit CancellationToken forwarded to ICoachBankAccountService
        var mockBankService = new Mock<ICoachBankAccountService>();
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var controller = CreateControllerWithClaims(
            mockBankService,
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()));

        var dto = CreateValidUpdateDto();
        mockBankService.Setup(s => s.UpdateAccountAsync(coachId, bankId, dto, token))
            .ReturnsAsync(ApiResponseDto<CoachBankAccountDto>.Ok(new CoachBankAccountDto { BankId = bankId }));

        var actionResult = await controller.UpdateBankAccount(bankId, dto, token);

        Assert.IsType<OkObjectResult>(actionResult);
        mockBankService.Verify(s => s.UpdateAccountAsync(coachId, bankId, dto, token), Times.Once);
    }

    [Fact]
    public async Task CoachesController_UpdateBankAccount_WhenNameIdentifierIsEmptyOrWhitespace_ShouldReturn401Unauthorized()
    {
        // Controller UTCID05 - Boundary: Whitespace NameIdentifier fails Guid.TryParse -> 401 Unauthorized
        var mockBankService = new Mock<ICoachBankAccountService>();
        var controller = CreateControllerWithClaims(
            mockBankService,
            new Claim(ClaimTypes.NameIdentifier, "   "));

        var actionResult = await controller.UpdateBankAccount(Guid.NewGuid(), CreateValidUpdateDto(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        var body = Assert.IsType<ApiResponseDto<CoachBankAccountDto>>(unauthorized.Value);
        Assert.False(body.Success);
        Assert.Equal("Unauthorized", body.Message);
        mockBankService.Verify(s => s.UpdateAccountAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UpdateCoachBankAccountDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CoachesController_UpdateBankAccount_WhenBankIdIsEmptyGuid_ShouldDelegateToServiceAndPropagateNotFound()
    {
        // Controller UTCID06 - Boundary: Guid.Empty bankId is forwarded to service, which throws NotFoundException
        var mockBankService = new Mock<ICoachBankAccountService>();
        var coachId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockBankService,
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()));

        var dto = CreateValidUpdateDto();
        mockBankService.Setup(s => s.UpdateAccountAsync(coachId, Guid.Empty, dto, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Bank account not found."));

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            controller.UpdateBankAccount(Guid.Empty, dto, CancellationToken.None));

        Assert.Equal("Bank account not found.", ex.Message);
    }

    [Fact]
    public async Task CoachesController_UpdateBankAccount_WhenUnauthenticated_ShouldReturn401Unauthorized()
    {
        // Controller UTCID07 - Abnormal: Missing claims -> 401 Unauthorized
        var mockBankService = new Mock<ICoachBankAccountService>();
        var controller = CreateControllerWithClaims(mockBankService);

        var actionResult = await controller.UpdateBankAccount(Guid.NewGuid(), CreateValidUpdateDto(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponseDto<CoachBankAccountDto>>(unauthorized.Value);
        Assert.False(body.Success);
        Assert.Equal("Unauthorized", body.Message);
        mockBankService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CoachesController_UpdateBankAccount_WhenUserIdClaimIsInvalidGuid_ShouldReturn401Unauthorized()
    {
        // Controller UTCID08 - Abnormal: Invalid non-GUID "sub" claim -> 401 Unauthorized
        var mockBankService = new Mock<ICoachBankAccountService>();
        var controller = CreateControllerWithClaims(
            mockBankService,
            new Claim("sub", "not-a-valid-guid"));

        var actionResult = await controller.UpdateBankAccount(Guid.NewGuid(), CreateValidUpdateDto(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        mockBankService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CoachesController_UpdateBankAccount_WhenAccountNotFound_ShouldPropagateNotFoundException()
    {
        // Controller UTCID09 - Abnormal: NotFoundException from service propagates
        var mockBankService = new Mock<ICoachBankAccountService>();
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockBankService,
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()));

        var dto = CreateValidUpdateDto();
        mockBankService.Setup(s => s.UpdateAccountAsync(coachId, bankId, dto, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Bank account not found."));

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            controller.UpdateBankAccount(bankId, dto, CancellationToken.None));

        Assert.Equal("Bank account not found.", ex.Message);
    }

    [Fact]
    public async Task CoachesController_UpdateBankAccount_WhenValidationFails_ShouldPropagateValidationException()
    {
        // Controller UTCID10 - Abnormal: ValidationException (duplicate/invalid format) propagates
        var mockBankService = new Mock<ICoachBankAccountService>();
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockBankService,
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()));

        var dto = CreateValidUpdateDto();
        mockBankService.Setup(s => s.UpdateAccountAsync(coachId, bankId, dto, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("This bank account number already exists in your account list."));

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            controller.UpdateBankAccount(bankId, dto, CancellationToken.None));

        Assert.Equal("This bank account number already exists in your account list.", ex.Message);
    }

    #endregion

    #region CoachBankAccountService.UpdateAccountAsync (UTCID01 - UTCID18)

    [Fact]
    public async Task UpdateAccountAsync_WithValidInput_ShouldSanitizeNormalizeEncryptAndReturnMaskedDto()
    {
        // Service UTCID01 - Normal: Trims BankName/Branch, uppercases BankCode, normalizes Vietnamese AccountName, strips spaces/hyphens in AccountNumber, encrypts & masks
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var oldCipher = new byte[] { 1, 2, 3 };
        var newCipher = new byte[] { 4, 5, 6, 7 };

        var existing = new CoachBankAccount
        {
            BankId = bankId, CoachId = coachId, BankName = "Old Bank", BankCode = "OLD",
            AccountName = "OLD NAME", EncryptedAccountNumber = oldCipher, IsDefault = false, IsActive = true
        };

        var dto = CreateValidUpdateDto(
            bankName: "  Techcombank  ",
            bankCode: " tcb ",
            accountName: "  Đặng Văn Hùng  ",
            accountNumber: " 1903 1234-5678 ",
            branch: "  Saigon  ",
            isDefault: false);

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount> { existing });
        _mockEncryption.Setup(e => e.Encrypt("190312345678", false)).Returns(newCipher);
        _mockEncryption.Setup(e => e.Decrypt(newCipher)).Returns("190312345678");
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.UpdateAccountAsync(coachId, bankId, dto);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Bank account updated successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal("Techcombank", result.Data.BankName);
        Assert.Equal("TCB", result.Data.BankCode);
        Assert.Equal("DANG VAN HUNG", result.Data.AccountName);
        Assert.Equal("190312345678", result.Data.AccountNumber);
        Assert.Equal("********5678", result.Data.MaskedAccountNumber);
        Assert.Equal("Saigon", result.Data.Branch);
        Assert.False(result.Data.IsDefault);
        Assert.Equal(newCipher, existing.EncryptedAccountNumber);
        Assert.NotNull(existing.UpdatedAt);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenKeepingSameBankCodeAndAccountNumber_ShouldNotTriggerDuplicateError()
    {
        // Service UTCID02 - Normal: Updating other fields of targetAccount with its own existing BankCode & AccountNumber succeeds
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var cipher = new byte[] { 9, 9, 9 };

        var existing = new CoachBankAccount
        {
            BankId = bankId, CoachId = coachId, BankName = "VCB", BankCode = "VCB",
            AccountName = "NGUYEN A", EncryptedAccountNumber = cipher, Branch = "Old Branch", IsDefault = true
        };

        var dto = CreateValidUpdateDto(
            bankName: "Vietcombank",
            bankCode: "VCB",
            accountName: "NGUYEN VAN A",
            accountNumber: "1012345678",
            branch: "New Branch",
            isDefault: true);

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount> { existing });
        _mockEncryption.Setup(e => e.Encrypt("1012345678", false)).Returns(cipher);
        _mockEncryption.Setup(e => e.Decrypt(cipher)).Returns("1012345678");
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.UpdateAccountAsync(coachId, bankId, dto);

        Assert.True(result.Success);
        Assert.Equal("New Branch", result.Data!.Branch);
        Assert.Equal("NGUYEN VAN A", result.Data.AccountName);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenPromotingToDefault_ShouldDemotePreviousDefaultAccount()
    {
        // Service UTCID03 - Normal: Setting dto.IsDefault=true on non-default account sets it default and clears previous default
        var coachId = Guid.NewGuid();
        var targetBankId = Guid.NewGuid();
        var oldDefaultBankId = Guid.NewGuid();
        var targetCipher = new byte[] { 11, 12 };
        var otherCipher = new byte[] { 21, 22 };

        var oldDefault = new CoachBankAccount
        {
            BankId = oldDefaultBankId, CoachId = coachId, BankCode = "ACB",
            EncryptedAccountNumber = otherCipher, IsDefault = true
        };
        var target = new CoachBankAccount
        {
            BankId = targetBankId, CoachId = coachId, BankCode = "MBB",
            EncryptedAccountNumber = targetCipher, IsDefault = false
        };

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount> { oldDefault, target });
        _mockEncryption.Setup(e => e.Decrypt(otherCipher)).Returns("9999888877");
        _mockEncryption.Setup(e => e.Encrypt("1012345678", false)).Returns(targetCipher);
        _mockEncryption.Setup(e => e.Decrypt(targetCipher)).Returns("1012345678");
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = CreateValidUpdateDto(bankName: "MBBank", bankCode: "MBB", accountNumber: "1012345678", isDefault: true);

        var result = await _service.UpdateAccountAsync(coachId, targetBankId, dto);

        Assert.True(result.Success);
        Assert.True(target.IsDefault);
        Assert.False(oldDefault.IsDefault);
        Assert.NotNull(oldDefault.UpdatedAt);
        Assert.True(result.Data!.IsDefault);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenSameAccountNumberInDifferentBank_ShouldAllowUpdate()
    {
        // Service UTCID04 - Normal: Duplicate check requires BOTH matching BankCode and AccountNumber; different BankCode succeeds
        var coachId = Guid.NewGuid();
        var targetBankId = Guid.NewGuid();
        var otherBankId = Guid.NewGuid();
        var sharedNumber = "1234567890";
        var targetCipher = new byte[] { 1, 1 };
        var otherCipher = new byte[] { 2, 2 };

        var other = new CoachBankAccount { BankId = otherBankId, CoachId = coachId, BankCode = "VCB", EncryptedAccountNumber = otherCipher };
        var target = new CoachBankAccount { BankId = targetBankId, CoachId = coachId, BankCode = "TCB", EncryptedAccountNumber = targetCipher };

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount> { other, target });
        _mockEncryption.Setup(e => e.Decrypt(otherCipher)).Returns(sharedNumber);
        _mockEncryption.Setup(e => e.Encrypt(sharedNumber, false)).Returns(targetCipher);
        _mockEncryption.Setup(e => e.Decrypt(targetCipher)).Returns(sharedNumber);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = CreateValidUpdateDto(bankName: "Techcombank", bankCode: "TCB", accountNumber: sharedNumber);
        var result = await _service.UpdateAccountAsync(coachId, targetBankId, dto);

        Assert.True(result.Success);
        Assert.Equal("TCB", result.Data!.BankCode);
        Assert.Equal(sharedNumber, result.Data.AccountNumber);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenBranchIsWhitespaceOrNull_ShouldSetBranchToNull()
    {
        // Service UTCID05 - Normal: Blank/whitespace Branch is stored as null
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var cipher = new byte[] { 3, 4 };
        var target = new CoachBankAccount
        {
            BankId = bankId, CoachId = coachId, BankCode = "VCB", Branch = "Old Branch", EncryptedAccountNumber = cipher
        };

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount> { target });
        _mockEncryption.Setup(e => e.Encrypt("1012345678", false)).Returns(cipher);
        _mockEncryption.Setup(e => e.Decrypt(cipher)).Returns("1012345678");
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.UpdateAccountAsync(coachId, bankId, CreateValidUpdateDto(branch: "   "));

        Assert.True(result.Success);
        Assert.Null(target.Branch);
        Assert.Null(result.Data!.Branch);
    }

    [Fact]
    public async Task UpdateAccountAsync_ShouldForwardCancellationTokenToRepositoryAndUnitOfWork()
    {
        // Service UTCID06 - Normal: Explicit CancellationToken is passed through to ListByCoachIdAsync and SaveChangesAsync
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var cipher = new byte[] { 5, 6 };
        var target = new CoachBankAccount { BankId = bankId, CoachId = coachId, BankCode = "VCB", EncryptedAccountNumber = cipher };

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, token))
            .ReturnsAsync(new List<CoachBankAccount> { target });
        _mockEncryption.Setup(e => e.Encrypt("1012345678", false)).Returns(cipher);
        _mockEncryption.Setup(e => e.Decrypt(cipher)).Returns("1012345678");
        _mockUow.Setup(u => u.SaveChangesAsync(token)).ReturnsAsync(1);

        var result = await _service.UpdateAccountAsync(coachId, bankId, CreateValidUpdateDto(), token);

        Assert.True(result.Success);
        _mockBankRepo.Verify(r => r.ListByCoachIdAsync(coachId, token), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenAccountNumberIsAtMinimumBoundary6Digits_ShouldSucceed()
    {
        // Service UTCID07 - Boundary: Exact 6-digit account number (minimum valid length) succeeds
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var cipher = new byte[] { 7, 8 };
        var target = new CoachBankAccount { BankId = bankId, CoachId = coachId, BankCode = "VCB", EncryptedAccountNumber = cipher };

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount> { target });
        _mockEncryption.Setup(e => e.Encrypt("123456", false)).Returns(cipher);
        _mockEncryption.Setup(e => e.Decrypt(cipher)).Returns("123456");
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.UpdateAccountAsync(coachId, bankId, CreateValidUpdateDto(accountNumber: "123456"));

        Assert.True(result.Success);
        Assert.Equal("123456", result.Data!.AccountNumber);
        Assert.Equal("**3456", result.Data.MaskedAccountNumber);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenAccountNumberIsAtMaximumBoundary20Digits_ShouldSucceed()
    {
        // Service UTCID08 - Boundary: Exact 20-digit account number (maximum valid length) succeeds
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var cipher = new byte[] { 9, 10 };
        var maxNumber = "12345678901234567890";
        var target = new CoachBankAccount { BankId = bankId, CoachId = coachId, BankCode = "VCB", EncryptedAccountNumber = cipher };

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount> { target });
        _mockEncryption.Setup(e => e.Encrypt(maxNumber, false)).Returns(cipher);
        _mockEncryption.Setup(e => e.Decrypt(cipher)).Returns(maxNumber);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.UpdateAccountAsync(coachId, bankId, CreateValidUpdateDto(accountNumber: maxNumber));

        Assert.True(result.Success);
        Assert.Equal(maxNumber, result.Data!.AccountNumber);
        Assert.Equal("****************7890", result.Data.MaskedAccountNumber);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenAccountNumberIsBelowMinLength5Digits_ShouldThrowValidationException()
    {
        // Service UTCID09 - Boundary: 5 digits (just below 6-digit min boundary) -> ValidationException
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var target = new CoachBankAccount { BankId = bankId, CoachId = coachId, BankCode = "VCB" };

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount> { target });

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAccountAsync(coachId, bankId, CreateValidUpdateDto(accountNumber: "12345")));

        Assert.Equal("Account number must contain only digits (6-20 characters).", ex.Message);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenAccountNumberExceedsMaxLength21Digits_ShouldThrowValidationException()
    {
        // Service UTCID10 - Boundary: 21 digits (just above 20-digit max boundary) -> ValidationException
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var target = new CoachBankAccount { BankId = bankId, CoachId = coachId, BankCode = "VCB" };

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount> { target });

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAccountAsync(coachId, bankId, CreateValidUpdateDto(accountNumber: "123456789012345678901")));

        Assert.Equal("Account number must contain only digits (6-20 characters).", ex.Message);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenDtoIsDefaultFalseOnExistingDefaultAccount_ShouldKeepIsDefaultTrue()
    {
        // Service UTCID11 - Boundary: Passing IsDefault=false when updating an already-default account preserves IsDefault=true
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var cipher = new byte[] { 12, 13 };
        var target = new CoachBankAccount
        {
            BankId = bankId, CoachId = coachId, BankCode = "VCB", EncryptedAccountNumber = cipher, IsDefault = true
        };

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount> { target });
        _mockEncryption.Setup(e => e.Encrypt("1012345678", false)).Returns(cipher);
        _mockEncryption.Setup(e => e.Decrypt(cipher)).Returns("1012345678");
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.UpdateAccountAsync(coachId, bankId, CreateValidUpdateDto(isDefault: false));

        Assert.True(result.Success);
        Assert.True(target.IsDefault);
        Assert.True(result.Data!.IsDefault);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenAccountNameIsWhitespace_ShouldNormalizeToEmptyString()
    {
        // Service UTCID12 - Boundary: Whitespace AccountName normalizes safely to string.Empty
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var cipher = new byte[] { 14, 15 };
        var target = new CoachBankAccount { BankId = bankId, CoachId = coachId, BankCode = "VCB", EncryptedAccountNumber = cipher };

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount> { target });
        _mockEncryption.Setup(e => e.Encrypt("1012345678", false)).Returns(cipher);
        _mockEncryption.Setup(e => e.Decrypt(cipher)).Returns("1012345678");
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.UpdateAccountAsync(coachId, bankId, CreateValidUpdateDto(accountName: "   "));

        Assert.True(result.Success);
        Assert.Equal(string.Empty, target.AccountName);
        Assert.Equal(string.Empty, result.Data!.AccountName);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenDtoIsNull_ShouldThrowValidationException()
    {
        // Service UTCID13 - Abnormal: Null DTO throws ValidationException before accessing repository
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAccountAsync(Guid.NewGuid(), Guid.NewGuid(), null!));

        Assert.Equal("Account number is required.", ex.Message);
        _mockBankRepo.VerifyNoOtherCalls();
        _mockUow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenAccountNumberIsEmptyOrWhitespace_ShouldThrowValidationException()
    {
        // Service UTCID14 - Abnormal: Empty or whitespace AccountNumber throws ValidationException
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAccountAsync(Guid.NewGuid(), Guid.NewGuid(), CreateValidUpdateDto(accountNumber: "   ")));

        Assert.Equal("Account number is required.", ex.Message);
        _mockBankRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenAccountNumberContainsLettersOrOnlySeparators_ShouldThrowValidationException()
    {
        // Service UTCID15 - Abnormal: Non-digit characters in AccountNumber fail regex validation
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var target = new CoachBankAccount { BankId = bankId, CoachId = coachId, BankCode = "VCB" };

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount> { target });

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAccountAsync(coachId, bankId, CreateValidUpdateDto(accountNumber: "1234AB78")));

        Assert.Equal("Account number must contain only digits (6-20 characters).", ex.Message);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenBankAccountNotFound_ShouldThrowNotFoundException()
    {
        // Service UTCID16 - Abnormal: Target bankId does not exist in coach's accounts -> NotFoundException
        var coachId = Guid.NewGuid();
        var missingBankId = Guid.NewGuid();

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount>());

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAccountAsync(coachId, missingBankId, CreateValidUpdateDto()));

        Assert.Equal("Bank account not found.", ex.Message);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenDuplicateAccountExistsInOtherRecord_ShouldThrowValidationException()
    {
        // Service UTCID17 - Abnormal: Another account of the same coach already has the same BankCode (case-insensitive) and AccountNumber
        var coachId = Guid.NewGuid();
        var targetBankId = Guid.NewGuid();
        var otherBankId = Guid.NewGuid();
        var otherCipher = new byte[] { 30, 31 };

        var other = new CoachBankAccount { BankId = otherBankId, CoachId = coachId, BankCode = "VCB", EncryptedAccountNumber = otherCipher };
        var target = new CoachBankAccount { BankId = targetBankId, CoachId = coachId, BankCode = "TCB", EncryptedAccountNumber = new byte[] { 40, 41 } };

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount> { other, target });
        _mockEncryption.Setup(e => e.Decrypt(otherCipher)).Returns("1012345678");

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAccountAsync(coachId, targetBankId, CreateValidUpdateDto(bankCode: " vcb ", accountNumber: "1012-3456 78")));

        Assert.Equal("This bank account number already exists in your account list.", ex.Message);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenSaveChangesFails_ShouldPropagateException()
    {
        // Service UTCID18 - Abnormal: Database SaveChangesAsync throws exception -> propagates cleanly
        var coachId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var cipher = new byte[] { 50, 51 };
        var target = new CoachBankAccount { BankId = bankId, CoachId = coachId, BankCode = "VCB", EncryptedAccountNumber = cipher };

        _mockBankRepo.Setup(r => r.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount> { target });
        _mockEncryption.Setup(e => e.Encrypt("1012345678", false)).Returns(cipher);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database commit failed"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateAccountAsync(coachId, bankId, CreateValidUpdateDto()));

        Assert.Equal("Database commit failed", ex.Message);
    }

    #endregion

    #region CoachBankAccountRepository.ListByCoachIdAsync (UTCID01 - UTCID05)

    private static FitSocialDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FitSocialDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new FitSocialDbContext(options);
    }

    [Fact]
    public async Task Repository_ListByCoachIdAsync_ShouldFilterStrictlyByCoachId()
    {
        // Repo UTCID01 - Normal: Returns only bank accounts belonging to target coachId
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var otherCoachId = Guid.NewGuid();

        context.CoachBankAccounts.AddRange(
            new CoachBankAccount { BankId = Guid.NewGuid(), CoachId = coachId, BankCode = "VCB", IsDefault = true, CreatedAt = DateTime.UtcNow },
            new CoachBankAccount { BankId = Guid.NewGuid(), CoachId = otherCoachId, BankCode = "TCB", IsDefault = true, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var repo = new CoachBankAccountRepository(context);
        var accounts = await repo.ListByCoachIdAsync(coachId);

        var single = Assert.Single(accounts);
        Assert.Equal(coachId, single.CoachId);
        Assert.Equal("VCB", single.BankCode);
    }

    [Fact]
    public async Task Repository_ListByCoachIdAsync_ShouldOrderDefaultFirstThenCreatedAtDescending()
    {
        // Repo UTCID02 - Normal: Orders by IsDefault descending, then CreatedAt descending
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();

        var oldestDefaultId = Guid.NewGuid();
        var newerNonDefaultId = Guid.NewGuid();
        var olderNonDefaultId = Guid.NewGuid();

        context.CoachBankAccounts.AddRange(
            new CoachBankAccount { BankId = olderNonDefaultId, CoachId = coachId, BankCode = "ACB", IsDefault = false, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new CoachBankAccount { BankId = newerNonDefaultId, CoachId = coachId, BankCode = "TCB", IsDefault = false, CreatedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc) },
            new CoachBankAccount { BankId = oldestDefaultId, CoachId = coachId, BankCode = "VCB", IsDefault = true, CreatedAt = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc) });
        await context.SaveChangesAsync();

        var repo = new CoachBankAccountRepository(context);
        var accounts = await repo.ListByCoachIdAsync(coachId);

        Assert.Equal(3, accounts.Count);
        Assert.Equal(oldestDefaultId, accounts[0].BankId);
        Assert.Equal(newerNonDefaultId, accounts[1].BankId);
        Assert.Equal(olderNonDefaultId, accounts[2].BankId);
    }

    [Fact]
    public async Task Repository_ListByCoachIdAsync_WhenCoachHasNoAccounts_ShouldReturnEmptyList()
    {
        // Repo UTCID03 - Boundary: Coach with zero accounts returns empty list
        using var context = CreateInMemoryDbContext();
        var repo = new CoachBankAccountRepository(context);

        var accounts = await repo.ListByCoachIdAsync(Guid.NewGuid());

        Assert.Empty(accounts);
    }

    [Fact]
    public async Task Repository_ListByCoachIdAsync_WhenGuidEmptyOrNullSortFields_ShouldHandleSafely()
    {
        // Repo UTCID04 - Boundary: Guid.Empty returns empty list and null IsDefault/CreatedAt sorts without exception
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        context.CoachBankAccounts.AddRange(
            new CoachBankAccount { BankId = Guid.NewGuid(), CoachId = coachId, BankCode = "BIDV", IsDefault = null, CreatedAt = null },
            new CoachBankAccount { BankId = Guid.NewGuid(), CoachId = coachId, BankCode = "VCB", IsDefault = true, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var repo = new CoachBankAccountRepository(context);
        var withNulls = await repo.ListByCoachIdAsync(coachId);
        var withEmptyGuid = await repo.ListByCoachIdAsync(Guid.Empty);

        Assert.Equal(2, withNulls.Count);
        Assert.Equal("VCB", withNulls[0].BankCode);
        Assert.Empty(withEmptyGuid);
    }

    [Fact]
    public async Task Repository_ListByCoachIdAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Repo UTCID05 - Abnormal: Pre-cancelled CancellationToken throws OperationCanceledException
        using var context = CreateInMemoryDbContext();
        var repo = new CoachBankAccountRepository(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repo.ListByCoachIdAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion
}
