using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class PostSearchAndFilterTests
{
    private readonly Mock<IPostRepository> _mockPostRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<ILocationRepository> _mockLocationRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly PostService _postService;

    public PostSearchAndFilterTests()
    {
        _mockPostRepo = new Mock<IPostRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockLocationRepo = new Mock<ILocationRepository>();
        _mockUow = new Mock<IUnitOfWork>();

        _postService = new PostService(
            _mockPostRepo.Object,
            _mockUserRepo.Object,
            _mockLocationRepo.Object,
            _mockUow.Object);
    }

    [Fact]
    public async Task GetPostsAsync_DefaultFeed_WhenNoKeywordAndNoFilter_PassesNullsToRepository()
    {
        // Arrange
        var query = new GetPostsQueryDto
        {
            PageNumber = 1,
            PageSize = 10,
            Keyword = null,
            SearchTerm = null,
            PostType = null,
            LocationId = null
        };

        var posts = new List<Post>
        {
            new() { Id = Guid.NewGuid(), Content = "Hello FitSocial", PostType = "Normal", CreatedAt = DateTime.UtcNow }
        };

        _mockPostRepo.Setup(r => r.GetPagedPostsAsync(
                null,
                null,
                null,
                null,
                1,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((posts, 1));

        // Act
        var result = await _postService.GetPostsAsync(query);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(1, result.Data.TotalCount);
        _mockPostRepo.Verify(r => r.GetPagedPostsAsync(
            null, null, null, null, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPostsAsync_SearchOnly_PassesKeywordWithNullFilters()
    {
        // Arrange
        var query = new GetPostsQueryDto
        {
            PageNumber = 1,
            PageSize = 10,
            SearchTerm = "  gym workout  ",
            PostType = null,
            LocationId = null
        };

        var posts = new List<Post>
        {
            new() { Id = Guid.NewGuid(), Content = "Great gym workout today", PostType = "Normal", CreatedAt = DateTime.UtcNow }
        };

        _mockPostRepo.Setup(r => r.GetPagedPostsAsync(
                null,
                null,
                null,
                "gym workout",
                1,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((posts, 1));

        // Act
        var result = await _postService.GetPostsAsync(query);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("Great gym workout today", result.Data.Items[0].Content);
        _mockPostRepo.Verify(r => r.GetPagedPostsAsync(
            null, null, null, "gym workout", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPostsAsync_FilterOnly_PassesNormalizedPostTypeAndLocationWithoutKeyword()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var query = new GetPostsQueryDto
        {
            PageNumber = 1,
            PageSize = 10,
            Keyword = null,
            PostType = "Find Coach",
            LocationId = locationId
        };

        _mockLocationRepo.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location { LocationId = locationId, LocationName = "Downtown Gym" });

        var posts = new List<Post>
        {
            new() { Id = Guid.NewGuid(), Content = "Looking for coach", PostType = "FindCoach", LocationId = locationId, CreatedAt = DateTime.UtcNow }
        };

        _mockPostRepo.Setup(r => r.GetPagedPostsAsync(
                "FindCoach",
                locationId,
                null,
                null,
                1,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((posts, 1));

        // Act
        var result = await _postService.GetPostsAsync(query);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("FindCoach", result.Data.Items[0].PostType);
        _mockPostRepo.Verify(r => r.GetPagedPostsAsync(
            "FindCoach", locationId, null, null, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPostsAsync_SearchAndFilterCombined_PassesBothKeywordAndFiltersSimultaneously()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var query = new GetPostsQueryDto
        {
            PageNumber = 1,
            PageSize = 10,
            SearchTerm = "calisthenics",
            PostType = "Find Trainee",
            LocationId = locationId
        };

        _mockLocationRepo.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location { LocationId = locationId, LocationName = "District 7 Hub" });

        var posts = new List<Post>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Content = "Coaching calisthenics trainees",
                PostType = "FindTrainee",
                LocationId = locationId,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockPostRepo.Setup(r => r.GetPagedPostsAsync(
                "FindTrainee",
                locationId,
                null,
                "calisthenics",
                1,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((posts, 1));

        // Act
        var result = await _postService.GetPostsAsync(query);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("FindTrainee", result.Data.Items[0].PostType);
        Assert.Equal(locationId, result.Data.Items[0].LocationId);
        _mockPostRepo.Verify(r => r.GetPagedPostsAsync(
            "FindTrainee", locationId, null, "calisthenics", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPostsAsync_InvalidPostType_ThrowsValidationException()
    {
        // Arrange
        var query = new GetPostsQueryDto
        {
            PostType = "InvalidType123"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _postService.GetPostsAsync(query));
        Assert.Contains("Invalid PostType", ex.Message);
    }

    [Fact]
    public async Task GetPostsAsync_InvalidLocationId_ThrowsValidationExceptionWhenEmptyGuid()
    {
        // Arrange
        var query = new GetPostsQueryDto
        {
            LocationId = Guid.Empty
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _postService.GetPostsAsync(query));
        Assert.Contains("LocationId cannot be empty GUID", ex.Message);
    }

    [Fact]
    public async Task GetPostsAsync_NonExistentLocation_ThrowsNotFoundException()
    {
        // Arrange
        var missingId = Guid.NewGuid();
        var query = new GetPostsQueryDto
        {
            LocationId = missingId
        };

        _mockLocationRepo.Setup(l => l.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _postService.GetPostsAsync(query));
        Assert.Contains("does not exist", ex.Message);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    [InlineData(1, 51)]
    public async Task GetPostsAsync_PaginationLimits_ThrowsValidationException(int pageNumber, int pageSize)
    {
        // Arrange
        var query = new GetPostsQueryDto
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _postService.GetPostsAsync(query));
    }
}
