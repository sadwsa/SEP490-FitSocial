using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Infrastructure.Data;
using FitSocial.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class NotificationServiceTests
{
    private readonly Mock<IChatRealtimeNotifier> _realtimeNotifierMock;
    private readonly Mock<ILogger<NotificationService>> _loggerMock;

    public NotificationServiceTests()
    {
        _realtimeNotifierMock = new Mock<IChatRealtimeNotifier>();
        _loggerMock = new Mock<ILogger<NotificationService>>();
    }

    private FitSocialDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FitSocialDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FitSocialDbContext(options);
    }

    [Fact]
    public async Task CreateAndSendNewMessageNotificationAsync_CreatesNotificationAndRealtimeBroadcast()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var sender = new User { UserId = senderId, FullName = "Coach John", Email = "john@fitsocial.com" };
        var recipient = new User { UserId = recipientId, FullName = "Trainee Mike", Email = "mike@fitsocial.com" };
        context.Users.AddRange(sender, recipient);

        var convId = Guid.NewGuid();
        var message = new Message
        {
            MessageId = Guid.NewGuid(),
            ConversationId = convId,
            SenderId = senderId,
            Content = "Let's start your workout program today!",
            MessageType = "TEXT",
            CreatedAt = DateTime.UtcNow
        };
        context.Messages.Add(message);
        await context.SaveChangesAsync();

        var service = new NotificationService(context, _realtimeNotifierMock.Object, _loggerMock.Object);

        // Act
        await service.CreateAndSendNewMessageNotificationAsync(message, sender, new List<Guid> { recipientId });

        // Assert
        var savedNotif = await context.Notifications.FirstOrDefaultAsync(n => n.UserId == recipientId && n.ReferenceId == message.MessageId);
        Assert.NotNull(savedNotif);
        Assert.Equal("NewMessage", savedNotif.Type);
        Assert.Equal(senderId, savedNotif.ActorId);
        Assert.False(savedNotif.IsRead);
        Assert.Equal(message.Content, savedNotif.Description);

        // Realtime notification was pushed
        _realtimeNotifierMock.Verify(r => r.SendNotificationAsync(recipientId, It.Is<Application.DTOs.Notifications.NotificationDto>(
            d => d.SenderName == "Coach John" && d.ConversationId == convId && d.MessageId == message.MessageId)),
            Times.Once);
    }

    [Fact]
    public async Task CreateAndSendNewMessageNotificationAsync_DoesNotNotifySenderThemselves()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var senderId = Guid.NewGuid();
        var sender = new User { UserId = senderId, FullName = "Coach John", Email = "john@fitsocial.com" };
        context.Users.Add(sender);

        var message = new Message
        {
            MessageId = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            SenderId = senderId,
            Content = "Test"
        };
        context.Messages.Add(message);
        await context.SaveChangesAsync();

        var service = new NotificationService(context, _realtimeNotifierMock.Object, _loggerMock.Object);

        // Act
        await service.CreateAndSendNewMessageNotificationAsync(message, sender, new List<Guid> { senderId });

        // Assert
        var count = await context.Notifications.CountAsync();
        Assert.Equal(0, count);
        _realtimeNotifierMock.Verify(r => r.SendNotificationAsync(It.IsAny<Guid>(), It.IsAny<Application.DTOs.Notifications.NotificationDto>()), Times.Never);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_RetrievesNotificationsAndMapsMessageCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var senderId = Guid.NewGuid();

        var sender = new User { UserId = senderId, FullName = "Sender Guy", Email = "sender@test.com" };
        context.Users.Add(sender);

        var msg = new Message
        {
            MessageId = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            SenderId = senderId,
            Content = "Message preview check",
            CreatedAt = DateTime.UtcNow,
            Sender = sender
        };
        context.Messages.Add(msg);

        var notif = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ActorId = senderId,
            ReferenceId = msg.MessageId,
            Type = "NewMessage",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        context.Notifications.Add(notif);
        await context.SaveChangesAsync();

        var service = new NotificationService(context, _realtimeNotifierMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GetUserNotificationsAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        var item = result.Data[0];
        Assert.Equal(notif.Id, item.NotificationId);
        Assert.Equal(msg.MessageId, item.MessageId);
        Assert.Equal(msg.ConversationId, item.ConversationId);
        Assert.Equal("Sender Guy", item.SenderName);
        Assert.Equal("Message preview check", item.MessagePreview);
    }

    [Fact]
    public async Task MarkAsReadAsync_And_MarkAllAsReadAsync_UpdatesDatabase()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        var notif1 = new Notification { Id = Guid.NewGuid(), UserId = userId, IsRead = false };
        var notif2 = new Notification { Id = Guid.NewGuid(), UserId = userId, IsRead = false };
        context.Notifications.AddRange(notif1, notif2);
        await context.SaveChangesAsync();

        var service = new NotificationService(context, _realtimeNotifierMock.Object, _loggerMock.Object);

        // Act 1: Mark single as read
        var readResult = await service.MarkAsReadAsync(notif1.Id, userId);
        Assert.True(readResult.Success);
        var updated1 = await context.Notifications.FindAsync(notif1.Id);
        Assert.True(updated1?.IsRead);

        // Act 2: Mark all as read
        var readAllResult = await service.MarkAllAsReadAsync(userId);
        Assert.True(readAllResult.Success);
        var updated2 = await context.Notifications.FindAsync(notif2.Id);
        Assert.True(updated2?.IsRead);
    }
}
