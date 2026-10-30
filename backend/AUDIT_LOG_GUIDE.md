# Hướng dẫn Tái sử dụng Hệ thống Audit Log (Dành cho Thành viên SEP490-FitConnect)

Tài liệu này hướng dẫn các thành viên trong nhóm phát triển cách sử dụng hệ thống **Audit Log dùng chung** ở Backend khi xây dựng các chức năng quản trị dành cho **Staff** và **Admin**.

---

## 1. Tổng quan Kiến trúc

Hệ thống Audit Log được thiết kế theo chuẩn **Clean Architecture**:
```text
Controller (Staff/Admin)
   │
   ▼
Application Service (e.g. CoachService, RefundRequestService, ...)
   │
   ▼ (Inject & Call LogAsync)
IAuditLogService (FitSocial.Application)
   │
   ├─► ICurrentUserContext (Tự động resolve UserId, Role, IP từ JWT Claims / HttpContext)
   ├─► Bộ lọc nhạy cảm (Tự động che giấu password, token, key thành [REDACTED])
   └─► Serialization an toàn (Chuẩn hóa thành JSON hợp lệ cho PostgreSQL jsonb)
   │
   ▼
IAuditLogRepository / IUnitOfWork (FitSocial.Infrastructure)
   │
   ▼
PostgreSQL Table: "AuditLogs"
```

### Điểm mạnh của hệ thống:
- **Không phải viết lại code tạo log:** Chỉ cần inject `IAuditLogService` và gọi 1 dòng lệnh.
- **Tự động nhận diện Actor:** Tự động lấy ID người thao tác, Role (`STAFF`/`ADMIN`) và IP từ request hiện tại nếu developer không truyền.
- **An toàn bảo mật:** Tự động xóa sạch password, hash, token, apiKey trước khi lưu vào DB.
- **Đồng bộ Transaction:** Khi nghiệp vụ chạy trong database transaction (`BeginTransactionAsync`), audit log được lưu trong cùng transaction và tự động rollback nếu nghiệp vụ lỗi.

---

## 2. Các bước tích hợp vào một Service mới

### Bước 1: Inject `IAuditLogService` vào Constructor

Trong file Service của bạn (tại tầng `FitSocial.Application/Services`):

```csharp
using FitSocial.Application.Interfaces;

namespace FitSocial.Application.Services;

public class TrainingPackageService : ITrainingPackageService
{
    private readonly ITrainingPackageRepository _packageRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService; // 1. Khai báo interface

    public TrainingPackageService(
        ITrainingPackageRepository packageRepo,
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService) // 2. Inject qua DI
    {
        _packageRepo = packageRepo;
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
    }
```

> **Lưu ý:** `IAuditLogService` đã được đăng ký Scoped sẵn trong `ServiceCollectionExtensions.cs`. Bạn không cần phải cấu hình thêm DI gì nữa.

---

### Bước 2: Gọi ghi log sau khi thao tác nghiệp vụ thành công

> **Quy tắc vàng:** Chỉ ghi log **sau khi** nghiệp vụ đã được kiểm tra hợp lệ và lưu thành công (hoặc ngay trước khi commit transaction). Tuyệt đối không ghi log nếu nghiệp vụ bị lỗi hoặc ném ngoại lệ.

#### Trường hợp 1: Phê duyệt (APPROVE) hoặc Từ chối (REJECT)

```csharp
public async Task<ApiResponseDto<bool>> ApprovePackageAsync(Guid packageId, CancellationToken ct)
{
    var package = await _packageRepo.GetByIdAsync(packageId, ct);
    if (package == null) return ApiResponseDto<bool>.Fail("Package not found");

    var oldStatus = package.Status;
    package.Status = "ACTIVE";
    package.UpdatedAt = DateTime.UtcNow;

    await _unitOfWork.SaveChangesAsync(ct);

    // Ghi Audit Log:
    // - actorAccountId để null -> Hệ thống tự động lấy từ JWT Claims của Staff/Admin đang đăng nhập
    // - ipAddress để null -> Hệ thống tự động lấy IP client
    await _auditLogService.LogAsync(
        actorAccountId: null,
        action: "APPROVE_PACKAGE",
        entityType: "TrainingPackage",
        entityId: package.PackageId.ToString(),
        description: $"Training package '{package.Title}' was approved by staff.",
        oldValue: new { status = oldStatus },
        newValue: new { status = "ACTIVE" },
        cancellationToken: ct);

    return ApiResponseDto<bool>.Ok(true, "Package approved successfully");
}
```

#### Trường hợp 2: Cập nhật dữ liệu (UPDATE) - Lưu OldValue và NewValue

Hệ thống hỗ trợ truyền Anonymous Object hoặc DTO bất kỳ vào `oldValue` và `newValue`, hệ thống sẽ tự động chuyển thành JSON lưu vào cột `jsonb`:

```csharp
public async Task<ApiResponseDto<bool>> UpdatePackagePriceAsync(Guid packageId, decimal newPrice, CancellationToken ct)
{
    var package = await _packageRepo.GetByIdAsync(packageId, ct);
    if (package == null) return ApiResponseDto<bool>.Fail("Package not found");

    var oldPrice = package.Price;
    package.Price = newPrice;
    package.UpdatedAt = DateTime.UtcNow;

    await _unitOfWork.SaveChangesAsync(ct);

    await _auditLogService.LogAsync(
        actorAccountId: null,
        action: "UPDATE_PACKAGE_PRICE",
        entityType: "TrainingPackage",
        entityId: package.PackageId.ToString(),
        description: $"Price for package '{package.Title}' updated from {oldPrice:N0} to {newPrice:N0} VND.",
        oldValue: new { price = oldPrice },
        newValue: new { price = newPrice },
        cancellationToken: ct);

    return ApiResponseDto<bool>.Ok(true, "Price updated successfully");
}
```

#### Trường hợp 3: Khóa tài khoản / Xử lý vi phạm (LOCK / WARN)

```csharp
await _auditLogService.LogAsync(
    actorAccountId: staffId, // Nếu method của bạn đã có sẵn staffId thì truyền trực tiếp
    action: isLocked ? "LOCK_USER" : "UNLOCK_USER",
    entityType: "User",
    entityId: targetUser.UserId.ToString(),
    description: $"User '{targetUser.FullName}' was {(isLocked ? "locked" : "unlocked")}. Reason: {reason}",
    oldValue: new { isLocked = !isLocked },
    newValue: new { isLocked, reason },
    cancellationToken: ct);
```

#### Trường hợp 4: Chạy trong Background Job hoặc hàm không có HTTP Context

Nếu bạn chạy trong Background Job (ví dụ `IHostedService`, hàng đợi nền), HTTP context sẽ không tồn tại. Lúc này chỉ cần **truyền rõ `actorAccountId` và `actorRole`**:

```csharp
await _auditLogService.LogAsync(
    actorAccountId: systemAdminId, // hoặc Guid hệ thống
    action: "AUTO_CLEANUP_EXPIRED",
    entityType: "EkycVerification",
    entityId: verificationId.ToString(),
    description: "System automatically purged expired biometric data.",
    actorRole: "SYSTEM",
    cancellationToken: ct);
```

---

## 3. Bảng Quy chuẩn Đặt tên (Action & EntityType)

Để bảo đảm việc lọc và tìm kiếm log trên giao diện Admin hiển thị đồng nhất, hãy tuân thủ quy chuẩn đặt tên sau:

### Quy chuẩn `Action`
Luôn viết **IN HOA** dạng `VERB_NOUN`:
- `CREATE_*`: Tạo mới (`CREATE_STAFF`, `CREATE_REFUND_REQUEST`)
- `UPDATE_*`: Cập nhật (`UPDATE_STAFF`, `UPDATE_PRICE`)
- `DELETE_*`: Xóa (`DELETE_POST`)
- `APPROVE_*`: Duyệt (`APPROVE_COACH_CERTIFICATE`, `APPROVE_COACH_APPLICATION`, `APPROVE_REFUND`)
- `REJECT_*`: Từ chối (`REJECT_COACH_CERTIFICATE`, `REJECT_COACH_APPLICATION`, `REJECT_REFUND`)
- `LOCK_*` / `UNLOCK_*`: Khóa / Mở khóa (`LOCK_USER`, `UNLOCK_USER`, `LOCK_STAFF`)
- `WARN_*`: Cảnh cáo vi phạm (`WARN_USER`)

### Quy chuẩn `EntityType`
Luôn viết theo chuẩn PascalCase của tên thực thể:
- `User`
- `Staff`
- `CoachProfile`
- `CoachCertificate`
- `CoachSubscriptionPlan`
- `PaymentGatewayConfig`
- `RefundRequest`
- `TrainingPackage`
- `Post`
- `Payout`

---

## 3.1. Các Action đã được ghi cho chức năng quản trị (Staff/Admin)

Những chức năng dưới đây **đã được tích hợp sẵn** — mỗi lần thao tác thành công đều ghi 1 dòng
vào bảng `"AuditLogs"`:

| Chức năng (Use Case)                | `Action`                            | `EntityType`              | Service chịu trách nhiệm          |
| ----------------------------------- | ----------------------------------- | ------------------------- | --------------------------------- |
| Log In to Management Portal         | `LOGIN_MANAGEMENT_PORTAL`           | `User`                    | `AuthService.AdminLoginAsync`     |
| Staff/Admin đổi mật khẩu            | `CHANGE_PASSWORD`                   | `User`                    | `AuthService.ChangePasswordAsync` |
| Tạo Coach Subscription Plan         | `CREATE_COACH_SUBSCRIPTION_PLAN`    | `CoachSubscriptionPlan`   | `CoachSubscriptionPlanService`    |
| Sửa Coach Subscription Plan         | `UPDATE_COACH_SUBSCRIPTION_PLAN`    | `CoachSubscriptionPlan`   | `CoachSubscriptionPlanService`    |
| Xóa Coach Subscription Plan         | `DELETE_COACH_SUBSCRIPTION_PLAN`    | `CoachSubscriptionPlan`   | `CoachSubscriptionPlanService`    |
| Thêm Payment Account                | `ADD_PAYMENT_ACCOUNT`               | `PaymentGatewayConfig`    | `PaymentGatewayConfigService`     |
| Sửa Payment Account                 | `EDIT_PAYMENT_ACCOUNT`              | `PaymentGatewayConfig`    | `PaymentGatewayConfigService`     |
| Bật Payment Account                 | `ACTIVATE_PAYMENT_ACCOUNT`          | `PaymentGatewayConfig`    | `PaymentGatewayConfigService`     |
| Tắt Payment Account                 | `DEACTIVATE_PAYMENT_ACCOUNT`        | `PaymentGatewayConfig`    | `PaymentGatewayConfigService`     |
| Xóa Payment Account                 | `DELETE_PAYMENT_ACCOUNT`            | `PaymentGatewayConfig`    | `PaymentGatewayConfigService`     |
| Generate chu kỳ chi trả            | `GENERATE_COACH_PAYOUT`             | `Payout`                  | `PayoutService`                   |
| Process Coach Payout (UC-23.1)      | `PAYOUT_PROCESSED` / `PAYOUT_PARTIAL`| `Payout`                 | `PayoutService`                   |
| Payout bị chặn / lỗi gateway       | `PAYOUT_BLOCKED` / `PAYOUT_FAILED` / `PAYOUT_SUBMITTED` / `PAYOUT_NOOP` | `Payout` | `PayoutService` |

> **Lưu ý bảo mật:**
> - `CHANGE_PASSWORD` chỉ ghi `tokenVersion` (số phiên bị vô hiệu), **không bao giờ ghi mật khẩu**.
> - Payment Account chỉ ghi cờ `apiKeyPresent` / `apiKeyRotated`, **không ghi giá trị key**.
>
> Bạn viết chức năng mới thì chọn `Action` theo bảng trên và đặt `actorAccountId: null` để hệ thống
> tự lấy Staff/Admin đang đăng nhập.

---

## 4. Cách Kiểm tra và Truy vấn Log

### Cách 1: Qua API (Swagger / Postman)
Endpoint dành riêng cho Staff và Admin:
- **Danh sách log phân trang & tìm kiếm:**
  ```http
  GET /api/audit-logs?entityType=CoachCertificate&pageNumber=1&pageSize=10
  Authorization: Bearer <token_staff_hoac_admin>
  ```
- **Chi tiết 1 log:**
  ```http
  GET /api/audit-logs/{id}
  Authorization: Bearer <token_staff_hoac_admin>
  ```

### Cách 2: Qua PostgreSQL Query
```sql
SELECT 
    "AuditLogID",
    "Action",
    "EntityType",
    "EntityID",
    "ActorAccountID",
    "IPAddress",
    "NewValue",
    "CreatedAt"
FROM "AuditLogs"
ORDER BY "CreatedAt" DESC
LIMIT 10;
```

---

## 5. Cách Viết Unit Test khi Service có sử dụng `IAuditLogService`

Khi viết Unit Test cho một Service có inject `IAuditLogService`, bạn sử dụng `Moq`:

```csharp
using FitSocial.Application.Interfaces;
using Moq;
using Xunit;

public class MyServiceTests
{
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly MyService _service;

    public MyServiceTests()
    {
        _mockAuditLogService = new Mock<IAuditLogService>();
        _service = new MyService(..., _mockAuditLogService.Object);
    }

    [Fact]
    public async Task Approve_WhenSuccessful_ShouldRecordAuditLog()
    {
        // Act
        var result = await _service.ApproveAsync(testId);

        // Assert - Xác nhận LogAsync đã được gọi đúng Action và EntityId
        Assert.True(result.Success);
        _mockAuditLogService.Verify(a => a.LogAsync(
            It.IsAny<Guid?>(),
            "APPROVE_PACKAGE",
            "TrainingPackage",
            testId.ToString(),
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Approve_WhenNotFound_ShouldNotRecordAuditLog()
    {
        // Act
        var result = await _service.ApproveAsync(nonExistentId);

        // Assert - Khi thao tác thất bại, tuyệt đối không được ghi log thành công
        Assert.False(result.Success);
        _mockAuditLogService.Verify(a => a.LogAsync(
            It.IsAny<Guid?>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

---

## 6. Lưu ý Quan trọng

1. **Không log dữ liệu mật:** Mặc dù hệ thống đã có bộ lọc tự động che giấu `password`, `token`, developer vẫn nên chủ động không truyền nguyên đối tượng chứa mật khẩu hoặc mã OTP vào `oldValue`/`newValue`.
2. **Không bọc try-catch nuốt lỗi audit log nếu đó là bắt buộc:** Nếu việc ghi log là bắt buộc của một hành động nhạy cảm tài chính/pháp lý, để lỗi ném ra ngoài để transaction rollback hoàn toàn.
3. **Giữ description ngắn gọn, súc tích:** Trường `description` giúp admin đọc nhanh trên giao diện quản trị mà không cần mở xem chi tiết JSON.
