using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Conversations;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Infrastructure.Data;
using FitSocial.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ConversationServiceTests
{
    private readonly Mock<IChatRealtimeNotifier> _realtimeNotifierMock;
    private readonly Mock<INotificationService> _notificationServiceMock;

    public ConversationServiceTests()
    {
        _realtimeNotifierMock = new Mock<IChatRealtimeNotifier>();
        _notificationServiceMock = new Mock<INotificationService>();
    }

    private FitSocialDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FitSocialDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FitSocialDbContext(options);
    }

    [Fact]
    public async Task GetUserConversationsAsync_ReturnsOrderedConversations_ExcludesDeletedHistoryWhenNoNewMessages()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var user3Id = Guid.NewGuid();

        var user1 = new User { UserId = user1Id, FullName = "User One", Email = "u1@test.com" };
        var user2 = new User { UserId = user2Id, FullName = "User Two", Email = "u2@test.com" };
        var user3 = new User { UserId = user3Id, FullName = "User Three", Email = "u3@test.com" };
        context.Users.AddRange(user1, user2, user3);

        // Conv 1: Normal active conversation
        var conv1 = new Conversation
        {
            ConversationId = Guid.NewGuid(),
            User1Id = user1Id,
            User2Id = user2Id,
            LastMessageContent = "Hello from User 2",
            LastMessageSenderId = user2Id,
            LastMessageAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        var msg1 = new Message
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conv1.ConversationId,
            SenderId = user2Id,
            Content = "Hello from User 2",
            MessageType = "TEXT",
            CreatedAt = conv1.LastMessageAt
        };
        conv1.Messages.Add(msg1);

        // Conv 2: User1 deleted history and no new message arrived
        var conv2 = new Conversation
        {
            ConversationId = Guid.NewGuid(),
            User1Id = user1Id,
            User2Id = user3Id,
            LastMessageContent = "Old message",
            LastMessageSenderId = user3Id,
            LastMessageAt = DateTime.UtcNow.AddHours(-3),
            User1DeletedHistoryAt = DateTime.UtcNow.AddHours(-2), // Deleted AFTER last message
            CreatedAt = DateTime.UtcNow.AddHours(-4)
        };
        var msg2 = new Message
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conv2.ConversationId,
            SenderId = user3Id,
            Content = "Old message",
            MessageType = "TEXT",
            CreatedAt = conv2.LastMessageAt
        };
        conv2.Messages.Add(msg2);

        context.Conversations.AddRange(conv1, conv2);
        await context.SaveChangesAsync();

        var service = new ConversationService(context, _realtimeNotifierMock.Object, _notificationServiceMock.Object);

        // Act
        var result = await service.GetUserConversationsAsync(user1Id);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data); // Conv 2 must be hidden because history was deleted
        Assert.Equal(conv1.ConversationId, result.Data[0].ConversationId);
        Assert.Equal("User Two", result.Data[0].OtherUserName);
        Assert.Equal("Hello from User 2", result.Data[0].LastMessage);
        Assert.Equal(1, result.Data[0].UnreadCount); // 1 unread message from User 2
    }

    [Fact]
    public async Task GetUserConversationsAsync_WhenOtherUserBlocked_HidesAutoReplyAndSetsUnreadToZero()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var blockerId = Guid.NewGuid();
        var blockedId = Guid.NewGuid();

        var blocker = new User { UserId = blockerId, FullName = "Blocker User", Email = "blocker@test.com" };
        var blocked = new User { UserId = blockedId, FullName = "Blocked User", Email = "blocked@test.com" };
        context.Users.AddRange(blocker, blocked);

        var block = new UserBlock
        {
            Id = Guid.NewGuid(),
            BlockerId = blockerId,
            BlockedId = blockedId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30)
        };
        context.UserBlocks.Add(block);

        var conv = new Conversation
        {
            ConversationId = Guid.NewGuid(),
            User1Id = blockerId,
            User2Id = blockedId,
            LastMessageContent = "Sorry, I do not want to receive messages from you at the moment.",
            LastMessageSenderId = blockedId,
            LastMessageAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        // An earlier valid message
        var earlierValidMessage = new Message
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conv.ConversationId,
            SenderId = blockerId,
            Content = "Old chat before block",
            MessageType = "TEXT",
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };
        // A block auto-reply message
        var autoReplyMessage = new Message
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conv.ConversationId,
            SenderId = blockedId,
            Content = "Sorry, I do not want to receive messages from you at the moment.",
            MessageType = "AUTO",
            CreatedAt = DateTime.UtcNow
        };
        conv.Messages.Add(earlierValidMessage);
        conv.Messages.Add(autoReplyMessage);

        context.Conversations.Add(conv);
        await context.SaveChangesAsync();

        var service = new ConversationService(context, _realtimeNotifierMock.Object, _notificationServiceMock.Object);

        // Act
        var result = await service.GetUserConversationsAsync(blockerId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        var convDto = result.Data[0];
        Assert.Equal("Old chat before block", convDto.LastMessage); // Should NOT show auto-reply
        Assert.Equal(0, convDto.UnreadCount); // Should be 0 unread for blocked user
    }

    [Fact]
    public async Task GetConversationDetailAsync_ValidParticipant_ReturnsMessagesAndMarksAsRead()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var user1 = new User { UserId = user1Id, FullName = "Alice", Email = "alice@test.com" };
        var user2 = new User { UserId = user2Id, FullName = "Bob", Email = "bob@test.com" };
        context.Users.AddRange(user1, user2);

        var conv = new Conversation
        {
            ConversationId = Guid.NewGuid(),
            User1Id = user1Id,
            User2Id = user2Id,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var msg1 = new Message
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conv.ConversationId,
            SenderId = user2Id,
            Content = "Hey Alice!",
            MessageType = "TEXT",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            Sender = user2
        };
        conv.Messages.Add(msg1);
        context.Conversations.Add(conv);

        // Add an unread notification for Alice
        var notif = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user1Id,
            ActorId = user2Id,
            ReferenceId = msg1.MessageId,
            Type = "NewMessage",
            IsRead = false,
            CreatedAt = msg1.CreatedAt
        };
        context.Notifications.Add(notif);
        await context.SaveChangesAsync();

        var service = new ConversationService(context, _realtimeNotifierMock.Object, _notificationServiceMock.Object);

        // Act
        var result = await service.GetConversationDetailAsync(conv.ConversationId, user1Id);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Messages);
        Assert.Equal("Hey Alice!", result.Data.Messages[0].Content);
        Assert.False(result.Data.Messages[0].IsMine);

        // Check that conv.User1LastReadMessageId was updated
        var updatedConv = await context.Conversations.FindAsync(conv.ConversationId);
        Assert.Equal(msg1.MessageId, updatedConv?.User1LastReadMessageId);

        // Check that notification was marked as read
        var updatedNotif = await context.Notifications.FindAsync(notif.Id);
        Assert.True(updatedNotif?.IsRead);
    }

    [Fact]
    public async Task GetConversationDetailAsync_NonParticipant_ReturnsForbidden()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var strangerId = Guid.NewGuid();

        var conv = new Conversation
        {
            ConversationId = Guid.NewGuid(),
            User1Id = user1Id,
            User2Id = user2Id
        };
        context.Conversations.Add(conv);
        await context.SaveChangesAsync();

        var service = new ConversationService(context, _realtimeNotifierMock.Object, _notificationServiceMock.Object);

        // Act
        var result = await service.GetConversationDetailAsync(conv.ConversationId, strangerId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Forbidden", result.Message);
    }

    [Fact]
    public async Task SendMessageAsync_NormalMessage_SavesBroadcastsAndSendsNotification()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();

        var sender = new User { UserId = senderId, FullName = "Alice", Email = "alice@test.com" };
        var receiver = new User { UserId = receiverId, FullName = "Bob", Email = "bob@test.com" };
        context.Users.AddRange(sender, receiver);

        var conv = new Conversation
        {
            ConversationId = Guid.NewGuid(),
            User1Id = senderId,
            User2Id = receiverId,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        context.Conversations.Add(conv);
        await context.SaveChangesAsync();

        var service = new ConversationService(context, _realtimeNotifierMock.Object, _notificationServiceMock.Object);

        var request = new SendMessageRequestDto
        {
            Content = "Hello Bob!",
            MessageType = "TEXT"
        };

        // Act
        var result = await service.SendMessageAsync(conv.ConversationId, senderId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Hello Bob!", result.Data.Content);
        Assert.True(result.Data.IsMine);

        // DB verification
        var savedMessage = await context.Messages.FirstOrDefaultAsync(m => m.ConversationId == conv.ConversationId);
        Assert.NotNull(savedMessage);
        Assert.Equal("Hello Bob!", savedMessage.Content);
        Assert.Equal(senderId, savedMessage.SenderId);

        var updatedConv = await context.Conversations.FindAsync(conv.ConversationId);
        Assert.Equal("Hello Bob!", updatedConv?.LastMessageContent);
        Assert.Equal(senderId, updatedConv?.LastMessageSenderId);
        Assert.Equal(savedMessage.MessageId, updatedConv?.User1LastReadMessageId);

        // Realtime notifier verification
        _realtimeNotifierMock.Verify(n => n.BroadcastMessageAsync(
            It.Is<List<Guid>>(ids => ids.Contains(senderId) && ids.Contains(receiverId)),
            It.Is<MessageDto>(m => m.Content == "Hello Bob!")),
            Times.Once);

        // Notification service verification
        _notificationServiceMock.Verify(n => n.CreateAndSendNewMessageNotificationAsync(
            It.Is<Message>(m => m.Content == "Hello Bob!"),
            It.Is<User>(u => u.UserId == senderId),
            It.Is<List<Guid>>(ids => ids.Contains(receiverId))),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WhenBlockedByRecipient_GeneratesAutoReplyAndDoesNotNotify()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var senderId = Guid.NewGuid();
        var blockerId = Guid.NewGuid();

        var sender = new User { UserId = senderId, FullName = "Alice", Email = "alice@test.com" };
        var blocker = new User { UserId = blockerId, FullName = "Bob", Email = "bob@test.com" };
        context.Users.AddRange(sender, blocker);

        // Blocker has blocked Sender
        var block = new UserBlock
        {
            Id = Guid.NewGuid(),
            BlockerId = blockerId,
            BlockedId = senderId,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        context.UserBlocks.Add(block);

        var conv = new Conversation
        {
            ConversationId = Guid.NewGuid(),
            User1Id = senderId,
            User2Id = blockerId,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        context.Conversations.Add(conv);
        await context.SaveChangesAsync();

        var service = new ConversationService(context, _realtimeNotifierMock.Object, _notificationServiceMock.Object);

        var request = new SendMessageRequestDto
        {
            Content = "Can we talk?",
            MessageType = "TEXT"
        };

        // Act
        var result = await service.SendMessageAsync(conv.ConversationId, senderId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Can we talk?", result.Data.Content);
        Assert.Equal("BLOCK", result.Data.MessageType);
        Assert.NotNull(result.Data.AutoReply);
        Assert.Equal("AUTO", result.Data.AutoReply.MessageType);
        Assert.Contains("do not want to receive messages", result.Data.AutoReply.Content);

        // Verify that NO broadcast was sent to blocker and NO notification was created
        _realtimeNotifierMock.Verify(n => n.BroadcastMessageAsync(It.IsAny<List<Guid>>(), It.IsAny<MessageDto>()), Times.Never);
        _notificationServiceMock.Verify(n => n.CreateAndSendNewMessageNotificationAsync(It.IsAny<Message>(), It.IsAny<User>(), It.IsAny<List<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task SendMessageAsync_WhenSenderBlockedRecipient_ReturnsError()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var sender = new User { UserId = senderId, FullName = "Alice", Email = "alice@test.com" };
        var recipient = new User { UserId = recipientId, FullName = "Bob", Email = "bob@test.com" };
        context.Users.AddRange(sender, recipient);

        // Sender has blocked Recipient
        var block = new UserBlock
        {
            Id = Guid.NewGuid(),
            BlockerId = senderId,
            BlockedId = recipientId,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        context.UserBlocks.Add(block);

        var conv = new Conversation
        {
            ConversationId = Guid.NewGuid(),
            User1Id = senderId,
            User2Id = recipientId
        };
        context.Conversations.Add(conv);
        await context.SaveChangesAsync();

        var service = new ConversationService(context, _realtimeNotifierMock.Object, _notificationServiceMock.Object);

        // Act
        var result = await service.SendMessageAsync(conv.ConversationId, senderId, new SendMessageRequestDto { Content = "Test" });

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("You have blocked this user", result.Message);
    }

    [Fact]
    public async Task DeleteConversationAsync_User1_SetsUser1DeletedHistoryAt()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var conv = new Conversation
        {
            ConversationId = Guid.NewGuid(),
            User1Id = user1Id,
            User2Id = user2Id,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        context.Conversations.Add(conv);
        await context.SaveChangesAsync();

        var service = new ConversationService(context, _realtimeNotifierMock.Object, _notificationServiceMock.Object);

        // Act
        var result = await service.DeleteConversationAsync(conv.ConversationId, user1Id);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);

        var updatedConv = await context.Conversations.FindAsync(conv.ConversationId);
        Assert.NotNull(updatedConv?.User1DeletedHistoryAt);
        Assert.Null(updatedConv?.User2DeletedHistoryAt);
    }

    [Fact]
    public async Task BlockAndUnblockUserAsync_WorksCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var blockerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var conv = new Conversation
        {
            ConversationId = Guid.NewGuid(),
            User1Id = blockerId,
            User2Id = targetId
        };
        context.Conversations.Add(conv);
        await context.SaveChangesAsync();

        var service = new ConversationService(context, _realtimeNotifierMock.Object, _notificationServiceMock.Object);

        // Act 1: Block user
        var blockResult = await service.BlockUserAsync(conv.ConversationId, blockerId);

        // Assert 1
        Assert.NotNull(blockResult);
        Assert.True(blockResult.Success);
        var blockEntry = await context.UserBlocks.FirstOrDefaultAsync(b => b.BlockerId == blockerId && b.BlockedId == targetId);
        Assert.NotNull(blockEntry);

        // Act 2: Unblock user
        var unblockResult = await service.UnblockUserAsync(conv.ConversationId, blockerId);

        // Assert 2
        Assert.NotNull(unblockResult);
        Assert.True(unblockResult.Success);
        var unblockEntry = await context.UserBlocks.FirstOrDefaultAsync(b => b.BlockerId == blockerId && b.BlockedId == targetId);
        Assert.Null(unblockEntry);
    }

    [Fact]
    public async Task GetOrCreateDirectConversationAsync_CreatesNewOrReturnsExisting()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var user1 = new User { UserId = user1Id, FullName = "User 1", Email = "u1@test.com" };
        var user2 = new User { UserId = user2Id, FullName = "User 2", Email = "u2@test.com" };
        context.Users.AddRange(user1, user2);
        await context.SaveChangesAsync();

        var service = new ConversationService(context, _realtimeNotifierMock.Object, _notificationServiceMock.Object);

        // Act 1: First call creates new conversation
        var createResult = await service.GetOrCreateDirectConversationAsync(user1Id, user2Id);

        // Assert 1
        Assert.NotNull(createResult);
        Assert.True(createResult.Success);
        Assert.NotNull(createResult.Data);
        var convId = createResult.Data.ConversationId;

        // Act 2: Second call returns existing conversation
        var existingResult = await service.GetOrCreateDirectConversationAsync(user2Id, user1Id);

        // Assert 2
        Assert.NotNull(existingResult);
        Assert.True(existingResult.Success);
        Assert.Equal(convId, existingResult.Data?.ConversationId);

        var totalConvs = await context.Conversations.CountAsync();
        Assert.Equal(1, totalConvs);
    }
}
