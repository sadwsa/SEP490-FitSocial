using System;
using System.Threading.Tasks;
using FitSocial.API.Hubs;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace FitSocial.API.Services;

/// <summary>
/// SignalR-based implementation of IPostRealtimeNotifier.
/// Broadcasts PostDeleted events to all active clients.
/// </summary>
public class PostRealtimeNotifier : IPostRealtimeNotifier
{
    private readonly IHubContext<PostHub> _hubContext;
    private readonly ILogger<PostRealtimeNotifier>? _logger;

    public PostRealtimeNotifier(IHubContext<PostHub> hubContext, ILogger<PostRealtimeNotifier>? logger = null)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyPostDeletedAsync(Guid postId)
    {
        if (postId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _hubContext.Clients.All.SendAsync("PostDeleted", postId);
            _logger?.LogInformation("[PostRealtime] Successfully broadcast PostDeleted for PostId: {PostId}", postId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[PostRealtime] Failed to broadcast PostDeleted for PostId: {PostId}", postId);
        }
    }
}
