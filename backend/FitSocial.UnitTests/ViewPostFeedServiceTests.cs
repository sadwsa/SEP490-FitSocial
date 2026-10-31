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

public class ViewPostFeedServiceTests
{
    private readonly Mock<IPostRepository> _postRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<ILocationRepository> _locationRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly PostService _postService;

    public ViewPostFeedServiceTests()
    {
        _postRepoMock = new Mock<IPostRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _locationRepoMock = new Mock<ILocationRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _postService = new PostService(
            _postRepoMock.Object,
            _userRepoMock.Object,
            _locationRepoMock.Object,
            _unitOfWorkMock.Object);
    }

    #region Normal Cases (N)

    [Fact]
    public async Task GetPostsAsync_WhenDefaultFeedWithoutFilters_ReturnsPagedPostsSuccessfully()
    {
        // Arrange
        var query = new GetPostsQueryDto { PageNumber = 1, PageSize = 10 };
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        var author = new User
        {
            UserId = authorId,
            FullName = "John Doe",
            AvatarUrl = "https://fitsocial.com/avatar.jpg"
        };

        var location = new Location
        {
            LocationId = locationId,
            LocationName = "District 1 Gym",
            Address = "123 Main St"
        };

        var posts = new List<Post>
        {
            new()
            {
                Id = Guid.NewGuid(),
                AuthorId = authorId,
                Author = author,
                Content = "Morning workout routine!",
                PostType = "Normal",
                LocationId = locationId,
                Location = location,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                Comments = new List<Comment> { new() { Id = Guid.NewGuid() } },
                PostInteractions = new List<PostInteraction> { new() { UserId = Guid.NewGuid() } },
                PostMedia = new List<PostMedium>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        MediaUrl = "https://fitsocial.com/img1.jpg",
                        MediaType = "IMAGE",
                        CreatedAt = DateTime.UtcNow
                    }
                }
            },
            new()
            {
                Id = Guid.NewGuid(),
                AuthorId = authorId,
                Author = author,
                Content = "Evening cardio session",
                PostType = "Normal",
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                Comments = new List<Comment>(),
                PostInteractions = new List<PostInteraction>(),
                PostMedia = new List<PostMedium>()
            }
        };

        _postRepoMock.Setup(p => p.GetPagedPostsAsync(
                null, null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((posts, 2));

        // Act
        var result = await _postService.GetPostsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Posts feed retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal(2, result.Data.TotalItems);
        Assert.Equal(1, result.Data.PageNumber);
        Assert.Equal(10, result.Data.PageSize);

        var firstDto = result.Data.Items[0];
        Assert.Equal("John Doe", firstDto.AuthorName);
        Assert.Equal("District 1 Gym", firstDto.LocationName);
        Assert.Equal(1, firstDto.CommentCount);
        Assert.Equal(1, firstDto.LikeCount);
        Assert.Single(firstDto.Media);

        _postRepoMock.Verify(p => p.GetPagedPostsAsync(
            null, null, null, null, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPostsAsync_WhenAllFiltersAppliedAndLikedByUser_ReturnsFilteredPostsWithIsLikedTrue()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        var query = new GetPostsQueryDto
        {
            PageNumber = 1,
            PageSize = 10,
            PostType = "FindCoach",
            LocationId = locationId,
            AuthorId = authorId,
            Keyword = "cardio"
        };

        var location = new Location
        {
            LocationId = locationId,
            LocationName = "District 3 Fitness",
            Address = "456 Park Ave"
        };

        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            Content = "Looking for a cardio coach",
            PostType = "FindCoach",
            LocationId = locationId,
            Location = location,
            CreatedAt = DateTime.UtcNow,
            Comments = new List<Comment>(),
            PostInteractions = new List<PostInteraction>
            {
                new() { UserId = currentUserId }
            },
            PostMedia = new List<PostMedium>()
        };

        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        _postRepoMock.Setup(p => p.GetPagedPostsAsync(
                "FindCoach", locationId, authorId, "cardio", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Post> { post }, 1));

        // Act
        var result = await _postService.GetPostsAsync(query, currentUserId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);

        var dto = result.Data.Items[0];
        Assert.Equal("FindCoach", dto.PostType);
        Assert.Equal("District 3 Fitness", dto.LocationName);
        Assert.True(dto.IsLikedByCurrentUser);

        _locationRepoMock.Verify(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()), Times.Once);
        _postRepoMock.Verify(p => p.GetPagedPostsAsync(
            "FindCoach", locationId, authorId, "cardio", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPostsAsync_WhenMinimumPaginationBoundary_ReturnsSinglePostInPage()
    {
        // Arrange
        var query = new GetPostsQueryDto { PageNumber = 1, PageSize = 1 };
        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = "Single post on page",
            PostType = "Normal",
            CreatedAt = DateTime.UtcNow
        };

        _postRepoMock.Setup(p => p.GetPagedPostsAsync(
                null, null, null, null, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Post> { post }, 10));

        // Act
        var result = await _postService.GetPostsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(10, result.Data.TotalItems);
        Assert.Equal(10, result.Data.TotalPages);

        _postRepoMock.Verify(p => p.GetPagedPostsAsync(
            null, null, null, null, 1, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact]
    public async Task GetPostsAsync_WhenInvalidPostType_ThrowsValidationException()
    {
        // Arrange
        var query = new GetPostsQueryDto { PostType = "InvalidType" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _postService.GetPostsAsync(query));
        Assert.Contains("Invalid PostType 'InvalidType'", ex.Message);

        _postRepoMock.Verify(p => p.GetPagedPostsAsync(
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPostsAsync_WhenLocationDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentLocationId = Guid.NewGuid();
        var query = new GetPostsQueryDto { LocationId = nonExistentLocationId };

        _locationRepoMock.Setup(l => l.GetByIdAsync(nonExistentLocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _postService.GetPostsAsync(query));
        Assert.Contains($"Selected location with ID '{nonExistentLocationId}' does not exist.", ex.Message);

        _locationRepoMock.Verify(l => l.GetByIdAsync(nonExistentLocationId, It.IsAny<CancellationToken>()), Times.Once);
        _postRepoMock.Verify(p => p.GetPagedPostsAsync(
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPostsAsync_WhenRepositoryThrowsException_PropagatesException()
    {
        // Arrange
        var query = new GetPostsQueryDto();
        _postRepoMock.Setup(p => p.GetPagedPostsAsync(
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database query failed"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _postService.GetPostsAsync(query));
        Assert.Equal("Database query failed", ex.Message);
    }

    [Fact]
    public async Task GetPostsAsync_WhenCancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var query = new GetPostsQueryDto();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _postRepoMock.Setup(p => p.GetPagedPostsAsync(
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _postService.GetPostsAsync(query, null, cts.Token));
    }

    #endregion

    #region Boundary Cases (B)

    [Fact]
    public async Task GetPostsAsync_WhenPageNumberLessThanOne_ThrowsValidationException()
    {
        // Arrange
        var query = new GetPostsQueryDto { PageNumber = 0, PageSize = 10 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _postService.GetPostsAsync(query));
        Assert.Equal("PageNumber must be greater than or equal to 1.", ex.Message);
    }

    [Fact]
    public async Task GetPostsAsync_WhenPageSizeLessThanOne_ThrowsValidationException()
    {
        // Arrange
        var query = new GetPostsQueryDto { PageNumber = 1, PageSize = 0 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _postService.GetPostsAsync(query));
        Assert.Equal("PageSize must be greater than or equal to 1.", ex.Message);
    }

    [Fact]
    public async Task GetPostsAsync_WhenPageSizeExceedsMaximumFifty_ThrowsValidationException()
    {
        // Arrange
        var query = new GetPostsQueryDto { PageNumber = 1, PageSize = 51 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _postService.GetPostsAsync(query));
        Assert.Equal("PageSize must not exceed 50 items per page.", ex.Message);
    }

    [Fact]
    public async Task GetPostsAsync_WhenLocationIdIsEmptyGuid_ThrowsValidationException()
    {
        // Arrange
        var query = new GetPostsQueryDto { LocationId = Guid.Empty };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _postService.GetPostsAsync(query));
        Assert.Equal("LocationId cannot be empty GUID.", ex.Message);
    }

    [Fact]
    public async Task GetPostsAsync_WhenFeedIsEmpty_ReturnsEmptyPagedResult()
    {
        // Arrange
        var query = new GetPostsQueryDto { PageNumber = 1, PageSize = 10 };
        _postRepoMock.Setup(p => p.GetPagedPostsAsync(
                null, null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Post>(), 0));

        // Act
        var result = await _postService.GetPostsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
        Assert.Equal(0, result.Data.TotalItems);
        Assert.Equal(0, result.Data.TotalPages);

        _postRepoMock.Verify(p => p.GetPagedPostsAsync(
            null, null, null, null, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}

