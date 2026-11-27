using System;
using System.Collections.Generic;
using FitSocial.Client.Models.Toast;

namespace FitSocial.Client.Services.Toast;

/// <summary>
/// Service quản lý hệ thống Toast Notification tập trung cho Staff & Admin.
/// </summary>
public interface IToastService
{
    /// <summary>
    /// Danh sách các Toast đang hiển thị.
    /// </summary>
    IReadOnlyList<ToastNotification> Toasts { get; }

    /// <summary>
    /// Sự kiện thông báo khi danh sách Toast thay đổi (thêm, cập nhật animation, đóng).
    /// </summary>
    event Action? OnChange;

    /// <summary>
    /// Hiển thị thông báo thành công.
    /// </summary>
    Guid ShowSuccess(string message, string? title = null, int durationMs = 4000);

    /// <summary>
    /// Hiển thị thông báo lỗi.
    /// </summary>
    Guid ShowError(string message, string? title = null, int durationMs = 5000);

    /// <summary>
    /// Hiển thị thông báo cảnh báo.
    /// </summary>
    Guid ShowWarning(string message, string? title = null, int durationMs = 4500);

    /// <summary>
    /// Hiển thị thông báo thông tin.
    /// </summary>
    Guid ShowInfo(string message, string? title = null, int durationMs = 4000);

    /// <summary>
    /// Hiển thị một ToastNotification với type, message, title và durationMs tùy chọn.
    /// </summary>
    Guid Show(ToastType type, string message, string? title = null, int durationMs = 4000);

    /// <summary>
    /// Hiển thị một ToastNotification tùy chỉnh.
    /// </summary>
    Guid Show(ToastNotification toast);

    /// <summary>
    /// Đóng một Toast cụ thể theo Id (có animation mượt mà).
    /// </summary>
    void Dismiss(Guid id);

    /// <summary>
    /// Xóa toàn bộ Toast đang hiển thị.
    /// </summary>
    void ClearAll();

    /// <summary>
    /// Bí danh cho ClearAll().
    /// </summary>
    void Clear();
}
