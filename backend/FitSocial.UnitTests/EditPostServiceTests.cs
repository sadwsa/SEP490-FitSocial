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

public class EditPostServiceTests
{
    private const string StandardValidContent = "Updated workout routine with new exercises.";

    private readonly Mock<IPostRepository> _postRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<ILocationRepository> _locationRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly PostService _postService;

    public EditPostServiceTests()
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
    public async Task EditPostAsync_WhenCoachUpdatesPostWithNewContentAndMediaModifications_ReturnsUpdatedPostDto()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var existingMediaId1 = Guid.NewGuid();
        var existingMediaId2 = Guid.NewGuid();

        var existingPost = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Content = "Old initial workout post.",
            PostType = "Normal",
            LocationId = locationId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2),
            PostMedia = new List<PostMedium>
            {
                new() { Id = existingMediaId1, PostId = postId, MediaUrl = "https://fitsocial.com/img1.jpg", MediaType = "IMAGE", CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new() { Id = existingMediaId2, PostId = postId, MediaUrl = "https://fitsocial.com/img2.jpg", MediaType = "IMAGE", CreatedAt = DateTime.UtcNow.AddDays(-2) }
            }
        };

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

        var request = new EditPostRequestDto
        {
            PostType = "FindTrainee",
            Content = StandardValidContent,
            LocationId = locationId,
            RemoveMediaIds = new List<Guid> { existingMediaId1 },
            NewMedia = new List<CreatePostMediaDto>
            {
                new() { MediaUrl = "https://fitsocial.com/new_img.jpg", MediaType = "IMAGE" }
            }
        };

        _postRepoMock.Setup(r => r.GetByIdWithMediaAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPost);
        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        // Act
        var result = await _postService.EditPostAsync(postId, authorId, "COACH", request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Post updated successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(postId, result.Data.Id);
        Assert.Equal(StandardValidContent, result.Data.Content);
        Assert.Equal("FindTrainee", result.Data.PostType);
        Assert.Equal("John Doe", result.Data.AuthorName);
        Assert.Equal(2, result.Data.Media.Count);

        _postRepoMock.Verify(r => r.RemoveMediaRange(It.Is<List<PostMedium>>(list => list.Count == 1 && list[0].Id == existingMediaId1)), Times.Once);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EditPostAsync_WhenTraineeUpdatesContentAndLocationWithoutMediaChanges_ReturnsUpdatedPostDto()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var oldLocationId = Guid.NewGuid();
        var newLocationId = Guid.NewGuid();
        var existingMediaId = Guid.NewGuid();

        var existingPost = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Content = "Old schedule.",
            PostType = "Normal",
            LocationId = oldLocationId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            PostMedia = new List<PostMedium>
            {
                new() { Id = existingMediaId, PostId = postId, MediaUrl = "https://fitsocial.com/img.jpg", MediaType = "IMAGE", CreatedAt = DateTime.UtcNow.AddDays(-1) }
            }
        };

        var author = new User
        {
            UserId = authorId,
            FullName = "John Doe",
            AvatarUrl = "https://fitsocial.com/avatar.jpg",
            IsLocked = false
        };

        var newLocation = new Location
        {
            LocationId = newLocationId,
            LocationName = "District 2 Gym",
            Address = "456 Fitness Way"
        };

        var request = new EditPostRequestDto
        {
            PostType = "Normal",
            Content = StandardValidContent,
            LocationId = newLocationId,
            RemoveMediaIds = new List<Guid>(),
            NewMedia = new List<CreatePostMediaDto>()
        };

        _postRepoMock.Setup(r => r.GetByIdWithMediaAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPost);
        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(newLocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newLocation);

        // Act
        var result = await _postService.EditPostAsync(postId, authorId, "TRAINEE", request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(StandardValidContent, result.Data!.Content);
        Assert.Equal(newLocationId, result.Data.LocationId);
        Assert.Equal("District 2 Gym", result.Data.LocationName);
        Assert.Single(result.Data.Media);

        _postRepoMock.Verify(r => r.RemoveMediaRange(It.IsAny<List<PostMedium>>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EditPostAsync_WhenTraineeSwitchesAllowedPostTypeToFindCoach_ReturnsUpdatedPostDto()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        var existingPost = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Content = "Initial search.",
            PostType = "Normal",
            LocationId = locationId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            PostMedia = new List<PostMedium>()
        };

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

        var request = new EditPostRequestDto
        {
            PostType = "FindCoach",
            Content = StandardValidContent,
            LocationId = locationId
        };

        _postRepoMock.Setup(r => r.GetByIdWithMediaAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPost);
        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        // Act
        var result = await _postService.EditPostAsync(postId, authorId, "TRAINEE", request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("FindCoach", result.Data!.PostType);
        Assert.Equal(StandardValidContent, result.Data.Content);

        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact]
    public async Task EditPostAsync_WhenPostDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var request = new EditPostRequestDto
        {
            PostType = "Normal",
            Content = StandardValidContent,
            LocationId = locationId
        };

        _postRepoMock.Setup(r => r.GetByIdWithMediaAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _postService.EditPostAsync(postId, authorId, "COACH", request));

        Assert.Equal($"Post with ID '{postId}' was not found.", ex.Message);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditPostAsync_WhenPostIsDeleted_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deletedPost = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Content = "Deleted post content.",
            PostType = "Normal",
            IsDeleted = true
        };

        var request = new EditPostRequestDto
        {
            PostType = "Normal",
            Content = StandardValidContent,
            LocationId = locationId
        };

        _postRepoMock.Setup(r => r.GetByIdWithMediaAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedPost);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _postService.EditPostAsync(postId, authorId, "COACH", request));

        Assert.Equal($"Post with ID '{postId}' was not found.", ex.Message);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditPostAsync_WhenCurrentUserIsNotAuthor_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        var existingPost = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Content = "Post authored by someone else.",
            PostType = "Normal",
            LocationId = locationId,
            IsDeleted = false
        };

        var request = new EditPostRequestDto
        {
            PostType = "Normal",
            Content = StandardValidContent,
            LocationId = locationId
        };

        _postRepoMock.Setup(r => r.GetByIdWithMediaAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPost);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _postService.EditPostAsync(postId, otherUserId, "TRAINEE", request));

        Assert.Equal("You do not have permission to edit this post. Only the author can edit their own post.", ex.Message);
        _userRepoMock.Verify(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditPostAsync_WhenUserAccountIsLocked_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        var existingPost = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Content = "Valid author post.",
            PostType = "Normal",
            LocationId = locationId,
            IsDeleted = false
        };

        var lockedAuthor = new User
        {
            UserId = authorId,
            FullName = "John Doe",
            IsLocked = true
        };

        var request = new EditPostRequestDto
        {
            PostType = "Normal",
            Content = StandardValidContent,
            LocationId = locationId
        };

        _postRepoMock.Setup(r => r.GetByIdWithMediaAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPost);
        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedAuthor);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _postService.EditPostAsync(postId, authorId, "COACH", request));

        Assert.Equal("Your account is locked and cannot edit posts.", ex.Message);
        _locationRepoMock.Verify(l => l.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditPostAsync_WhenTraineeSelectsDisallowedFindTraineePostType_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        var request = new EditPostRequestDto
        {
            PostType = "FindTrainee",
            Content = StandardValidContent,
            LocationId = locationId
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _postService.EditPostAsync(postId, authorId, "TRAINEE", request));

        Assert.Contains("Role 'TRAINEE' is not permitted to select PostType 'FindTrainee'", ex.Message);
        _postRepoMock.Verify(r => r.GetByIdWithMediaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditPostAsync_WhenLocationDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var nonexistentLocationId = Guid.NewGuid();

        var existingPost = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Content = "Existing post content.",
            PostType = "Normal",
            LocationId = Guid.NewGuid(),
            IsDeleted = false
        };

        var author = new User
        {
            UserId = authorId,
            FullName = "John Doe",
            IsLocked = false
        };

        var request = new EditPostRequestDto
        {
            PostType = "Normal",
            Content = StandardValidContent,
            LocationId = nonexistentLocationId
        };

        _postRepoMock.Setup(r => r.GetByIdWithMediaAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPost);
        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(nonexistentLocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _postService.EditPostAsync(postId, authorId, "COACH", request));

        Assert.Equal($"Selected location with ID '{nonexistentLocationId}' does not exist.", ex.Message);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditPostAsync_WhenRemoveMediaIdsContainsForeignMediaId_ThrowsBusinessException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var existingMediaId = Guid.NewGuid();
        var foreignMediaId = Guid.NewGuid();

        var existingPost = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Content = "Original post content.",
            PostType = "Normal",
            LocationId = locationId,
            IsDeleted = false,
            PostMedia = new List<PostMedium>
            {
                new() { Id = existingMediaId, PostId = postId, MediaUrl = "https://fitsocial.com/img.jpg", MediaType = "IMAGE" }
            }
        };

        var author = new User
        {
            UserId = authorId,
            FullName = "John Doe",
            IsLocked = false
        };

        var location = new Location
        {
            LocationId = locationId,
            LocationName = "District 1 Gym",
            Address = "123 Gym St"
        };

        var request = new EditPostRequestDto
        {
            PostType = "Normal",
            Content = StandardValidContent,
            LocationId = locationId,
            RemoveMediaIds = new List<Guid> { foreignMediaId }
        };

        _postRepoMock.Setup(r => r.GetByIdWithMediaAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPost);
        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => _postService.EditPostAsync(postId, authorId, "COACH", request));

        Assert.Equal($"Media item with ID '{foreignMediaId}' does not belong to this post or does not exist.", ex.Message);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditPostAsync_WhenCancellationTokenIsCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new EditPostRequestDto
        {
            PostType = "Normal",
            Content = StandardValidContent,
            LocationId = locationId
        };

        _postRepoMock.Setup(r => r.GetByIdWithMediaAsync(postId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _postService.EditPostAsync(postId, authorId, "COACH", request, cts.Token));
    }

    #endregion

    #region Boundary Cases (B)

    [Fact]
    public async Task EditPostAsync_WhenContentIsMax500Characters_ReturnsUpdatedPostDto()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var content500 = new string('A', 500);

        var existingPost = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Content = "Initial post content.",
            PostType = "Normal",
            LocationId = locationId,
            IsDeleted = false,
            PostMedia = new List<PostMedium>()
        };

        var author = new User
        {
            UserId = authorId,
            FullName = "John Doe",
            IsLocked = false
        };

        var location = new Location
        {
            LocationId = locationId,
            LocationName = "District 1 Gym",
            Address = "123 Gym St"
        };

        var request = new EditPostRequestDto
        {
            PostType = "Normal",
            Content = content500,
            LocationId = locationId
        };

        _postRepoMock.Setup(r => r.GetByIdWithMediaAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPost);
        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        // Act
        var result = await _postService.EditPostAsync(postId, authorId, "COACH", request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Post updated successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(500, result.Data.Content.Length);
        Assert.Equal(content500, result.Data.Content);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EditPostAsync_WhenTotalMediaExceedsMaxLimit_ThrowsBusinessException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        // 5 existing media items
        var existingMedia = new List<PostMedium>();
        for (int i = 0; i < 5; i++)
        {
            existingMedia.Add(new PostMedium
            {
                Id = Guid.NewGuid(),
                PostId = postId,
                MediaUrl = $"https://fitsocial.com/img{i}.jpg",
                MediaType = "IMAGE"
            });
        }

        var existingPost = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Content = "Post with 5 media items.",
            PostType = "Normal",
            LocationId = locationId,
            IsDeleted = false,
            PostMedia = existingMedia
        };

        var author = new User
        {
            UserId = authorId,
            FullName = "John Doe",
            IsLocked = false
        };

        var location = new Location
        {
            LocationId = locationId,
            LocationName = "District 1 Gym",
            Address = "123 Gym St"
        };

        // Add 6 new media items (kept 5 + new 6 = 11 > 10)
        var newMedia = new List<CreatePostMediaDto>();
        for (int i = 0; i < 6; i++)
        {
            newMedia.Add(new CreatePostMediaDto
            {
                MediaUrl = $"https://fitsocial.com/new_img{i}.jpg",
                MediaType = "IMAGE"
            });
        }

        var request = new EditPostRequestDto
        {
            PostType = "Normal",
            Content = StandardValidContent,
            LocationId = locationId,
            RemoveMediaIds = new List<Guid>(),
            NewMedia = newMedia
        };

        _postRepoMock.Setup(r => r.GetByIdWithMediaAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPost);
        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _locationRepoMock.Setup(l => l.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => _postService.EditPostAsync(postId, authorId, "COACH", request));

        Assert.Contains("A post cannot contain more than 10 media items in total", ex.Message);
        Assert.Contains("Total would be: 11.", ex.Message);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
