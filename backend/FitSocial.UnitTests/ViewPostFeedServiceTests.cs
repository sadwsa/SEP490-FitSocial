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

    /// <summary>
    /// UTCID01: Normal case - Default feed query without filters returns paged posts successfully.
    /// Conditions: Precondition = Database connected, PostType = empty/null, LocationId = empty/null, AuthorId = empty/null, Keyword = empty/null, CurrentUserId = empty/null, Posts in DB = Has posts (totalCount = 2), CancellationToken = Active, Repository = Normal execution.
    /// Confirm: Return PagedResultDto<PostDto> with Success=true.
    /// Result: Type = N, Status = Passed
    /// </summary>
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
        Assert.Equal("Posts retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);

        var data = result.Data;
        Assert.Equal(2, data.TotalItems);
        Assert.Equal(1, data.TotalPages);
        Assert.Equal(1, data.PageNumber);
        Assert.Equal(10, data.PageSize);
        Assert.Equal(2, data.Items.Count);

        // Verify post with complete relations
        var post1 = data.Items[0];
        Assert.Equal("Morning workout routine!", post1.Content);
        Assert.Equal("Normal", post1.PostType);
        Assert.Equal(authorId, post1.AuthorId);
        Assert.Equal("John Doe", post1.AuthorName);
        Assert.Equal("https://fitsocial.com/avatar.jpg", post1.AuthorAvatarUrl);
        Assert.Equal("District 1 Gym", post1.LocationName);
        Assert.Equal(1, post1.LikeCount);
        Assert.Equal(1, post1.CommentCount);
        Assert.False(post1.IsLikedByCurrentUser); // No currentUserId provided
        Assert.Single(post1.Media);
        Assert.Equal("https://fitsocial.com/img1.jpg", post1.Media[0].MediaUrl);

        // Verify post with minimal relations
        var post2 = data.Items[1];
        Assert.Equal("Evening cardio session", post2.Content);
        Assert.Null(post2.LocationName);
        Assert.Equal(0, post2.LikeCount);
        Assert.Equal(0, post2.CommentCount);
        Assert.False(post2.IsLikedByCurrentUser);
        Assert.Empty(post2.Media);

        _postRepoMock.Verify(p => p.GetPagedPostsAsync(
            null, null, null, null, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID02: Normal case - Query with all filters applied (PostType, LocationId, AuthorId, Keyword) and post liked by user.
    /// Conditions: Precondition = Database connected, PostType = "FindCoach", LocationId = Valid GUID (Found in DB), AuthorId = Valid GUID, Keyword = "cardio", CurrentUserId = Valid GUID, Posts in DB = Has 1 post (totalCount = 10), CancellationToken = Active, Repository = Normal execution.
    /// Confirm: Return PagedResultDto<PostDto> with Success=true.
    /// Result: Type = N, Status = Passed
    /// </summary>
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
            Content = "Looking for personal trainer for cardio training",
            PostType = "FindCoach",
            LocationId = locationId,
            Location = location,
            CreatedAt = DateTime.UtcNow,
            PostInteractions = new List<PostInteraction>
            {
                new() { UserId = currentUserId } // Liked by current user
            }
        };

        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        _postRepoMock.Setup(p => p.GetPagedPostsAsync(
                "FindCoach", locationId, authorId, "cardio", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Post> { post }, 10));

        // Act
        var result = await _postService.GetPostsAsync(query, currentUserId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);

        var returnedPost = result.Data.Items[0];
        Assert.True(returnedPost.IsLikedByCurrentUser);
        Assert.Equal("FindCoach", returnedPost.PostType);
        Assert.Equal("District 3 Fitness", returnedPost.LocationName);

        _locationRepoMock.Verify(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()), Times.Once);
        _postRepoMock.Verify(p => p.GetPagedPostsAsync(
            "FindCoach", locationId, authorId, "cardio", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Abnormal Cases (A)

    /// <summary>
    /// UTCID03: Abnormal case - Query contains invalid PostType string.
    /// Conditions: Precondition = Database connected, PostType = "InvalidType", LocationId = empty/null, AuthorId = empty/null, Keyword = empty/null, CurrentUserId = empty/null, Posts in DB = empty/null, CancellationToken = Active, Repository = Normal execution.
    /// Confirm: ValidationException ("Invalid PostType 'InvalidType'").
    /// Result: Type = A, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID04: Abnormal case - LocationId does not exist in database.
    /// Conditions: Precondition = Database connected, PostType = empty/null, LocationId = Non-existent GUID (Not found in DB), AuthorId = empty/null, Keyword = empty/null, CurrentUserId = empty/null, Posts in DB = empty/null, CancellationToken = Active, Repository = Normal execution.
    /// Confirm: NotFoundException ("Selected location does not exist.").
    /// Result: Type = A, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID05: Abnormal case - Repository throws database exception.
    /// Conditions: Precondition = Database connected, PostType = empty/null, LocationId = empty/null, AuthorId = empty/null, Keyword = empty/null, CurrentUserId = empty/null, Posts in DB = empty/null, CancellationToken = Active, Repository = Throws InvalidOperationException.
    /// Confirm: InvalidOperationException ("Database query failed").
    /// Result: Type = A, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID06: Abnormal case - CancellationToken is cancelled before query execution.
    /// Conditions: Precondition = Database connected, PostType = empty/null, LocationId = empty/null, AuthorId = empty/null, Keyword = empty/null, CurrentUserId = empty/null, Posts in DB = empty/null, CancellationToken = Cancelled, Repository = Normal execution.
    /// Confirm: OperationCanceledException.
    /// Result: Type = A, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID07: Boundary case - Feed has no posts, returns empty paged result.
    /// Conditions: Precondition = Database connected, PostType = empty/null, LocationId = empty/null, AuthorId = empty/null, Keyword = empty/null, CurrentUserId = empty/null, Posts in DB = empty/null, CancellationToken = Active, Repository = Normal execution.
    /// Confirm: Return PagedResultDto<PostDto> with Success=true.
    /// Result: Type = B, Status = Passed
    /// </summary>
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
