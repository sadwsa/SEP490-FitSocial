using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Client.Models.Toast;

namespace FitSocial.Client.Services.Toast;

public class ToastService : IToastService, IDisposable
{
    private const int MaxVisibleToasts = 5;
    private const int AnimationDurationMs = 260;

    private readonly object _lock = new();
    private readonly List<ToastNotification> _toasts = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _timers = new();

    public IReadOnlyList<ToastNotification> Toasts
    {
        get
        {
            lock (_lock)
            {
                return _toasts.ToList();
            }
        }
    }

    public event Action? OnChange;

    public Guid ShowSuccess(string message, string? title = null, int durationMs = 4000)
    {
        return Show(new ToastNotification
        {
            Type = ToastType.Success,
            Title = string.IsNullOrWhiteSpace(title) ? "Success" : title.Trim(),
            Message = message,
            DurationMs = durationMs
        });
    }

    public Guid ShowError(string message, string? title = null, int durationMs = 5000)
    {
        return Show(new ToastNotification
        {
            Type = ToastType.Error,
            Title = string.IsNullOrWhiteSpace(title) ? "Error" : title.Trim(),
            Message = message,
            DurationMs = durationMs
        });
    }

    public Guid ShowWarning(string message, string? title = null, int durationMs = 4500)
    {
        return Show(new ToastNotification
        {
            Type = ToastType.Warning,
            Title = string.IsNullOrWhiteSpace(title) ? "Warning" : title.Trim(),
            Message = message,
            DurationMs = durationMs
        });
    }

    public Guid ShowInfo(string message, string? title = null, int durationMs = 4000)
    {
        return Show(new ToastNotification
        {
            Type = ToastType.Info,
            Title = string.IsNullOrWhiteSpace(title) ? "Information" : title.Trim(),
            Message = message,
            DurationMs = durationMs
        });
    }

    public Guid Show(ToastType type, string message, string? title = null, int durationMs = 4000)
    {
        return Show(new ToastNotification
        {
            Type = type,
            Title = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim(),
            Message = message,
            DurationMs = durationMs
        });
    }

    public Guid Show(ToastNotification toast)
    {
        ArgumentNullException.ThrowIfNull(toast);

        if (string.IsNullOrWhiteSpace(toast.Title))
        {
            toast.Title = toast.Type switch
            {
                ToastType.Success => "Success",
                ToastType.Error => "Error",
                ToastType.Warning => "Warning",
                ToastType.Info => "Information",
                _ => "Notification"
            };
        }

        lock (_lock)
        {
            // Nếu đã vượt quá giới hạn hiển thị, loại bỏ Toast cũ nhất
            while (_toasts.Count >= MaxVisibleToasts)
            {
                var oldest = _toasts.Last();
                if (_timers.TryGetValue(oldest.Id, out var oldestCts))
                {
                    oldestCts.Cancel();
                    oldestCts.Dispose();
                    _timers.Remove(oldest.Id);
                }
                _toasts.Remove(oldest);
            }

            // Chèn Toast mới lên đầu danh sách
            _toasts.Insert(0, toast);

            if (toast.DurationMs > 0)
            {
                var cts = new CancellationTokenSource();
                _timers[toast.Id] = cts;

                _ = ScheduleAutoDismissAsync(toast.Id, toast.DurationMs, cts.Token);
            }
        }

        NotifyStateChanged();
        return toast.Id;
    }

    private async Task ScheduleAutoDismissAsync(Guid id, int delayMs, CancellationToken token)
    {
        try
        {
            await Task.Delay(delayMs, token);
            if (!token.IsCancellationRequested)
            {
                Dismiss(id);
            }
        }
        catch (TaskCanceledException)
        {
            // Hủy timer bình thường khi Toast bị đóng thủ công
        }
    }

    public void Dismiss(Guid id)
    {
        ToastNotification? target = null;

        lock (_lock)
        {
            if (_timers.TryGetValue(id, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _timers.Remove(id);
            }

            target = _toasts.FirstOrDefault(t => t.Id == id);
            if (target == null || target.IsClosing)
            {
                return;
            }

            // Đánh dấu IsClosing để chạy animation CSS fade-out / slide-out
            target.IsClosing = true;
        }

        NotifyStateChanged();

        _ = RemoveAfterAnimationAsync(id, target);
    }

    private async Task RemoveAfterAnimationAsync(Guid id, ToastNotification toast)
    {
        try
        {
            await Task.Delay(AnimationDurationMs);
        }
        catch { }

        lock (_lock)
        {
            _toasts.RemoveAll(t => t.Id == id);
        }

        toast.OnClose?.Invoke();
        NotifyStateChanged();
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            foreach (var cts in _timers.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _timers.Clear();
            _toasts.Clear();
        }

        NotifyStateChanged();
    }

    public void Clear() => ClearAll();

    private void NotifyStateChanged()
    {
        OnChange?.Invoke();
    }

    public void Dispose()
    {
        ClearAll();
    }
}
