# 🏋️ FitSocial - Hướng Dẫn Phát Triển Frontend Blazor (.NET 8)

Tài liệu này hướng dẫn các thành viên trong nhóm cách thiết lập, phát triển và mở rộng khung sườn dự án Frontend **FitSocial** sử dụng **Blazor WebAssembly (.NET 8)**.

---

## 📌 1. Tech Stack (Công nghệ sử dụng)

* **Framework:** Blazor WebAssembly (.NET 8 - C#)
* **Hosting:** Client-side SPA độc lập tại `frontend/FitSocial.Client`
* **Xác thực (Authentication):** `CustomAuthenticationStateProvider` tích hợp JWT Bearer Token & `Blazored.LocalStorage`
* **Giao tiếp API:** `ApiClient` đóng gói sẵn kèm Token Interceptor & xử lý ngoại lệ
* **Thời gian thực (Realtime):** `Microsoft.AspNetCore.SignalR.Client` (NotificationHub, ChatHub)
* **Form & Validation:** Blazor `EditForm`, `DataAnnotationsValidator`, `ValidationMessage`
* **Styling & UI:** CSS Variables chủ đề thể thao (Dark/Light ready), responsive layout trên Mobile & Desktop

---

## 🚀 2. Hướng Dẫn Cài Đặt & Chạy Dự Án

### Yêu cầu môi trường:
* **.NET 8 SDK** (phiên bản `8.0.x` trở lên)
* IDE: Visual Studio 2022, Visual Studio Code (với C# Dev Kit), hoặc JetBrains Rider

### Các bước khởi chạy:

1. **Khởi động Backend (ASP.NET Core Web API):**
   ```bash
   cd backend/FitSocial.API
   dotnet run
   ```
   * Swagger UI: `https://localhost:7200/swagger`
   * API Base URL: `https://localhost:7200/api/`

2. **Khởi động Frontend (Blazor WebAssembly):**
   ```bash
   cd frontend/FitSocial.Client
   dotnet run
   ```
   * Trình duyệt sẽ mở: `https://localhost:7012` hoặc `http://localhost:5112`

3. **Kiểm tra kết nối:**
   * Truy cập trang **Kiểm tra API** tại menu bên trái (`/api-status`) để kiểm tra tức thì kết nối giữa Blazor Client và Web API / SignalR.

---

## 📂 3. Cấu Trúc Khung Sườn Dự Án (`frontend/FitSocial.Client`)

```plaintext
FitSocial.Client/
├── wwwroot/                           # Static assets
│   ├── appsettings.json               # Cấu hình ApiBaseUrl & SignalR URLs
│   ├── css/
│   │   └── app.css                    # Toàn bộ Style giao diện, màu sắc FitSocial
│   └── index.html                     # HTML Entry point
│
├── Models/                            # DTOs & Request/Response models
│   ├── Common/
│   │   └── ApiResponse.cs             # Wrapper chuẩn cho kết quả API (Success, Data, Message)
│   ├── Auth/
│   │   └── AuthModels.cs              # LoginRequest, RegisterRequest, AuthResponse, UserInfo
│   ├── Posts/
│   │   └── PostModels.cs              # PostDto, CreatePostRequest, CommentDto
│   └── Workouts/
│       └── WorkoutModels.cs           # WorkoutDto
│
├── Services/                          # Tầng xử lý Logic & Giao tiếp ngoài
│   ├── Auth/
│   │   ├── CustomAuthenticationStateProvider.cs  # Quản lý Claims & Token từ LocalStorage
│   │   └── AuthService.cs                        # IAuthService (Login, Register, Logout)
│   ├── Http/
│   │   └── ApiClient.cs                          # Wrapper gọi GET/POST/DELETE tự gắn Bearer Token
│   ├── Posts/
│   │   └── PostService.cs                        # IPostService (Lấy feed, tạo bài viết, like)
│   └── Realtime/
│       └── NotificationHubClient.cs              # INotificationHubClient (SignalR realtime)
│
├── Layout/                            # Giao diện khung
│   ├── MainLayout.razor               # Header, thanh tìm kiếm, Avatar, Auth state
│   ├── NavMenu.razor                  # Sidebar điều hướng (Bảng tin, Bài tập, Cộng đồng, Profile)
│   └── NavMenu.razor.css
│
├── Pages/                             # Các trang chức năng chính (Routes)
│   ├── Home.razor                     # Route "/" - Bảng tin bài tập FitSocial
│   ├── Workouts.razor                 # Route "/workouts" - Danh mục bài tập & lịch tập
│   ├── Community.razor                # Route "/community" - Hội nhóm & thử thách
│   ├── Profile.razor                  # Route "/profile" - Trang cá nhân (yêu cầu [Authorize])
│   ├── Login.razor                    # Route "/login" - Đăng nhập
│   ├── Register.razor                 # Route "/register" - Đăng ký
│   └── ApiStatus.razor                # Route "/api-status" - Kiểm tra kết nối API & SignalR
│
├── App.razor                          # Cấu hình Router & CascadingAuthenticationState
├── Program.cs                         # Đăng ký Dependency Injection (Services, Auth, Http)
└── _Imports.razor                     # Khai báo các using phổ biến
```

---

## 🛠️ 4. Hướng Dẫn Thêm Tính Năng Mới

### 1. Thêm một Service mới gọi API Backend:
1. Tạo Interface & Implementation trong thư mục `Services/`:
   ```csharp
   public interface IExerciseService
   {
       Task<ApiResponse<List<ExerciseDto>>> GetExercisesAsync();
   }
   
   public class ExerciseService : IExerciseService
   {
       private readonly ApiClient _apiClient;
       public ExerciseService(ApiClient apiClient) => _apiClient = apiClient;
       
       public async Task<ApiResponse<List<ExerciseDto>>> GetExercisesAsync()
           => await _apiClient.GetAsync<List<ExerciseDto>>("exercises");
   }
   ```
2. Đăng ký vào `Program.cs`:
   ```csharp
   builder.Services.AddScoped<IExerciseService, ExerciseService>();
   ```
3. Sử dụng trong Razor Component:
   ```razor
   @inject IExerciseService ExerciseService
   ```

### 2. Bảo vệ trang yêu cầu Đăng nhập:
Chỉ cần thêm attribute `@attribute [Authorize]` ở đầu file Razor:
```razor
@page "/my-private-page"
@attribute [Authorize]
```

### 3. Kiểm tra quyền/trạng thái người dùng trong giao diện:
Sử dụng component `<AuthorizeView>`:
```razor
<AuthorizeView>
    <Authorized>
        <p>Xin chào, @context.User.Identity?.Name!</p>
    </Authorized>
    <NotAuthorized>
        <a href="login">Vui lòng đăng nhập</a>
    </NotAuthorized>
</AuthorizeView>
```
