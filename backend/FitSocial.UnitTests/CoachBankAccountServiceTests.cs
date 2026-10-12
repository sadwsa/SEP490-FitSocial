using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FitSocial.UnitTests;

public class CoachBankAccountServiceTests
{
    private readonly Mock<ICoachBankAccountRepository> _mockBankRepo;
    private readonly Mock<ICoachProfileRepository> _mockCoachRepo;
    private readonly Mock<IEncryptionService> _mockEncryption;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly CoachBankAccountService _service;

    public CoachBankAccountServiceTests()
    {
        _mockBankRepo = new Mock<ICoachBankAccountRepository>();
        _mockCoachRepo = new Mock<ICoachProfileRepository>();
        _mockEncryption = new Mock<IEncryptionService>();
        _mockUow = new Mock<IUnitOfWork>();

        var mockHttpFactory = new Mock<IHttpClientFactory>();
        _service = new CoachBankAccountService(
            _mockBankRepo.Object,
            _mockCoachRepo.Object,
            _mockEncryption.Object,
            _mockUow.Object,
            mockHttpFactory.Object);
    }

    [Fact]
    public async Task AddAccountAsync_ShouldEncryptAccountNumberAndSave()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var coach = new CoachProfile { CoachId = coachId };
        var dto = new CreateCoachBankAccountDto
        {
            BankName = "Vietcombank",
            BankCode = "VCB",
            AccountName = "NGUYEN VAN A",
            AccountNumber = "1012345678",
            Branch = "Hanoi",
            IsDefault = true
        };

        var cipherBytes = new byte[] { 10, 20, 30, 40 };

        _mockCoachRepo.Setup(c => c.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);
        _mockBankRepo.Setup(b => b.ListByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount>());
        _mockEncryption.Setup(e => e.Encrypt(dto.AccountNumber, false))
            .Returns(cipherBytes);
        _mockEncryption.Setup(e => e.Decrypt(cipherBytes))
            .Returns(dto.AccountNumber);
        _mockBankRepo.Setup(b => b.AddAsync(It.IsAny<CoachBankAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.AddAccountAsync(coachId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("VCB", result.Data!.BankCode);
        Assert.Equal("1012345678", result.Data.AccountNumber);
        Assert.True(result.Data.IsDefault);

        _mockBankRepo.Verify(b => b.AddAsync(It.Is<CoachBankAccount>(acc =>
            acc.EncryptedAccountNumber == cipherBytes &&
            acc.BankCode == "VCB" &&
            acc.IsDefault == true), It.IsAny<CancellationToken>()), Times.Once);
    }
}
