using Microsoft.AspNetCore.SignalR;

namespace FitSocial.API.Hubs;

/// <summary>
/// SignalR Hub for real-time post lifecycle events (e.g. PostDeleted).
/// Clients connect to listen for real-time updates without polling.
/// </summary>
public class PostHub : Hub
{
    // Clients only receive events broadcast by the server.
}
