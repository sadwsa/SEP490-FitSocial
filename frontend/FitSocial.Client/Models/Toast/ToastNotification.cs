using System;

namespace FitSocial.Client.Models.Toast;

/// <summary>
/// Đại diện cho một thông báo Toast trong hệ thống Staff & Admin.
/// </summary>
public class ToastNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ToastType Type { get; set; } = ToastType.Info;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Thời gian hiển thị tính bằng milliseconds. Nếu &lt;= 0, thông báo sẽ giữ nguyên cho đến khi người dùng đóng thủ công.
    /// </summary>
    public int DurationMs { get; set; } = 4000;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Trạng thái đang đóng dần (phục vụ CSS animation fade-out / slide-out trước khi xóa khỏi DOM).
    /// </summary>
    public bool IsClosing { get; set; } = false;

    /// <summary>
    /// Callback tùy chọn được kích hoạt khi Toast bị đóng.
    /// </summary>
    public Action? OnClose { get; set; }
}

