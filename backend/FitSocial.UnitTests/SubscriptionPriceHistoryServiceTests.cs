using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.SubscriptionPriceHistory;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class SubscriptionPriceHistoryServiceTests
{
    private readonly Mock<ISubscriptionPriceHistoryRepository> _repositoryMock;
    private readonly SubscriptionPriceHistoryService _service;

    public SubscriptionPriceHistoryServiceTests()
    {
        _repositoryMock = new Mock<ISubscriptionPriceHistoryRepository>();
        _service = new SubscriptionPriceHistoryService(_repositoryMock.Object);
    }

    [Fact]
    public async Task GetPriceHistoryAsync_CalculatesPriceDifferenceAndPercentageChange_Correctly()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var items = new List<SubscriptionPriceHistoryItem>
        {
            // Case 1: Price Increase (100k -> 150k => +50k, +50%)
            new()
            {
                HistoryId = 1,
                PlanId = planId,
                PlanName = "1 Month Standard",
                OldPrice = 100000m,
                NewPrice = 150000m,
                ChangedAt = DateTime.UtcNow.AddDays(-10),
                ChangedByUserId = adminId,
                ChangedByName = "Admin One",
                Notes = "Annual inflation adjustment"
            },
            // Case 2: Price Decrease (200k -> 160k => -40k, -20%)
            new()
            {
                HistoryId = 2,
                PlanId = planId,
                PlanName = "1 Month Standard",
                OldPrice = 200000m,
                NewPrice = 160000m,
                ChangedAt = DateTime.UtcNow.AddDays(-5),
                ChangedByUserId = adminId,
                ChangedByName = "Admin One",
                Notes = "Seasonal discount promotion"
            },
            // Case 3: Initial price from 0 to 100k (0 -> 100k => +100k, +100%)
            new()
            {
                HistoryId = 3,
                PlanId = planId,
                PlanName = "1 Month Standard",
                OldPrice = 0m,
                NewPrice = 100000m,
                ChangedAt = DateTime.UtcNow.AddDays(-30),
                ChangedByUserId = adminId,
                ChangedByName = "Admin One",
                Notes = "Initial launch price"
            },
            // Case 4: No change (120k -> 120k => 0, 0%)
            new()
            {
                HistoryId = 4,
                PlanId = planId,
                PlanName = "1 Month Standard",
                OldPrice = 120000m,
                NewPrice = 120000m,
                ChangedAt = DateTime.UtcNow.AddDays(-1),
                ChangedByUserId = adminId,
                ChangedByName = "Admin One",
                Notes = "Plan re-activated without price change"
            }
        };

        _repositoryMock
            .Setup(r => r.GetPriceHistoryAsync(
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, items.Count));

        var filter = new GetPriceHistoryFilterDto
        {
            PlanId = planId,
            PageIndex = 1,
            PageSize = 10
        };

        // Act
        var result = await _service.GetPriceHistoryAsync(filter);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(4, result.Data.Items.Count);
        Assert.Equal(4, result.Data.TotalCount);

        // Verify Case 1
        var item1 = result.Data.Items[0];
        Assert.Equal(50000m, item1.PriceDifference);
        Assert.Equal(50.00m, item1.PercentageChange);
        Assert.Equal("Annual inflation adjustment", item1.Notes);

        // Verify Case 2
        var item2 = result.Data.Items[1];
        Assert.Equal(-40000m, item2.PriceDifference);
        Assert.Equal(-20.00m, item2.PercentageChange);
        Assert.Equal("Seasonal discount promotion", item2.Notes);

        // Verify Case 3
        var item3 = result.Data.Items[2];
        Assert.Equal(100000m, item3.PriceDifference);
        Assert.Equal(100.00m, item3.PercentageChange);

        // Verify Case 4
        var item4 = result.Data.Items[3];
        Assert.Equal(0m, item4.PriceDifference);
        Assert.Equal(0.00m, item4.PercentageChange);
    }

    [Fact]
    public async Task GetPriceHistoryAsync_Pagination_ComputesCorrectMetadata()
    {
        // Arrange
        var items = new List<SubscriptionPriceHistoryItem>
        {
            new() { HistoryId = 11, PlanId = Guid.NewGuid(), OldPrice = 50000, NewPrice = 60000 }
        };

        _repositoryMock
            .Setup(r => r.GetPriceHistoryAsync(
                null,
                null,
                null,
                null,
                2,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 25));

        var filter = new GetPriceHistoryFilterDto
        {
            PageIndex = 2,
            PageSize = 10
        };

        // Act
        var result = await _service.GetPriceHistoryAsync(filter);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        var page = result.Data;
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(25, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasNextPage);
        Assert.True(page.HasPreviousPage);
    }

    [Fact]
    public async Task GetPriceHistoryAsync_WithDateFilter_PassesDatesToRepository()
    {
        // Arrange
        var fromDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var toDate = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

        _repositoryMock
            .Setup(r => r.GetPriceHistoryAsync(
                null,
                null,
                fromDate,
                toDate,
                1,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<SubscriptionPriceHistoryItem>(), 0));

        var filter = new GetPriceHistoryFilterDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            PageIndex = 1,
            PageSize = 10
        };

        // Act
        var result = await _service.GetPriceHistoryAsync(filter);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        _repositoryMock.Verify(r => r.GetPriceHistoryAsync(
            null,
            null,
            fromDate,
            toDate,
            1,
            10,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPriceHistoryAsync_InvalidDateRange_ThrowsValidationException()
    {
        // Arrange
        var fromDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var toDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var filter = new GetPriceHistoryFilterDto
        {
            FromDate = fromDate,
            ToDate = toDate
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _service.GetPriceHistoryAsync(filter));
    }

    [Fact]
    public async Task GetPriceHistoryAsync_KeywordAndPlanIdFilter_PassesParametersToRepository()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var keyword = "PRO COACH";

        _repositoryMock
            .Setup(r => r.GetPriceHistoryAsync(
                planId,
                keyword,
                null,
                null,
                1,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<SubscriptionPriceHistoryItem>(), 0));

        var filter = new GetPriceHistoryFilterDto
        {
            PlanId = planId,
            Keyword = keyword,
            PageIndex = 1,
            PageSize = 10
        };

        // Act
        var result = await _service.GetPriceHistoryAsync(filter);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        _repositoryMock.Verify(r => r.GetPriceHistoryAsync(
            planId,
            keyword,
            null,
            null,
            1,
            10,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
