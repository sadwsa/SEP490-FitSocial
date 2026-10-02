using System;
using System.Threading.Tasks;

namespace FitSocial.Application.Interfaces;

/// <summary>
/// Abstraction for broadcasting post realtime lifecycle events (e.g. via SignalR).
/// </summary>
public interface IPostRealtimeNotifier
{
    /// <summary>
    /// Broadcasts to all connected clients that a post has been deleted.
    /// </summary>
    Task NotifyPostDeletedAsync(Guid postId);
}
