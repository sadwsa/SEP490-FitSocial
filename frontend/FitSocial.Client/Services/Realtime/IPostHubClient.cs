using System;
using System.Threading.Tasks;

namespace FitSocial.Client.Services.Realtime;

public interface IPostHubClient : IAsyncDisposable
{
    bool IsConnected { get; }
    Task StartAsync(string? token = null);
    Task StopAsync();
    event Action<Guid>? OnPostDeleted;
}
