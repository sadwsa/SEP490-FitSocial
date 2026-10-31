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

public class CreatePostServiceTests
{
    private readonly Mock<IPostRepository> _postRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<ILocationRepository> _locationRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly PostService _postService;

    public CreatePostServiceTests()
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
    public async Task CreatePostAsync_WhenCoachCreatesPostWithContentAndMedia_ReturnsCreatedPostDto()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var author = new User
        {
            UserId = authorId,
            FullName = "John Doe",
            AvatarUrl = "https://fitsocial.com/avatar.jpg",
            IsLocked = false
        };
        var location = new Location
        {
            LocationId = locationId,
            LocationName = "District 1 Gym",
            Address = "123 Gym St"
        };
        var request = new CreatePostRequestDto
        {
            PostType = "FindTrainee",
            LocationId = locationId,
            Content = "Coaching session available for beginners.",
            Media = new List<CreatePostMediaDto>
            {
                new() { MediaUrl = "https://fitsocial.com/img1.jpg", MediaType = "IMAGE" }
            }
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        // Act
        var result = await _postService.CreatePostAsync(authorId, "COACH", request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Post created successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal("John Doe", result.Data.AuthorName);
        Assert.Equal("FindTrainee", result.Data.PostType);
        Assert.Equal("District 1 Gym", result.Data.LocationName);
        Assert.Equal("Coaching session available for beginners.", result.Data.Content);
        Assert.Single(result.Data.Media);

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _postRepoMock.Verify(p => p.AddAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePostAsync_WhenTraineeCreatesTextOnlyPost_ReturnsCreatedPostDto()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var author = new User
        {
            UserId = authorId,
            FullName = "John Doe",
            IsLocked = false
        };
        var location = new Location
        {
            LocationId = locationId,
            LocationName = "Downtown Gym"
        };
        var request = new CreatePostRequestDto
        {
            PostType = "Normal",
            LocationId = locationId,
            Content = "Great workout today without media.",
            Media = null
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        // Act
        var result = await _postService.CreatePostAsync(authorId, "TRAINEE", request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Great workout today without media.", result.Data.Content);
        Assert.Empty(result.Data.Media);

        _postRepoMock.Verify(p => p.AddAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePostAsync_WhenMediaOnlyPostWithoutContent_ReturnsCreatedPostDto()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var author = new User { UserId = authorId, FullName = "John Doe", IsLocked = false };
        var location = new Location { LocationId = locationId, LocationName = "District 3 Gym" };
        var request = new CreatePostRequestDto
        {
            PostType = "Normal",
            LocationId = locationId,
            Content = null,
            Media = new List<CreatePostMediaDto>
            {
                new() { MediaUrl = "https://fitsocial.com/video1.mp4", MediaType = "VIDEO" }
            }
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        // Act
        var result = await _postService.CreatePostAsync(authorId, "TRAINEE", request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Null(result.Data.Content);
        Assert.Single(result.Data.Media);
        Assert.Equal("VIDEO", result.Data.Media[0].MediaType);

        _postRepoMock.Verify(p => p.AddAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact]
    public async Task CreatePostAsync_WhenAuthorNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var request = new CreatePostRequestDto { PostType = "Normal", LocationId = Guid.NewGuid(), Content = "Hello" };

        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _postService.CreatePostAsync(authorId, "TRAINEE", request));
        Assert.Equal("Author account not found.", ex.Message);

        _postRepoMock.Verify(p => p.AddAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePostAsync_WhenAuthorIsLocked_ThrowsForbiddenException()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var lockedAuthor = new User { UserId = authorId, FullName = "John Doe", IsLocked = true };
        var request = new CreatePostRequestDto { PostType = "Normal", LocationId = Guid.NewGuid(), Content = "Hello" };

        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedAuthor);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _postService.CreatePostAsync(authorId, "TRAINEE", request));
        Assert.Equal("Your account is locked and cannot create posts.", ex.Message);
    }

    [Fact]
    public async Task CreatePostAsync_WhenTraineeSelectsFindTrainee_ThrowsForbiddenException()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var author = new User { UserId = authorId, FullName = "John Doe", IsLocked = false };
        var request = new CreatePostRequestDto
        {
            PostType = "FindTrainee",
            LocationId = Guid.NewGuid(),
            Content = "Looking for trainees"
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _postService.CreatePostAsync(authorId, "TRAINEE", request));
        Assert.Contains("Role 'TRAINEE' is not permitted to select PostType 'FindTrainee'", ex.Message);
    }

    [Fact]
    public async Task CreatePostAsync_WhenLocationDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var author = new User { UserId = authorId, FullName = "John Doe", IsLocked = false };
        var request = new CreatePostRequestDto
        {
            PostType = "Normal",
            LocationId = locationId,
            Content = "Hello from unknown location"
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _postService.CreatePostAsync(authorId, "TRAINEE", request));
        Assert.Contains($"Selected location with ID '{locationId}' does not exist.", ex.Message);
    }

    [Fact]
    public async Task CreatePostAsync_WhenMediaUrlIsEmpty_ThrowsValidationException()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var author = new User { UserId = authorId, FullName = "John Doe", IsLocked = false };
        var location = new Location { LocationId = locationId, LocationName = "Downtown" };
        var request = new CreatePostRequestDto
        {
            PostType = "Normal",
            LocationId = locationId,
            Content = "Post with broken media",
            Media = new List<CreatePostMediaDto>
            {
                new() { MediaUrl = "   ", MediaType = "IMAGE" }
            }
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _postService.CreatePostAsync(authorId, "TRAINEE", request));
        Assert.Equal("Media #1 is missing MediaUrl.", ex.Message);
    }

    [Fact]
    public async Task CreatePostAsync_WhenDatabaseCommitFails_RollsBackAndPropagatesException()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var author = new User { UserId = authorId, FullName = "John Doe", IsLocked = false };
        var location = new Location { LocationId = locationId, LocationName = "District 1 Gym" };
        var request = new CreatePostRequestDto
        {
            PostType = "Normal",
            LocationId = locationId,
            Content = "Post causing db failure"
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Transaction commit failed"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _postService.CreatePostAsync(authorId, "TRAINEE", request));
        Assert.Equal("Transaction commit failed", ex.Message);

        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePostAsync_WhenCancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var request = new CreatePostRequestDto { PostType = "Normal", LocationId = Guid.NewGuid(), Content = "Hello" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _postService.CreatePostAsync(authorId, "TRAINEE", request, cts.Token));
    }

    #endregion

    #region Boundary Cases (B)

    [Fact]
    public async Task CreatePostAsync_WhenLocationIdIsEmptyGuid_ThrowsValidationException()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var author = new User { UserId = authorId, FullName = "John Doe", IsLocked = false };
        var request = new CreatePostRequestDto
        {
            PostType = "Normal",
            LocationId = Guid.Empty,
            Content = "Post with empty location"
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _postService.CreatePostAsync(authorId, "TRAINEE", request));
        Assert.Equal("Location is required and cannot be empty.", ex.Message);
    }

    [Fact]
    public async Task CreatePostAsync_WhenNeitherContentNorMediaProvided_ThrowsValidationException()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var author = new User { UserId = authorId, FullName = "John Doe", IsLocked = false };
        var location = new Location { LocationId = locationId, LocationName = "Gym" };
        var request = new CreatePostRequestDto
        {
            PostType = "Normal",
            LocationId = locationId,
            Content = "   ",
            Media = new List<CreatePostMediaDto>()
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _postService.CreatePostAsync(authorId, "TRAINEE", request));
        Assert.Equal("Post must contain text content or at least one image/video.", ex.Message);
    }

    [Fact]
    public async Task CreatePostAsync_WhenMediaCountExceedsTen_ThrowsBusinessException()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var author = new User { UserId = authorId, FullName = "John Doe", IsLocked = false };
        var location = new Location { LocationId = locationId, LocationName = "Gym" };

        var elevenMediaItems = new List<CreatePostMediaDto>();
        for (int i = 0; i < 11; i++)
        {
            elevenMediaItems.Add(new CreatePostMediaDto
            {
                MediaUrl = $"https://fitsocial.com/img{i}.jpg",
                MediaType = "IMAGE"
            });
        }

        var request = new CreatePostRequestDto
        {
            PostType = "Normal",
            LocationId = locationId,
            Content = "Post with 11 media items",
            Media = elevenMediaItems
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _postService.CreatePostAsync(authorId, "TRAINEE", request));
        Assert.Contains("A post can contain at most 10 media items", ex.Message);
    }

    #endregion
}

