using System;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Client.Services;

/// <summary>
/// Service quản lý trạng thái loading toàn cục (Global Loading State).
/// Hỗ trợ đếm số lượng tác vụ đồng thời (request counter) và thread-safe.
/// </summary>
public class LoadingService
{
    private int _loadingCount = 0;
    private readonly object _lock = new();

    /// <summary>
    /// Sự kiện thông báo khi trạng thái hoặc thông điệp loading thay đổi.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Cho biết ứng dụng có đang trong trạng thái loading hay không.
    /// </summary>
    public bool IsLoading => _loadingCount > 0;

    /// <summary>
    /// Thông điệp hiển thị kèm theo khi đang loading.
    /// </summary>
    public string CurrentMessage { get; private set; } = "Đang tải dữ liệu...";

    /// <summary>
    /// Bật loading với thông điệp tùy chọn.
    /// </summary>
    /// <param name="message">Nội dung hiển thị trên overlay</param>
    public void Show(string message = "Đang tải dữ liệu...")
    {
        lock (_lock)
        {
            CurrentMessage = message;
            Interlocked.Increment(ref _loadingCount);
        }
        NotifyStateChanged();
    }

    /// <summary>
    /// Cập nhật thông điệp hiển thị ngay trong lúc đang loading mà không thay đổi bộ đếm tác vụ.
    /// Rất hữu ích cho các tác vụ gồm nhiều bước (VD: "Đang xử lý ảnh..." -> "Đang lưu bài tập...").
    /// </summary>
    /// <param name="message">Nội dung thông điệp mới</param>
    public void SetMessage(string message)
    {
        lock (_lock)
        {
            CurrentMessage = message;
        }
        NotifyStateChanged();
    }

    /// <summary>
    /// Tắt loading (giảm bộ đếm tác vụ).
    /// Overlay chỉ tắt hẳn khi tất cả các tác vụ kết thúc (bộ đếm về 0).
    /// </summary>
    public void Hide()
    {
        lock (_lock)
        {
            if (_loadingCount > 0)
            {
                Interlocked.Decrement(ref _loadingCount);
            }
        }
        NotifyStateChanged();
    }

    /// <summary>
    /// Đặt lại bộ đếm về 0, buộc tắt overlay ngay lập tức nếu cần khôi phục trạng thái.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _loadingCount = 0;
        }
        NotifyStateChanged();
    }

    /// <summary>
    /// Thực thi một tác vụ bất đồng bộ kèm hiển thị loading tự động.
    /// Tự động gọi Hide() trong khối finally kể cả khi xảy ra Exception.
    /// </summary>
    /// <param name="action">Hàm bất đồng bộ cần thực thi</param>
    /// <param name="message">Thông điệp hiển thị</param>
    public async Task RunWithLoadingAsync(Func<Task> action, string message = "Đang xử lý...")
    {
        Show(message);
        try
        {
            await action();
        }
        finally
        {
            Hide();
        }
    }

    /// <summary>
    /// Thực thi một tác vụ bất đồng bộ có giá trị trả về kèm hiển thị loading tự động.
    /// Tự động gọi Hide() trong khối finally kể cả khi xảy ra Exception.
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu trả về</typeparam>
    /// <param name="action">Hàm bất đồng bộ trả về kết quả</param>
    /// <param name="message">Thông điệp hiển thị</param>
    /// <returns>Kết quả từ action</returns>
    public async Task<T> RunWithLoadingAsync<T>(Func<Task<T>> action, string message = "Đang xử lý...")
    {
        Show(message);
        try
        {
            return await action();
        }
        finally
        {
            Hide();
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

