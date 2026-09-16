using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FitSocial.API.Hubs;
using FitSocial.Application.DTOs.Conversations;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace FitSocial.API.Services;

public class ChatRealtimeNotifier : IChatRealtimeNotifier
{
    private readonly IHubContext<ChatHub> _hubContext;

    public ChatRealtimeNotifier(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastMessageAsync(List<Guid> participantUserIds, MessageDto message)
    {
        if (participantUserIds == null || !participantUserIds.Any())
        {
            return;
        }

        var groupNames = participantUserIds.Select(id => $"User_{id}").ToList();
        await _hubContext.Clients.Groups(groupNames).SendAsync("ReceiveMessage", message);
    }
}
