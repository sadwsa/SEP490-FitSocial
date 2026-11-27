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

    private static Post CreatePost(
        Guid? id = null,
        string content = "Morning workout routine!",
        string postType = "Normal",
        Guid? locationId = null)
    {
        var pId = id ?? Guid.NewGuid();
        return new Post
        {
            Id = pId,
            AuthorId = Guid.NewGuid(),
            Content = content,
            PostType = postType,
            LocationId = locationId,
            CreatedAt = DateTime.UtcNow,
            Comments = new List<Comment>(),
            PostInteractions = new List<PostInteraction>(),
            PostMedia = new List<PostMedium>()
        };
    }

    #region Normal Cases (N)

    /// <summary>
    /// UTCID01 - GetPostsAsync_DefaultFeed_ReturnsPagedPostsSuccessfully
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Keyword: empty/null
    /// - PostType: empty/null
    /// - LocationId: empty/null
    /// - Location: empty/null
    /// - DatabaseResults: Found
    /// Expected: Return ApiResponseDto<PagedResultDto<PostDto>> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPostsAsync_DefaultFeed_ReturnsPagedPostsSuccessfully()
    {
        // Arrange
        var query = new GetPostsQueryDto();
        var posts = new List<Post>
        {
            CreatePost(content: "Morning workout routine!"),
            CreatePost(content: "Evening cardio session")
        };

        _mockPostRepo.Setup(r => r.GetPagedPostsAsync(
                null, null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((posts, 2));

        // Act
        var result = await _postService.GetPostsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Posts feed retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.TotalItems);
        Assert.Equal(2, result.Data.Items.Count);
        _mockPostRepo.Verify(r => r.GetPagedPostsAsync(
            null, null, null, null, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID02 - GetPostsAsync_SearchOnly_FiltersByEffectiveKeyword
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Keyword: "gym workout"
    /// - PostType: empty/null
    /// - LocationId: empty/null
    /// - Location: empty/null
    /// - DatabaseResults: Found
    /// Expected: Return ApiResponseDto<PagedResultDto<PostDto>> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPostsAsync_SearchOnly_FiltersByEffectiveKeyword()
    {
        // Arrange
        var query = new GetPostsQueryDto
        {
            SearchTerm = "  gym workout  "
        };
        var posts = new List<Post>
        {
            CreatePost(content: "Great gym workout today")
        };

        _mockPostRepo.Setup(r => r.GetPagedPostsAsync(
                null, null, null, "gym workout", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((posts, 1));

        // Act
        var result = await _postService.GetPostsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("Great gym workout today", result.Data.Items[0].Content);
        _mockPostRepo.Verify(r => r.GetPagedPostsAsync(
            null, null, null, "gym workout", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID03 - GetPostsAsync_FilterByPostType_NormalizesAndFiltersSuccessfully
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Keyword: empty/null
    /// - PostType: "FindCoach"
    /// - LocationId: empty/null
    /// - Location: empty/null
    /// - DatabaseResults: Found
    /// Expected: Return ApiResponseDto<PagedResultDto<PostDto>> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPostsAsync_FilterByPostType_NormalizesAndFiltersSuccessfully()
    {
        // Arrange
        var query = new GetPostsQueryDto
        {
            PostType = "FindCoach"
        };
        var posts = new List<Post>
        {
            CreatePost(content: "Looking for coach", postType: "FindCoach")
        };

        _mockPostRepo.Setup(r => r.GetPagedPostsAsync(
                "FindCoach", null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((posts, 1));

        // Act
        var result = await _postService.GetPostsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("FindCoach", result.Data.Items[0].PostType);
        _mockPostRepo.Verify(r => r.GetPagedPostsAsync(
            "FindCoach", null, null, null, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID04 - GetPostsAsync_FilterByLocation_ValidatesAndFiltersSuccessfully
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Keyword: empty/null
    /// - PostType: empty/null
    /// - LocationId: Valid GUID
    /// - Location: Found
    /// - DatabaseResults: Found
    /// Expected: Return ApiResponseDto<PagedResultDto<PostDto>> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPostsAsync_FilterByLocation_ValidatesAndFiltersSuccessfully()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var query = new GetPostsQueryDto
        {
            LocationId = locationId
        };

        _mockLocationRepo.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location { LocationId = locationId, LocationName = "Downtown Gym" });

        var posts = new List<Post>
        {
            CreatePost(content: "Workout at Downtown Gym", locationId: locationId)
        };

        _mockPostRepo.Setup(r => r.GetPagedPostsAsync(
                null, locationId, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((posts, 1));

        // Act
        var result = await _postService.GetPostsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(locationId, result.Data.Items[0].LocationId);
        _mockLocationRepo.Verify(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()), Times.Once);
        _mockPostRepo.Verify(r => r.GetPagedPostsAsync(
            null, locationId, null, null, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID05 - GetPostsAsync_CombinedSearchAndFilter_AppliesAllCriteriaSimultaneously
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Keyword: "gym workout"
    /// - PostType: "FindTrainee"
    /// - LocationId: Valid GUID
    /// - Location: Found
    /// - DatabaseResults: Found
    /// Expected: Return ApiResponseDto<PagedResultDto<PostDto>> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPostsAsync_CombinedSearchAndFilter_AppliesAllCriteriaSimultaneously()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var query = new GetPostsQueryDto
        {
            Keyword = "gym workout",
            PostType = "FindTrainee",
            LocationId = locationId
        };

        _mockLocationRepo.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Location { LocationId = locationId, LocationName = "District 7 Hub" });

        var posts = new List<Post>
        {
            CreatePost(content: "Personal coach offering gym workout routines", postType: "FindTrainee", locationId: locationId)
        };

        _mockPostRepo.Setup(r => r.GetPagedPostsAsync(
                "FindTrainee", locationId, null, "gym workout", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((posts, 1));

        // Act
        var result = await _postService.GetPostsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("FindTrainee", result.Data.Items[0].PostType);
        Assert.Equal(locationId, result.Data.Items[0].LocationId);
        _mockLocationRepo.Verify(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()), Times.Once);
        _mockPostRepo.Verify(r => r.GetPagedPostsAsync(
            "FindTrainee", locationId, null, "gym workout", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Boundary Cases (B)

    /// <summary>
    /// UTCID06 - GetPostsAsync_EmptyResult_ReturnsEmptyPagedResultSuccessfully
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Keyword: "gym workout"
    /// - PostType: "Normal"
    /// - LocationId: empty/null
    /// - Location: empty/null
    /// - DatabaseResults: Not found
    /// Expected: Return ApiResponseDto<PagedResultDto<PostDto>> with Success=true
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPostsAsync_EmptyResult_ReturnsEmptyPagedResultSuccessfully()
    {
        // Arrange
        var query = new GetPostsQueryDto
        {
            Keyword = "gym workout",
            PostType = "Normal"
        };

        _mockPostRepo.Setup(r => r.GetPagedPostsAsync(
                "Normal", null, null, "gym workout", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Post>(), 0));

        // Act
        var result = await _postService.GetPostsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
        Assert.Equal(0, result.Data.TotalItems);
        _mockPostRepo.Verify(r => r.GetPagedPostsAsync(
            "Normal", null, null, "gym workout", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID07 - GetPostsAsync_EmptyGuidLocationId_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Keyword: empty/null
    /// - PostType: empty/null
    /// - LocationId: Guid.Empty
    /// - Location: empty/null
    /// - DatabaseResults: Not found
    /// Expected: ValidationException
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPostsAsync_EmptyGuidLocationId_ThrowsValidationException()
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

    #endregion

    #region Abnormal Cases (A)

    /// <summary>
    /// UTCID08 - GetPostsAsync_InvalidPostType_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Keyword: empty/null
    /// - PostType: "InvalidType"
    /// - LocationId: empty/null
    /// - Location: empty/null
    /// - DatabaseResults: Not found
    /// Expected: ValidationException
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPostsAsync_InvalidPostType_ThrowsValidationException()
    {
        // Arrange
        var query = new GetPostsQueryDto
        {
            PostType = "InvalidType"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _postService.GetPostsAsync(query));
        Assert.Contains("Invalid PostType", ex.Message);
        _mockPostRepo.Verify(r => r.GetPagedPostsAsync(
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID09 - GetPostsAsync_NonExistentLocation_ThrowsNotFoundException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Keyword: empty/null
    /// - PostType: empty/null
    /// - LocationId: Valid GUID
    /// - Location: Not found
    /// - DatabaseResults: Not found
    /// Expected: NotFoundException
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPostsAsync_NonExistentLocation_ThrowsNotFoundException()
    {
        // Arrange
        var missingLocationId = Guid.NewGuid();
        var query = new GetPostsQueryDto
        {
            LocationId = missingLocationId
        };

        _mockLocationRepo.Setup(l => l.GetByIdAsync(missingLocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _postService.GetPostsAsync(query));
        Assert.Contains("does not exist", ex.Message);
        _mockLocationRepo.Verify(l => l.GetByIdAsync(missingLocationId, It.IsAny<CancellationToken>()), Times.Once);
        _mockPostRepo.Verify(r => r.GetPagedPostsAsync(
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID10 - GetPostsAsync_DatabaseFailure_ThrowsException
    /// Condition:
    /// - Precondition: Database connection failure
    /// - Keyword: empty/null
    /// - PostType: empty/null
    /// - LocationId: empty/null
    /// - Location: empty/null
    /// - DatabaseResults: Not found
    /// Expected: Exception ("Database query failed")
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPostsAsync_DatabaseFailure_ThrowsException()
    {
        // Arrange
        var query = new GetPostsQueryDto();

        _mockPostRepo.Setup(r => r.GetPagedPostsAsync(
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database query failed"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() => _postService.GetPostsAsync(query));
        Assert.Equal("Database query failed", ex.Message);
    }

    /// <summary>
    /// UTCID11 - GetPostsAsync_CancelledToken_ThrowsOperationCanceledException
    /// Condition:
    /// - Precondition: Cancelled token
    /// - Keyword: empty/null
    /// - PostType: empty/null
    /// - LocationId: empty/null
    /// - Location: empty/null
    /// - DatabaseResults: Not found
    /// Expected: OperationCanceledException
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPostsAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var query = new GetPostsQueryDto();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockPostRepo.Setup(r => r.GetPagedPostsAsync(
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _postService.GetPostsAsync(query, null, cts.Token));
    }

    #endregion
}
