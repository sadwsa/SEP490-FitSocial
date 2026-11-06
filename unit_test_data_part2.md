# PHẦN A: DỮ LIỆU BÁO CÁO UNIT TEST (`unit_test_data_part2.md`)

- **Dự án**: `SEP490-FitSocial`
- **Tài liệu đích**: `Report5_Unit Test Case.xlsx`
- **Người thực hiện (Created By / Executed By / Tester)**: `HATTV`
- **Ngày chạy kiểm thử**: `2026-11-03`

---

## A1. Kết Quả Thực Thi Lệnh `dotnet test` Thực Tế (TRX Logger)

### 1. Lệnh thực thi
```powershell
dotnet test backend/FitSocial.UnitTests/FitSocial.UnitTests.csproj --filter "FullyQualifiedName~FitSocial.UnitTests.StaffAdminProfileServiceTests|FullyQualifiedName~FitSocial.UnitTests.CoachApplicationsServiceTests|FullyQualifiedName~FitSocial.UnitTests.AdminUserServiceTests|FullyQualifiedName~FitSocial.UnitTests.LocationServiceTests|FullyQualifiedName~FitSocial.UnitTests.SystemLogServiceTests|FullyQualifiedName~FitSocial.UnitTests.UpdateOwnProfileServiceTests|FullyQualifiedName~FitSocial.UnitTests.CartServiceTests|FullyQualifiedName~FitSocial.UnitTests.ReportServiceTests|FullyQualifiedName~FitSocial.UnitTests.ManagementControllersAuthorizationTests" --no-restore --logger "trx;LogFileName=part2_tests.trx"
```

### 2. Thông tin kết quả từ file TRX (`backend/FitSocial.UnitTests/TestResults/part2_tests.trx`)
- **Test Runner**: `VSTest version 17.14.1 (x64)` — Target: `backend/FitSocial.UnitTests/bin/Debug/net8.0/FitSocial.UnitTests.dll` (`.NETCoreApp,Version=v8.0`)
- **Start Time**: `2026-11-03T23:29:42.2321695+07:00`
- **Finish Time**: `2026-11-03T23:29:42.9428015+07:00`
- **Creation Time**: `2026-11-03T23:29:42.7858900+07:00`
- **Ngày ghi nhận vào báo cáo (`YYYY-MM-DD`)**: `2026-11-03`
- **Tổng kết thực thi toàn bộ 9 Test Classes**:
  - **Total Test Executions**: `316`
  - **Passed**: `316`
  - **Failed**: `0`
  - **Skipped / Untested**: `0`
  - **Execution Duration**: `172 ms`

### 3. Thống kê số lượng Test Executions theo từng Test File
| STT | Test Class (`FitSocial.UnitTests`) | Đường dẫn file test | Total | Passed | Failed | Skipped |
| :-: | :--- | :--- | :---: | :---: | :---: | :---: |
| 1 | `StaffAdminProfileServiceTests` | `backend/FitSocial.UnitTests/StaffAdminProfileServiceTests.cs` | 23 | 23 | 0 | 0 |
| 2 | `CoachApplicationsServiceTests` | `backend/FitSocial.UnitTests/CoachApplicationsServiceTests.cs` | 28 | 28 | 0 | 0 |
| 3 | `AdminUserServiceTests` | `backend/FitSocial.UnitTests/AdminUserServiceTests.cs` | 46 | 46 | 0 | 0 |
| 4 | `LocationServiceTests` | `backend/FitSocial.UnitTests/LocationServiceTests.cs` | 72 | 72 | 0 | 0 |
| 5 | `SystemLogServiceTests` | `backend/FitSocial.UnitTests/SystemLogServiceTests.cs` | 20 | 20 | 0 | 0 |
| 6 | `UpdateOwnProfileServiceTests` | `backend/FitSocial.UnitTests/UpdateOwnProfileServiceTests.cs` | 17 | 17 | 0 | 0 |
| 7 | `CartServiceTests` | `backend/FitSocial.UnitTests/CartServiceTests.cs` | 52 | 52 | 0 | 0 |
| 8 | `ReportServiceTests` | `backend/FitSocial.UnitTests/ReportServiceTests.cs` | 43 *(19 cho `ReportUserAsync` + 24 cho Report Moderation)* | 43 | 0 | 0 |
| 9 | `ManagementControllersAuthorizationTests` | `backend/FitSocial.UnitTests/ManagementControllersAuthorizationTests.cs` | 15 | 15 | 0 | 0 |
| **Tổng** | **9 Test Classes** | | **316** | **316** | **0** | **0** |

---

## A2. Bảng Tổng Hợp `FunctionList` (19 Dòng Cho 19 Chức Năng)

*(Quy ước đếm LOC: Đếm chính xác số dòng code thực thi bên trong thân hàm từ sau dấu `{` mở hàm đến trước dấu `}` đóng hàm, hoàn toàn không tính dòng trống và dòng comment `//`. Trong ngoặc ghi kèm số dòng câu lệnh thuần không tính các dòng chỉ chứa dấu `{` hoặc `}` đơn lẻ).*

| STT | Requirement Name | Class Name | Function Name | Function Code | Sheet Name (<=31 ký tự) | Description | Pre-Condition | LOC của hàm |
| :-: | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :---: |
| 1 | Update Own Profile(admin/staff) | `UserService` | Update Own Profile (Admin/Staff) | `UpdateStaffAdminProfileAsync` | `UpdateStaffAdminProfileAsync` (28 ký tự) | Cập nhật thông tin hồ sơ cá nhân (FullName, PhoneNumber, DateOfBirth, Gender, Avatar) dành riêng cho tài khoản nội bộ có role `STAFF` hoặc `ADMIN`. | `IUserRepository`, `IUnitOfWork` được khởi tạo; `userId` tồn tại trong DB, `IsLocked != true`, và `RoleCode` thuộc `{"STAFF", "ADMIN"}`. | **112** *(65 câu lệnh không tính `{}`)* |
| 2 | View Coach Application Requests | `CoachService` | View Coach Application Requests | `GetCoachApplicationsAsync` | `GetCoachApplicationsAsync` (25 ký tự) | Truy vấn danh sách hồ sơ đăng ký làm Coach có phân trang, lọc theo trạng thái (`ALL`/`PENDING`/`APPROVED`/`REJECTED`), tìm kiếm theo từ khóa và thống kê số lượng theo từng trạng thái. | `ICoachProfileRepository` được khởi tạo; người gọi có role `STAFF` hoặc `ADMIN`. | **49** *(41 câu lệnh không tính `{}`; nếu gộp cả `GetCoachApplicationDetailsAsync` [45] & `GetCoachApplicationStatusCountsAsync` [15] là **109**)* |
| 3 | Approve / Reject a coach application | `CoachService` | Approve / Reject Coach Application | `ApproveCoachApplicationAsync` / `RejectCoachApplicationAsync` | `ApproveRejectCoachAppAsync` (26 ký tự) | Duyệt (`ApproveCoachApplicationAsync`) hoặc từ chối (`RejectCoachApplicationAsync`) hồ sơ đăng ký Coach đang ở trạng thái `PENDING`, cập nhật trạng thái tài khoản, chứng chỉ, eKYC, ghi System Log và gửi email thông báo. | `ICoachProfileRepository`, `IUserRepository`, `IUnitOfWork`, `ISystemLogService`, `IEmailService` được khởi tạo; hồ sơ Coach tồn tại và có `ApprovalStatus == "PENDING"`. | **238** (`Approve`: **130** [87] + `Reject`: **108** [71]) |
| 4 | View List User's Account | `AdminUserService` | View List User's Account | `GetUsersAsync` | `GetUsersAsync` (13 ký tự) | Lấy danh sách tài khoản người dùng (`TRAINEE`, `COACH`) có phân trang, hỗ trợ tìm kiếm theo tên/email và lọc theo `role` hoặc trạng thái khóa `isLocked`. | `IUserRepository` được khởi tạo; người gọi có role `STAFF` hoặc `ADMIN`. | **12** *(8 câu lệnh không tính `{}`)* |
| 5 | View Location List | `LocationService` | View Location List | `GetPagedLocationsAsync` | `GetPagedLocationsAsync` (22 ký tự) | Lấy danh sách địa điểm/phòng tập có phân trang và tìm kiếm theo từ khóa (kèm hàm lấy danh sách công khai `GetPublicLocationsAsync`), đồng thời tính số lượng bài viết đang hoạt động (`PostCount`) tại mỗi địa điểm. | `ILocationRepository` được khởi tạo; người gọi có role `STAFF` hoặc `ADMIN` (hoặc `Anonymous` với endpoint public). | **18** *(13 câu lệnh không tính `{}`; nếu gộp `GetLocationsAsync` [1] & `GetPublicLocationsAsync` [16] là **35**)* |
| 6 | Create Location | `LocationService` | Create Location | `CreateLocationAsync` | `CreateLocationAsync` (19 ký tự) | Kiểm tra tính hợp lệ của tên và địa chỉ phòng tập (không rỗng, tối đa 200 ký tự sau khi trim, không trùng tên) và lưu địa điểm mới vào cơ sở dữ liệu. | `ILocationRepository`, `IUnitOfWork` được khởi tạo; `dto != null`, `LocationName` chưa tồn tại trong DB. | **49** *(31 câu lệnh không tính `{}`)* |
| 7 | Edit Location | `LocationService` | Edit Location | `UpdateLocationAsync` | `UpdateLocationAsync` (19 ký tự) | Cập nhật tên và địa chỉ của một phòng tập đã tồn tại theo `locationId` sau khi kiểm tra dữ liệu đầu vào và đảm bảo tên mới không trùng với địa điểm khác. | `ILocationRepository`, `IUnitOfWork` được khởi tạo; `locationId` tồn tại trong DB và `LocationName` không trùng với bản ghi khác. | **49** *(30 câu lệnh không tính `{}`)* |
| 8 | Delete Location | `LocationService` | Delete Location | `DeleteLocationAsync` | `DeleteLocationAsync` (19 ký tự) | Xóa một địa điểm phòng tập khỏi hệ thống nếu địa điểm tồn tại và hiện không gắn với bất kỳ bài viết đang hoạt động (`IsDeleted != true`) hoặc hồ sơ huấn luyện viên (`Coaches`) nào. | `ILocationRepository`, `IUnitOfWork` được khởi tạo; `locationId` tồn tại, không có bài viết active và không có Coach liên kết. | **23** *(13 câu lệnh không tính `{}`)* |
| 9 | View System Logs | `SystemLogService` | View System Logs | `GetSystemLogsAsync` | `GetSystemLogsAsync` (18 ký tự) | Truy vấn nhật ký hệ thống (System Logs) có phân trang, chuẩn hóa `pageNumber >= 1` và `pageSize <= 100`, tự động phân loại mức độ log (`Information`/`Warning`/`Error`) và ẩn thông tin nhạy cảm (`Sanitize`). | `ISystemLogRepository` được khởi tạo; người gọi có role `ADMIN`. | **25** *(21 câu lệnh không tính `{}`; nếu tính cả `GetLogDetailAsync` [25], `DetermineLogLevel` [18], `Sanitize` [26] là **94**)* |
| 10 | View Staff Accounts List | `AdminUserService` | View Staff Accounts List | `GetStaffUsersAsync` | `GetStaffUsersAsync` (18 ký tự) | Lấy danh sách tài khoản nhân viên (`RoleCode == "STAFF"`) có phân trang, hỗ trợ tìm kiếm theo tên/email và lọc theo trạng thái khóa (`isLocked`). | `IUserRepository` được khởi tạo; người gọi có role `ADMIN`. | **12** *(8 câu lệnh không tính `{}`)* |
| 11 | Create Staff Accounts | `AdminUserService` | Create Staff Accounts | `CreateStaffAccountAsync` | `CreateStaffAccountAsync` (23 ký tự) | Tạo mới tài khoản nhân viên (`STAFF`, `IsInternal = true`) sau khi kiểm tra định dạng FullName, Email, PhoneNumber, Password/ConfirmPassword, kiểm tra trùng Email/Phone, băm mật khẩu và ghi System Log `CREATE_STAFF`. | `IUserRepository`, `IPasswordHasher`, `IUnitOfWork`, `ISystemLogService` được khởi tạo; Email và PhoneNumber chưa được đăng ký. | **97** *(63 câu lệnh không tính `{}`; nếu tính thêm `CheckEmailExistsAsync` [15] là **112**)* |
| 12 | Edit Staff Accounts | `AdminUserService` | Edit Staff Accounts | `UpdateStaffAccountAsync` | `UpdateStaffAccountAsync` (23 ký tự) | Cập nhật thông tin tài khoản nhân viên (`FullName`, `Email`, `PhoneNumber`, và tùy chọn đặt lại `Password` kèm tăng `TokenVersion`) và ghi System Log `UPDATE_STAFF`. | `IUserRepository`, `IPasswordHasher`, `IUnitOfWork`, `ISystemLogService` được khởi tạo; `userId` tồn tại và có `RoleCode == "STAFF"`. | **103** *(63 câu lệnh không tính `{}`)* |
| 13 | Lock Staff Account | `AdminUserService` | Lock Staff Account | `SetUserLockStatusAsync` | `LockStaffAccountAsync` (21 ký tự) | Khóa tài khoản nhân viên (`isLocked = true`), lưu `LockedBy = adminId`, tăng `TokenVersion` thêm 1 để thu hồi phiên đăng nhập hiện tại và ghi System Log `LOCK_USER`. | `IUserRepository`, `IUnitOfWork`, `ISystemLogService` được khởi tạo; `userId` tồn tại và khác `currentUserId` của Admin đang thao tác. | **42** *(28 câu lệnh không tính `{}`)* |
| 14 | Unlock Staff Account | `AdminUserService` | Unlock Staff Account | `SetUserLockStatusAsync` | `UnlockStaffAccountAsync` (23 ký tự) | Mở khóa tài khoản nhân viên (`isLocked = false`), xóa `LockedBy = null`, giữ nguyên `TokenVersion` và ghi System Log `UNLOCK_USER`. | `IUserRepository`, `IUnitOfWork`, `ISystemLogService` được khởi tạo; `userId` tồn tại trong hệ thống. | **42** *(28 câu lệnh không tính `{}`)* |
| 15 | Update own Profile(trainee/coach) | `UserService` | Update Own Profile (Trainee/Coach) | `UpdateOwnProfileAsync` | `UpdateOwnProfileAsync` (21 ký tự) | Cập nhật thông tin hồ sơ cá nhân của `TRAINEE` hoặc `COACH` (bao gồm cả `ExperienceYears` từ 0..60 và `Bio` tối đa 2000 ký tự đối với Coach) và trả về DTO tương ứng với vai trò. | `IUserRepository`, `IUnitOfWork` được khởi tạo; `userId` tồn tại, `IsLocked != true`, và `RoleCode` thuộc `{"TRAINEE", "COACH"}`. | **156** *(94 câu lệnh không tính `{}`)* |
| 16 | View Cart | `CartService` | View Cart | `GetCartAsync` | `GetCartAsync` (12 ký tự) | Lấy chi tiết giỏ hàng của người dùng hiện tại, kiểm tra tính khả dụng của từng gói tập (`IsActive`, chặn Coach mua gói của chính mình), tính tổng tiền (`Subtotal`, `TotalPrice`, `ApproxUsd`) và số lượng gói trong giỏ (`GetCartCountAsync`). | `ICartRepository`, `IUserRepository` được khởi tạo; người dùng đã xác thực với role `TRAINEE` hoặc `COACH`. | **101** *(81 câu lệnh không tính `{}`; nếu gộp `GetCartCountAsync` [13] là **114**)* |
| 17 | Remove from Cart | `CartService` | Remove from Cart | `RemoveCartItemAsync` | `RemoveCartItemAsync` (19 ký tự) | Xóa một gói tập cụ thể khỏi giỏ hàng (`RemoveCartItemAsync`) sau khi xác thực quyền sở hữu `cart.UserId == userId`, hoặc xóa toàn bộ giỏ hàng của người dùng (`ClearCartAsync`). | `ICartRepository`, `IUnitOfWork` được khởi tạo; `cartId` tồn tại và thuộc về `userId` đang đăng nhập. | **15** *(9 câu lệnh không tính `{}`; nếu gộp `ClearCartAsync` [10] là **25**)* |
| 18 | Submit Coach Complaint | `ReportService` | Submit Coach Complaint | `ReportUserAsync` | `ReportUserAsync` (15 ký tự) | Cho phép học viên/người dùng gửi báo cáo khiếu nại Coach hoặc người dùng khác kèm lý do (`Reason`), mô tả chi tiết (`Description`) và danh sách hình ảnh/video bằng chứng (`MediaUrls`). | `IReportRepository`, `IUserRepository`, `IUnitOfWork` được khởi tạo; `reporterId != Guid.Empty`, `ReportedUserId` tồn tại và `reporterId != ReportedUserId`. | **54** *(38 câu lệnh không tính `{}`)* |
| 19 | Add To Cart | `CartService` | Add To Cart | `AddToCartAsync` | `AddToCartAsync` (14 ký tự) | Thêm một gói tập vào giỏ hàng với số lượng cố định là `1` sau khi kiểm tra gói tập tồn tại, đang hoạt động (`IsActive != false`), không phải gói của chính Coach đó và chưa có trong giỏ hàng. | `ICartRepository`, `ITrainingPackageRepository`, `IUnitOfWork` được khởi tạo; `userId != Guid.Empty`, gói tập hợp lệ và chưa nằm trong giỏ. | **76** *(54 câu lệnh không tính `{}`)* |

---

## A3. Chi Tiết Từng Chức Năng (19 Sheets Độc Lập Cho `Report5_Unit Test Case.xlsx`)

---

### CHỨC NĂNG 1: Update Own Profile(admin/staff) (`UOPAS`) — Sheet `UpdateStaffAdminProfileAsync`

#### 1. Header Info
- **Function Code**: `UpdateStaffAdminProfileAsync` *(kèm `GetStaffAdminProfileAsync` & Controller Auth)*
- **Function Name**: `Update Own Profile(admin/staff)`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `112` *(riêng `UpdateStaffAdminProfileAsync`: 112 dòng thực thi / 65 câu lệnh; `GetStaffAdminProfileAsync`: 39 dòng)*
- **Test requirement**: `Verify that UpdateStaffAdminProfileAsync (and GetStaffAdminProfileAsync) correctly retrieves and updates personal profile fields for ADMIN and STAFF accounts, retains existing values when optional inputs are blank, clears avatar when requested, and throws appropriate ValidationException, ConflictException, ForbiddenException, or NotFoundException for invalid inputs, duplicate phone numbers, locked accounts, or unauthorized roles.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **24** | **0** | **0** | **7** | **13** | **4** | **24** |
  *(Ghi chú: File `StaffAdminProfileServiceTests.cs` có 23 test cases [6 cho `GetStaffAdminProfileAsync` + 17 cho `UpdateStaffAdminProfileAsync`] + 1 test phân quyền trong `ManagementControllersAuthorizationTests.cs` = **24** UTCIDs. Nếu chỉ điền riêng 17 test gọi trực tiếp `UpdateStaffAdminProfileAsync`, dùng `UTCID07`..`UTCID23` với thống kê: Passed=17, Failed=0, Untested=0, N=4, A=9, B=4, Total=17).*

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID24`
| UTCID | Tên test method gốc (trong `StaffAdminProfileServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `GetStaffAdminProfileAsync_WhenStaff_ReturnsCorrectProfile` | N | Passed | 2026-11-03 |
| `UTCID02` | `GetStaffAdminProfileAsync_WhenAdmin_ReturnsCorrectProfile` | N | Passed | 2026-11-03 |
| `UTCID03` | `GetStaffAdminProfileAsync_WhenUserNotFound_ThrowsNotFoundException` | A | Passed | 2026-11-03 |
| `UTCID04` | `GetStaffAdminProfileAsync_WhenAccountIsLocked_ThrowsForbiddenException` | A | Passed | 2026-11-03 |
| `UTCID05` | `GetStaffAdminProfileAsync_WhenUserIsTrainee_ThrowsForbiddenException` | A | Passed | 2026-11-03 |
| `UTCID06` | `GetStaffAdminProfileAsync_WhenUserIsCoach_ThrowsForbiddenException` | A | Passed | 2026-11-03 |
| `UTCID07` | `UpdateStaffAdminProfileAsync_WhenAdmin_UpdatesProfileSuccessfully` | N | Passed | 2026-11-03 |
| `UTCID08` | `UpdateStaffAdminProfileAsync_WhenStaff_UpdatesProfileSuccessfully` | N | Passed | 2026-11-03 |
| `UTCID09` | `UpdateStaffAdminProfileAsync_WhenFieldsEmptyOrNull_RetainsExistingValues` | N | Passed | 2026-11-03 |
| `UTCID10` | `UpdateStaffAdminProfileAsync_WhenFullNameHasNumbers_ThrowsValidationException` | A | Passed | 2026-11-03 |
| `UTCID11` | `UpdateStaffAdminProfileAsync_WhenPhoneFormatInvalid_ThrowsValidationException` | A | Passed | 2026-11-03 |
| `UTCID12` | `UpdateStaffAdminProfileAsync_WhenPhoneAlreadyExists_ThrowsConflictException` | A | Passed | 2026-11-03 |
| `UTCID13` | `UpdateStaffAdminProfileAsync_WhenDobInFuture_ThrowsValidationException` | A | Passed | 2026-11-03 |
| `UTCID14` | `UpdateStaffAdminProfileAsync_WhenUserNotFound_ThrowsNotFoundException` | A | Passed | 2026-11-03 |
| `UTCID15` | `UpdateStaffAdminProfileAsync_WhenUserIsLocked_ThrowsForbiddenException` | A | Passed | 2026-11-03 |
| `UTCID16` | `UpdateStaffAdminProfileAsync_WhenNotStaffOrAdmin_ThrowsForbiddenException` | A | Passed | 2026-11-03 |
| `UTCID17` | `UpdateStaffAdminProfileAsync_WhenAvatarExplicitlyRemoved_ClearsAvatarUrl` | N | Passed | 2026-11-03 |
| `UTCID18` | `UpdateStaffAdminProfileAsync_NullRequest_ThrowsValidationException` | A | Passed | 2026-11-03 |
| `UTCID19` | `UpdateStaffAdminProfileAsync_FullNameLength1_ThrowsValidationException` | B | Passed | 2026-11-03 |
| `UTCID20` | `UpdateStaffAdminProfileAsync_FullNameLength2_UpdatesSuccessfully` | B | Passed | 2026-11-03 |
| `UTCID21` | `UpdateStaffAdminProfileAsync_FullNameLength100_UpdatesSuccessfully` | B | Passed | 2026-11-03 |
| `UTCID22` | `UpdateStaffAdminProfileAsync_FullNameLength101_ThrowsValidationException` | B | Passed | 2026-11-03 |
| `UTCID23` | `UpdateStaffAdminProfileAsync_WhenDatabaseThrowsException_ThrowsException` | A | Passed | 2026-11-03 |
| `UTCID24` | `UsersController_ProfileEndpoints_EnforceRoleRestrictions` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Target Operation** | `GetStaffAdminProfileAsync(userId)` | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID05, UTCID06` |
| | `UpdateStaffAdminProfileAsync(userId, request)` | `UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID13, UTCID14, UTCID15, UTCID16, UTCID17, UTCID18, UTCID19, UTCID20, UTCID21, UTCID22, UTCID23` |
| | `UsersController` `[Authorize]` attribute reflection check | `UTCID24` |
| **2. User Existence (`_userRepo.GetByIdAsync`)** | Exists (`User` entity returned) | `UTCID01, UTCID02, UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID13, UTCID15, UTCID16, UTCID17, UTCID19, UTCID20, UTCID21, UTCID22, UTCID23` |
| | Not found (`null`) | `UTCID03, UTCID14` |
| | Not invoked (short-circuited before repo call or reflection test) | `UTCID18, UTCID24` |
| **3. Account Lock Status (`user.IsLocked`)** | `false` (Unlocked) | `UTCID01, UTCID02, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID13, UTCID16, UTCID17, UTCID19, UTCID20, UTCID21, UTCID22, UTCID23` |
| | `true` (Locked) | `UTCID04, UTCID15` |
| | N/A (User not found / Null request / Reflection) | `UTCID03, UTCID14, UTCID18, UTCID24` |
| **4. User Role (`user.RoleCode`)** | `"ADMIN"` | `UTCID02, UTCID07, UTCID09, UTCID10, UTCID15, UTCID17, UTCID19, UTCID20, UTCID21, UTCID22, UTCID23` |
| | `"STAFF"` | `UTCID01, UTCID04, UTCID08, UTCID11, UTCID12, UTCID13` |
| | `"TRAINEE"` | `UTCID05, UTCID16` |
| | `"COACH"` | `UTCID06` |
| | N/A (User not found / Null request / Controller attribute check) | `UTCID03, UTCID14, UTCID18, UTCID24` |
| **5. Request DTO (`UpdateStaffAdminProfileRequestDto`)** | Valid DTO with updated fields | `UTCID07, UTCID08, UTCID14, UTCID15, UTCID16, UTCID23` |
| | Valid DTO with blank/null fields (`FullName = ""`, `PhoneNumber = "   "`, `Gender = null`, `DateOfBirth = null`) | `UTCID09` |
| | Valid DTO with `RemoveAvatar = true`, `Avatar = null` | `UTCID17` |
| | `null` (`request == null`) | `UTCID18` |
| | Invalid / Boundary field values (`FullName`, `PhoneNumber`, `DateOfBirth`) | `UTCID10, UTCID11, UTCID12, UTCID13, UTCID19, UTCID20, UTCID21, UTCID22` |
| | N/A (`GetStaffAdminProfileAsync` / Reflection) | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID05, UTCID06, UTCID24` |
| **6. `request.FullName` Value** | Valid normal string (`"New Admin Name"`, `"Staff Tran Updated"`, `"Admin Name"`, `"Attempted Update"`, `"Valid Admin Name"`) | `UTCID07, UTCID08, UTCID14, UTCID15, UTCID16, UTCID23` |
| | Empty or `null` (retains existing `FullName`) | `UTCID09, UTCID11, UTCID12, UTCID13, UTCID17` |
| | Contains digits (`"Admin123"`) | `UTCID10` |
| | Boundary length = `1` (`"A"`) | `UTCID19` |
| | Boundary length = `2` (`"An"`) | `UTCID20` |
| | Boundary length = `100` (`new string('A', 100)`) | `UTCID21` |
| | Boundary length = `101` (`new string('A', 101)`) | `UTCID22` |
| | N/A (Null DTO / Get / Reflection) | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID05, UTCID06, UTCID18, UTCID24` |
| **7. `request.PhoneNumber` & Duplicate Check** | Valid 10-digit (`"0987654321"`, `"0999888777"`) & `ExistsByPhoneAsync == false` | `UTCID07, UTCID08` |
| | Invalid format (`"09123"`) | `UTCID11` |
| | Valid 10-digit (`"0909999888"`) & `ExistsByPhoneAsync == true` (Duplicate) | `UTCID12` |
| | Whitespace / `null` (retains existing phone) | `UTCID09, UTCID10, UTCID13, UTCID14, UTCID15, UTCID16, UTCID17, UTCID19, UTCID20, UTCID21, UTCID22, UTCID23` |
| | N/A (Null DTO / Get / Reflection) | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID05, UTCID06, UTCID18, UTCID24` |
| **8. `request.DateOfBirth` & Persistence (`_unitOfWork.SaveChangesAsync`)** | Valid past date / `null` & `SaveChangesAsync` succeeds | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID14, UTCID15, UTCID16, UTCID17, UTCID18, UTCID19, UTCID20, UTCID21, UTCID22, UTCID24` |
| | Future date (`DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5))`) | `UTCID13` |
| | `SaveChangesAsync` throws `InvalidOperationException("Database connection timed out")` | `UTCID23` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<StaffAdminProfileDto>` với `Success = true`, trả đúng thông tin `Staff` / `Admin` | `UTCID01, UTCID02` |
| | `ApiResponseDto<StaffAdminProfileDto>` với `Success = true`, cập nhật thành công `FullName`, `PhoneNumber`, `Gender`, `DateOfBirth`, `Avatar` | `UTCID07, UTCID08, UTCID20, UTCID21` |
| | `ApiResponseDto<StaffAdminProfileDto>` với `Success = true`, giữ nguyên giá trị cũ khi các trường rỗng/null | `UTCID09` |
| | `ApiResponseDto<StaffAdminProfileDto>` với `Success = true`, `result.Data.Avatar == null` (xóa ảnh đại diện) | `UTCID17` |
| | `AuthorizeAttribute.Roles` trên `UpdateStaffAdminProfile` chứa `"STAFF,ADMIN"` và trên `UpdateOwnProfile` chứa `"TRAINEE,COACH"` | `UTCID24` |
| **Exception** | `NotFoundException` | `UTCID03, UTCID14` |
| | `ForbiddenException` | `UTCID04, UTCID05, UTCID06, UTCID15, UTCID16` |
| | `ValidationException` | `UTCID10, UTCID11, UTCID13, UTCID18, UTCID19, UTCID22` |
| | `ConflictException` | `UTCID12` |
| | `InvalidOperationException` | `UTCID23` |
| **Log / Assert Message** | `"Profile retrieved successfully."` / Assert khớp toàn bộ thuộc tính `StaffAdminProfileDto` | `UTCID01, UTCID02` |
| | `"Your profile has been updated successfully."` & Verify `_userRepo.Update` + `_unitOfWork.SaveChangesAsync` `Times.Once` | `UTCID07, UTCID08, UTCID09, UTCID17, UTCID20, UTCID21` |
| | `"User account not found."` (`NotFoundException`) | `UTCID03, UTCID14` |
| | `"Your account has been locked. Please contact an administrator."` (`ForbiddenException`) | `UTCID04, UTCID15` |
| | `"Access denied. This endpoint is reserved for Staff and Admin accounts."` (`ForbiddenException`) | `UTCID05, UTCID06, UTCID16` |
| | `"Invalid profile update request."` (`ValidationException` khi `request == null`) | `UTCID18` |
| | `"Full name cannot contain numbers."` | `UTCID10` |
| | `"Full name must be between 2 and 100 characters."` | `UTCID19, UTCID22` |
| | `"Phone number must be a valid 10-digit number starting with 0."` | `UTCID11` |
| | `"This phone number is already registered to another account."` (`ConflictException`) | `UTCID12` |
| | `"Date of birth cannot be in the future."` | `UTCID13` |
| | `"Database connection timed out"` (`InvalidOperationException`) | `UTCID23` |

---

### CHỨC NĂNG 2: View Coach Application Requests (`VCAR`) — Sheet `GetCoachApplicationsAsync`

#### 1. Header Info
- **Function Code**: `GetCoachApplicationsAsync` *(kèm `GetCoachApplicationDetailsAsync` & `GetCoachApplicationStatusCountsAsync`)*
- **Function Name**: `View Coach Application Requests`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `49` *(riêng `GetCoachApplicationsAsync`: 49 dòng; nếu tính cả `GetCoachApplicationDetailsAsync` [45 dòng] và `GetCoachApplicationStatusCountsAsync` [15 dòng] là `109` dòng)*
- **Test requirement**: `Verify that GetCoachApplicationsAsync, GetCoachApplicationDetailsAsync, and GetCoachApplicationStatusCountsAsync accurately retrieve paged coach application lists, status counts, eKYC live photos, and certificate eligibility flags (CanApprove), auto-reject pending applications lacking certificates, and return failure responses when applications are not found or database exceptions occur.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **10** | **0** | **0** | **6** | **2** | **2** | **10** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID10`
| UTCID | Tên test method gốc (trong `CoachApplicationsServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `GetCoachApplicationsAsync_WhenApplicationsExist_ReturnsPagedResultAndStatusCounts` | N | Passed | 2026-11-03 |
| `UTCID02` | `GetCoachApplicationDetailsAsync_WhenFound_ReturnsFullDetails` | N | Passed | 2026-11-03 |
| `UTCID03` | `GetCoachApplicationDetailsAsync_WhenNotFound_ReturnsFail` | A | Passed | 2026-11-03 |
| `UTCID04` | `GetCoachApplicationStatusCountsAsync_ReturnsCorrectCounts` | N | Passed | 2026-11-03 |
| `UTCID05` | `GetCoachApplicationsAsync_WhenHasApprovedCertificate_ReturnsCanApproveTrue` | N | Passed | 2026-11-03 |
| `UTCID06` | `GetCoachApplicationDetailsAsync_ReturnsAllLivePhotos` | N | Passed | 2026-11-03 |
| `UTCID07` | `GetCoachApplicationDetailsAsync_WhenNoCertificates_AutoRejectsAndSavesToDb` | B | Passed | 2026-11-03 |
| `UTCID08` | `GetCoachApplicationsAsync_WhenNoApplicationsExist_ReturnsEmptyPagedResult` | B | Passed | 2026-11-03 |
| `UTCID09` | `GetCoachApplicationsAsync_WhenRepositoryThrows_ReturnsFailResult` | A | Passed | 2026-11-03 |
| `UTCID10` | `CoachesController_ApplicationReviewEndpoints_RequireStaffOrAdmin` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Target Method** | `GetCoachApplicationsAsync(query)` | `UTCID01, UTCID05, UTCID08, UTCID09` |
| | `GetCoachApplicationDetailsAsync(coachId)` | `UTCID02, UTCID03, UTCID06, UTCID07` |
| | `GetCoachApplicationStatusCountsAsync()` | `UTCID04` |
| | `CoachesController` `[Authorize(Roles = "STAFF,ADMIN")]` check | `UTCID10` |
| **2. Repository Return Data** | List with 1 `PENDING` coach application (`Tran Quoc Bao` / `Ready Coach`) & status counts | `UTCID01, UTCID05` |
| | Empty list (`Items = []`, `TotalCount = 0`) | `UTCID08` |
| | Full `CoachProfile` detail (`Nguyen Thu Thao` / `Photo Coach` / `Zero Certs`) | `UTCID02, UTCID06, UTCID07` |
| | `null` (Application not found) | `UTCID03` |
| | Status tuple `(All: 100, Pending: 20, Approved: 70, Rejected: 10)` | `UTCID04` |
| | Repository throws `InvalidOperationException("Database connection failed")` | `UTCID09` |
| | N/A (Controller attribute reflection) | `UTCID10` |
| **3. Certificates & eKYC State** | 1 `PENDING` certificate (`CanApprove = false`) | `UTCID01, UTCID02` |
| | 1 `APPROVED` certificate (`CanApprove = true`, 1 or 3 eKYC LivePhotos) | `UTCID05, UTCID06` |
| | Empty `CoachCertificates` collection (`Count == 0` -> triggers auto-reject) | `UTCID07` |
| | N/A (NotFound / Counts only / Empty list / Repo exception / Reflection) | `UTCID03, UTCID04, UTCID08, UTCID09, UTCID10` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<CoachApplicationListResponseDto>` với `Success = true`, `Items.Count = 1`, `StatusCounts` chính xác | `UTCID01, UTCID05` |
| | `ApiResponseDto<CoachApplicationListResponseDto>` với `Success = true`, `Items` rỗng, `TotalCount = 0` | `UTCID08` |
| | `ApiResponseDto<CoachApplicationDetailDto>` với `Success = true`, đầy đủ thông tin chứng chỉ, eKYC & `LivePhotoUrls` | `UTCID02, UTCID06` |
| | `ApiResponseDto<CoachApplicationDetailDto>` với `Success = true`, tự động chuyển `ApprovalStatus = "REJECTED"`, `user.IsLocked = true` khi không có chứng chỉ | `UTCID07` |
| | `ApiResponseDto<CoachApplicationStatusCountsDto>` với `Success = true` (`All=100, Pending=20, Approved=70, Rejected=10`) | `UTCID04` |
| | `ApiResponseDto` với `Success = false`, `Data = null` | `UTCID03, UTCID09` |
| | `AuthorizeAttribute.Roles` trên `CoachesController` chứa `"STAFF"` và `"ADMIN"` | `UTCID10` |
| **Exception** | None (Exceptions được catch và trả về `ApiResponseDto.Fail`) | `UTCID01..UTCID10` |
| **Log / Assert Message** | `"Coach applications retrieved successfully."` | `UTCID01, UTCID05, UTCID08` |
| | `"Coach application details retrieved successfully."` | `UTCID02, UTCID06, UTCID07` |
| | `"Coach application not found."` | `UTCID03` |
| | `"Status counts retrieved successfully."` | `UTCID04` |
| | `"Error retrieving coach applications: Database connection failed"` | `UTCID09` |

---

### CHỨC NĂNG 3: Approve / Reject a coach application (`ARCA`) — Sheet `ApproveRejectCoachAppAsync`

#### 1. Header Info
- **Function Code**: `ApproveCoachApplicationAsync` & `RejectCoachApplicationAsync` *(kèm `ReviewCertificateAsync`)*
- **Function Name**: `Approve / Reject a coach application`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `238` *(trong đó `ApproveCoachApplicationAsync`: `130` dòng [87 câu lệnh]; `RejectCoachApplicationAsync`: `108` dòng [71 câu lệnh]; nếu tính thêm `ReviewCertificateAsync`: `71` dòng)*
- **Test requirement**: `Verify that ApproveCoachApplicationAsync and RejectCoachApplicationAsync (along with ReviewCertificateAsync) properly validate pending application status and certificate approval prerequisites, update CoachProfile.ApprovalStatus, user RoleCode ("COACH") and IsLocked status, auto-reject applications when all certificates are rejected or missing, send notification emails gracefully even on SMTP failure, and handle database save failures.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **20** | **0** | **0** | **8** | **10** | **2** | **20** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID20`
| UTCID | Tên test method gốc (trong `CoachApplicationsServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `ApproveCoachApplicationAsync_WhenPending_SuccessfullyApprovesAndUnlocksCoach` | N | Passed | 2026-11-03 |
| `UTCID02` | `ApproveCoachApplicationAsync_WhenAlreadyApproved_ReturnsFail` | A | Passed | 2026-11-03 |
| `UTCID03` | `ApproveCoachApplicationAsync_WhenNotFound_ReturnsFail` | A | Passed | 2026-11-03 |
| `UTCID04` | `RejectCoachApplicationAsync_WhenPending_SuccessfullyRejectsAndLeavesCoachLocked` | N | Passed | 2026-11-03 |
| `UTCID05` | `RejectCoachApplicationAsync_WhenAlreadyProcessed_ReturnsFail` | A | Passed | 2026-11-03 |
| `UTCID06` | `RejectCoachApplicationAsync_WhenNotFound_ReturnsFail` | A | Passed | 2026-11-03 |
| `UTCID07` | `ApproveCoachApplicationAsync_WhenNoCertificateApproved_ReturnsFail` | A | Passed | 2026-11-03 |
| `UTCID08` | `ApproveCoachApplicationAsync_WhenAllCertificatesRejected_AutoRejectsAndReturnsFail` | A | Passed | 2026-11-03 |
| `UTCID09` | `ApproveCoachApplicationAsync_WhenNoCertificates_AutoRejectsAndReturnsFail` | B | Passed | 2026-11-03 |
| `UTCID10` | `ApproveCoachApplicationAsync_WhenOneRejectedAndOneApproved_SuccessfullyApproves` | B | Passed | 2026-11-03 |
| `UTCID11` | `ReviewCertificateAsync_WhenOneRejectedAndOthersPending_CoachApplicationRemainsPending` | N | Passed | 2026-11-03 |
| `UTCID12` | `ReviewCertificateAsync_WhenAllCertificatesBecomeRejected_AutoRejectsCoachApplication` | A | Passed | 2026-11-03 |
| `UTCID13` | `ReviewCertificateAsync_WhenOneRejectedAndOneApproved_ApplicationRemainsPendingWithCanApproveTrue` | N | Passed | 2026-11-03 |
| `UTCID14` | `ApproveCoachApplicationAsync_WhenEmailServiceConfigured_SendsApprovalEmailWithNote` | N | Passed | 2026-11-03 |
| `UTCID15` | `RejectCoachApplicationAsync_WhenEmailServiceConfigured_SendsRejectionEmailWithReason` | N | Passed | 2026-11-03 |
| `UTCID16` | `ApproveCoachApplicationAsync_WhenEmailServiceThrows_StillSucceedsApproval` | N | Passed | 2026-11-03 |
| `UTCID17` | `RejectCoachApplicationAsync_WhenEmailServiceThrows_StillSucceedsRejection` | N | Passed | 2026-11-03 |
| `UTCID18` | `ApproveCoachApplicationAsync_WhenDatabaseSaveFails_ReturnsFailResult` | A | Passed | 2026-11-03 |
| `UTCID19` | `RejectCoachApplicationAsync_WhenDatabaseSaveFails_ReturnsFailResult` | A | Passed | 2026-11-03 |
| `UTCID20` | `CoachesController_ApplicationReviewEndpoints_RequireStaffOrAdmin` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Action Type** | `ApproveCoachApplicationAsync(coachId, approverId, request)` | `UTCID01, UTCID02, UTCID03, UTCID07, UTCID08, UTCID09, UTCID10, UTCID14, UTCID16, UTCID18` |
| | `RejectCoachApplicationAsync(coachId, rejectorId, request)` | `UTCID04, UTCID05, UTCID06, UTCID15, UTCID17, UTCID19` |
| | `ReviewCertificateAsync(coachId, certificateId, reviewerId, request)` | `UTCID11, UTCID12, UTCID13` |
| | `CoachesController` `[Authorize(Roles = "STAFF,ADMIN")]` check | `UTCID20` |
| **2. Application Existence & Current `ApprovalStatus`** | Exists & `ApprovalStatus == "PENDING"` | `UTCID01, UTCID04, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID13, UTCID14, UTCID15, UTCID16, UTCID17, UTCID18, UTCID19` |
| | Exists & `ApprovalStatus == "APPROVED"` | `UTCID02` |
| | Exists & `ApprovalStatus == "REJECTED"` | `UTCID05` |
| | Not found (`null`) | `UTCID03, UTCID06` |
| | N/A (Controller attribute reflection) | `UTCID20` |
| **3. Certificates Verification State** | At least 1 certificate `APPROVED` (or 1 `REJECTED` + 1 `APPROVED`) | `UTCID01, UTCID10, UTCID13, UTCID14, UTCID16, UTCID18` |
| | Only `PENDING` certificates (none `APPROVED` yet) | `UTCID04, UTCID07, UTCID11, UTCID15, UTCID17, UTCID19` |
| | All certificates `REJECTED` | `UTCID08, UTCID12` |
| | No certificates (`CoachCertificates` empty) | `UTCID09` |
| | N/A (Already processed / NotFound / Reflection) | `UTCID02, UTCID03, UTCID05, UTCID06, UTCID20` |
| **4. Email Service & Database Save Behavior** | Email service `null` (not configured) & `SaveChangesAsync` succeeds | `UTCID01, UTCID04, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID13` |
| | Email service configured & `SendEmailAsync` succeeds | `UTCID14, UTCID15` |
| | Email service configured & `SendEmailAsync` throws `InvalidOperationException("SMTP connection failed")` | `UTCID16, UTCID17` |
| | `_unitOfWork.SaveChangesAsync` throws `InvalidOperationException` | `UTCID18, UTCID19` |
| | Not reached (Validation/NotFound short-circuit or Reflection) | `UTCID02, UTCID03, UTCID05, UTCID06, UTCID07, UTCID20` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<CoachApplicationDetailDto>` với `Success = true`, `ApprovalStatus = "APPROVED"`, `user.RoleCode = "COACH"`, `user.IsLocked = false` | `UTCID01, UTCID10, UTCID14, UTCID16` |
| | `ApiResponseDto<CoachApplicationDetailDto>` với `Success = true`, `ApprovalStatus = "REJECTED"`, `user.IsLocked = true` | `UTCID04, UTCID12, UTCID15, UTCID17` |
| | `ApiResponseDto<CoachApplicationDetailDto>` với `Success = true`, `ApprovalStatus = "PENDING"` sau khi duyệt từng chứng chỉ | `UTCID11, UTCID13` |
| | `ApiResponseDto<CoachApplicationDetailDto>` với `Success = false`, `Data = null` | `UTCID02, UTCID03, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID18, UTCID19` |
| | `AuthorizeAttribute.Roles` trên `ApproveCoachApplication` và `RejectCoachApplication` chứa `"STAFF,ADMIN"` | `UTCID20` |
| **Exception** | None (Tất cả exception từ EmailService và UnitOfWork đều được catch bên trong `CoachService`) | `UTCID01..UTCID20` |
| **Log / Assert Message** | `"Coach application for '<FullName>' has been approved successfully."` & Verify `SaveChangesAsync` `Times.Once` | `UTCID01, UTCID10, UTCID14, UTCID16` |
| | `"Coach application for '<FullName>' has been rejected."` & Verify `SaveChangesAsync` `Times.Once` | `UTCID04, UTCID15, UTCID17` |
| | `"Coach application not found."` | `UTCID03, UTCID06` |
| | `"Only pending applications can be approved. Current status: APPROVED"` | `UTCID02` |
| | `"Only pending applications can be rejected. Current status: REJECTED"` | `UTCID05` |
| | `"At least one certificate must be approved before approving the coach application."` | `UTCID07` |
| | `"This application was automatically rejected because all certificates have been rejected."` | `UTCID08` |
| | `"This application was automatically rejected because no certificates were submitted."` | `UTCID09` |
| | `"Certificate rejected. All certificates are now rejected — the coach application has been automatically rejected."` | `UTCID12` |
| | `"Error approving coach application: Failed to commit approval transaction"` | `UTCID18` |
| | `"Error rejecting coach application: Failed to commit rejection transaction"` | `UTCID19` |

---

### CHỨC NĂNG 4: View List User's Account (`VLUA`) — Sheet `GetUsersAsync`

#### 1. Header Info
- **Function Code**: `GetUsersAsync`
- **Function Name**: `View List User's Account`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `12` *(8 câu lệnh không tính `{}`)*
- **Test requirement**: `Verify that GetUsersAsync retrieves a paginated list of user accounts (TRAINEE, COACH), accurately passes search, role, and lock status filters to IUserRepository.ListUsersForAdminAsync, applies fallback defaults when FullName, RoleCode, or IsLocked are null, calculates pagination boundaries, and returns a failure result when repository exceptions or cancellation occur.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **11** | **0** | **0** | **5** | **2** | **4** | **11** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID11`
| UTCID | Tên test method gốc (trong `AdminUserServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `GetUsersAsync_ShouldCallListUsersForAdminAsync_AndReturnPagedUsers` | N | Passed | 2026-11-03 |
| `UTCID02` | `GetUsersAsync_WithAllFilters_ShouldPassParametersCorrectlyToRepository` | N | Passed | 2026-11-03 |
| `UTCID03` | `GetUsersAsync_WhenUserFullNameIsNull_ShouldFallbackToEmailPrefix` | B | Passed | 2026-11-03 |
| `UTCID04` | `GetUsersAsync_WhenRoleCodeAndIsLockedAreNull_ShouldApplyDefaults` | B | Passed | 2026-11-03 |
| `UTCID05` | `GetUsersAsync_FilterByRole_ShouldPassRoleParameter` | N | Passed | 2026-11-03 |
| `UTCID06` | `GetUsersAsync_FilterByLockedStatus_ShouldPassIsLockedParameter` | N | Passed | 2026-11-03 |
| `UTCID07` | `GetUsersAsync_EmptyResult_ShouldReturnEmptyPagedResult` | B | Passed | 2026-11-03 |
| `UTCID08` | `GetUsersAsync_CustomPaginationBoundaries_ShouldCalculateTotalPagesCorrectly` | B | Passed | 2026-11-03 |
| `UTCID09` | `GetUsersAsync_WhenRepositoryThrows_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID10` | `GetUsersAsync_WhenCancellationRequested_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID11` | `AdminUsersController_GetUsersEndpoint_RequiresStaffOrAdmin` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Filter Parameters (`search`, `role`, `isLocked`)** | All `null` (`search: null, role: null, isLocked: null`) | `UTCID01, UTCID03, UTCID04, UTCID08, UTCID09, UTCID10` |
| | All provided (`search: "matched"`, `role: "COACH"`, `isLocked: true`) | `UTCID02` |
| | Only `role` provided (`role: "TRAINEE"`) | `UTCID05` |
| | Only `isLocked` provided (`isLocked: false`) | `UTCID06` |
| | Non-matching search (`search: "nonexistent"`) | `UTCID07` |
| | N/A (Controller attribute reflection) | `UTCID11` |
| **2. Pagination Parameters (`pageNumber`, `pageSize`)** | Default (`pageNumber = 1, pageSize = 10`) | `UTCID01, UTCID03, UTCID04, UTCID05, UTCID06, UTCID07, UTCID09, UTCID10` |
| | Custom (`pageNumber = 2, pageSize = 5`, `TotalCount = 12`) | `UTCID02, UTCID08` |
| | N/A (Controller attribute reflection) | `UTCID11` |
| **3. Repository Behavior & Entity Fields** | Returns populated `User` list with standard fields | `UTCID01, UTCID02, UTCID05, UTCID06, UTCID08` |
| | Returns `User` with `FullName = null`, `Email = "john.doe@fitsocial.com"` | `UTCID03` |
| | Returns `User` with `RoleCode = null`, `IsLocked = null` | `UTCID04` |
| | Returns empty list `(Items: [], TotalCount: 0)` | `UTCID07` |
| | Throws `InvalidOperationException("PostgreSQL connection refused")` | `UTCID09` |
| | Throws `OperationCanceledException` | `UTCID10` |
| | N/A (Controller attribute reflection) | `UTCID11` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<PagedResultDto<AdminUserDto>>` với `Success = true`, danh sách `Items` và thông số phân trang chính xác | `UTCID01, UTCID02, UTCID05, UTCID06, UTCID08` |
| | `ApiResponseDto<PagedResultDto<AdminUserDto>>` với `Success = true`, `FullName` fallback về `"john.doe"` | `UTCID03` |
| | `ApiResponseDto<PagedResultDto<AdminUserDto>>` với `Success = true`, `RoleCode` mặc định `"TRAINEE"` và `IsLocked` mặc định `false` | `UTCID04` |
| | `ApiResponseDto<PagedResultDto<AdminUserDto>>` với `Success = true`, `Items` rỗng, `TotalCount = 0`, `TotalPages = 0` | `UTCID07` |
| | `ApiResponseDto<PagedResultDto<AdminUserDto>>` với `Success = false`, `Data = null` | `UTCID09, UTCID10` |
| | `AdminUsersController` có `[Authorize(Roles = "STAFF,ADMIN")]` | `UTCID11` |
| **Exception** | None (Exceptions được catch và gói vào `ApiResponseDto.Fail`) | `UTCID01..UTCID11` |
| **Log / Assert Message** | `"Users list retrieved successfully."` | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID05, UTCID06, UTCID07, UTCID08` |
| | `"Error retrieving users: PostgreSQL connection refused"` | `UTCID09` |
| | `"Error retrieving users: ..."` (OperationCanceledException) | `UTCID10` |

---

### CHỨC NĂNG 5: View Location List (`VLL`) — Sheet `GetPagedLocationsAsync`

#### 1. Header Info
- **Function Code**: `GetPagedLocationsAsync` *(kèm `GetLocationsAsync` & `GetPublicLocationsAsync`)*
- **Function Name**: `View Location List`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `18` *(riêng `GetPagedLocationsAsync`: 18 dòng [13 câu lệnh]; nếu gộp `GetLocationsAsync` [1 dòng] và `GetPublicLocationsAsync` [16 dòng] là `35` dòng)*
- **Test requirement**: `Verify that GetPagedLocationsAsync, GetLocationsAsync, and GetPublicLocationsAsync retrieve paginated and public location lists, accurately calculate active PostCount (excluding soft-deleted posts where IsDeleted == true), handle null Posts or Address safely, respect search and pagination boundaries, and return failure responses when exceptions or cancellation occur.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **15** | **0** | **0** | **7** | **3** | **5** | **15** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID15`
| UTCID | Tên test method gốc (trong `LocationServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `GetPagedLocationsAsync_WithoutSearchTerm_ShouldReturnPagedLocationsWithCalculatedPostCounts` | N | Passed | 2026-11-03 |
| `UTCID02` | `GetPagedLocationsAsync_WithSearchTerm_ShouldPassSearchTermToRepository` | N | Passed | 2026-11-03 |
| `UTCID03` | `GetLocationsAsync_ShouldCallGetPagedLocationsAsync_AndReturnSameResult` | N | Passed | 2026-11-03 |
| `UTCID04` | `GetPublicLocationsAsync_ShouldReturnAllPublicLocationsWithPostCounts` | N | Passed | 2026-11-03 |
| `UTCID05` | `LocationsController_GetLocations_ShouldFallbackToSearchParameterWhenSearchTermIsNull` | N | Passed | 2026-11-03 |
| `UTCID06` | `GetPagedLocationsAsync_WhenNoLocationsFound_ShouldReturnEmptyPagedResult` | B | Passed | 2026-11-03 |
| `UTCID07` | `GetPublicLocationsAsync_WhenNoLocationsExist_ShouldReturnEmptyList` | B | Passed | 2026-11-03 |
| `UTCID08` | `GetPagedLocationsAsync_PaginationBoundaries_ShouldCalculateTotalPagesAndFlagsCorrectly` | B | Passed | 2026-11-03 |
| `UTCID09` | `GetPagedLocationsAsync_WhenLocationHasNullPostsOrNullAddress_ShouldHandleSafelyWithoutError` | B | Passed | 2026-11-03 |
| `UTCID10` | `GetPagedLocationsAsync_PostCountCalculation_ShouldOnlyCountNonDeletedPosts` | B | Passed | 2026-11-03 |
| `UTCID11` | `GetPagedLocationsAsync_WhenRepositoryThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID12` | `GetPagedLocationsAsync_WhenCancellationRequested_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID13` | `GetPublicLocationsAsync_WhenRepositoryThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID14` | `LocationsController_GetLocationsEndpoint_RequiresStaffOrAdmin` | N | Passed | 2026-11-03 |
| `UTCID15` | `LocationsController_GetPublicLocationsEndpoint_AllowsAnonymous` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Target Method** | `LocationService.GetPagedLocationsAsync` | `UTCID01, UTCID02, UTCID06, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12` |
| | `LocationService.GetLocationsAsync` | `UTCID03` |
| | `LocationService.GetPublicLocationsAsync` | `UTCID04, UTCID07, UTCID13` |
| | `LocationsController.GetLocations` / Attribute check | `UTCID05, UTCID14, UTCID15` |
| **2. Search Term (`searchTerm` / `search`)** | `null` | `UTCID01, UTCID04, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID13, UTCID14, UTCID15` |
| | Provided (`"District 1"`, `"Central"`, `"FallbackQuery"`, `"NonExistent"`) | `UTCID02, UTCID03, UTCID05, UTCID06` |
| **3. Repository Data & Post States** | Locations with active & soft-deleted posts | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID05, UTCID08, UTCID10` |
| | Location with `Posts = null` and `Address = null` | `UTCID09` |
| | Empty location list (`Count = 0`) | `UTCID06, UTCID07` |
| | Repository throws `InvalidOperationException` / `Exception` | `UTCID11, UTCID13` |
| | Repository throws `OperationCanceledException` | `UTCID12` |
| | N/A (Controller attribute check) | `UTCID14, UTCID15` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<PagedResultDto<LocationDto>>` (`Success = true`) với `PostCount` chỉ đếm bài viết `IsDeleted != true` | `UTCID01, UTCID02, UTCID03, UTCID08, UTCID09, UTCID10` |
| | `ApiResponseDto<PagedResultDto<LocationDto>>` (`Success = true`) rỗng (`Items` empty, `TotalCount = 0`) | `UTCID06` |
| | `ApiResponseDto<List<LocationDto>>` (`Success = true`) cho danh sách public (có dữ liệu hoặc rỗng) | `UTCID04, UTCID07` |
| | `OkObjectResult` (HTTP 200) từ `LocationsController.GetLocations` | `UTCID05` |
| | `ApiResponseDto` với `Success = false`, `Data = null` | `UTCID11, UTCID12, UTCID13` |
| | `AuthorizeAttribute(Roles = "STAFF,ADMIN")` cho `GetLocations` & `[AllowAnonymous]` cho `GetPublicLocations` | `UTCID14, UTCID15` |
| **Exception** | None (Exceptions được catch và trả về `ApiResponseDto.Fail`) | `UTCID01..UTCID15` |
| **Log / Assert Message** | `"Locations list retrieved successfully."` | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10` |
| | `"Failed to retrieve locations: Database connection timeout."` | `UTCID11` |
| | `"Failed to retrieve locations: ..."` (OperationCanceledException) | `UTCID12` |
| | `"Failed to retrieve public locations: Database down."` | `UTCID13` |

---

### CHỨC NĂNG 6: Create Location (`CLOC`) — Sheet `CreateLocationAsync`

#### 1. Header Info
- **Function Code**: `CreateLocationAsync`
- **Function Name**: `Create Location`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `49` *(31 câu lệnh không tính `{}`)*
- **Test requirement**: `Verify that CreateLocationAsync validates the CreateLocationDto payload (non-null DTO, non-empty LocationName and Address, maximum length of 200 characters after trimming, and unique LocationName), saves the new Location entity via ILocationRepository and IUnitOfWork, maps controller responses (201 Created, 400 Bad Request, 409 Conflict), and handles repository/unit-of-work exceptions.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **21** | **0** | **0** | **4** | **13** | **4** | **21** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID21`
| UTCID | Tên test method gốc (trong `LocationServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `CreateLocationAsync_ValidData_ShouldCreateLocationSuccessfully` | N | Passed | 2026-11-03 |
| `UTCID02` | `CreateLocationAsync_WithLeadingAndTrailingWhitespace_ShouldTrimBeforeSaving` | N | Passed | 2026-11-03 |
| `UTCID03` | `LocationsController_CreateLocation_WhenSuccessful_ShouldReturn201Created` | N | Passed | 2026-11-03 |
| `UTCID04` | `CreateLocationAsync_WhenDtoIsNull_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID05` | `CreateLocationAsync_WhenLocationNameIsNullOrWhitespace_ShouldReturnFailureResult(invalidName: null)` | A | Passed | 2026-11-03 |
| `UTCID06` | `CreateLocationAsync_WhenLocationNameIsNullOrWhitespace_ShouldReturnFailureResult(invalidName: "")` | A | Passed | 2026-11-03 |
| `UTCID07` | `CreateLocationAsync_WhenLocationNameIsNullOrWhitespace_ShouldReturnFailureResult(invalidName: "   ")` | A | Passed | 2026-11-03 |
| `UTCID08` | `CreateLocationAsync_WhenAddressIsNullOrWhitespace_ShouldReturnFailureResult(invalidAddress: null)` | A | Passed | 2026-11-03 |
| `UTCID09` | `CreateLocationAsync_WhenAddressIsNullOrWhitespace_ShouldReturnFailureResult(invalidAddress: "")` | A | Passed | 2026-11-03 |
| `UTCID10` | `CreateLocationAsync_WhenAddressIsNullOrWhitespace_ShouldReturnFailureResult(invalidAddress: "   ")` | A | Passed | 2026-11-03 |
| `UTCID11` | `CreateLocationAsync_WhenLocationNameAlreadyExists_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID12` | `LocationsController_CreateLocation_WhenNameAlreadyExists_ShouldReturn409Conflict` | A | Passed | 2026-11-03 |
| `UTCID13` | `LocationsController_CreateLocation_WhenValidationFails_ShouldReturn400BadRequest` | A | Passed | 2026-11-03 |
| `UTCID14` | `CreateLocationAsync_NameAndAddressExactly200Characters_ShouldSucceed` | B | Passed | 2026-11-03 |
| `UTCID15` | `CreateLocationAsync_WhenNameExceeds200Characters_ShouldReturnFailureResult` | B | Passed | 2026-11-03 |
| `UTCID16` | `CreateLocationAsync_WhenAddressExceeds200Characters_ShouldReturnFailureResult` | B | Passed | 2026-11-03 |
| `UTCID17` | `CreateLocationAsync_WhenNameWithWhitespaceExceeds200BeforeTrimButUnder200AfterTrim_ShouldSucceed` | B | Passed | 2026-11-03 |
| `UTCID18` | `CreateLocationAsync_WhenRepositoryThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID19` | `CreateLocationAsync_WhenUnitOfWorkThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID20` | `CreateLocationAsync_WhenCancellationRequested_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID21` | `LocationsController_CreateLocationEndpoint_RequiresStaffOrAdmin` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Request DTO (`CreateLocationDto`)** | Valid non-null DTO | `UTCID01, UTCID02, UTCID03, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID13, UTCID14, UTCID15, UTCID16, UTCID17, UTCID18, UTCID19, UTCID20` |
| | `null` | `UTCID04` |
| | N/A (Controller attribute reflection) | `UTCID21` |
| **2. `dto.LocationName` Input** | Valid normal string (`"Saigon Central Fitness"`, `"Valid Gym"`, `"Existing Gym"`, `"Saigon Fitness"`) | `UTCID01, UTCID03, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID16, UTCID18, UTCID19, UTCID20` |
| | Valid string with leading/trailing spaces (`"   Hanoi Fitness Club   "`) | `UTCID02` |
| | `null` | `UTCID05` |
| | Empty string `""` | `UTCID06, UTCID13` |
| | Whitespace only `"   "` | `UTCID07` |
| | Boundary: `200` chars (`new string('A', 200)`) | `UTCID14` |
| | Boundary: `201` chars (`new string('A', 201)`) | `UTCID15` |
| | Boundary: `206` chars before trim, `200` chars after trim (`"   " + new string('C', 200) + "   "`) | `UTCID17` |
| | N/A (`dto == null` / Reflection) | `UTCID04, UTCID21` |
| **3. `dto.Address` Input** | Valid normal string (`"123 Le Loi, D1, HCMC"`, `"Valid Address"`, `"   456 Ba Dinh, Hanoi   "`) | `UTCID01, UTCID02, UTCID03, UTCID05, UTCID06, UTCID07, UTCID11, UTCID12, UTCID13, UTCID15, UTCID17, UTCID18, UTCID19, UTCID20` |
| | `null` | `UTCID08` |
| | Empty string `""` | `UTCID09` |
| | Whitespace only `"   "` | `UTCID10` |
| | Boundary: `200` chars (`new string('B', 200)`) | `UTCID14` |
| | Boundary: `201` chars (`new string('B', 201)`) | `UTCID16` |
| | N/A (`dto == null` / Reflection) | `UTCID04, UTCID21` |
| **4. Duplicate Check (`_locationRepo.GetByNameAsync`) & Persistence** | Returns `null` (Unique name) & `SaveChangesAsync` succeeds | `UTCID01, UTCID02, UTCID03, UTCID14, UTCID17` |
| | Returns existing `Location` (Duplicate name) | `UTCID11, UTCID12` |
| | `_locationRepo.GetByNameAsync` throws `InvalidOperationException` | `UTCID18` |
| | `_unitOfWork.SaveChangesAsync` throws `Exception("Deadlock detected during commit.")` | `UTCID19` |
| | Cancellation requested (`OperationCanceledException`) | `UTCID20` |
| | Not reached (Validation failed or Reflection) | `UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID13, UTCID15, UTCID16, UTCID21` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<LocationDto>` với `Success = true`, `Data` chứa `LocationId != Guid.Empty`, tên và địa chỉ đã trim | `UTCID01, UTCID02, UTCID14, UTCID17` |
| | `ObjectResult` với `StatusCode = 201 Created` | `UTCID03` |
| | `ConflictObjectResult` với `StatusCode = 409 Conflict` | `UTCID12` |
| | `BadRequestObjectResult` với `StatusCode = 400 Bad Request` | `UTCID13` |
| | `ApiResponseDto<LocationDto>` với `Success = false`, `Data = null` | `UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID15, UTCID16, UTCID18, UTCID19, UTCID20` |
| | `LocationsController.CreateLocation` có `[HttpPost]` và `[Authorize(Roles = "STAFF,ADMIN")]` | `UTCID21` |
| **Exception** | None (Exceptions được catch trong `CreateLocationAsync` và trả về `ApiResponseDto.Fail`) | `UTCID01..UTCID21` |
| **Log / Assert Message** | `"Location created successfully."` & Verify `AddAsync` + `SaveChangesAsync` `Times.Once` | `UTCID01, UTCID02, UTCID03, UTCID14, UTCID17` |
| | `"Request payload cannot be null."` | `UTCID04` |
| | `"Location name cannot be empty."` | `UTCID05, UTCID06, UTCID07, UTCID13` |
| | `"Address cannot be empty."` | `UTCID08, UTCID09, UTCID10` |
| | `"Location 'Existing Gym' already exists."` | `UTCID11, UTCID12` |
| | `"Location name cannot exceed 200 characters."` | `UTCID15` |
| | `"Address cannot exceed 200 characters."` | `UTCID16` |
| | `"Failed to create location: DB connection dropped."` | `UTCID18` |
| | `"Failed to create location: Deadlock detected during commit."` | `UTCID19` |
| | `"Failed to create location: ..."` (OperationCanceledException) | `UTCID20` |

---

### CHỨC NĂNG 7: Edit Location (`ELOC`) — Sheet `UpdateLocationAsync`

#### 1. Header Info
- **Function Code**: `UpdateLocationAsync`
- **Function Name**: `Edit Location`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `49` *(30 câu lệnh không tính `{}`)*
- **Test requirement**: `Verify that UpdateLocationAsync validates the UpdateLocationDto input (non-null DTO, non-empty LocationName and Address, max 200 characters after trimming), verifies target location existence via GetByIdAsync, checks name uniqueness excluding the current locationId via GetByNameExcludingIdAsync, persists updates, maps HTTP status codes (200, 400, 404, 409), and handles exceptions.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **24** | **0** | **0** | **5** | **15** | **4** | **24** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID24`
| UTCID | Tên test method gốc (trong `LocationServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `UpdateLocationAsync_ValidData_ShouldUpdateLocationSuccessfully` | N | Passed | 2026-11-03 |
| `UTCID02` | `UpdateLocationAsync_SameNameForSameLocation_ShouldSucceedWithoutConflict` | N | Passed | 2026-11-03 |
| `UTCID03` | `UpdateLocationAsync_WithLeadingAndTrailingWhitespace_ShouldTrimBeforeSaving` | N | Passed | 2026-11-03 |
| `UTCID04` | `LocationsController_UpdateLocation_WhenSuccessful_ShouldReturn200Ok` | N | Passed | 2026-11-03 |
| `UTCID05` | `UpdateLocationAsync_WhenDtoIsNull_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID06` | `UpdateLocationAsync_WhenLocationNameIsNullOrWhitespace_ShouldReturnFailureResult(invalidName: null)` | A | Passed | 2026-11-03 |
| `UTCID07` | `UpdateLocationAsync_WhenLocationNameIsNullOrWhitespace_ShouldReturnFailureResult(invalidName: "")` | A | Passed | 2026-11-03 |
| `UTCID08` | `UpdateLocationAsync_WhenLocationNameIsNullOrWhitespace_ShouldReturnFailureResult(invalidName: "   ")` | A | Passed | 2026-11-03 |
| `UTCID09` | `UpdateLocationAsync_WhenAddressIsNullOrWhitespace_ShouldReturnFailureResult(invalidAddress: null)` | A | Passed | 2026-11-03 |
| `UTCID10` | `UpdateLocationAsync_WhenAddressIsNullOrWhitespace_ShouldReturnFailureResult(invalidAddress: "")` | A | Passed | 2026-11-03 |
| `UTCID11` | `UpdateLocationAsync_WhenAddressIsNullOrWhitespace_ShouldReturnFailureResult(invalidAddress: "   ")` | A | Passed | 2026-11-03 |
| `UTCID12` | `UpdateLocationAsync_WhenLocationDoesNotExist_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID13` | `UpdateLocationAsync_WhenAnotherLocationHasSameName_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID14` | `LocationsController_UpdateLocation_WhenLocationNotFound_ShouldReturn404NotFound` | A | Passed | 2026-11-03 |
| `UTCID15` | `LocationsController_UpdateLocation_WhenDuplicateName_ShouldReturn409Conflict` | A | Passed | 2026-11-03 |
| `UTCID16` | `LocationsController_UpdateLocation_WhenValidationFails_ShouldReturn400BadRequest` | A | Passed | 2026-11-03 |
| `UTCID17` | `UpdateLocationAsync_NameAndAddressExactly200Characters_ShouldSucceed` | B | Passed | 2026-11-03 |
| `UTCID18` | `UpdateLocationAsync_WhenNameExceeds200Characters_ShouldReturnFailureResult` | B | Passed | 2026-11-03 |
| `UTCID19` | `UpdateLocationAsync_WhenAddressExceeds200Characters_ShouldReturnFailureResult` | B | Passed | 2026-11-03 |
| `UTCID20` | `UpdateLocationAsync_WhenNameWithWhitespaceExceeds200BeforeTrimButUnder200AfterTrim_ShouldSucceed` | B | Passed | 2026-11-03 |
| `UTCID21` | `UpdateLocationAsync_WhenRepositoryThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID22` | `UpdateLocationAsync_WhenUnitOfWorkThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID23` | `UpdateLocationAsync_WhenCancellationRequested_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID24` | `LocationsController_UpdateLocationEndpoint_RequiresStaffOrAdmin` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Request DTO (`UpdateLocationDto`)** | Valid non-null DTO | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID13, UTCID14, UTCID15, UTCID16, UTCID17, UTCID18, UTCID19, UTCID20, UTCID21, UTCID22, UTCID23` |
| | `null` | `UTCID05` |
| | N/A (Controller attribute reflection) | `UTCID24` |
| **2. `dto.LocationName` Input** | Valid new/same string | `UTCID01, UTCID02, UTCID04, UTCID09, UTCID10, UTCID11, UTCID12, UTCID13, UTCID14, UTCID15, UTCID19, UTCID21, UTCID22, UTCID23` |
| | Valid string with leading/trailing spaces (`"   Trimmed Name Gym   "`) | `UTCID03` |
| | `null` | `UTCID06` |
| | Empty string `""` | `UTCID07, UTCID16` |
| | Whitespace only `"   "` | `UTCID08` |
| | Boundary: `200` chars (`new string('A', 200)`) | `UTCID17` |
| | Boundary: `201` chars (`new string('A', 201)`) | `UTCID18` |
| | Boundary: `206` chars before trim, `200` chars after trim | `UTCID20` |
| | N/A (`dto == null` / Reflection) | `UTCID05, UTCID24` |
| **3. `dto.Address` Input** | Valid normal / trimmed string | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID06, UTCID07, UTCID08, UTCID12, UTCID13, UTCID14, UTCID15, UTCID16, UTCID18, UTCID20, UTCID21, UTCID22, UTCID23` |
| | `null` | `UTCID09` |
| | Empty string `""` | `UTCID10` |
| | Whitespace only `"   "` | `UTCID11` |
| | Boundary: `200` chars (`new string('B', 200)`) | `UTCID17` |
| | Boundary: `201` chars (`new string('B', 201)`) | `UTCID19` |
| | N/A (`dto == null` / Reflection) | `UTCID05, UTCID24` |
| **4. Location Existence (`GetByIdAsync`) & Name Uniqueness (`GetByNameExcludingIdAsync`)** | Exists & Name is unique (`GetByNameExcludingIdAsync == null`) & `SaveChangesAsync` succeeds | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID17, UTCID20` |
| | Location does not exist (`GetByIdAsync == null`) | `UTCID12, UTCID14` |
| | Exists & Another location already uses the name (`GetByNameExcludingIdAsync != null`) | `UTCID13, UTCID15` |
| | `GetByIdAsync` throws `InvalidOperationException("Database read failure.")` | `UTCID21` |
| | `SaveChangesAsync` throws `Exception("Concurrency conflict during update.")` | `UTCID22` |
| | Cancellation requested (`OperationCanceledException`) | `UTCID23` |
| | Not reached (Validation failed or Reflection) | `UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID16, UTCID18, UTCID19, UTCID24` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<LocationDto>` với `Success = true`, cập nhật đúng `LocationName` và `Address` | `UTCID01, UTCID02, UTCID03, UTCID17, UTCID20` |
| | `OkObjectResult` với `StatusCode = 200 OK` | `UTCID04` |
| | `NotFoundObjectResult` với `StatusCode = 404 Not Found` | `UTCID14` |
| | `ConflictObjectResult` với `StatusCode = 409 Conflict` | `UTCID15` |
| | `BadRequestObjectResult` với `StatusCode = 400 Bad Request` | `UTCID16` |
| | `ApiResponseDto<LocationDto>` với `Success = false`, `Data = null` | `UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID13, UTCID18, UTCID19, UTCID21, UTCID22, UTCID23` |
| | `LocationsController.UpdateLocation` có `[HttpPut("{locationId:guid}")]` và `[Authorize(Roles = "STAFF,ADMIN")]` | `UTCID24` |
| **Exception** | None (Exceptions được catch trong `UpdateLocationAsync` và trả về `ApiResponseDto.Fail`) | `UTCID01..UTCID24` |
| **Log / Assert Message** | `"Location updated successfully."` & Verify `SaveChangesAsync` `Times.Once` | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID17, UTCID20` |
| | `"Request payload cannot be null."` | `UTCID05` |
| | `"Location name cannot be empty."` | `UTCID06, UTCID07, UTCID08, UTCID16` |
| | `"Address cannot be empty."` | `UTCID09, UTCID10, UTCID11` |
| | `"Location not found."` | `UTCID12, UTCID14` |
| | `"Location 'Taken Gym Name' already exists."` / `"Location 'Taken Gym' already exists."` | `UTCID13, UTCID15` |
| | `"Location name cannot exceed 200 characters."` | `UTCID18` |
| | `"Address cannot exceed 200 characters."` | `UTCID19` |
| | `"Failed to update location: Database read failure."` | `UTCID21` |
| | `"Failed to update location: Concurrency conflict during update."` | `UTCID22` |
| | `"Failed to update location: ..."` (OperationCanceledException) | `UTCID23` |

---

### CHỨC NĂNG 8: Delete Location (`DLOC`) — Sheet `DeleteLocationAsync`

#### 1. Header Info
- **Function Code**: `DeleteLocationAsync`
- **Function Name**: `Delete Location`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `23` *(13 câu lệnh không tính `{}`)*
- **Test requirement**: `Verify that DeleteLocationAsync checks whether the target location exists via GetByIdWithDetailsAsync, blocks deletion when the location has active posts (IsDeleted != true, including null IsDeleted) or linked coaches, removes the location and commits via IUnitOfWork when only soft-deleted posts or empty/null collections exist, maps controller status codes (200, 400, 404, 409), and handles exceptions.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **17** | **0** | **0** | **4** | **10** | **3** | **17** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID17`
| UTCID | Tên test method gốc (trong `LocationServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `DeleteLocationAsync_ValidLocationWithNoPostsAndNoCoaches_ShouldDeleteSuccessfully` | N | Passed | 2026-11-03 |
| `UTCID02` | `DeleteLocationAsync_LocationWithOnlyDeletedPosts_ShouldDeleteSuccessfully` | N | Passed | 2026-11-03 |
| `UTCID03` | `LocationsController_DeleteLocation_WhenSuccessful_ShouldReturn200Ok` | N | Passed | 2026-11-03 |
| `UTCID04` | `DeleteLocationAsync_WhenLocationNotFound_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID05` | `DeleteLocationAsync_WhenLocationHasActivePosts_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID06` | `DeleteLocationAsync_WhenLocationHasLinkedCoaches_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID07` | `LocationsController_DeleteLocation_WhenLocationNotFound_ShouldReturn404NotFound` | A | Passed | 2026-11-03 |
| `UTCID08` | `LocationsController_DeleteLocation_WhenLocationUsedByActivePosts_ShouldReturn409Conflict` | A | Passed | 2026-11-03 |
| `UTCID09` | `LocationsController_DeleteLocation_WhenLocationLinkedToCoaches_ShouldReturn409Conflict` | A | Passed | 2026-11-03 |
| `UTCID10` | `LocationsController_DeleteLocation_WhenOtherFailureOccurs_ShouldReturn400BadRequest` | A | Passed | 2026-11-03 |
| `UTCID11` | `DeleteLocationAsync_WhenLocationHasNullPostsAndNullCoachesCollections_ShouldDeleteSuccessfully` | B | Passed | 2026-11-03 |
| `UTCID12` | `DeleteLocationAsync_WhenLocationHasPostWithNullIsDeleted_ShouldTreatAsActiveAndRejectDeletion` | B | Passed | 2026-11-03 |
| `UTCID13` | `DeleteLocationAsync_WithEmptyGuid_WhenNotFound_ShouldReturnFailureResult` | B | Passed | 2026-11-03 |
| `UTCID14` | `DeleteLocationAsync_WhenRepositoryThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID15` | `DeleteLocationAsync_WhenUnitOfWorkThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID16` | `DeleteLocationAsync_WhenCancellationRequested_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID17` | `LocationsController_DeleteLocationEndpoint_RequiresStaffOrAdmin` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. `locationId` Input** | Valid non-empty `Guid` | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID14, UTCID15, UTCID16` |
| | `Guid.Empty` | `UTCID13` |
| | N/A (Controller attribute reflection) | `UTCID17` |
| **2. Location Existence (`GetByIdWithDetailsAsync`)** | Exists (`Location` returned) | `UTCID01, UTCID02, UTCID03, UTCID05, UTCID06, UTCID08, UTCID09, UTCID11, UTCID12, UTCID15` |
| | Not found (`null`) | `UTCID04, UTCID07, UTCID13` |
| | Throws `InvalidOperationException` / `OperationCanceledException` / Service error | `UTCID10, UTCID14, UTCID16` |
| | N/A (Controller attribute reflection) | `UTCID17` |
| **3. Associated `Posts` Collection State** | Empty `Posts = []` | `UTCID01, UTCID03, UTCID06, UTCID09, UTCID15` |
| | Only soft-deleted posts (`IsDeleted = true`) | `UTCID02` |
| | Has active post (`IsDeleted = false`) | `UTCID05, UTCID08` |
| | Has post with `IsDeleted = null` (treated as active) | `UTCID12` |
| | `Posts = null!` | `UTCID11` |
| | N/A (Location not found / Repo exception / Reflection) | `UTCID04, UTCID07, UTCID10, UTCID13, UTCID14, UTCID16, UTCID17` |
| **4. Associated `Coaches` Collection & Persistence** | Empty (`Coaches = []`) or `null!` & `SaveChangesAsync` succeeds | `UTCID01, UTCID02, UTCID03, UTCID11` |
| | Has linked `CoachProfile` (`Coaches.Count == 1`) | `UTCID06, UTCID09` |
| | `_unitOfWork.SaveChangesAsync` throws `Exception("DB constraint violation during save.")` | `UTCID15` |
| | N/A (Short-circuited by NotFound / ActivePosts / Repo exception / Reflection) | `UTCID04, UTCID05, UTCID07, UTCID08, UTCID10, UTCID12, UTCID13, UTCID14, UTCID16, UTCID17` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<bool>` với `Success = true`, `Data = true` | `UTCID01, UTCID02, UTCID11` |
| | `OkObjectResult` với `StatusCode = 200 OK` | `UTCID03` |
| | `NotFoundObjectResult` với `StatusCode = 404 Not Found` | `UTCID07` |
| | `ConflictObjectResult` với `StatusCode = 409 Conflict` | `UTCID08, UTCID09` |
| | `BadRequestObjectResult` với `StatusCode = 400 Bad Request` | `UTCID10` |
| | `ApiResponseDto<bool>` với `Success = false`, `Data = false` | `UTCID04, UTCID05, UTCID06, UTCID12, UTCID13, UTCID14, UTCID15, UTCID16` |
| | `LocationsController.DeleteLocation` có `[HttpDelete("{locationId:guid}")]` và `[Authorize(Roles = "STAFF,ADMIN")]` | `UTCID17` |
| **Exception** | None (Exceptions được catch trong `DeleteLocationAsync` và trả về `ApiResponseDto.Fail`) | `UTCID01..UTCID17` |
| **Log / Assert Message** | `"Location deleted successfully."` & Verify `Remove(location)` + `SaveChangesAsync` `Times.Once` | `UTCID01, UTCID02, UTCID03, UTCID11` |
| | `"Location not found."` | `UTCID04, UTCID07, UTCID13` |
| | `"Cannot delete this location because it is currently used by active posts."` | `UTCID05, UTCID08, UTCID12` |
| | `"Cannot delete this location because it is currently linked to coaches."` | `UTCID06, UTCID09` |
| | `"Error deleting location: Database connection timeout."` | `UTCID10, UTCID14` |
| | `"Error deleting location: DB constraint violation during save."` | `UTCID15` |
| | `"Error deleting location: ..."` (OperationCanceledException) | `UTCID16` |

---

### CHỨC NĂNG 9: View System Logs (`VSL`) — Sheet `GetSystemLogsAsync`

#### 1. Header Info
- **Function Code**: `GetSystemLogsAsync` *(kèm `GetLogDetailAsync`, `DetermineLogLevel`, `Sanitize`)*
- **Function Name**: `View System Logs`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `25` *(riêng `GetSystemLogsAsync`: 25 dòng [21 câu lệnh]; nếu tính cả `GetLogDetailAsync` [25], `DetermineLogLevel` [18], `Sanitize` [26] là `94` dòng)*
- **Test requirement**: `Verify that GetSystemLogsAsync, GetLogDetailAsync, DetermineLogLevel, and Sanitize accurately retrieve paginated system logs and log details, normalize pagination bounds (PageNumber >= 1, PageSize <= 100), classify action codes into Information/Warning/Error levels, redact sensitive credentials/tokens/connection strings, and enforce ADMIN-only authorization.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **21** | **0** | **0** | **16** | **2** | **3** | **21** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID21`
| UTCID | Tên test method gốc (trong `SystemLogServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `GetSystemLogsAsync_ShouldReturnPagedLogsSuccessfully` | N | Passed | 2026-11-03 |
| `UTCID02` | `GetLogDetailAsync_ExistingLog_ShouldReturnLogDetail` | N | Passed | 2026-11-03 |
| `UTCID03` | `GetLogDetailAsync_NonExistentLog_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID04` | `DetermineLogLevel_ShouldAccuratelyCategorizeLogLevel(action: "SYSTEM_ERROR", expectedLevel: "Error")` | N | Passed | 2026-11-03 |
| `UTCID05` | `DetermineLogLevel_ShouldAccuratelyCategorizeLogLevel(action: "UNHANDLED_EXCEPTION", expectedLevel: "Error")` | N | Passed | 2026-11-03 |
| `UTCID06` | `DetermineLogLevel_ShouldAccuratelyCategorizeLogLevel(action: "PAYMENT_FAILED", expectedLevel: "Error")` | N | Passed | 2026-11-03 |
| `UTCID07` | `DetermineLogLevel_ShouldAccuratelyCategorizeLogLevel(action: "CRITICAL_SECURITY_ALERT", expectedLevel: "Error")` | N | Passed | 2026-11-03 |
| `UTCID08` | `DetermineLogLevel_ShouldAccuratelyCategorizeLogLevel(action: "REJECT_REFUND_REQUEST", expectedLevel: "Warning")` | N | Passed | 2026-11-03 |
| `UTCID09` | `DetermineLogLevel_ShouldAccuratelyCategorizeLogLevel(action: "LOCK_ACCOUNT", expectedLevel: "Warning")` | N | Passed | 2026-11-03 |
| `UTCID10` | `DetermineLogLevel_ShouldAccuratelyCategorizeLogLevel(action: "CANCEL_SUBSCRIPTION", expectedLevel: "Warning")` | N | Passed | 2026-11-03 |
| `UTCID11` | `DetermineLogLevel_ShouldAccuratelyCategorizeLogLevel(action: "DELETE_PACKAGE", expectedLevel: "Warning")` | N | Passed | 2026-11-03 |
| `UTCID12` | `DetermineLogLevel_ShouldAccuratelyCategorizeLogLevel(action: "APPROVE_REFUND_REQUEST", expectedLevel: "Information")` | N | Passed | 2026-11-03 |
| `UTCID13` | `DetermineLogLevel_ShouldAccuratelyCategorizeLogLevel(action: "UPDATE_PRICE", expectedLevel: "Information")` | N | Passed | 2026-11-03 |
| `UTCID14` | `DetermineLogLevel_ShouldAccuratelyCategorizeLogLevel(action: "PROCESS_PAYOUT", expectedLevel: "Information")` | N | Passed | 2026-11-03 |
| `UTCID15` | `Sanitize_ShouldMaskSensitiveCredentialsAndTokens` | N | Passed | 2026-11-03 |
| `UTCID16` | `Sanitize_ShouldMaskConnectionStringAndBearerTokens` | N | Passed | 2026-11-03 |
| `UTCID17` | `GetSystemLogsAsync_WhenPageSizeExceeds100_ClampsPageSizeTo100` | B | Passed | 2026-11-03 |
| `UTCID18` | `GetSystemLogsAsync_WhenPageNumberLessThan1_NormalizesToOne` | B | Passed | 2026-11-03 |
| `UTCID19` | `GetSystemLogsAsync_WhenNoLogsFound_ReturnsEmptyPagedResult` | B | Passed | 2026-11-03 |
| `UTCID20` | `GetSystemLogsAsync_WhenRepositoryThrows_ReturnsFailResult` | A | Passed | 2026-11-03 |
| `UTCID21` | `SystemLogsController_HasAuthorizeAttribute_OnlyForAdmin` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Target Method** | `GetSystemLogsAsync(request)` | `UTCID01, UTCID17, UTCID18, UTCID19, UTCID20` |
| | `GetLogDetailAsync(logId)` | `UTCID02, UTCID03` |
| | `DetermineLogLevel(action, details)` | `UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID13, UTCID14` |
| | `Sanitize(input)` | `UTCID15, UTCID16` |
| | `SystemLogsController` `[Authorize(Roles = "ADMIN")]` check | `UTCID21` |
| **2. Pagination / Input Parameters** | Standard (`PageNumber = 1, PageSize = 20`) | `UTCID01, UTCID19, UTCID20` |
| | Boundary: `PageSize = 500` (> 100 max limit) | `UTCID17` |
| | Boundary: `PageNumber = 0` (< 1 min limit) | `UTCID18` |
| | `logId = 100` (Exists) / `logId = 999` (Not found) | `UTCID02, UTCID03` |
| | Action code / Sensitive string inputs | `UTCID04..UTCID16` |
| | N/A (Controller attribute reflection) | `UTCID21` |
| **3. Repository / Action Category** | Returns 2 `SystemLog` records / 1 existing detail record | `UTCID01, UTCID02` |
| | Returns empty list / `null` detail | `UTCID03, UTCID17, UTCID18, UTCID19` |
| | Throws `InvalidOperationException("Failed to read audit logs from database")` | `UTCID20` |
| | Error actions (`SYSTEM_ERROR`, `UNHANDLED_EXCEPTION`, `PAYMENT_FAILED`, `CRITICAL_SECURITY_ALERT`) | `UTCID04, UTCID05, UTCID06, UTCID07` |
| | Warning actions (`REJECT_REFUND_REQUEST`, `LOCK_ACCOUNT`, `CANCEL_SUBSCRIPTION`, `DELETE_PACKAGE`) | `UTCID08, UTCID09, UTCID10, UTCID11` |
| | Information actions (`APPROVE_REFUND_REQUEST`, `UPDATE_PRICE`, `PROCESS_PAYOUT`) | `UTCID12, UTCID13, UTCID14` |
| | Sensitive strings (`password`, `token`, `apiKey`, `Password=`, `Bearer `) | `UTCID15, UTCID16` |
| | N/A (Controller attribute reflection) | `UTCID21` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<PagedResultDto<SystemLogListItemDto>>` (`Success = true`), `LogIdDisplay = "LOG-000001"` | `UTCID01` |
| | `ApiResponseDto<PagedResultDto<SystemLogListItemDto>>` (`Success = true`) với `PageSize = 100` / `PageNumber = 1` / `Items` rỗng | `UTCID17, UTCID18, UTCID19` |
| | `ApiResponseDto<SystemLogDetailDto>` (`Success = true`, `LogId = 100`, `Level = "Error"`) | `UTCID02` |
| | `ApiResponseDto` với `Success = false` | `UTCID03, UTCID20` |
| | Level `"Error"` / `"Warning"` / `"Information"` chính xác | `UTCID04..UTCID14` |
| | Chuỗi đã che mật khẩu/token thành `"***REDACTED***"` | `UTCID15, UTCID16` |
| | `SystemLogsController` chỉ cho phép duy nhất `RoleConstants.Admin` | `UTCID21` |
| **Exception** | None (Exceptions được catch trong `SystemLogService`) | `UTCID01..UTCID21` |
| **Log / Assert Message** | `"System logs retrieved successfully."` | `UTCID01, UTCID17, UTCID18, UTCID19` |
| | `"System log detail retrieved successfully."` | `UTCID02` |
| | `"System log entry not found."` | `UTCID03` |
| | `"Failed to load system logs. Please try again."` | `UTCID20` |
| | Assert `"***REDACTED***"`取代 `SuperSecret123!`, `SecretDbPassword123`, `my_jwt_token_123456789` | `UTCID15, UTCID16` |

---

### CHỨC NĂNG 10: View Staff Accounts List (`VSAL`) — Sheet `GetStaffUsersAsync`

#### 1. Header Info
- **Function Code**: `GetStaffUsersAsync`
- **Function Name**: `View Staff Accounts List`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `12` *(8 câu lệnh không tính `{}`)*
- **Test requirement**: `Verify that GetStaffUsersAsync retrieves a paginated list of STAFF accounts via IUserRepository.ListStaffForAdminAsync, passes search and isLocked filter parameters accurately, returns an empty PagedResultDto when no staff accounts match, handles repository exceptions, and enforces ADMIN-only endpoint authorization.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **5** | **0** | **0** | **3** | **1** | **1** | **5** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID05`
| UTCID | Tên test method gốc (trong `AdminUserServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `GetStaffUsersAsync_ShouldCallListStaffForAdminAsync_AndReturnPagedStaffAccounts` | N | Passed | 2026-11-03 |
| `UTCID02` | `GetStaffUsersAsync_WithSearchAndLockedFilter_ShouldPassParametersCorrectly` | N | Passed | 2026-11-03 |
| `UTCID03` | `GetStaffUsersAsync_WhenRepositoryThrows_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID04` | `GetStaffUsersAsync_EmptyResult_ShouldReturnEmptyPagedResult` | B | Passed | 2026-11-03 |
| `UTCID05` | `AdminUsersController_ClassAndStaffEndpoints_RequireProperRoles` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Filter Parameters (`search`, `isLocked`, `pageNumber`, `pageSize`)** | Default (`search: null, isLocked: null, pageNumber: 1, pageSize: 10`) | `UTCID01, UTCID03` |
| | Filtered (`search: "active"`, `isLocked: false`, `pageNumber: 1`, `pageSize: 5`) | `UTCID02` |
| | Non-matching search (`search: "nonexistent"`, `isLocked: false`, `pageNumber: 1`, `pageSize: 10`) | `UTCID04` |
| | N/A (Controller attribute reflection) | `UTCID05` |
| **2. Repository (`_userRepo.ListStaffForAdminAsync`) Behavior** | Returns 2 staff accounts (`Staff One` unlocked, `Staff Two` locked) | `UTCID01` |
| | Returns 1 active staff account (`Active Staff`) | `UTCID02` |
| | Returns empty list `(Items: [], TotalCount: 0)` | `UTCID04` |
| | Throws `Exception("Database connection failure")` | `UTCID03` |
| | N/A (Controller attribute reflection) | `UTCID05` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<PagedResultDto<AdminUserDto>>` (`Success = true`), toàn bộ `Items` có `RoleCode == "STAFF"` | `UTCID01, UTCID02` |
| | `ApiResponseDto<PagedResultDto<AdminUserDto>>` (`Success = true`), `Items` rỗng, `TotalCount = 0` | `UTCID04` |
| | `ApiResponseDto<PagedResultDto<AdminUserDto>>` (`Success = false`, `Data = null`) | `UTCID03` |
| | `AdminUsersController.GetStaff` có `[Authorize(Roles = "ADMIN")]` | `UTCID05` |
| **Exception** | None (Exceptions được catch và trả về `ApiResponseDto.Fail`) | `UTCID01..UTCID05` |
| **Log / Assert Message** | `"Staff accounts retrieved successfully."` | `UTCID01, UTCID02, UTCID04` |
| | `"Error retrieving staff accounts: Database connection failure"` | `UTCID03` |

---

### CHỨC NĂNG 11: Create Staff Accounts (`CSA`) — Sheet `CreateStaffAccountAsync`

#### 1. Header Info
- **Function Code**: `CreateStaffAccountAsync` *(kèm `CheckEmailExistsAsync`)*
- **Function Name**: `Create Staff Accounts`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `97` *(riêng `CreateStaffAccountAsync`: 97 dòng [63 câu lệnh]; nếu tính thêm `CheckEmailExistsAsync` [15 dòng] là `112` dòng)*
- **Test requirement**: `Verify that CreateStaffAccountAsync (and CheckEmailExistsAsync) validates all input fields (non-null request, FullName length >= 2 without numbers, valid unique Email, optional valid 10-digit PhoneNumber starting with 0 without letters and not duplicated, Password length >= 6 matching ConfirmPassword), hashes the password, creates an internal STAFF user, logs CREATE_STAFF, and handles repository exceptions.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **17** | **0** | **0** | **4** | **9** | **4** | **17** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID17`
| UTCID | Tên test method gốc (trong `AdminUserServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `CreateStaffAccountAsync_ValidRequest_ShouldCreateStaffWithStaffRoleAndHashedPassword` | N | Passed | 2026-11-03 |
| `UTCID02` | `CreateStaffAccountAsync_EmailAlreadyExists_ShouldReturnFailureWithoutCreating` | A | Passed | 2026-11-03 |
| `UTCID03` | `CreateStaffAccountAsync_PasswordMismatch_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID04` | `CreateStaffAccountAsync_PhoneAlreadyExists_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID05` | `CreateStaffAccountAsync_FullNameContainsNumbers_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID06` | `CreateStaffAccountAsync_PhoneNumberContainsLetters_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID07` | `CreateStaffAccountAsync_PhoneNumberInvalidFormat_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID08` | `CreateStaffAccountAsync_WhenExceptionOccurs_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID09` | `CheckEmailExistsAsync_WhenEmailExists_ShouldReturnTrueWithRegisteredMessage` | N | Passed | 2026-11-03 |
| `UTCID10` | `CheckEmailExistsAsync_WhenEmailDoesNotExist_ShouldReturnFalseWithAvailableMessage` | N | Passed | 2026-11-03 |
| `UTCID11` | `CheckEmailExistsAsync_WhenEmailIsEmpty_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID12` | `CreateStaffAccountAsync_NullRequest_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID13` | `CreateStaffAccountAsync_FullNameLength1_ShouldReturnFailure` | B | Passed | 2026-11-03 |
| `UTCID14` | `CreateStaffAccountAsync_PasswordLength5_ShouldReturnFailure` | B | Passed | 2026-11-03 |
| `UTCID15` | `CreateStaffAccountAsync_PhoneNumberInvalidBoundaryLength_ShouldReturnFailure(phone: "090123456")` | B | Passed | 2026-11-03 |
| `UTCID16` | `CreateStaffAccountAsync_PhoneNumberInvalidBoundaryLength_ShouldReturnFailure(phone: "09012345678")` | B | Passed | 2026-11-03 |
| `UTCID17` | `AdminUsersController_ClassAndStaffEndpoints_RequireProperRoles` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Target Method & Request DTO** | `CreateStaffAccountAsync(request)` with non-null DTO | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID13, UTCID14, UTCID15, UTCID16` |
| | `CreateStaffAccountAsync(null!)` | `UTCID12` |
| | `CheckEmailExistsAsync(email)` | `UTCID09, UTCID10, UTCID11` |
| | `AdminUsersController.CreateStaff` `[Authorize(Roles = "ADMIN")]` check | `UTCID17` |
| **2. `FullName` Input** | Valid name (`"New Staff Member"`, `"Duplicate Staff"`, `"Valid Name"`, `"Nguyen Staff"`, etc.) | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID06, UTCID07, UTCID08, UTCID14, UTCID15, UTCID16` |
| | Contains numbers (`"John Doe 123"`) | `UTCID05` |
| | Boundary length = `1` (`"A"`) | `UTCID13` |
| | N/A (`null` DTO / `CheckEmailExistsAsync` / Reflection) | `UTCID09, UTCID10, UTCID11, UTCID12, UTCID17` |
| **3. `Email` & Duplicate Check (`ExistsByEmailAsync`)** | Valid email & `ExistsByEmailAsync == false` | `UTCID01, UTCID03, UTCID04, UTCID10` |
| | Valid email & `ExistsByEmailAsync == true` (Duplicate) | `UTCID02, UTCID09` |
| | Empty email `""` | `UTCID11` |
| | `ExistsByEmailAsync` throws `Exception("Database write error")` | `UTCID08` |
| | Short-circuited before email check or Reflection | `UTCID05, UTCID06, UTCID07, UTCID12, UTCID13, UTCID14, UTCID15, UTCID16, UTCID17` |
| **4. `PhoneNumber` & Duplicate Check (`ExistsByPhoneAsync`)** | Valid `"0987654321"` (`ExistsByPhoneAsync == false`) or `null` | `UTCID01, UTCID02, UTCID03, UTCID05, UTCID08, UTCID13, UTCID14` |
| | Duplicate phone `"0911222333"` (`ExistsByPhoneAsync == true`) | `UTCID04` |
| | Contains letters (`"090123abc"`) | `UTCID06` |
| | Invalid format (`"12345"`) | `UTCID07` |
| | Boundary length = `9` digits (`"090123456"`) | `UTCID15` |
| | Boundary length = `11` digits (`"09012345678"`) | `UTCID16` |
| | N/A (`null` DTO / `CheckEmailExistsAsync` / Reflection) | `UTCID09, UTCID10, UTCID11, UTCID12, UTCID17` |
| **5. `Password` & `ConfirmPassword`** | Valid matching password (`"Password123!"`) | `UTCID01, UTCID02, UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID13, UTCID15, UTCID16` |
| | Mismatched (`"Password123!"` vs `"DifferentPassword!"`) | `UTCID03` |
| | Boundary length = `5` chars (`"12345"`) | `UTCID14` |
| | N/A (`null` DTO / `CheckEmailExistsAsync` / Reflection) | `UTCID09, UTCID10, UTCID11, UTCID12, UTCID17` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<AdminUserDto>` với `Success = true`, `RoleCode = "STAFF"`, `IsInternal = true`, `PasswordHash = "hashed_pw_secret"`, `TokenVersion = 1` | `UTCID01` |
| | `ApiResponseDto<bool>` với `Success = true`, `Data = true` (Email đã tồn tại) / `Data = false` (Email khả dụng) | `UTCID09, UTCID10` |
| | `ApiResponseDto` với `Success = false` | `UTCID02, UTCID03, UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID11, UTCID12, UTCID13, UTCID14, UTCID15, UTCID16` |
| | `AdminUsersController.CreateStaff` có `[Authorize(Roles = "ADMIN")]` | `UTCID17` |
| **Exception** | None (Exceptions được catch và trả về `ApiResponseDto.Fail`) | `UTCID01..UTCID17` |
| **Log / Assert Message** | `"Staff account 'newstaff@fitsocial.com' has been created successfully."` & Verify `AddAsync` + `SaveChangesAsync` `Times.Once` | `UTCID01` |
| | `"Request cannot be null."` | `UTCID12` |
| | `"Full name is required (at least 2 characters)."` | `UTCID13` |
| | `"Full name cannot contain numbers."` | `UTCID05` |
| | `"Phone number cannot contain letters."` | `UTCID06` |
| | `"Phone number must be a valid 10-digit number starting with 0 (e.g. 0901234567)."` | `UTCID07, UTCID15, UTCID16` |
| | `"Password must be at least 6 characters long."` | `UTCID14` |
| | `"Passwords do not match."` | `UTCID03` |
| | `"Email is already registered. Please use a different email address."` / `"Email is already registered."` | `UTCID02, UTCID09` |
| | `"Email is available."` | `UTCID10` |
| | `"Email cannot be empty."` | `UTCID11` |
| | `"Phone number is already registered to another account."` | `UTCID04` |
| | `"Error creating staff account: Database write error"` | `UTCID08` |

---

### CHỨC NĂNG 12: Edit Staff Accounts (`ESA`) — Sheet `UpdateStaffAccountAsync`

#### 1. Header Info
- **Function Code**: `UpdateStaffAccountAsync`
- **Function Name**: `Edit Staff Accounts`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `103` *(63 câu lệnh không tính `{}`)*
- **Test requirement**: `Verify that UpdateStaffAccountAsync validates the UpdateStaffAccountRequestDto input, verifies that the target user exists and has RoleCode == "STAFF", checks email and phone uniqueness excluding the current staff userId, updates FullName, Email, PhoneNumber, and optionally hashes a new Password while incrementing TokenVersion, and handles exceptions.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **11** | **0** | **0** | **2** | **8** | **1** | **11** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID11`
| UTCID | Tên test method gốc (trong `AdminUserServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `UpdateStaffAccountAsync_ValidRequest_ShouldUpdateStaffSuccessfully` | N | Passed | 2026-11-03 |
| `UTCID02` | `UpdateStaffAccountAsync_UserNotFound_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID03` | `UpdateStaffAccountAsync_UserNotStaff_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID04` | `UpdateStaffAccountAsync_FullNameContainsNumbers_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID05` | `UpdateStaffAccountAsync_PhoneContainsLetters_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID06` | `UpdateStaffAccountAsync_PhoneInvalidFormat_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID07` | `UpdateStaffAccountAsync_EmailAlreadyUsedByAnotherUser_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID08` | `UpdateStaffAccountAsync_NullRequest_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID09` | `UpdateStaffAccountAsync_FullNameLength1_ShouldReturnFailure` | B | Passed | 2026-11-03 |
| `UTCID10` | `UpdateStaffAccountAsync_WhenExceptionOccurs_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID11` | `AdminUsersController_ClassAndStaffEndpoints_RequireProperRoles` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Request DTO (`UpdateStaffAccountRequestDto`)** | Non-null DTO | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID05, UTCID06, UTCID07, UTCID09, UTCID10` |
| | `null` | `UTCID08` |
| | N/A (Controller attribute reflection) | `UTCID11` |
| **2. Staff Existence & Role (`_userRepo.GetByIdAsync`)** | Exists & `RoleCode == "STAFF"` | `UTCID01, UTCID04, UTCID05, UTCID06, UTCID07, UTCID09` |
| | Not found (`null`) | `UTCID02` |
| | Exists & `RoleCode == "TRAINEE"` (Not a staff member) | `UTCID03` |
| | Throws `InvalidOperationException("DB error")` | `UTCID10` |
| | Not reached (`request == null` / Reflection) | `UTCID08, UTCID11` |
| **3. `FullName` Input** | Valid (`"Updated Staff"`, `"Staff Member"`, `"Valid Name"`) | `UTCID01, UTCID02, UTCID03, UTCID05, UTCID06, UTCID07, UTCID10` |
| | Contains numbers (`"Staff 123"`) | `UTCID04` |
| | Boundary length = `1` (`"X"`) | `UTCID09` |
| | N/A (`request == null` / Reflection) | `UTCID08, UTCID11` |
| **4. `PhoneNumber` & `Email` Uniqueness** | Valid phone `"0987654321"` / `null` & Unique email (`ExistsByEmailAsync == false`) | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID09, UTCID10` |
| | Phone contains letters (`"090123abc"`) | `UTCID05` |
| | Phone invalid format (`"12345"`) | `UTCID06` |
| | Email already used by another user (`ExistsByEmailAsync == true`) | `UTCID07` |
| | N/A (`request == null` / Reflection) | `UTCID08, UTCID11` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<AdminUserDto>` với `Success = true`, cập nhật `FullName`, `Email`, `PhoneNumber`, `PasswordHash = "new_hash"`, `TokenVersion = 2` | `UTCID01` |
| | `ApiResponseDto<AdminUserDto>` với `Success = false`, `Data = null` | `UTCID02, UTCID03, UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10` |
| | `AdminUsersController.UpdateStaff` có `[Authorize(Roles = "ADMIN")]` | `UTCID11` |
| **Exception** | None (Exceptions được catch và trả về `ApiResponseDto.Fail`) | `UTCID01..UTCID11` |
| **Log / Assert Message** | `"Staff account 'updated@fitsocial.com' has been updated successfully."` & Verify `SaveChangesAsync` `Times.Once` | `UTCID01` |
| | `"Request cannot be null."` | `UTCID08` |
| | `"Staff account not found."` | `UTCID02` |
| | `"User is not a staff member."` | `UTCID03` |
| | `"Full name is required (at least 2 characters)."` | `UTCID09` |
| | `"Full name cannot contain numbers."` | `UTCID04` |
| | `"Phone number cannot contain letters."` | `UTCID05` |
| | `"Phone number must be a valid 10-digit number starting with 0 (e.g. 0901234567)."` | `UTCID06` |
| | `"Email is already registered to another account."` | `UTCID07` |
| | `"Error updating staff account: DB error"` | `UTCID10` |

---

### CHỨC NĂNG 13: Lock Staff Account (`LSA`) — Sheet `LockStaffAccountAsync`

#### 1. Header Info
- **Function Code**: `SetUserLockStatusAsync` *(nhánh `isLocked = true`)*
- **Function Name**: `Lock Staff Account`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `42` *(28 câu lệnh không tính `{}`)*
- **Test requirement**: `Verify that SetUserLockStatusAsync (with isLocked = true) locks the target account (IsLocked = true), records LockedBy = adminId, increments TokenVersion by 1 to invalidate active sessions, logs LOCK_USER, and returns a failure result when the target user is not found or a repository exception occurs.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **6** | **0** | **0** | **3** | **2** | **1** | **6** |
  *(Ghi chú: Bao gồm 3 TC trực tiếp trên `SetUserLockStatusAsync` cho nhánh Lock [`UTCID01..UTCID03`], 2 TC liên quan cơ chế auto-lock và tăng `TokenVersion` trong `AdminUserServiceTests.cs` [`UTCID04, UTCID05`], và 1 TC kiểm tra phân quyền Controller [`UTCID06`]).*

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID06`
| UTCID | Tên test method gốc (trong `AdminUserServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `SetUserLockStatusAsync_LockUser_ShouldIncrementTokenVersion` | N | Passed | 2026-11-03 |
| `UTCID02` | `SetUserLockStatusAsync_UserNotFound_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID03` | `SetUserLockStatusAsync_WhenRepositoryThrows_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID04` | `WarnUserAsync_FirstWarning_ShouldIncrementWarningCountOnly` | N | Passed | 2026-11-03 |
| `UTCID05` | `WarnUserAsync_ThirdWarning_ShouldAutoLockAndInvalidateTokens` | B | Passed | 2026-11-03 |
| `UTCID06` | `AdminUsersController_ClassAndStaffEndpoints_RequireProperRoles` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Target Method & `isLocked` Parameter** | `SetUserLockStatusAsync(userId, isLocked: true, adminId)` | `UTCID01, UTCID02, UTCID03` |
| | `WarnUserAsync(userId, staffId, reason)` (Warning threshold lock check) | `UTCID04, UTCID05` |
| | `AdminUsersController` `[Authorize]` attribute check | `UTCID06` |
| **2. User Existence & Initial State (`_userRepo.GetByIdAsync`)** | Exists (`IsLocked = false`, `TokenVersion = 1`, `WarningCount = 0`) | `UTCID01, UTCID04` |
| | Exists (`IsLocked = false`, `TokenVersion = 1`, `WarningCount = 2` -> boundary 3rd warning) | `UTCID05` |
| | Not found (`null`) | `UTCID02` |
| | Throws `InvalidOperationException("DB timeout")` | `UTCID03` |
| | N/A (Controller attribute reflection) | `UTCID06` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<bool>` với `Success = true`, `Data = true`, `user.IsLocked = true`, `user.LockedBy = adminId`, `user.TokenVersion = 2` | `UTCID01` |
| | `ApiResponseDto<AdminUserDto>` với `Success = true`, `WarningCount = 1`, `IsLocked = false` (`UTCID04`) / `WarningCount = 3`, `IsLocked = true`, `TokenVersion = 2` (`UTCID05`) | `UTCID04, UTCID05` |
| | `ApiResponseDto<bool>` với `Success = false` | `UTCID02, UTCID03` |
| | `AdminUsersController` yêu cầu role `ADMIN` / `STAFF` | `UTCID06` |
| **Exception** | None (Exceptions được catch và trả về `ApiResponseDto.Fail`) | `UTCID01..UTCID06` |
| **Log / Assert Message** | `"User 'Violator' has been locked."` & Verify `SaveChangesAsync` `Times.Once` | `UTCID01` |
| | `"User not found."` | `UTCID02` |
| | `"Error setting lock status: DB timeout"` | `UTCID03` |
| | `"Account has been automatically locked"` (khi `WarningCount >= 3`) | `UTCID05` |

---

### CHỨC NĂNG 14: Unlock Staff Account (`USA`) — Sheet `UnlockStaffAccountAsync`

#### 1. Header Info
- **Function Code**: `SetUserLockStatusAsync` *(nhánh `isLocked = false`)*
- **Function Name**: `Unlock Staff Account`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `42` *(28 câu lệnh không tính `{}`)*
- **Test requirement**: `Verify that SetUserLockStatusAsync (with isLocked = false) unlocks a locked account (IsLocked = false), clears LockedBy = null without altering TokenVersion, logs UNLOCK_USER, and returns a failure result when the user does not exist or a database exception occurs.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **4** | **0** | **0** | **2** | **2** | **0** | **4** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID04`
| UTCID | Tên test method gốc (trong `AdminUserServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `SetUserLockStatusAsync_UnlockUser_ShouldSetIsLockedFalse_AndClearLockedBy` | N | Passed | 2026-11-03 |
| `UTCID02` | `SetUserLockStatusAsync_UserNotFound_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID03` | `SetUserLockStatusAsync_WhenRepositoryThrows_ShouldReturnFailure` | A | Passed | 2026-11-03 |
| `UTCID04` | `AdminUsersController_ClassAndStaffEndpoints_RequireProperRoles` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. `isLocked` Input** | `false` (Unlock account) | `UTCID01` |
| | `true` / Any (Shared NotFound & Exception paths) | `UTCID02, UTCID03` |
| | N/A (Controller attribute reflection) | `UTCID04` |
| **2. User Existence (`_userRepo.GetByIdAsync`)** | Exists (`IsLocked = true`, `LockedBy = Guid.NewGuid()`, `TokenVersion = 3`) | `UTCID01` |
| | Not found (`null`) | `UTCID02` |
| | Throws `InvalidOperationException("DB timeout")` | `UTCID03` |
| | N/A (Controller attribute reflection) | `UTCID04` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<bool>` với `Success = true`, `Data = true`, `lockedUser.IsLocked = false`, `lockedUser.LockedBy = null` | `UTCID01` |
| | `ApiResponseDto<bool>` với `Success = false` | `UTCID02, UTCID03` |
| | `AdminUsersController` yêu cầu role `ADMIN` / `STAFF` | `UTCID04` |
| **Exception** | None (Exceptions được catch và trả về `ApiResponseDto.Fail`) | `UTCID01..UTCID04` |
| **Log / Assert Message** | `"User 'Locked User' has been unlocked."` & Verify `SaveChangesAsync` `Times.Once` | `UTCID01` |
| | `"User not found."` | `UTCID02` |
| | `"Error setting lock status: DB timeout"` | `UTCID03` |

---

### CHỨC NĂNG 15: Update own Profile(trainee/coach) (`UOPTC`) — Sheet `UpdateOwnProfileAsync`

#### 1. Header Info
- **Function Code**: `UpdateOwnProfileAsync`
- **Function Name**: `Update own Profile(trainee/coach)`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `156` *(94 câu lệnh không tính `{}`)*
- **Test requirement**: `Verify that UpdateOwnProfileAsync updates personal profile fields for TRAINEE and COACH accounts (including Coach-specific Bio and ExperienceYears), retains existing values when fields are blank, clears AvatarUrl when RemoveAvatar is true, and throws ValidationException, ConflictException, ForbiddenException, or NotFoundException for invalid inputs, duplicate phone numbers, locked accounts, or non-Trainee/Coach roles.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **18** | **0** | **0** | **5** | **9** | **4** | **18** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID18`
| UTCID | Tên test method gốc (trong `UpdateOwnProfileServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `UpdateOwnProfileAsync_WhenTrainee_UpdatesProfileSuccessfully` | N | Passed | 2026-11-03 |
| `UTCID02` | `UpdateOwnProfileAsync_WhenCoach_UpdatesCoachProfileSuccessfully` | N | Passed | 2026-11-03 |
| `UTCID03` | `UpdateOwnProfileAsync_WhenFullNameHasNumbers_ThrowsValidationException` | A | Passed | 2026-11-03 |
| `UTCID04` | `UpdateOwnProfileAsync_WhenPhoneFormatInvalid_ThrowsValidationException` | A | Passed | 2026-11-03 |
| `UTCID05` | `UpdateOwnProfileAsync_WhenPhoneAlreadyExists_ThrowsConflictException` | A | Passed | 2026-11-03 |
| `UTCID06` | `UpdateOwnProfileAsync_WhenDobInFuture_ThrowsValidationException` | A | Passed | 2026-11-03 |
| `UTCID07` | `UpdateOwnProfileAsync_WhenUserNotFound_ThrowsNotFoundException` | A | Passed | 2026-11-03 |
| `UTCID08` | `UpdateOwnProfileAsync_WhenUserIsLocked_ThrowsForbiddenException` | A | Passed | 2026-11-03 |
| `UTCID09` | `UpdateOwnProfileAsync_WhenFieldsEmptyOrNull_RetainsExistingValues` | N | Passed | 2026-11-03 |
| `UTCID10` | `UpdateOwnProfileAsync_WhenAvatarExplicitlyRemoved_ClearsAvatarUrl` | N | Passed | 2026-11-03 |
| `UTCID11` | `UpdateOwnProfileAsync_NullRequest_ThrowsValidationException` | A | Passed | 2026-11-03 |
| `UTCID12` | `UpdateOwnProfileAsync_WhenUserRoleIsNotTraineeOrCoach_ThrowsForbiddenException` | A | Passed | 2026-11-03 |
| `UTCID13` | `UpdateOwnProfileAsync_FullNameLength1_ThrowsValidationException` | B | Passed | 2026-11-03 |
| `UTCID14` | `UpdateOwnProfileAsync_FullNameLength2_UpdatesSuccessfully` | B | Passed | 2026-11-03 |
| `UTCID15` | `UpdateOwnProfileAsync_FullNameLength100_UpdatesSuccessfully` | B | Passed | 2026-11-03 |
| `UTCID16` | `UpdateOwnProfileAsync_FullNameLength101_ThrowsValidationException` | B | Passed | 2026-11-03 |
| `UTCID17` | `UpdateOwnProfileAsync_WhenDatabaseThrowsException_ThrowsException` | A | Passed | 2026-11-03 |
| `UTCID18` | `UsersController_ProfileEndpoints_EnforceRoleRestrictions` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Request DTO (`UpdateOwnProfileRequestDto`)** | Non-null DTO | `UTCID01..UTCID10, UTCID12..UTCID17` |
| | `null` | `UTCID11` |
| | N/A (Controller attribute reflection) | `UTCID18` |
| **2. User Existence (`_userRepo.GetUserForUpdateAsync`)** | Exists (`User` returned) | `UTCID01..UTCID06, UTCID08..UTCID10, UTCID12..UTCID17` |
| | Not found (`null`) | `UTCID07` |
| | Not called (`request == null` / Reflection) | `UTCID11, UTCID18` |
| **3. Account Lock Status (`user.IsLocked`)** | `false` (Unlocked) | `UTCID01..UTCID06, UTCID09, UTCID10, UTCID12..UTCID17` |
| | `true` (Locked) | `UTCID08` |
| | N/A (User not found / Null request / Reflection) | `UTCID07, UTCID11, UTCID18` |
| **4. User Role (`user.RoleCode`)** | `"TRAINEE"` | `UTCID01, UTCID03, UTCID04, UTCID05, UTCID06, UTCID08, UTCID09, UTCID10, UTCID13, UTCID14, UTCID15, UTCID16, UTCID17` |
| | `"COACH"` (with `CoachProfile`, `ExperienceYears = 7`, `Bio = "Updated Master Coach Bio"`) | `UTCID02` |
| | `"ADMIN"` (Not Trainee or Coach) | `UTCID12` |
| | N/A (User not found / Null request / Reflection) | `UTCID07, UTCID11, UTCID18` |
| **5. `request.FullName` Value** | Valid normal string | `UTCID01, UTCID02, UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID12, UTCID17` |
| | Empty / `null` (retains existing `FullName`) | `UTCID09, UTCID10` |
| | Contains numbers (`"Name123"`) | `UTCID03` |
| | Boundary length = `1` (`"A"`) | `UTCID13` |
| | Boundary length = `2` (`"An"`) | `UTCID14` |
| | Boundary length = `100` (`new string('A', 100)`) | `UTCID15` |
| | Boundary length = `101` (`new string('A', 101)`) | `UTCID16` |
| | N/A (`request == null` / Reflection) | `UTCID11, UTCID18` |
| **6. `PhoneNumber`, `DateOfBirth` & Persistence** | Valid phone & `ExistsByPhoneAsync == false` (or blank) & `SaveChangesAsync` succeeds | `UTCID01, UTCID02, UTCID09, UTCID10, UTCID14, UTCID15` |
| | Invalid phone format (`"12345"`) | `UTCID04` |
| | Duplicate phone (`"0912345678"`, `ExistsByPhoneAsync == true`) | `UTCID05` |
| | Future `DateOfBirth` (`DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))`) | `UTCID06` |
| | `_unitOfWork.SaveChangesAsync` throws `InvalidOperationException("Database write error")` | `UTCID17` |
| | Short-circuited before persistence or Reflection | `UTCID03, UTCID07, UTCID08, UTCID11, UTCID12, UTCID13, UTCID16, UTCID18` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<TraineeProfileDto>` với `Success = true`, cập nhật/giữ nguyên/xóa avatar đúng | `UTCID01, UTCID09, UTCID10, UTCID14, UTCID15` |
| | `ApiResponseDto<CoachProfileDto>` (`IsType<CoachProfileDto>`) với `Success = true`, `Bio = "Updated Master Coach Bio"`, `ExperienceYears = 7` | `UTCID02` |
| | `UsersController.UpdateOwnProfile` có `[Authorize(Roles = "TRAINEE,COACH")]` | `UTCID18` |
| **Exception** | `ValidationException` | `UTCID03, UTCID04, UTCID06, UTCID11, UTCID13, UTCID16` |
| | `ConflictException` | `UTCID05` |
| | `NotFoundException` | `UTCID07` |
| | `ForbiddenException` | `UTCID08, UTCID12` |
| | `InvalidOperationException` | `UTCID17` |
| **Log / Assert Message** | `"Your profile has been updated successfully."` & Verify `_userRepo.Update` + `SaveChangesAsync` `Times.Once` | `UTCID01, UTCID02, UTCID09, UTCID10, UTCID14, UTCID15` |
| | `"Invalid profile update request."` (`ValidationException`) | `UTCID11` |
| | `"User account not found."` (`NotFoundException`) | `UTCID07` |
| | `"Your account has been locked. Please contact an administrator."` (`ForbiddenException`) | `UTCID08` |
| | `"Only Trainee and Coach profiles can be updated through this endpoint."` (`ForbiddenException`) | `UTCID12` |
| | `"Full name cannot contain numbers."` | `UTCID03` |
| | `"Full name must be between 2 and 100 characters."` | `UTCID13, UTCID16` |
| | `"Phone number must be a valid 10-digit number starting with 0."` | `UTCID04` |
| | `"This phone number is already registered to another account."` (`ConflictException`) | `UTCID05` |
| | `"Date of birth cannot be in the future."` | `UTCID06` |
| | `"Database write error"` (`InvalidOperationException`) | `UTCID17` |

---

### CHỨC NĂNG 16: View Cart (`VC`) — Sheet `GetCartAsync`

#### 1. Header Info
- **Function Code**: `GetCartAsync` *(kèm `GetCartCountAsync`)*
- **Function Name**: `View Cart`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `101` *(riêng `GetCartAsync`: 101 dòng [81 câu lệnh]; nếu gộp `GetCartCountAsync` [13 dòng] là `114` dòng)*
- **Test requirement**: `Verify that GetCartAsync and GetCartCountAsync retrieve the authenticated user's cart items and total quantity, calculate Subtotal, TotalPrice, and ApproxUsd for valid items only, flag unavailable items (inactive packages or a Coach's own package), apply default duration (30 Days) and rating (4.9), map controller status codes (200, 400, 401), and handle exceptions.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **23** | **0** | **0** | **8** | **9** | **6** | **23** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID23`
| UTCID | Tên test method gốc (trong `CartServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `GetCartCountAsync_AuthenticatedUser_ShouldReturnTotalQuantity` | N | Passed | 2026-11-03 |
| `UTCID02` | `GetCartCountAsync_EmptyUserId_ShouldReturnZero` | B | Passed | 2026-11-03 |
| `UTCID03` | `GetCartAsync_WithValidItems_ShouldCalculateSubtotalAndReturnCartSummary` | N | Passed | 2026-11-03 |
| `UTCID04` | `GetCartAsync_WhenUserIsCoach_ShouldFlagIsCoachTrue` | N | Passed | 2026-11-03 |
| `UTCID05` | `GetCartCountAsync_ValidUserId_ShouldReturnCorrectCount` | N | Passed | 2026-11-03 |
| `UTCID06` | `CartsController_GetCart_WhenUserIsAuthenticated_ShouldReturn200Ok` | N | Passed | 2026-11-03 |
| `UTCID07` | `CartsController_GetCartCount_WhenUserIsAuthenticated_ShouldReturn200Ok` | N | Passed | 2026-11-03 |
| `UTCID08` | `GetCartAsync_WhenCartIsEmpty_ShouldReturnZeroTotalSummary` | B | Passed | 2026-11-03 |
| `UTCID09` | `GetCartCountAsync_WhenUserIdIsEmptyGuid_ShouldReturnZeroWithoutCallingRepository` | B | Passed | 2026-11-03 |
| `UTCID10` | `CartsController_GetCart_WhenUserUnauthenticated_ShouldReturn401Unauthorized` | A | Passed | 2026-11-03 |
| `UTCID11` | `CartsController_GetCartCount_WhenUserUnauthenticated_ShouldReturn401Unauthorized` | A | Passed | 2026-11-03 |
| `UTCID12` | `CartsController_GetCart_WhenServiceReturnsError_ShouldReturn400BadRequest` | A | Passed | 2026-11-03 |
| `UTCID13` | `GetCartAsync_WhenCartItemHasCoachOwnPackage_ShouldMarkAsUnavailable` | A | Passed | 2026-11-03 |
| `UTCID14` | `GetCartAsync_WhenCartItemIsInactive_ShouldMarkAsUnavailable` | A | Passed | 2026-11-03 |
| `UTCID15` | `GetCartAsync_WhenDurationDaysIsNull_ShouldDefaultTo30Days` | B | Passed | 2026-11-03 |
| `UTCID16` | `GetCartAsync_WhenCartEntityHasNullPackage_ShouldSkipGracefully` | B | Passed | 2026-11-03 |
| `UTCID17` | `GetCartAsync_WhenCoachReviewsHaveNoValidRatings_ShouldUseDefaultRating` | B | Passed | 2026-11-03 |
| `UTCID18` | `GetCartAsync_WhenUserRepositoryThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID19` | `GetCartAsync_WhenCartRepositoryThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID20` | `GetCartAsync_WhenCancellationRequested_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID21` | `GetCartCountAsync_WhenRepositoryThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID22` | `CartsController_ClassLevel_RequiresTraineeOrCoach` | N | Passed | 2026-11-03 |
| `UTCID23` | `CartsController_GetCartEndpoints_HaveHttpGetAttributes` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Target Method** | `CartService.GetCartAsync(userId)` | `UTCID03, UTCID04, UTCID08, UTCID13, UTCID14, UTCID15, UTCID16, UTCID17, UTCID18, UTCID19, UTCID20` |
| | `CartService.GetCartCountAsync(userId)` | `UTCID01, UTCID02, UTCID05, UTCID09, UTCID21` |
| | `CartsController.GetCart` / `GetCartCount` & Attribute check | `UTCID06, UTCID07, UTCID10, UTCID11, UTCID12, UTCID22, UTCID23` |
| **2. User Authentication & Role (`userId`, `RoleCode`)** | Authenticated `TRAINEE` (`userId != Guid.Empty`) | `UTCID01, UTCID03, UTCID05, UTCID06, UTCID07, UTCID08, UTCID12, UTCID14, UTCID15, UTCID16, UTCID17, UTCID18, UTCID19, UTCID20, UTCID21` |
| | Authenticated `COACH` (`userId != Guid.Empty`, `RoleCode == "COACH"`) | `UTCID04, UTCID13` |
| | Unauthenticated / `Guid.Empty` | `UTCID02, UTCID09, UTCID10, UTCID11` |
| | N/A (Controller attribute reflection) | `UTCID22, UTCID23` |
| **3. Cart Items & Package State** | 2 valid active packages (`pkg1 = 500,000`, `pkg2 = 300,000`) | `UTCID03, UTCID06` |
| | Empty cart (`Items = []`) | `UTCID04, UTCID08` |
| | Coach's own package (`Package.CoachId == userId`) + 1 valid package | `UTCID13` |
| | Inactive package (`Package.IsActive == false`) | `UTCID14` |
| | Package with `DurationDays = null` / `Package = null` / `Rating = null` | `UTCID15, UTCID16, UTCID17` |
| | Count query / Repo exception / Unauthenticated / Reflection | `UTCID01, UTCID02, UTCID05, UTCID07, UTCID09, UTCID10, UTCID11, UTCID12, UTCID18, UTCID19, UTCID20, UTCID21, UTCID22, UTCID23` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<CartSummaryDto>` (`Success = true`), `Subtotal = 800000`, `ApproxUsd = 32.00m`, `ValidItemsCount = 2` | `UTCID03` |
| | `ApiResponseDto<CartSummaryDto>` (`Success = true`) với `IsCoach = true` / giỏ rỗng / lọc gói lỗi / default `30 Days` & `4.9` rating | `UTCID04, UTCID08, UTCID13, UTCID14, UTCID15, UTCID16, UTCID17` |
| | `ApiResponseDto<int>` (`Success = true`, `Data = 5` hoặc `Data = 0`) | `UTCID01, UTCID02, UTCID05, UTCID09` |
| | `OkObjectResult` (200) / `UnauthorizedObjectResult` (401) / `BadRequestObjectResult` (400) | `UTCID06, UTCID07, UTCID10, UTCID11, UTCID12` |
| | `ApiResponseDto` với `Success = false` | `UTCID18, UTCID19, UTCID20, UTCID21` |
| | `CartsController` có `[Authorize(Roles = "TRAINEE,COACH")]` & `[HttpGet]` | `UTCID22, UTCID23` |
| **Exception** | None (Exceptions được catch trong `CartService`) | `UTCID01..UTCID23` |
| **Log / Assert Message** | `"Cart retrieved successfully."` | `UTCID03, UTCID04, UTCID06, UTCID08, UTCID13, UTCID14, UTCID15, UTCID16, UTCID17` |
| | `"Your own training package - Cannot purchase"` (`UnavailableReason`) | `UTCID13` |
| | `"This package is currently suspended from taking new trainees"` (`UnavailableReason`) | `UTCID14` |
| | `"Error loading cart: DB timeout reading user"` / `"Error loading cart: Network connectivity dropped"` | `UTCID18, UTCID19, UTCID20` |
| | `"Error counting cart items: Database error while counting"` | `UTCID21` |

---

### CHỨC NĂNG 17: Remove from Cart (`RFC`) — Sheet `RemoveCartItemAsync`

#### 1. Header Info
- **Function Code**: `RemoveCartItemAsync` *(kèm `ClearCartAsync`)*
- **Function Name**: `Remove from Cart`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `15` *(riêng `RemoveCartItemAsync`: 15 dòng [9 câu lệnh]; nếu gộp `ClearCartAsync` [10 dòng] là `25` dòng)*
- **Test requirement**: `Verify that RemoveCartItemAsync and ClearCartAsync remove a single cart item (only when the cart item exists and belongs to the requesting userId) or clear all cart items for the user, persist changes via IUnitOfWork, map controller responses (200 OK, 400 Bad Request, 401 Unauthorized), and handle repository/unit-of-work exceptions.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **19** | **0** | **0** | **6** | **11** | **2** | **19** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID19`
| UTCID | Tên test method gốc (trong `CartServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `RemoveCartItemAsync_ValidCartItemAndOwner_ShouldRemoveSuccessfully` | N | Passed | 2026-11-03 |
| `UTCID02` | `ClearCartAsync_ValidUser_ShouldClearAllItemsSuccessfully` | N | Passed | 2026-11-03 |
| `UTCID03` | `CartsController_RemoveCartItem_WhenAuthenticatedAndSuccessful_ShouldReturn200Ok` | N | Passed | 2026-11-03 |
| `UTCID04` | `CartsController_ClearCart_WhenAuthenticatedAndSuccessful_ShouldReturn200Ok` | N | Passed | 2026-11-03 |
| `UTCID05` | `RemoveCartItemAsync_WhenCartItemDoesNotExist_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID06` | `RemoveCartItemAsync_WhenCartItemBelongsToDifferentUser_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID07` | `CartsController_RemoveCartItem_WhenUnauthenticated_ShouldReturn401Unauthorized` | A | Passed | 2026-11-03 |
| `UTCID08` | `CartsController_ClearCart_WhenUnauthenticated_ShouldReturn401Unauthorized` | A | Passed | 2026-11-03 |
| `UTCID09` | `CartsController_RemoveCartItem_WhenServiceFails_ShouldReturn400BadRequest` | A | Passed | 2026-11-03 |
| `UTCID10` | `CartsController_ClearCart_WhenServiceFails_ShouldReturn400BadRequest` | A | Passed | 2026-11-03 |
| `UTCID11` | `RemoveCartItemAsync_WithEmptyGuidCartId_WhenNotFound_ShouldReturnFailureResult` | B | Passed | 2026-11-03 |
| `UTCID12` | `RemoveCartItemAsync_WithEmptyGuidUserId_ShouldPreventUnauthorizedRemoval` | B | Passed | 2026-11-03 |
| `UTCID13` | `RemoveCartItemAsync_WhenRepositoryThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID14` | `RemoveCartItemAsync_WhenUnitOfWorkThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID15` | `RemoveCartItemAsync_WhenCancellationRequested_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID16` | `ClearCartAsync_WhenRepositoryThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID17` | `ClearCartAsync_WhenUnitOfWorkThrowsException_ShouldReturnFailureResult` | A | Passed | 2026-11-03 |
| `UTCID18` | `CartsController_ClassLevel_RequiresTraineeOrCoach` | N | Passed | 2026-11-03 |
| `UTCID19` | `CartsController_RemoveCartEndpoints_HaveHttpDeleteAttributes` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Target Method** | `CartService.RemoveCartItemAsync(userId, cartId)` | `UTCID01, UTCID05, UTCID06, UTCID11, UTCID12, UTCID13, UTCID14, UTCID15` |
| | `CartService.ClearCartAsync(userId)` | `UTCID02, UTCID16, UTCID17` |
| | `CartsController.RemoveCartItem` / `ClearCart` & Attribute check | `UTCID03, UTCID04, UTCID07, UTCID08, UTCID09, UTCID10, UTCID18, UTCID19` |
| **2. Cart Item Existence & Ownership (`_mockCartRepo.GetByIdAsync`)** | Exists & `cart.UserId == userId` (Owner matches) | `UTCID01, UTCID03, UTCID14` |
| | Not found (`null`, including `cartId == Guid.Empty`) | `UTCID05, UTCID09, UTCID11` |
| | Exists & `cart.UserId != userId` (Belongs to another user or `userId == Guid.Empty`) | `UTCID06, UTCID12` |
| | N/A (`ClearCart` / Unauthenticated / Repo throws / Reflection) | `UTCID02, UTCID04, UTCID07, UTCID08, UTCID10, UTCID13, UTCID15, UTCID16, UTCID17, UTCID18, UTCID19` |
| **3. Repository & UnitOfWork Behavior** | Succeeds (`Remove` / `ClearCartAsync` + `SaveChangesAsync`) | `UTCID01, UTCID02, UTCID03, UTCID04` |
| | Repository throws `InvalidOperationException` / `Exception` / `OperationCanceledException` | `UTCID13, UTCID15, UTCID16` |
| | `SaveChangesAsync` throws `Exception` | `UTCID14, UTCID17` |
| | Not reached (NotFound / Unauthorized / Controller / Reflection) | `UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID18, UTCID19` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<bool>` với `Success = true`, `Data = true` | `UTCID01, UTCID02` |
| | `OkObjectResult` (200) / `UnauthorizedObjectResult` (401) / `BadRequestObjectResult` (400) | `UTCID03, UTCID04, UTCID07, UTCID08, UTCID09, UTCID10` |
| | `ApiResponseDto<bool>` với `Success = false`, `Data = false` | `UTCID05, UTCID06, UTCID11, UTCID12, UTCID13, UTCID14, UTCID15, UTCID16, UTCID17` |
| | `CartsController` có `[Authorize(Roles = "TRAINEE,COACH")]` & `[HttpDelete("{cartId:guid}")]`, `[HttpDelete("clear")]` | `UTCID18, UTCID19` |
| **Exception** | None (Exceptions được catch trong `CartService`) | `UTCID01..UTCID19` |
| **Log / Assert Message** | `"Removed package from cart successfully."` & Verify `Remove(cart)` + `SaveChangesAsync` `Times.Once` | `UTCID01, UTCID03` |
| | `"Cart cleared successfully."` & Verify `ClearCartAsync` + `SaveChangesAsync` `Times.Once` | `UTCID02, UTCID04` |
| | `"Cart item does not exist or does not belong to you."` | `UTCID05, UTCID06, UTCID09, UTCID11, UTCID12` |
| | `"Error removing package from cart: DB connection timeout"` / `"DB constraint error during commit"` | `UTCID13, UTCID14, UTCID15` |
| | `"Error clearing cart: DB connection dropped"` / `"Deadlock detected during commit"` | `UTCID10, UTCID16, UTCID17` |

---

### CHỨC NĂNG 18: Submit Coach Complaint (`SCC`) — Sheet `ReportUserAsync`

#### 1. Header Info
- **Function Code**: `ReportUserAsync`
- **Function Name**: `Submit Coach Complaint`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `54` *(38 câu lệnh không tính `{}`)*
- **Test requirement**: `Verify that ReportUserAsync validates reporter authentication (reporterId != Guid.Empty), non-null request, valid ReportedUserId (not empty and not self-reporting), non-empty Reason, and target Coach/User existence, trims Reason/Description, filters out blank MediaUrls, defaults Type to "USER" when omitted, persists the Report with Status = "PENDING", and propagates ValidationException, BusinessException, NotFoundException, or database exceptions.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **21** | **0** | **0** | **5** | **13** | **3** | **21** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID21`
| UTCID | Tên test method gốc (trong `ReportServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `ReportUserAsync_ValidCoachComplaintWithMedia_ShouldCreateReportSuccessfully` | N | Passed | 2026-11-03 |
| `UTCID02` | `ReportUserAsync_WithoutExplicitType_ShouldDefaultTypeToUSER` | N | Passed | 2026-11-03 |
| `UTCID03` | `ReportsController_ReportUser_WhenAuthenticatedAndSuccessful_ShouldReturn200Ok` | N | Passed | 2026-11-03 |
| `UTCID04` | `ReportUserAsync_WhenReporterIdIsEmptyGuid_ShouldThrowValidationException` | A | Passed | 2026-11-03 |
| `UTCID05` | `ReportUserAsync_WhenRequestIsNull_ShouldThrowValidationException` | A | Passed | 2026-11-03 |
| `UTCID06` | `ReportUserAsync_WhenReportedUserIdIsEmptyGuid_ShouldThrowValidationException` | A | Passed | 2026-11-03 |
| `UTCID07` | `ReportUserAsync_WhenUserReportsThemselves_ShouldThrowBusinessException` | A | Passed | 2026-11-03 |
| `UTCID08` | `ReportUserAsync_WhenReasonIsNullOrWhitespace_ShouldThrowValidationException(invalidReason: null)` | A | Passed | 2026-11-03 |
| `UTCID09` | `ReportUserAsync_WhenReasonIsNullOrWhitespace_ShouldThrowValidationException(invalidReason: "")` | A | Passed | 2026-11-03 |
| `UTCID10` | `ReportUserAsync_WhenReasonIsNullOrWhitespace_ShouldThrowValidationException(invalidReason: "   ")` | A | Passed | 2026-11-03 |
| `UTCID11` | `ReportUserAsync_WhenReportedUserDoesNotExist_ShouldThrowNotFoundException` | A | Passed | 2026-11-03 |
| `UTCID12` | `ReportsController_ReportUser_WhenUnauthenticated_ShouldReturn401Unauthorized` | A | Passed | 2026-11-03 |
| `UTCID13` | `ReportUserAsync_WithMediaUrlsContainingNullOrWhitespace_ShouldFilterThemOut` | B | Passed | 2026-11-03 |
| `UTCID14` | `ReportUserAsync_WithLeadingTrailingWhitespaceInReasonAndDescription_ShouldTrimProperly` | B | Passed | 2026-11-03 |
| `UTCID15` | `ReportUserAsync_WithoutMediaUrls_ShouldCreateReportWithEmptyMediaList` | B | Passed | 2026-11-03 |
| `UTCID16` | `ReportUserAsync_WhenUserRepositoryThrowsException_ShouldPropagateException` | A | Passed | 2026-11-03 |
| `UTCID17` | `ReportUserAsync_WhenRepositoryAddThrowsException_ShouldPropagateException` | A | Passed | 2026-11-03 |
| `UTCID18` | `ReportUserAsync_WhenUnitOfWorkThrowsException_ShouldPropagateException` | A | Passed | 2026-11-03 |
| `UTCID19` | `ReportUserAsync_WhenCancellationRequested_ShouldPropagateOperationCanceledException` | A | Passed | 2026-11-03 |
| `UTCID20` | `ReportsController_ClassLevel_RequiresAuthorize` | N | Passed | 2026-11-03 |
| `UTCID21` | `ReportsController_ReportUserEndpoint_HasHttpPostAttribute` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. Reporter Authentication (`reporterId`)** | Valid `Guid` (`reporterId != Guid.Empty`) | `UTCID01, UTCID02, UTCID03, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID13, UTCID14, UTCID15, UTCID16, UTCID17, UTCID18, UTCID19` |
| | `Guid.Empty` / Unauthenticated claim | `UTCID04, UTCID12` |
| | N/A (Controller attribute reflection) | `UTCID20, UTCID21` |
| **2. Request DTO & `ReportedUserId`** | Non-null DTO & `ReportedUserId` valid (`!= Guid.Empty` & `!= reporterId`) | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID13, UTCID14, UTCID15, UTCID16, UTCID17, UTCID18, UTCID19` |
| | `request == null` | `UTCID05` |
| | `ReportedUserId == Guid.Empty` | `UTCID06` |
| | `ReportedUserId == reporterId` (Self-report) | `UTCID07` |
| | N/A (Controller attribute reflection) | `UTCID20, UTCID21` |
| **3. `request.Reason` & `MediaUrls`** | Valid reason with 2 valid `MediaUrls` (`Type = "COACH"`) | `UTCID01` |
| | Valid reason without `Type` / without `MediaUrls` | `UTCID02, UTCID03, UTCID04, UTCID06, UTCID07, UTCID11, UTCID12, UTCID15, UTCID16, UTCID17, UTCID18, UTCID19` |
| | `Reason` is `null`, `""`, or `"   "` | `UTCID08, UTCID09, UTCID10` |
| | `MediaUrls` contains mixed valid, `null`, `""`, and `"   "` URLs | `UTCID13` |
| | `Reason` and `Description` have leading/trailing whitespace | `UTCID14` |
| | N/A (`request == null` / Reflection) | `UTCID05, UTCID20, UTCID21` |
| **4. Target User Existence & Repository Behavior** | Target Coach/User exists & `AddAsync` + `SaveChangesAsync` succeed | `UTCID01, UTCID02, UTCID03, UTCID13, UTCID14, UTCID15` |
| | Target user not found (`_mockUserRepo.GetByIdAsync == null`) | `UTCID11` |
| | `_mockUserRepo.GetByIdAsync` throws `InvalidOperationException` / `OperationCanceledException` | `UTCID16, UTCID19` |
| | `_mockReportRepo.AddAsync` throws `Exception("Database insert error")` | `UTCID17` |
| | `_mockUow.SaveChangesAsync` throws `Exception("Transaction deadlock")` | `UTCID18` |
| | Short-circuited before repo or Reflection | `UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID12, UTCID20, UTCID21` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<ReportListItemDto>` (`Success = true`), `Status = "PENDING"`, tạo đúng `Report` & `ReportMedia` (`MediaFor = "REPORT"`) | `UTCID01, UTCID02, UTCID13, UTCID14, UTCID15` |
| | `OkObjectResult` (200) / `UnauthorizedObjectResult` (401) | `UTCID03, UTCID12` |
| | `ReportsController` có `[Authorize]` và `[HttpPost("user")]` | `UTCID20, UTCID21` |
| **Exception** | `ValidationException` | `UTCID04, UTCID05, UTCID06, UTCID08, UTCID09, UTCID10` |
| | `BusinessException` | `UTCID07` |
| | `NotFoundException` | `UTCID11` |
| | `InvalidOperationException` / `Exception` / `OperationCanceledException` | `UTCID16, UTCID17, UTCID18, UTCID19` |
| **Log / Assert Message** | `"User reported successfully."` & Verify `AddAsync` + `SaveChangesAsync` `Times.Once` | `UTCID01, UTCID02, UTCID03, UTCID13, UTCID14, UTCID15` |
| | `"User is not authenticated."` (`ValidationException`) | `UTCID04` |
| | `"Reported user ID is required."` (`ValidationException`) | `UTCID05, UTCID06` |
| | `"You cannot report yourself."` (`BusinessException`) | `UTCID07` |
| | `"Reason is required."` (`ValidationException`) | `UTCID08, UTCID09, UTCID10` |
| | `"Reported user not found."` (`NotFoundException`) | `UTCID11` |

---

### CHỨC NĂNG 19: Add To Cart (`ATC`) — Sheet `AddToCartAsync`

#### 1. Header Info
- **Function Code**: `AddToCartAsync`
- **Function Name**: `Add To Cart`
- **Created By**: `HATTV`
- **Executed By**: `HATTV`
- **Lines of code (LOC)**: `76` *(54 câu lệnh không tính `{}`)*
- **Test requirement**: `Verify that AddToCartAsync validates user authentication (userId != Guid.Empty), non-null AddToCartDto, non-empty PackageId, and Quantity > 0, checks that the target TrainingPackage exists, is active (IsActive != false, treating null as active), does not belong to the Coach themselves, and is not already in the user's cart, enforces Quantity = 1 when saving, and handles database save exceptions.`
- **Thống kê số lượng**:
  | Passed | Failed | Untested | N (Normal) | A (Abnormal) | B (Boundary) | Total Test Cases |
  | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
  | **15** | **0** | **0** | **3** | **8** | **4** | **15** |

#### 2. Danh sách ánh xạ `UTCID01` .. `UTCID15`
| UTCID | Tên test method gốc (trong `CartServiceTests.cs` & `ManagementControllersAuthorizationTests.cs`) | Loại (N/A/B) | Kết quả | Ngày chạy |
| :---: | :--- | :---: | :---: | :---: |
| `UTCID01` | `AddToCartAsync_FirstTimeAddingProduct_ShouldCreateCartItemAndReturnSuccess` | N | Passed | 2026-11-03 |
| `UTCID02` | `AddToCartAsync_SecondTimeAddingSameProduct_ShouldReturnFail` | A | Passed | 2026-11-03 |
| `UTCID03` | `AddToCartAsync_DifferentPackage_ShouldSuccessfullyAddWithQuantityOne` | N | Passed | 2026-11-03 |
| `UTCID04` | `AddToCartAsync_UnauthenticatedUser_ShouldReturnFail` | A | Passed | 2026-11-03 |
| `UTCID05` | `AddToCartAsync_EmptyPackageId_ShouldReturnFail` | A | Passed | 2026-11-03 |
| `UTCID06` | `AddToCartAsync_InvalidQuantity_ShouldReturnFail(quantity: 0)` | B | Passed | 2026-11-03 |
| `UTCID07` | `AddToCartAsync_InvalidQuantity_ShouldReturnFail(quantity: -1)` | B | Passed | 2026-11-03 |
| `UTCID08` | `AddToCartAsync_InvalidQuantity_ShouldReturnFail(quantity: -10)` | B | Passed | 2026-11-03 |
| `UTCID09` | `AddToCartAsync_PackageNotFound_ShouldReturnFail` | A | Passed | 2026-11-03 |
| `UTCID10` | `AddToCartAsync_ProductInactive_ShouldReturnFail` | A | Passed | 2026-11-03 |
| `UTCID11` | `AddToCartAsync_CoachAddsOwnPackage_ShouldReturnFail` | A | Passed | 2026-11-03 |
| `UTCID12` | `AddToCartAsync_NullDto_ShouldReturnFail` | A | Passed | 2026-11-03 |
| `UTCID13` | `AddToCartAsync_PackageIsActiveNull_ShouldTreatAsActiveAndSucceed` | B | Passed | 2026-11-03 |
| `UTCID14` | `AddToCartAsync_WhenDatabaseSaveThrowsException_ShouldReturnFail` | A | Passed | 2026-11-03 |
| `UTCID15` | `CartsController_ClassLevel_RequiresTraineeOrCoach` | N | Passed | 2026-11-03 |

#### 3. MA TRẬN ĐIỀU KIỆN (Condition Matrix)
| Nhóm điều kiện (Condition Group) | Giá trị / Trạng thái cụ thể (Condition Value) | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **1. `userId` & Request DTO (`AddToCartDto`)** | Valid `userId != Guid.Empty` & non-null DTO | `UTCID01, UTCID02, UTCID03, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID13, UTCID14` |
| | `userId == Guid.Empty` (Unauthenticated) | `UTCID04` |
| | `dto == null` | `UTCID12` |
| | N/A (Controller attribute reflection) | `UTCID15` |
| **2. `dto.PackageId` & `dto.Quantity`** | Valid `PackageId` & `Quantity = 1` (or `Quantity = 3` normalized to `1`) | `UTCID01, UTCID02, UTCID03, UTCID04, UTCID09, UTCID10, UTCID11, UTCID13, UTCID14` |
| | `PackageId == Guid.Empty` | `UTCID05` |
| | Boundary / Invalid `Quantity = 0` | `UTCID06` |
| | Boundary / Invalid `Quantity = -1` | `UTCID07` |
| | Invalid `Quantity = -10` | `UTCID08` |
| | N/A (`dto == null` / Reflection) | `UTCID12, UTCID15` |
| **3. Package Existence & State (`_mockPackageRepo.GetByIdAsync`)** | Exists, `IsActive = true` (or `null` in `UTCID13`), `CoachId != userId` | `UTCID01, UTCID02, UTCID03, UTCID13, UTCID14` |
| | Not found (`null`) | `UTCID09` |
| | Exists, `IsActive = false` (Suspended) | `UTCID10` |
| | Exists, `CoachId == userId` (Coach adding own package) | `UTCID11` |
| | Not reached (Validation failed or Reflection) | `UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID12, UTCID15` |
| **4. Cart Duplicate Check (`GetCartItemAsync`) & Persistence** | `GetCartItemAsync == null` (Not yet in cart) & `SaveChangesAsync` succeeds | `UTCID01, UTCID03, UTCID13` |
| | `GetCartItemAsync != null` (Already in cart) | `UTCID02` |
| | `_mockUow.SaveChangesAsync` throws `InvalidOperationException("DB deadlock detected")` | `UTCID14` |
| | Not reached | `UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID15` |

#### 4. MA TRẬN XÁC NHẬN (Confirm Matrix)
| Nhóm xác nhận | Kết quả / Giá trị / Exception / Message cụ thể | Danh sách UTCID đánh dấu `O` |
| :--- | :--- | :--- |
| **Return** | `ApiResponseDto<CartItemDto>` với `Success = true`, `Quantity = 1`, `PackageId` và `Price` chính xác | `UTCID01, UTCID03, UTCID13` |
| | `ApiResponseDto<CartItemDto>` với `Success = false`, `Data = null` | `UTCID02, UTCID04, UTCID05, UTCID06, UTCID07, UTCID08, UTCID09, UTCID10, UTCID11, UTCID12, UTCID14` |
| | `CartsController` có `[Authorize(Roles = "TRAINEE,COACH")]` | `UTCID15` |
| **Exception** | None (Exceptions được catch trong `AddToCartAsync` và trả về `ApiResponseDto.Fail`) | `UTCID01..UTCID15` |
| **Log / Assert Message** | `"Added training package to cart successfully."` & Verify `AddAsync` (`Quantity == 1`) + `SaveChangesAsync` `Times.Once` | `UTCID01, UTCID03, UTCID13` |
| | `"You must be logged in to add items to cart."` | `UTCID04` |
| | `"Invalid training package information."` | `UTCID05, UTCID12` |
| | `"Quantity must be greater than 0."` | `UTCID06, UTCID07, UTCID08` |
| | `"Training package does not exist."` | `UTCID09` |
| | `"This training package is currently suspended from taking trainees."` | `UTCID10` |
| | `"You cannot add your own training package to cart."` | `UTCID11` |
| | `"This package is already in your cart. You cannot purchase multiple quantities of the same training package."` | `UTCID02` |
| | `"Error adding to cart: DB deadlock detected"` | `UTCID14` |