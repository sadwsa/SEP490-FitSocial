# BẢN KIỂM CHỨNG THÔNG SỐ KỸ THUẬT & PHI CHỨC NĂNG (NFR)
DỰ ÁN: SEP490-FitSocial (FitConnect)

---

## 1. Bảng đối chiếu chi tiết 36 thông số kỹ thuật

| STT | Thông số | Tôi nêu | Giá trị trong code | File:dòng | Trạng thái |
|:---:|:---|:---|:---|:---|:---:|
| **1** | Số DbSet/entity trong FitSocialDbContext | 45 | 46 DbSet | `backend/FitSocial.Infrastructure/Data/FitSocialDbContext.cs:15-60` | **MISMATCH** |
| **2** | Danh sách vai trò trong AppRoles | TRAINEE, COACH, STAFF, ADMIN | Trainee="TRAINEE", Coach="COACH", Staff="STAFF", Admin="ADMIN" | `frontend/FitSocial.Client/Models/Auth/AppRoles.cs:8-13` | **MATCH** |
| **3** | Project trong .sln & TargetFramework | 6 project (Domain, Application, Infrastructure, API, Client, UnitTests) | Không có file `.sln` trong repo; có đúng 6 file `.csproj`, tất cả đều dùng `net8.0` | `backend/FitSocial.Domain/FitSocial.Domain.csproj:4`<br>`backend/FitSocial.Application/FitSocial.Application.csproj:4`<br>`backend/FitSocial.Infrastructure/FitSocial.Infrastructure.csproj:4`<br>`backend/FitSocial.API/FitSocial.API.csproj:4`<br>`frontend/FitSocial.Client/FitSocial.Client.csproj:4`<br>`backend/FitSocial.UnitTests/FitSocial.UnitTests.csproj:4` | **MISMATCH / NOT FOUND** (với file .sln) / **MATCH** (với 6 project & net8.0) |
| **4** | Cấu hình Nullable trong từng .csproj | Bật (enable) trong cả 6 project | `<Nullable>enable</Nullable>` xuất hiện ở cả 6 file `.csproj` | Cả 6 file `.csproj` dòng 6 | **MATCH** |
| **5** | Số bước Wizard đăng ký Coach trong Register.razor | 5 bước | Đúng 5 bước: 1: Account, 2: CCCD, 3: Certificates, 4: Confirm & Plan, 5: Pay | `frontend/FitSocial.Client/Pages/Register.razor:58, 435` | **MATCH** |
| **6** | SignalR Hubs, MapHub path, Fallback & Reconnect | `/hubs/chat`, `/hubs/notifications`, `/hubs/posts`. Có transport fallback & automatic reconnect | `app.MapHub<ChatHub>("/hubs/chat")`, `/hubs/posts`, `/hubs/notifications`. Reconnect `.WithAutomaticReconnect()` trên cả 3 client, fallback WebSockets -> SSE -> Long Polling | `backend/FitSocial.API/Program.cs:230-232`<br>`frontend/FitSocial.Client/Services/Realtime/NotificationHubClient.cs:45`<br>`frontend/FitSocial.Client/Services/Realtime/ChatHubClient.cs:51`<br>`frontend/FitSocial.Client/Services/Realtime/PostHubClient.cs:52` | **MATCH** |
| **7** | Giới hạn dung lượng tải tệp (Image, Video, Kestrel, Chunk) | MaxImageBytes 10MB, MaxVideoBytes 100MB, Kestrel 100MB, bufferSize 20MB | `MaxImageBytes = 10 * 1024 * 1024;`<br>`MaxVideoBytes = 100 * 1024 * 1024;`<br>`Limits.MaxRequestBodySize = 100 * 1024 * 1024;`<br>`bufferSize: 20 * 1024 * 1024` | `backend/FitSocial.API/Controllers/UploadController.cs:17-18, 106`<br>`backend/FitSocial.API/Program.cs:21` | **MATCH** |
| **8** | Cloudinary concurrency limit & SemaphoreSlim | Giới hạn 10 luồng đồng thời bằng SemaphoreSlim | `var configuredConcurrency = _configuration.GetValue<int?>("Cloudinary:UploadConcurrency") ?? 10;`<br>`var uploadConcurrency = Math.Clamp(configuredConcurrency, 1, 10);`<br>`using var throttler = new SemaphoreSlim(uploadConcurrency, uploadConcurrency);` | `backend/FitSocial.API/Controllers/UploadController.cs:282-286` | **MATCH** |
| **9** | Cloudinary Signed URL cho CCCD, face, PDF credentials | Hạn ~10 phút, lưu trữ authenticated / private | Endpoint `GetSignedUrl` tạo URL có hạn ~10 phút, `Type("authenticated")`, `Signed(true)` | `backend/FitSocial.API/Controllers/UploadController.cs:458-494`<br>`backend/FitSocial.API/Services/EkycRetentionService.cs:68` | **MATCH** |
| **10** | Thông số OTP (chữ số, TTL, số lần thử, mã băm, cooldown) | 6 chữ số, 15 phút, 5 lần, SHA-256, 60s cooldown | `GetInt32(100000, 1000000)` (6 số), `ExpiryMinutes = 15`, `MaxAttempts = 5`, `ComputeSha256Hash`, `countdown = 60` | `backend/FitSocial.Infrastructure/Services/OtpService.cs:27, 45, 72, 129-139`<br>`backend/FitSocial.Domain/Constants/OtpConstants.cs:9-10`<br>`frontend/FitSocial.Client/Pages/Register.razor:404` | **MATCH** |
| **11** | Rate limiting đối với OTP | OtpPolicy, 5 req/phút, HTTP 429 | `AddFixedWindowLimiter("OtpPolicy", ... PermitLimit = 5, Window = 1 min)`, trả về status 429 | `backend/FitSocial.API/Program.cs:45-62`<br>`backend/FitSocial.API/Controllers/AuthController.cs:25, 50` | **MATCH** |
| **12** | Độ dài mật khẩu tối thiểu & thuật toán băm | Min 8 ký tự, băm BCrypt | `[MinLength(8)]`, `BCrypt.Net.BCrypt.HashPassword(password)` | `backend/FitSocial.Application/DTOs/Auth/RegisterTraineeRequestDto.cs:16`<br>`backend/FitSocial.Infrastructure/Services/PasswordHasher.cs:9` | **MATCH** |
| **13** | Thuật toán ký JWT & thời hạn Access Token | HS256 / HmacSha256, 15 phút | `SecurityAlgorithms.HmacSha256`, `ExpiryMinutes: "15"` | `backend/FitSocial.Infrastructure/Services/JwtTokenGenerator.cs:25, 46`<br>`backend/FitSocial.API/appsettings.json:16` | **MATCH** |
| **14** | Refresh token (bytes, hash, rotation, expiry 7/30d) | 48 bytes (base64 64 ký tự), SHA-256 hash, rotation revoke token cũ, 7d thường / 30d remember | `GetBytes(48)` -> Base64 64 ký tự, băm `SHA256.HashData`, `stored.RevokedAt = DateTime.UtcNow`, `refreshDays = rememberMe ? 30 : 7` | `backend/FitSocial.Application/Services/TokenService.cs:51, 104, 147, 152` | **MATCH** |
| **15** | Multi-layer token validation trong Program.cs | Layer 1: Redis JTI blacklist. Layer 2: DB TokenVersion & IsLocked | `blacklistService.IsTokenRevokedAsync(jti)` (Layer 1); `user == null || user.IsLocked == true || user.TokenVersion != tokenVersion` (Layer 2) | `backend/FitSocial.API/Program.cs:145-158` | **MATCH** |
| **16** | Endpoint đăng nhập Admin & kiểm soát 403 | AuthController có `admin-login`, route `/admin/login`, Staff không được vào trang Admin | Endpoint `[HttpPost("admin-login")]` chỉ cho `ADMIN,STAFF`. Client route `/admin/login`. Trang admin-only có `[Authorize(Roles = "ADMIN")]`, chặn bằng `<NotAuthorized>` và HTTP 403 | `backend/FitSocial.API/Controllers/AuthController.cs:120`<br>`backend/FitSocial.Application/Services/AuthService.cs:244-248`<br>`frontend/FitSocial.Client/Pages/Admin/AdminLogin.razor:1`<br>`frontend/FitSocial.Client/Layout/AdminLayout.razor:52-64`<br>`frontend/FitSocial.Client/App.razor:5-7` | **MATCH** |
| **17** | Thuật toán mã hoá trong AesEncryptionService | AES-256-CBC, PKCS7, key 32 bytes, IV 16 bytes prepend, fallback plain text | AES-256-CBC, PKCS7, key 32 bytes qua SHA-256, IV 16 bytes ngẫu nhiên prepend. KHÔNG có fallback giải mã plain text cũ (trả về null nếu lỗi) | `backend/FitSocial.Infrastructure/Services/AesEncryptionService.cs:22, 70, 76-77, 81, 119-123` | **MISMATCH** (về fallback plain text) |
| **18** | Dữ liệu được mã hoá AES và che (masking) | BankAccount che 4 số cuối dạng `****1234`, CCCD/eKYC mã hoá, PaymentGatewayConfig mã hoá API Key/Checksum Key | `CoachBankAccount`: mã hoá `EncryptedAccountNumber`, che `****1234`. `CoachEkycVerification`: mã hoá `EncryptedIdCardNumber`, `EncryptedRawInformationJson`. `PaymentGatewayConfig`: mã hoá `EncryptedApiKey`, `EncryptedChecksumKey`, che dạng `key[..4]...key[^4..]` | `backend/FitSocial.Application/Services/CoachBankAccountService.cs:98, 184-189`<br>`backend/FitSocial.Domain/Entities/CoachEkycVerification.cs:11, 65`<br>`backend/FitSocial.Domain/Entities/PaymentGatewayConfig.cs:14, 16`<br>`backend/FitSocial.Application/Services/PaymentGatewayConfigService.cs:332` | **MATCH** |
| **19** | EkycRetentionService (chu kỳ, thời gian xoá, nghị định) | Chu kỳ 24h, xoá sau 30 ngày, Nghị định 13/2023 | Chu kỳ 24h, xoá eKYC không đạt & xoá ảnh Cloudinary sau 30 ngày (`now.AddDays(-30)`), comment viện dẫn Nghị định 13/2023/NĐ-CP | `backend/FitSocial.API/Services/EkycRetentionService.cs:9, 27, 37` | **MATCH** |
| **20** | AuditLogs & regex redact dữ liệu nhạy cảm | Ghi chi tiết log kèm regex làm mờ password, token, secret | Các trường: ID, Actor, Action, EntityType/Id, OldValue/NewValue (jsonb), IPAddress. Regex `SensitiveKeyRegex` thay thế các key nhạy cảm thành `"[REDACTED]"` | `backend/FitSocial.Infrastructure/Data/FitSocialDbContext.cs:1368-1392`<br>`backend/FitSocial.Application/Services/AuditLogService.cs:24-26, 276-280` | **MATCH** |
| **21** | HttpClient timeouts cho các tích hợp ngoài | Viettel eKYC, VietQR lookup, default HttpClient | Viettel eKYC: 120s (`ViettelEkyc:TimeoutSeconds`). VietQR lookup: 6s. Default HttpClient: 100s | `backend/FitSocial.Application/Services/ViettelEkycService.cs:81-85`<br>`backend/FitSocial.Application/Services/CoachBankAccountService.cs:207`<br>`backend/FitSocial.API/Program.cs:68` | **MATCH** |
| **22** | Camera trong faceCapture.js & LiveFaceCapture.razor | Độ phân giải 640x480, JPEG, stop track, có fallback file upload | `canvas.width = 640; canvas.height = 480;`, JPEG quality 0.85, gọi `t.stop()`, có fallback `<InputFile>` max 10MB | `frontend/FitSocial.Client/wwwroot/js/faceCapture.js:21-22, 28, 33`<br>`frontend/FitSocial.Client/Components/LiveFaceCapture.razor:45-48, 89, 96-120` | **MATCH** |
| **23** | PayOS webhook, idempotency, concurrency token trên Order | Idempotency webhook, Order entity dùng xmin / RowVersion / ConcurrencyCheck | Order KHÔNG có xmin, RowVersion, ConcurrencyCheck. Idempotency xử lý bằng kiểm tra `OrderStatus == OrderStatusPaid`. Chỉ có `RefundRequest.Status` dùng `.IsConcurrencyToken()` | `backend/FitSocial.Domain/Entities/Order.cs:1-51`<br>`backend/FitSocial.Infrastructure/Data/FitSocialDbContext.cs:836-860, 1305`<br>`backend/FitSocial.Application/Services/PaymentService.cs:404, 503-536` | **MISMATCH** (Order không có xmin/RowVersion/ConcurrencyCheck) |
| **24** | Cấu hình SMTP & gửi email | smtp.gmail.com, port 587, MailKit, StartTls, appsettings credentials | Host `smtp.gmail.com`, Port 587, MailKit `SmtpClient`, `SecureSocketOptions.StartTls`, tài khoản đọc từ `MailSettings` | `backend/FitSocial.API/appsettings.json:34-39`<br>`backend/FitSocial.Infrastructure/Services/EmailService.cs:2, 39-44` | **MATCH** |
| **25** | Quản lý giao dịch DB (Database Transactions) | UnitOfWork bọc BeginTransactionAsync, các luồng sử dụng | `IUnitOfWork` bọc `BeginTransactionAsync()`. Dùng explicit transaction trong `RefundRequestService` (`Approve/Reject`). Các luồng khác dùng `_unitOfWork.SaveChangesAsync()` | `backend/FitSocial.Domain/Interfaces/IUnitOfWork.cs:6-8`<br>`backend/FitSocial.Infrastructure/Repositories/UnitOfWork.cs:22-30`<br>`backend/FitSocial.Application/Services/RefundRequestService.cs:1078, 1217` | **MATCH** |
| **26** | Global exception middleware | ExceptionHandlingMiddleware bắt lỗi, format ApiResponseDto, mã HTTP status | Bắt toàn cục: Validation/Business -> 400, Conflict -> 409, Forbidden -> 403, NotFound -> 404, Exception -> 500 (tự ghi audit log SYSTEM_ERROR). Format: `ApiResponseDto<object>.Fail(msg)` | `backend/FitSocial.API/Program.cs:216`<br>`backend/FitSocial.API/Middleware/ExceptionHandlingMiddleware.cs:33-41, 58, 91` | **MATCH** |
| **27** | Bảo mật kết nối: SSL DB, Redis SSL, HSTS, CORS | Neon VerifyFull, Redis ssl=True, HSTS bật, CORS origins | Neon: `SSL Mode=VerifyFull;Channel Binding=Require;`. Redis: `ssl=True`. HSTS: KHÔNG BẬT (thiếu `app.UseHsts()`). CORS: `AllowBlazorClient` (localhost 5112, 7012, 5000, 5001) | `backend/FitSocial.API/appsettings.json:9-10`<br>`backend/FitSocial.API/Program.cs:27-39, 218` | **MISMATCH** (về HSTS) |
| **28** | Phân trang: Max PageSize và cơ chế chặn | Giới hạn tối đa PageSize trong DTOs, clamp, xử lý pageSize=1000 | DTO mặc định 10. Có `Math.Clamp(pageSize, 1, 100)` trong `AuditLogRepository` và `AuditLogService`. Nếu gửi 1000 thì bị clamp về 100 | `backend/FitSocial.Application/DTOs/AuditLogs/GetAuditLogsQueryDto.cs:14`<br>`backend/FitSocial.Infrastructure/Repositories/AuditLogRepository.cs:120`<br>`backend/FitSocial.Application/Services/AuditLogService.cs:138` | **MATCH** |
| **29** | Connection pooling & Danh sách HasIndex | Cấu hình pool size trong connection string, danh sách HasIndex trong DbContext | Connection string KHÔNG có tham số `Pooling` hay `Min/Max Pool Size`. DbContext có đúng **7 lệnh `HasIndex`** trên **5 entities** (tất cả đều là Unique index) | `backend/FitSocial.API/appsettings.json:9`<br>`backend/FitSocial.Infrastructure/Data/FitSocialDbContext.cs:140-142, 340, 410, 960, 1229` | **MISMATCH** (về cấu hình pool size) |
| **30** | Swagger Bearer & UnitTests | Swagger có Bearer auth, framework xUnit/Moq, số lượng test class/method | Swagger có Bearer Scheme. UnitTests dùng `xUnit 2.9.3`, `Moq 4.21.0`, `EF Core InMemory 8.0.8`. Có **21 test class** trong dự án `FitSocial.UnitTests` | `backend/FitSocial.API/Program.cs:183-204`<br>`backend/FitSocial.UnitTests/FitSocial.UnitTests.csproj:11-16`<br>`backend/FitSocial.UnitTests/*Tests.cs` | **MATCH** |
| **31** | Các tệp DevOps/triển khai có mặt trong repo | k6/jmeter script, backup script .sql/.sh, docker-compose.yml, /health, cpu/ram limit | Không có file nào thuộc các loại này trong repo | Toàn bộ workspace root, `backend/`, `docs/`, `Program.cs` | **NOT FOUND** |
| **32** | CSS Framework & Responsive Breakpoints | Bootstrap phiên bản, Tailwind nhúng qua đâu, breakpoint media queries | Bootstrap **v5.1.0**, Bootstrap Icons **1.11.3**, Tailwind qua **CDN script**. Media queries: 768px (`app.css`), 1023px (< lg mobile/tablet drawer trong `AppShellLayout.razor.css`), 480px | `frontend/FitSocial.Client/wwwroot/css/bootstrap/bootstrap.min.css:2`<br>`frontend/FitSocial.Client/wwwroot/index.html:10, 15`<br>`frontend/FitSocial.Client/wwwroot/css/app.css:38, 51`<br>`frontend/FitSocial.Client/Layout/AppShellLayout.razor.css:134, 197` | **MATCH** |
| **33** | Layout 3 cột (Trainee/Coach) & 2 cột (Admin) | AppShellLayout 3 cột, AdminLayout 2 cột | `AppShellLayout`: Desktop (≥ 1024px) 3 cột (Left Nav, Center Main, Right Widget); Mobile (< 1024px) 1 cột + Drawer. `AdminLayout`: 2 cột (`console-side` + `console-main`) | `frontend/FitSocial.Client/Layout/AppShellLayout.razor:10-13, 35-75`<br>`frontend/FitSocial.Client/Layout/AppShellLayout.razor.css:3-4, 134`<br>`frontend/FitSocial.Client/Layout/AdminLayout.razor:7-60` | **MATCH** |
| **34** | AppSidebar & AppHeader (link, thành phần, số click) | Danh sách link Sidebar, thành phần Header, số click điều hướng | Sidebar: 7 link (News Feed, Package List, My Packages, Coaches List, Coach Dashboard, Trainee Orders, Reports). Header: Logo, Search box, Wallet, Cart, Messages dropdown, Notifications dropdown, Profile menu. 1 click cho mục chính, 2 clicks cho mục trong profile dropdown | `frontend/FitSocial.Client/Layout/AppSidebar.razor:10-93`<br>`frontend/FitSocial.Client/Layout/AppHeader.razor:20-200` | **MATCH** |
| **35** | localStorage / sessionStorage keys | `authToken`, `refreshToken`, `rememberMe`, `userRole`, các key khác | Có `authToken`, `refreshToken`, `rememberMe`. KHÔNG có key `userRole` trong storage (vai trò được trích xuất trực tiếp từ claim JWT) | `frontend/FitSocial.Client/Services/Auth/AuthService.cs:36-37`<br>`frontend/FitSocial.Client/Services/Auth/TokenStorage.cs:22`<br>`frontend/FitSocial.Client/Services/Auth/CustomAuthenticationStateProvider.cs:44-46` | **MISMATCH** (về `userRole`) |
| **36** | Ngôn ngữ giao diện & chuỗi tiếng Việt còn sót | Tiếng Anh là chính, còn sót chuỗi tiếng Việt hardcoded | Tiếng Anh là ngôn ngữ chính. Còn sót chuỗi tiếng Việt: `"Đang tải FitSocial..."` (app.css), `"Không tìm thấy trang - FitConnect"` (App.razor), `"Đóng menu"` (AppShellLayout), `"Tìm kiếm...", "Đăng xuất", "Đăng ký"` (MainLayout), AppRoles hiển thị tiếng Việt, các comment tiếng Việt | `frontend/FitSocial.Client/wwwroot/css/app.css:249`<br>`frontend/FitSocial.Client/App.razor:12`<br>`frontend/FitSocial.Client/Layout/AppShellLayout.razor:38`<br>`frontend/FitSocial.Client/Layout/MainLayout.razor:14, 30, 35-36`<br>`frontend/FitSocial.Client/Models/Auth/AppRoles.cs:17-20`<br>`frontend/FitSocial.Client/Pages/Register.razor:174, 220, 586` | **MATCH** |

---

## 2. Trích nguyên văn các mục MISMATCH và NOT FOUND

### Mục 1: Số DbSet trong FitSocialDbContext (Tôi nêu: 45 | Code: 46)
Trích nguyên văn từ `backend/FitSocial.Infrastructure/Data/FitSocialDbContext.cs:15-60`:
```csharp
    public virtual DbSet<Appeal> Appeals { get; set; }
    public virtual DbSet<AppealMedium> AppealMedia { get; set; }
    public virtual DbSet<AuditLog> AuditLogs { get; set; }
    public virtual DbSet<BodyMeasurementDetail> BodyMeasurementDetails { get; set; }
    public virtual DbSet<BodyMetricLog> BodyMetricLogs { get; set; }
    public virtual DbSet<Cart> Carts { get; set; }
    public virtual DbSet<CoachBankAccount> CoachBankAccounts { get; set; }
    public virtual DbSet<CoachCertificate> CoachCertificates { get; set; }
    public virtual DbSet<CoachEkycVerification> CoachEkycVerifications { get; set; }
    public virtual DbSet<CoachProfile> CoachProfiles { get; set; }
    public virtual DbSet<CoachSubscriptionPlan> CoachSubscriptionPlans { get; set; }
    public virtual DbSet<CoachUpgrade> CoachUpgrades { get; set; }
    public virtual DbSet<Comment> Comments { get; set; }
    public virtual DbSet<Conversation> Conversations { get; set; }
    public virtual DbSet<Location> Locations { get; set; }
    public virtual DbSet<MealPlan> MealPlans { get; set; }
    public virtual DbSet<MealPlanItem> MealPlanItems { get; set; }
    public virtual DbSet<Message> Messages { get; set; }
    public virtual DbSet<MessageAttachment> MessageAttachments { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<Order> Orders { get; set; }
    public virtual DbSet<OrderDetail> OrderDetails { get; set; }
    public virtual DbSet<Otplog> Otplogs { get; set; }
    public virtual DbSet<Payment> Payments { get; set; }
    public virtual DbSet<PaymentGatewayConfig> PaymentGatewayConfigs { get; set; }
    public virtual DbSet<Payout> Payouts { get; set; }
    public virtual DbSet<PayoutItem> PayoutItems { get; set; }
    public virtual DbSet<Post> Posts { get; set; }
    public virtual DbSet<PostInteraction> PostInteractions { get; set; }
    public virtual DbSet<PostMedium> PostMedia { get; set; }
    public virtual DbSet<Price> Prices { get; set; }
    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }
    public virtual DbSet<RefundRequest> RefundRequests { get; set; }
    public virtual DbSet<Report> Reports { get; set; }
    public virtual DbSet<ReportMedium> ReportMedia { get; set; }
    public virtual DbSet<Review> Reviews { get; set; }
    public virtual DbSet<TermsAndPolicy> TermsAndPolicies { get; set; }
    public virtual DbSet<TrainingPackage> TrainingPackages { get; set; }
    public virtual DbSet<TrainingPackageMedium> TrainingPackageMedia { get; set; }
    public virtual DbSet<TrainingPlan> TrainingPlans { get; set; }
    public virtual DbSet<TrainingPlanExercise> TrainingPlanExercises { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<UserAgreement> UserAgreements { get; set; }
    public virtual DbSet<UserBlock> UserBlocks { get; set; }
    public virtual DbSet<VideoTutorial> VideoTutorials { get; set; }
    public virtual DbSet<WorkoutCompletionLog> WorkoutCompletionLogs { get; set; }
```
*(Tổng cộng có 46 thuộc tính DbSet, không phải 45).*

---

### Mục 3: File .sln trong Repo (Tôi nêu: Có file .sln chứa 6 project | Code: Không có file .sln)
- Khi kiểm tra các đường dẫn `FitSocial.sln`, `backend/FitSocial.sln`, `SEP490-FitSocial.sln`, hệ điều hành đều báo lỗi: `The system cannot find the file specified`. File `.sln` không được commit vào git repository.
- Tuy nhiên, trong mã nguồn có đúng 6 project `.csproj` độc lập, tất cả đều khai báo `<TargetFramework>net8.0</TargetFramework>`:
  - `backend/FitSocial.Domain/FitSocial.Domain.csproj:4`
  - `backend/FitSocial.Application/FitSocial.Application.csproj:4`
  - `backend/FitSocial.Infrastructure/FitSocial.Infrastructure.csproj:4`
  - `backend/FitSocial.API/FitSocial.API.csproj:4`
  - `frontend/FitSocial.Client/FitSocial.Client.csproj:4`
  - `backend/FitSocial.UnitTests/FitSocial.UnitTests.csproj:4`

---

### Mục 17: Fallback plain text cũ trong AesEncryptionService (Tôi nêu: Có fallback | Code: Không có fallback)
Trích nguyên văn từ `backend/FitSocial.Infrastructure/Services/AesEncryptionService.cs:92-124`:
```csharp
    public byte[]? DecryptBytes(byte[]? cipherData)
    {
        if (cipherData == null || cipherData.Length <= 16)
        {
            return null;
        }

        try
        {
            var iv = new byte[16];
            Array.Copy(cipherData, 0, iv, 0, 16);

            var cipherPayloadLength = cipherData.Length - 16;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var msInput = new MemoryStream(cipherData, 16, cipherPayloadLength);
            using var msOutput = new MemoryStream();
            using var cs = new CryptoStream(msInput, aes.CreateDecryptor(), CryptoStreamMode.Read);
            cs.CopyTo(msOutput);

            return msOutput.ToArray();
        }
        catch
        {
            // Return null if ciphertext is corrupted or decryption fails
            return null;
        }
    }
```
*(Nếu giải mã lỗi hoặc dữ liệu <= 16 bytes, phương thức trả về `null`, không có bước fallback giải mã chuỗi plain text cũ).*

---

### Mục 23: Idempotency, Concurrency Token & xmin trên Order (Tôi nêu: Order dùng xmin/RowVersion/ConcurrencyCheck | Code: Order không dùng)
Trích nguyên văn cấu hình `Order` trong `backend/FitSocial.Infrastructure/Data/FitSocialDbContext.cs:836-860`:
```csharp
        // 26. Orders
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("Orders_pkey");
            entity.ToTable("Orders");
            entity.Property(e => e.OrderId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("OrderID");
            entity.Property(e => e.BuyerId).HasColumnName("BuyerID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.OrderStatus).HasMaxLength(50);
            entity.Property(e => e.OrderType).HasMaxLength(50);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Buyer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.BuyerId)
                .HasConstraintName("Orders_BuyerID_fkey");

            entity.HasOne(d => d.Coach).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CoachId)
                .HasConstraintName("Orders_CoachID_fkey");
        });
```
*(Entity `Order` hoàn toàn không có `xmin`, `RowVersion` hay `IsConcurrencyToken()`).*
*Lưu ý: Chỉ có `RefundRequest.Status` tại dòng 1305 được cấu hình `IsConcurrencyToken()`:*
```csharp
entity.Property(e => e.Status).HasMaxLength(30).IsConcurrencyToken();
```
*Idempotency của Order được xử lý bằng logic so khớp trạng thái tại `PaymentService.cs:404`:*
```csharp
if (order.OrderStatus == PaymentConstants.OrderStatusPaid)
{
    ...
    return ApiResponseDto<AuthResponseDto>.Ok(existingSession, "Payment already confirmed. Welcome back!");
}
```

---

### Mục 27: Cấu hình HSTS (Tôi nêu: HSTS có bật | Code: HSTS không được gọi)
Trích nguyên văn pipeline trong `backend/FitSocial.API/Program.cs:207-234`:
```csharp
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<FitSocial.API.Middleware.ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors("AllowBlazorClient");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<PostHub>("/hubs/posts");
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
```
*(Trong pipeline chỉ có `app.UseHttpsRedirection();`, hoàn toàn KHÔNG có `app.UseHsts();`).*

---

### Mục 29: Cấu hình Connection Pooling trong Connection String (Tôi nêu: Có tham số min/max pool size | Code: Không có)
Trích nguyên văn từ `backend/FitSocial.API/appsettings.json:8-10`:
```json
  "ConnectionStrings": {
    "DefaultConnection": "Host=ep-round-mountain-a1f94d48-pooler.ap-southeast-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=npg_O6pQZ1SjCckD;SSL Mode=VerifyFull;Channel Binding=Require;",
    "Redis": "fitsocial-redis-fitsocial.upstash.io:6379,password=AXzAAAIjcDE2NTQwYjU4NmU1MmI0N2I5YWFiNWZjNTRlNGFlYmQzOXAxMA,ssl=True,abortConnect=False"
  },
```
*(Chuỗi kết nối chỉ định host pooler của Neon `ep-round-mountain-a1f94d48-pooler...`, không cấu hình cụ thể các tham số `Pooling`, `Minimum Pool Size`, hay `Maximum Pool Size`).*

*Đồng thời, danh sách HasIndex trong `FitSocialDbContext.cs` gồm đúng 7 lệnh (tất cả đều là Unique):*
1. `User.Email`: `entity.HasIndex(e => e.Email, "Users_Email_key").IsUnique();` (`dòng 140`)
2. `User.GoogleProviderId`: `entity.HasIndex(e => e.GoogleProviderId, "Users_GoogleProviderID_key").IsUnique();` (`dòng 141`)
3. `User.PhoneNumber`: `entity.HasIndex(e => e.PhoneNumber, "Users_PhoneNumber_key").IsUnique();` (`dòng 142`)
4. `RefreshToken.TokenHash`: `entity.HasIndex(e => e.TokenHash, "RefreshTokens_TokenHash_key").IsUnique();` (`dòng 340`)
5. `UserBlock.{BlockerId, BlockedId}`: `entity.HasIndex(e => new { e.BlockerId, e.BlockedId }, "UserBlocks_BlockerID_BlockedID_key").IsUnique();` (`dòng 410`)
6. `CoachUpgrade.OrderId`: `entity.HasIndex(e => e.OrderId, "CoachUpgrades_OrderID_key").IsUnique();` (`dòng 960`)
7. `Payout.{CoachId, PayoutMonth, PayoutYear}`: `entity.HasIndex(e => new { e.CoachId, e.PayoutMonth, e.PayoutYear }, "Payouts_CoachID_PayoutMonth_PayoutYear_key").IsUnique();` (`dòng 1229`)

---

### Mục 31: Tệp tin DevOps / Triển khai (Tôi nêu: k6/jmeter, backup script, docker-compose, /health, cpu/ram | Code: NOT FOUND)
- `docker-compose.yml` / `Dockerfile`: Không tồn tại trong root hay backend.
- Script kiểm thử k6 / JMeter (`*.js`, `*.jmx`): Không có trong toàn bộ project.
- Backup script (`.sql`, `.sh`): Không có file script backup nào trong repo (file `.gitignore:67-68` chỉ ignore `*.bak` và `*.sql.bak`).
- Healthcheck endpoint `/health`: `Program.cs` không đăng ký dịch vụ `AddHealthChecks()` và không map route `MapHealthChecks("/health")`.
- Chỉ số CPU / RAM cụ thể: Không có file cấu hình giới hạn tài nguyên CPU/RAM trong repo.

---

### Mục 35: Key `userRole` trong localStorage / sessionStorage (Tôi nêu: Có lưu key userRole | Code: Không lưu)
Trích nguyên văn từ `frontend/FitSocial.Client/Services/Auth/AuthService.cs:281-287`:
```csharp
    private async Task PersistAuthAsync(AuthResponse data)
    {
        if (!string.IsNullOrEmpty(data.AccessToken))
        {
            await _localStorage.SetItemAsync(AuthTokenKey, data.AccessToken);
            await _localStorage.SetItemAsync(RefreshTokenKey, data.RefreshToken);
            ((CustomAuthenticationStateProvider)_authStateProvider).NotifyUserAuthentication(data.AccessToken);
        }
    }
```
Trích nguyên văn từ `frontend/FitSocial.Client/Services/Auth/TokenStorage.cs:22`:
```csharp
public class BrowserTokenStorage : ITokenStorage
{
    private const string RememberKey = "rememberMe";
```
Trích nguyên văn từ `frontend/FitSocial.Client/Services/Auth/CustomAuthenticationStateProvider.cs:44-46`:
```csharp
    var claims = ParseClaimsFromJwt(token);
    var identity = new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role);
    var user = new ClaimsPrincipal(identity);
```
*(Client chỉ lưu 3 key trong storage: `authToken`, `refreshToken`, và `rememberMe`. KHÔNG hề có key `userRole` trong storage; vai trò người dùng được giải mã động từ JWT claim `ClaimTypes.Role`).*

---

## 3. Danh sách tất cả file đã đọc trong quá trình kiểm chứng

1. `backend/FitSocial.Domain/FitSocial.Domain.csproj`
2. `backend/FitSocial.Domain/Entities/Order.cs`
3. `backend/FitSocial.Domain/Entities/CoachEkycVerification.cs`
4. `backend/FitSocial.Domain/Entities/PaymentGatewayConfig.cs`
5. `backend/FitSocial.Domain/Interfaces/IUnitOfWork.cs`
6. `backend/FitSocial.Domain/Constants/OtpConstants.cs`
7. `backend/FitSocial.Application/FitSocial.Application.csproj`
8. `backend/FitSocial.Application/DTOs/Auth/RegisterTraineeRequestDto.cs`
9. `backend/FitSocial.Application/DTOs/Auth/ChangePasswordRequestDto.cs`
10. `backend/FitSocial.Application/DTOs/AuditLogs/GetAuditLogsQueryDto.cs`
11. `backend/FitSocial.Application/DTOs/Common/PagedResultDto.cs`
12. `backend/FitSocial.Application/Interfaces/IEkycService.cs`
13. `backend/FitSocial.Application/Services/AuthService.cs`
14. `backend/FitSocial.Application/Services/TokenService.cs`
15. `backend/FitSocial.Application/Services/PaymentService.cs`
16. `backend/FitSocial.Application/Services/CoachBankAccountService.cs`
17. `backend/FitSocial.Application/Services/ViettelEkycService.cs`
18. `backend/FitSocial.Application/Services/PaymentGatewayConfigService.cs`
19. `backend/FitSocial.Application/Services/AuditLogService.cs`
20. `backend/FitSocial.Application/Services/RefundRequestService.cs`
21. `backend/FitSocial.Application/Services/PayoutService.cs`
22. `backend/FitSocial.Application/Services/AppealService.cs`
23. `backend/FitSocial.Infrastructure/FitSocial.Infrastructure.csproj`
24. `backend/FitSocial.Infrastructure/Data/FitSocialDbContext.cs`
25. `backend/FitSocial.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`
26. `backend/FitSocial.Infrastructure/Repositories/UnitOfWork.cs`
27. `backend/FitSocial.Infrastructure/Repositories/AuditLogRepository.cs`
28. `backend/FitSocial.Infrastructure/Repositories/ReportRepository.cs`
29. `backend/FitSocial.Infrastructure/Services/PasswordHasher.cs`
30. `backend/FitSocial.Infrastructure/Services/JwtTokenGenerator.cs`
31. `backend/FitSocial.Infrastructure/Services/AesEncryptionService.cs`
32. `backend/FitSocial.Infrastructure/Services/EmailService.cs`
33. `backend/FitSocial.API/FitSocial.API.csproj`
34. `backend/FitSocial.API/Program.cs`
35. `backend/FitSocial.API/appsettings.json`
36. `backend/FitSocial.API/Controllers/AuthController.cs`
37. `backend/FitSocial.API/Controllers/UploadController.cs`
38. `backend/FitSocial.API/Controllers/PaymentsController.cs`
39. `backend/FitSocial.API/Middleware/ExceptionHandlingMiddleware.cs`
40. `backend/FitSocial.API/Services/EkycRetentionService.cs`
41. `frontend/FitSocial.Client/FitSocial.Client.csproj`
42. `frontend/FitSocial.Client/Program.cs`
43. `frontend/FitSocial.Client/App.razor`
44. `frontend/FitSocial.Client/Models/Auth/AppRoles.cs`
45. `frontend/FitSocial.Client/Layout/AppShellLayout.razor`
46. `frontend/FitSocial.Client/Layout/AppShellLayout.razor.css`
47. `frontend/FitSocial.Client/Layout/AdminLayout.razor`
48. `frontend/FitSocial.Client/Layout/AppSidebar.razor`
49. `frontend/FitSocial.Client/Layout/AppHeader.razor`
50. `frontend/FitSocial.Client/Layout/MainLayout.razor`
51. `frontend/FitSocial.Client/Pages/Register.razor`
52. `frontend/FitSocial.Client/Pages/Admin/AdminLogin.razor`
53. `frontend/FitSocial.Client/Pages/Admin/Staff.razor`
54. `frontend/FitSocial.Client/Components/LiveFaceCapture.razor`
55. `frontend/FitSocial.Client/Services/Auth/TokenStorage.cs`
56. `frontend/FitSocial.Client/Services/Auth/CustomAuthenticationStateProvider.cs`
57. `frontend/FitSocial.Client/wwwroot/index.html`
58. `frontend/FitSocial.Client/wwwroot/css/app.css`
59. `frontend/FitSocial.Client/wwwroot/css/bootstrap/bootstrap.min.css`
60. `frontend/FitSocial.Client/wwwroot/js/faceCapture.js`
61. `backend/FitSocial.UnitTests/FitSocial.UnitTests.csproj`
62. `backend/FitSocial.UnitTests/AdminUserServiceTests.cs`
63. `backend/FitSocial.UnitTests/AppealServiceTests.cs`
64. `backend/FitSocial.UnitTests/AuditLogServiceTests.cs`
65. `backend/FitSocial.UnitTests/CartServiceTests.cs`
66. `backend/FitSocial.UnitTests/CoachBankAccountServiceTests.cs`
67. `backend/FitSocial.UnitTests/CoachCertificateServiceTests.cs`
68. `backend/FitSocial.UnitTests/CoachDashboardServiceTests.cs`
69. `backend/FitSocial.UnitTests/CoachSubscriptionPlanServiceTests.cs`
70. `backend/FitSocial.UnitTests/ConversationServiceTests.cs`
71. `backend/FitSocial.UnitTests/NotificationServiceTests.cs`
72. `backend/FitSocial.UnitTests/OrderServiceTests.cs`
73. `backend/FitSocial.UnitTests/PaymentGatewayConfigServiceTests.cs`
74. `backend/FitSocial.UnitTests/PostCommentServiceTests.cs`
75. `backend/FitSocial.UnitTests/PostReactionServiceTests.cs`
76. `backend/FitSocial.UnitTests/PostReportServiceTests.cs`
77. `backend/FitSocial.UnitTests/RefundRequestServiceTests.cs`
78. `backend/FitSocial.UnitTests/ReportServiceTests.cs`
79. `backend/FitSocial.UnitTests/RevenueAnalyticsServiceTests.cs`
80. `backend/FitSocial.UnitTests/SubscriptionPriceHistoryServiceTests.cs`
81. `backend/FitSocial.UnitTests/SystemLogServiceTests.cs`
82. `backend/FitSocial.UnitTests/TrainingPackageServiceTests.cs`
83. `.gitignore`
84. `.git/HEAD`
85. `.git/config`
86. `.git/logs/HEAD`

