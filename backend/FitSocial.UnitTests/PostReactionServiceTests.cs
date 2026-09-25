using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class PostReactionServiceTests
{
    private readonly Mock<IPostReactionRepository> _reactionRepoMock;
    private readonly Mock<IPostRepository> _postRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly PostReactionService _service;

    public PostReactionServiceTests()
    {
        _reactionRepoMock = new Mock<IPostReactionRepository>();
        _postRepoMock = new Mock<IPostRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _uowMock = new Mock<IUnitOfWork>();
        _service = new PostReactionService(
            _reactionRepoMock.Object,
            _postRepoMock.Object,
            _userRepoMock.Object,
            _uowMock.Object);
    }

    [Fact]
    public async Task ToggleReaction_WhenNotLikedYet_LikesPost_IncreasesCount()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userId, RoleCode = RoleConstants.Trainee, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = postId, IsDeleted = false });
        _reactionRepoMock.Setup(r => r.GetByUserAndPostAsync(postId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostInteraction?)null);
        _reactionRepoMock.Setup(r => r.GetReactionCountAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.ToggleReactionAsync(postId, userId, RoleConstants.Trainee);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(postId, result.Data.PostId);
        Assert.True(result.Data.IsLiked);
        Assert.Equal(1, result.Data.LikeCount);
        Assert.Equal("Post liked successfully.", result.Message);

        _reactionRepoMock.Verify(r => r.AddAsync(It.Is<PostInteraction>(pi => pi.PostId == postId && pi.UserId == userId), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleReaction_WhenAlreadyLiked_UnlikesPost_DecreasesCount()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var existingInteraction = new PostInteraction { PostId = postId, UserId = userId };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userId, RoleCode = RoleConstants.Coach, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = postId, IsDeleted = false });
        _reactionRepoMock.Setup(r => r.GetByUserAndPostAsync(postId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingInteraction);
        _reactionRepoMock.Setup(r => r.GetReactionCountAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.ToggleReactionAsync(postId, userId, RoleConstants.Coach);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(postId, result.Data.PostId);
        Assert.False(result.Data.IsLiked);
        Assert.Equal(0, result.Data.LikeCount);
        Assert.Equal("Post unliked successfully.", result.Message);

        _reactionRepoMock.Verify(r => r.Remove(existingInteraction), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleReaction_Twice_ResultsInToggleBehaviorNotDuplicate()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userId, RoleCode = RoleConstants.Trainee, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = postId, IsDeleted = false });

        PostInteraction? storedInteraction = null;

        _reactionRepoMock.Setup(r => r.GetByUserAndPostAsync(postId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => storedInteraction);

        _reactionRepoMock.Setup(r => r.AddAsync(It.IsAny<PostInteraction>(), It.IsAny<CancellationToken>()))
            .Callback<PostInteraction, CancellationToken>((pi, _) => storedInteraction = pi)
            .Returns(Task.CompletedTask);

        _reactionRepoMock.Setup(r => r.Remove(It.IsAny<PostInteraction>()))
            .Callback<PostInteraction>(_ => storedInteraction = null);

        _reactionRepoMock.Setup(r => r.GetReactionCountAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => storedInteraction == null ? 0 : 1);

        // First click -> Like
        var firstResult = await _service.ToggleReactionAsync(postId, userId, RoleConstants.Trainee);
        Assert.True(firstResult.Data?.IsLiked);
        Assert.Equal(1, firstResult.Data?.LikeCount);
        Assert.NotNull(storedInteraction);

        // Second click -> Unlike (toggled off)
        var secondResult = await _service.ToggleReactionAsync(postId, userId, RoleConstants.Trainee);
        Assert.False(secondResult.Data?.IsLiked);
        Assert.Equal(0, secondResult.Data?.LikeCount);
        Assert.Null(storedInteraction);
    }

    [Fact]
    public async Task ToggleReaction_NonExistingPost_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userId, RoleCode = RoleConstants.Trainee, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ToggleReactionAsync(postId, userId, RoleConstants.Trainee));
    }

    [Fact]
    public async Task ToggleReaction_DeletedPost_ThrowsBusinessException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userId, RoleCode = RoleConstants.Trainee, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = postId, IsDeleted = true });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ToggleReactionAsync(postId, userId, RoleConstants.Trainee));
        Assert.Equal("Cannot react to a post that has been deleted.", ex.Message);
    }

    [Fact]
    public async Task ToggleReaction_UnauthenticatedUser_ThrowsValidationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ToggleReactionAsync(Guid.NewGuid(), Guid.Empty, RoleConstants.Trainee));
    }

    [Fact]
    public async Task ToggleReaction_LockedUser_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userId, RoleCode = RoleConstants.Trainee, IsLocked = true });

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ToggleReactionAsync(postId, userId, RoleConstants.Trainee));
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("STAFF")]
    [InlineData("GUEST")]
    [InlineData("")]
    public async Task ToggleReaction_InvalidRole_ThrowsForbiddenException(string role)
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userId, RoleCode = role, IsLocked = false });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ToggleReactionAsync(postId, userId, role));
        Assert.Equal("Only Trainees and Coaches can react to posts.", ex.Message);
    }
}
