using FitSocial.Application.DTOs.TrainingPackage;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FitSocial.UnitTests;

public class TrainingPackageServiceTests
{
    private readonly Mock<ITrainingPackageRepository> _mockRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly TrainingPackageService _service;

    public TrainingPackageServiceTests()
    {
        _mockRepo = new Mock<ITrainingPackageRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _service = new TrainingPackageService(_mockRepo.Object, _mockUow.Object);
    }

    [Fact]
    public async Task CreatePackageAsync_With4NewFields_ShouldPersistAndReturnDto()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var dto = new CreateTrainingPackageDto
        {
            Title = "Advanced Hypertrophy Program",
            Description = "12-week muscle building plan",
            Price = 1500000,
            DurationDays = 90,
            SessionCount = 36,
            MinAge = 18,
            TargetAudience = "Intermediate to advanced lifters",
            IsActive = true
        };

        _mockRepo.Setup(r => r.AddAsync(It.IsAny<TrainingPackage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreatePackageAsync(coachId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(coachId, result.CoachId);
        Assert.Equal(dto.Title, result.Title);
        Assert.Equal(dto.Description, result.Description);
        Assert.Equal(dto.SessionCount, result.SessionCount);
        Assert.Equal(dto.MinAge, result.MinAge);
        Assert.Equal(dto.TargetAudience, result.TargetAudience);
        Assert.True(result.IsActive);
        _mockRepo.Verify(r => r.AddAsync(It.Is<TrainingPackage>(p =>
            p.Description == dto.Description &&
            p.SessionCount == dto.SessionCount &&
            p.MinAge == dto.MinAge &&
            p.TargetAudience == dto.TargetAudience), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePackageAsync_ShouldUpdateFieldsProperly()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var existing = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Old Title",
            Price = 1000000,
            DurationDays = 30,
            IsActive = true
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var updateDto = new UpdateTrainingPackageDto
        {
            Title = "New Title",
            Description = "Updated Description",
            Price = 1200000,
            SessionCount = 20,
            MinAge = 16,
            TargetAudience = "Beginners"
        };

        // Act
        var result = await _service.UpdatePackageAsync(packageId, coachId, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Title", result!.Title);
        Assert.Equal("Updated Description", result.Description);
        Assert.Equal(1200000m, result.Price);
        Assert.Equal((short)20, result.SessionCount);
        Assert.Equal((short)16, result.MinAge);
        Assert.Equal("Beginners", result.TargetAudience);
    }
}
