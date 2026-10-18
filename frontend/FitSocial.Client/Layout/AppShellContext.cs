using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FitSocial.Client.Layout;

/// <summary>
/// Kênh liên lạc giữa <see cref="AppShellLayout"/> và page đang hiển thị.
/// AppShellLayout tạo 1 instance và cung cấp nó qua <c>CascadingValue Name="AppShellContext"</c>.
/// <para>
/// Page dùng <c>@inject AppShellContext Shell</c>, đăng ký trong <c>OnInitialized</c> và dọn trong <c>Dispose</c>:
/// <code>
/// protected override void OnInitialized()
/// {
///     Shell.SearchTerm = searchTerm;
///     Shell.SearchPlaceholder = "Search packages...";
///     Shell.SearchInputHandler   = HandleHeaderSearchInput;
///     Shell.SearchKeyUpHandler   = HandleHeaderSearchKeyUp;
///     Shell.SearchExecuteHandler = ExecuteHeaderSearch;
///     Shell.SearchClearHandler   = ClearHeaderSearch;
/// }
///
/// public void Dispose()
/// {
///     Shell.Reset();
///     Shell.SearchTerm = string.Empty;
/// }
/// </code>
/// </para>
/// </summary>
public sealed class AppShellContext
{
    // ------------------------------------------------------------------
    // Topbar - ô tìm kiếm
    // ------------------------------------------------------------------

    /// <summary>Chuỗi tìm kiếm hiện tại (layout sở hữu ô nhập, page đọc để lọc dữ liệu).</summary>
    public string SearchTerm { get; set; } = string.Empty;

    /// <summary>Placeholder của ô tìm kiếm, do page tuỳ biến.</summary>
    public string SearchPlaceholder { get; set; } = "Search posts, coaches, friends...";

    /// <summary>Page có dùng ô tìm kiếm ở topbar hay không.</summary>
    public bool ShowSearch { get; set; } = true;

    /// <summary>Gọi khi người dùng gõ (ChangeEventArgs).</summary>
    public Func<ChangeEventArgs, Task>? SearchInputHandler { get; set; }

    /// <summary>Gọi khi người dùng nhấn phím (KeyboardEventArgs).</summary>
    public Func<KeyboardEventArgs, Task>? SearchKeyUpHandler { get; set; }

    /// <summary>Gọi khi nhấn nút kính lúp / Enter.</summary>
    public Func<Task>? SearchExecuteHandler { get; set; }

    /// <summary>Gọi khi bấm nút X xoá nhanh.</summary>
    public Func<Task>? SearchClearHandler { get; set; }

    // ------------------------------------------------------------------
    // Các vùng (slot) của layout
    // ------------------------------------------------------------------

    /// <summary>Nội dung page tự đặt cho sidebar phải. Bỏ trống => dùng widget mặc định.</summary>
    public RenderFragment? RightSidebar { get; set; }

    /// <summary>Nội dung bổ sung cho sidebar trái, hiển thị dưới menu điều hướng.</summary>
    public RenderFragment? LeftSidebarExtra { get; set; }

    // ------------------------------------------------------------------
    // Thông báo thay đổi
    // ------------------------------------------------------------------

    /// <summary>Được layout gán để yêu cầu render lại khi page đổi nội dung slot.</summary>
    public Action? StateChanged { get; set; }

    /// <summary>Báo cho layout biết slot vừa đổi.</summary>
    public void NotifyChanged() => StateChanged?.Invoke();

    /// <summary>Xoá toàn bộ đăng ký của page (dùng trong <c>Dispose</c>).</summary>
    public void Reset()
    {
        SearchTerm = string.Empty;
        SearchPlaceholder = "Search posts, coaches, friends...";
        ShowSearch = true;
        SearchInputHandler = null;
        SearchKeyUpHandler = null;
        SearchExecuteHandler = null;
        SearchClearHandler = null;
        RightSidebar = null;
        LeftSidebarExtra = null;
        NotifyChanged();
    }
}