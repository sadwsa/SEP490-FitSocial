using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Hubs;
using FitSocial.API.Services;
using FitSocial.Application.DTOs.Notifications;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Infrastructure.Data;
using FitSocial.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class NotificationClassificationTests
{
    private FitSocialDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FitSocialDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FitSocialDbContext(options);
    }

    [Fact]
    public void NotificationDto_CategoryProperty_ReturnsMessage_ForNewMessageType()
    {
        var dto1 = new NotificationDto { Type = "NewMessage" };
        var dto2 = new NotificationDto { Type = "NEW_MESSAGE" };
        var dto3 = new NotificationDto { Type = "COACH_CERTIFICATE_APPROVED" };
        var dto4 = new NotificationDto { Type = "REFUND_APPROVED" };

        Assert.Equal("MESSAGE", dto1.Category);
        Assert.Equal("MESSAGE", dto2.Category);
        Assert.Equal("NOTIFICATION", dto3.Category);
        Assert.Equal("NOTIFICATION", dto4.Category);
    }

    [Fact]
    public async Task NotificationService_CategoryFilter_ReturnsOnlyMessages_WhenCategoryIsMessage()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        context.Notifications.AddRange(
            new Notification { Id = Guid.NewGuid(), UserId = userId, Type = "NewMessage", Description = "Hello", CreatedAt = DateTime.UtcNow },
            new Notification { Id = Guid.NewGuid(), UserId = userId, Type = "COACH_CERTIFICATE_APPROVED", Description = "Approved", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var mockChatNotifier = new Mock<IChatRealtimeNotifier>();
        var mockLogger = new Mock<ILogger<NotificationService>>();
        var service = new NotificationService(context, mockChatNotifier.Object, mockLogger.Object);

        // Act
        var result = await service.GetUserNotificationsAsync(userId, "MESSAGE");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("NewMessage", result.Data![0].Type);
        Assert.Equal("MESSAGE", result.Data![0].Category);
    }

    [Fact]
    public async Task NotificationService_CategoryFilter_ReturnsOnlyNotifications_WhenCategoryIsNotification()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        context.Notifications.AddRange(
            new Notification { Id = Guid.NewGuid(), UserId = userId, Type = "NewMessage", Description = "Hello", CreatedAt = DateTime.UtcNow },
            new Notification { Id = Guid.NewGuid(), UserId = userId, Type = "REFUND_APPROVED", Description = "Refund processed", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var mockChatNotifier = new Mock<IChatRealtimeNotifier>();
        var mockLogger = new Mock<ILogger<NotificationService>>();
        var service = new NotificationService(context, mockChatNotifier.Object, mockLogger.Object);

        // Act
        var result = await service.GetUserNotificationsAsync(userId, "NOTIFICATION");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("REFUND_APPROVED", result.Data![0].Type);
        Assert.Equal("NOTIFICATION", result.Data![0].Category);
    }

    [Fact]
    public async Task ChatRealtimeNotifier_RoutesNewMessageToChatHub_AndOtherTypesToNotificationHub()
    {
        // Arrange
        var mockChatClients = new Mock<IHubClients>();
        var mockChatGroup = new Mock<IClientProxy>();
        mockChatClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockChatGroup.Object);

        var mockChatHubContext = new Mock<IHubContext<ChatHub>>();
        mockChatHubContext.Setup(c => c.Clients).Returns(mockChatClients.Object);

        var mockNotifClients = new Mock<IHubClients>();
        var mockNotifGroup = new Mock<IClientProxy>();
        mockNotifClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockNotifGroup.Object);

        var mockNotifHubContext = new Mock<IHubContext<NotificationHub>>();
        mockNotifHubContext.Setup(c => c.Clients).Returns(mockNotifClients.Object);

        var notifier = new ChatRealtimeNotifier(mockChatHubContext.Object, mockNotifHubContext.Object);
        var targetUserId = Guid.NewGuid();

        // Act 1: Send NewMessage
        var messageDto = new NotificationDto { Type = "NewMessage", MessagePreview = "Chat message" };
        await notifier.SendNotificationAsync(targetUserId, messageDto);

        // Act 2: Send Application Notification
        var appNotifDto = new NotificationDto { Type = "COACH_CERTIFICATE_APPROVED", MessagePreview = "Certificate approved" };
        await notifier.SendNotificationAsync(targetUserId, appNotifDto);

        // Assert
        // ChatHub should receive only NewMessage
        mockChatGroup.Verify(g => g.SendCoreAsync(
            "ReceiveNotification",
            It.Is<object[]>(args => args.Length == 1 && ((NotificationDto)args[0]).Type == "NewMessage"),
            default), Times.Once);

        // NotificationHub should receive only COACH_CERTIFICATE_APPROVED
        mockNotifGroup.Verify(g => g.SendCoreAsync(
            "ReceiveNotification",
            It.Is<object[]>(args => args.Length == 2 && (string)args[0] == "COACH_CERTIFICATE_APPROVED"),
            default), Times.Once);
    }
}
