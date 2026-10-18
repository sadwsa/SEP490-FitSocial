using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// UC-23.1 Process Coach Payout: every disbursement (full or partial) must
/// leave a notification for the coach, since money really moved either way.
/// </summary>
public class PayoutNotificationTests
{
    private readonly Mock<IPayoutRepository> _payouts = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICoachBankAccountRepository> _banks = new();
    private readonly Mock<IReportRepository> _reports = new();
    private readonly Mock<IEncryptionService> _encryption = new();
    private readonly Mock<IBankTransferGateway> _gateway = new();
    private readonly Mock<IAuditLogRepository> _auditLogs = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();

    public PayoutNotificationTests()
    {
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _reports.Setup(r => r.CountUnresolvedAgainstUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _banks.Setup(b => b.ListActiveByCoachIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachBankAccount>
            {
                new() { BankId = Guid.NewGuid(), CoachId = _coachId, BankCode = "970436",
                        AccountName = "Coach", EncryptedAccountNumber = new byte[] { 1, 2, 3 }, IsDefault = true, IsActive = true }
            });
        _encryption.Setup(e => e.Decrypt(It.IsAny<byte[]>())).Returns("1234567890");
    }

    private PayoutService CreateService() => new(
        _payouts.Object, _users.Object, _banks.Object, _reports.Object, _encryption.Object,
        _gateway.Object, _auditLogs.Object, _notifications.Object, _unitOfWork.Object);

    private Payout BuildPayoutWithTwoUnpaidLines()
    {
        var payout = new Payout
        {
            PayoutId = Guid.NewGuid(),
            CoachId = _coachId,
            PayoutMonth = 1,
            PayoutYear = DateTime.UtcNow.Year,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        for (var i = 1; i <= 2; i++)
        {
            payout.PayoutItems.Add(new PayoutItem
            {
                PayoutItemId = Guid.NewGuid(),
                PayoutId = payout.PayoutId,
                OrderId = Guid.NewGuid(),
                GrossAmount = 100000m,
                CommissionRate = 0.15m,
                CommissionAmount = 15000m,
                RefundAdjustment = 0,
                TaxAmount = 0,
                NetAmount = 85000m,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            });
        }

        return payout;
    }

    private void ArrangeSuccessfulTransfer()
    {
        _gateway.Setup(g => g.CreateTransferAsync(It.IsAny<BankTransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatedTransfer(
                Created: true, GatewayRef: "gw-1", Succeeded: true,
                State: "SUCCESS", FailureReason: null, TransactionRef: "TX-1"));
    }

    [Fact]
    public async Task ProcessPackageItemsAsync_PartialSettlement_NotifiesCoachAsPAYOUT_PARTIAL()
    {
        var payout = BuildPayoutWithTwoUnpaidLines();
        _payouts.Setup(p => p.GetByIdWithDetailsAsync(payout.PayoutId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payout);
        ArrangeSuccessfulTransfer();

        var firstItemId = payout.PayoutItems.First().PayoutItemId;

        var result = await CreateService()
            .ProcessPackageItemsAsync(payout.PayoutId, new List<Guid> { firstItemId }, _staffId);

        Assert.True(result.Success);
        Assert.Equal("PENDING", payout.Status); // still pending: one line left unpaid

        _notifications.Verify(n => n.AddAsync(
            It.Is<Notification>(x => x.UserId == _coachId && x.Type == "PAYOUT_PARTIAL"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPayoutAsync_FullSettlement_NotifiesCoachAsPAYOUT_PROCESSED()
    {
        var payout = BuildPayoutWithTwoUnpaidLines();
        _payouts.Setup(p => p.GetByIdWithDetailsAsync(payout.PayoutId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payout);
        ArrangeSuccessfulTransfer();

        var result = await CreateService().ProcessPayoutAsync(payout.PayoutId, _staffId);

        Assert.True(result.Success);
        Assert.Equal("PROCESSED", payout.Status);

        _notifications.Verify(n => n.AddAsync(
            It.Is<Notification>(x => x.UserId == _coachId && x.Type == "PAYOUT_PROCESSED"),
            It.IsAny<CancellationToken>()), Times.Once);

        _notifications.Verify(n => n.AddAsync(
            It.Is<Notification>(x => x.Type == "PAYOUT_PARTIAL"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPackageItemsAsync_WhenNotificationFails_StillPaysSuccessfully()
    {
        var payout = BuildPayoutWithTwoUnpaidLines();
        _payouts.Setup(p => p.GetByIdWithDetailsAsync(payout.PayoutId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payout);
        ArrangeSuccessfulTransfer();
        _notifications.Setup(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("notification table down"));

        var firstItemId = payout.PayoutItems.First().PayoutItemId;

        var result = await CreateService()
            .ProcessPackageItemsAsync(payout.PayoutId, new List<Guid> { firstItemId }, _staffId);

        Assert.True(result.Success);
    }
}