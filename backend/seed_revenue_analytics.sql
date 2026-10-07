-- ==============================================================================
-- FITSOCIAL - SCRIPT INSERT DỮ LIỆU MẪU CHO REVENUE ANALYTICS (UC_27)
-- Chạy trực tiếp trên PostgreSQL / Neon Console SQL Editor
-- Đảm bảo tương thích 100% với schema SEP490_FitConnect.sql
-- ==============================================================================

-- 1. BẬT EXTENSION UUID (Nếu chưa có)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ==============================================================================
-- 2. TẠO CÁC TÀI KHOẢN USERS (Admin, Coaches, Trainees)
-- ==============================================================================

-- Admin User
INSERT INTO Users (UserID, Email, PhoneNumber, PasswordHash, FullName, RoleCode, IsInternal, IsLocked, CreatedAt, UpdatedAt)
VALUES (
    'a0000000-0000-0000-0000-000000000001',
    'admin@fitsocial.com',
    '0901000001',
    '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    'Phan Phi Pham (Admin)',
    'ADMIN',
    TRUE,
    FALSE,
    '2026-09-01 08:00:00',
    '2026-09-01 08:00:00'
) ON CONFLICT (UserID) DO NOTHING;

-- Coach 1: HLV Nguyễn Văn Hùng
INSERT INTO Users (UserID, Email, PhoneNumber, PasswordHash, FullName, RoleCode, IsInternal, IsLocked, AvatarUrl, CreatedAt, UpdatedAt)
VALUES (
    'c1111111-1111-1111-1111-111111111111',
    'hung.nguyen@fitsocial.com',
    '0902000001',
    '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    'Nguyễn Văn Hùng (Coach)',
    'COACH',
    FALSE,
    FALSE,
    'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150',
    '2026-09-01 09:00:00',
    '2026-09-01 09:00:00'
) ON CONFLICT (UserID) DO NOTHING;

-- Coach 2: HLV Trần Thị Mai (Yoga & Pilates)
INSERT INTO Users (UserID, Email, PhoneNumber, PasswordHash, FullName, RoleCode, IsInternal, IsLocked, AvatarUrl, CreatedAt, UpdatedAt)
VALUES (
    'c2222222-2222-2222-2222-222222222222',
    'mai.tran@fitsocial.com',
    '0902000002',
    '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    'Trần Thị Mai (Coach)',
    'COACH',
    FALSE,
    FALSE,
    'https://images.unsplash.com/photo-1544005313-94ddf0286df2?w=150',
    '2026-09-02 09:00:00',
    '2026-09-02 09:00:00'
) ON CONFLICT (UserID) DO NOTHING;

-- Coach 3: HLV Lê Hoàng Nam (Boxing / Kickfit)
INSERT INTO Users (UserID, Email, PhoneNumber, PasswordHash, FullName, RoleCode, IsInternal, IsLocked, AvatarUrl, CreatedAt, UpdatedAt)
VALUES (
    'c3333333-3333-3333-3333-333333333333',
    'nam.le@fitsocial.com',
    '0902000003',
    '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    'Lê Hoàng Nam (Coach)',
    'COACH',
    FALSE,
    FALSE,
    'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150',
    '2026-09-03 09:00:00',
    '2026-09-03 09:00:00'
) ON CONFLICT (UserID) DO NOTHING;

-- Trainee 1: Lê Quốc Bảo
INSERT INTO Users (UserID, Email, PhoneNumber, PasswordHash, FullName, RoleCode, IsInternal, IsLocked, CreatedAt, UpdatedAt)
VALUES (
    't1111111-1111-1111-1111-111111111111',
    'bao.le@gmail.com',
    '0903000001',
    '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    'Lê Quốc Bảo',
    'TRAINEE',
    FALSE,
    FALSE,
    '2026-09-05 10:00:00',
    '2026-09-05 10:00:00'
) ON CONFLICT (UserID) DO NOTHING;

-- Trainee 2: Phạm Thảo Vy
INSERT INTO Users (UserID, Email, PhoneNumber, PasswordHash, FullName, RoleCode, IsInternal, IsLocked, CreatedAt, UpdatedAt)
VALUES (
    't2222222-2222-2222-2222-222222222222',
    'vy.pham@gmail.com',
    '0903000002',
    '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    'Phạm Thảo Vy',
    'TRAINEE',
    FALSE,
    FALSE,
    '2026-09-06 10:00:00',
    '2026-09-06 10:00:00'
) ON CONFLICT (UserID) DO NOTHING;

-- Trainee 3: Hoàng Đức Minh
INSERT INTO Users (UserID, Email, PhoneNumber, PasswordHash, FullName, RoleCode, IsInternal, IsLocked, CreatedAt, UpdatedAt)
VALUES (
    't3333333-3333-3333-3333-333333333333',
    'minh.hoang@gmail.com',
    '0903000003',
    '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    'Hoàng Đức Minh',
    'TRAINEE',
    FALSE,
    FALSE,
    '2026-09-07 10:00:00',
    '2026-09-07 10:00:00'
) ON CONFLICT (UserID) DO NOTHING;

-- ==============================================================================
-- 3. TẠO HỒ SƠ COACHPROFILES VÀ TÀI KHOẢN NGÂN HÀNG
-- ==============================================================================

INSERT INTO CoachProfiles (CoachID, ExperienceYears, Bio, ApprovalStatus, Status)
VALUES 
('c1111111-1111-1111-1111-111111111111', 5, 'HLV Thể hình chuyên nghiệp, chứng chỉ NASM CPT.', 'APPROVED', 'ACTIVE'),
('c2222222-2222-2222-2222-222222222222', 4, 'Chuyên gia Yoga & Phục hồi cơ xương khớp.', 'APPROVED', 'ACTIVE'),
('c3333333-3333-3333-3333-333333333333', 6, 'HLV Boxing và Kickfit tăng cơ giảm mỡ cấp tốc.', 'APPROVED', 'ACTIVE')
ON CONFLICT (CoachID) DO NOTHING;

INSERT INTO CoachBankAccounts (BankID, CoachID, BankName, BankCode, AccountName, IsDefault)
VALUES 
('b1111111-1111-1111-1111-111111111111', 'c1111111-1111-1111-1111-111111111111', 'Vietcombank', 'VCB', 'NGUYEN VAN HUNG', TRUE),
('b2222222-2222-2222-2222-222222222222', 'c2222222-2222-2222-2222-222222222222', 'Techcombank', 'TCB', 'TRAN THI MAI', TRUE),
('b3333333-3333-3333-3333-333333333333', 'c3333333-3333-3333-3333-333333333333', 'MB Bank', 'MBB', 'LE HOANG NAM', TRUE)
ON CONFLICT (BankID) DO NOTHING;

-- ==============================================================================
-- 4. TẠO CÁC GÓI THUÊ BAO COACH (COACH SUBSCRIPTION PLANS - DOANH THU SÀN)
-- ==============================================================================

INSERT INTO CoachSubscriptionPlans (CoachSubscriptionPlansID, Amount, Currency, Description, SubscriptionDuration, TrainingPackageDuration, IsActive, CreatedAt)
VALUES
('p1111111-0000-0000-0000-000000000001', 500000, 'VND', 'Coach Standard Tier (1 Tháng)', 30, 30, TRUE, '2026-08-01 00:00:00'),
('p2222222-0000-0000-0000-000000000002', 1200000, 'VND', 'Coach Professional Tier (3 Tháng)', 90, 90, TRUE, '2026-08-01 00:00:00'),
('p3333333-0000-0000-0000-000000000003', 3900000, 'VND', 'Coach Diamond Master (12 Tháng)', 365, 365, TRUE, '2026-08-01 00:00:00')
ON CONFLICT (CoachSubscriptionPlansID) DO NOTHING;

-- ==============================================================================
-- 5. TẠO CÁC GÓI TẬP CỦA COACH (TRAINING PACKAGES - GMV SÀN KÝ QUỸ)
-- ==============================================================================

INSERT INTO TrainingPackages (PackageID, CoachID, Title, Description, Price, DurationDays, SessionCount, MinAge, TargetAudience, IsActive, CreatedAt)
VALUES
(
    'k1111111-0000-0000-0000-000000000001',
    'c1111111-1111-1111-1111-111111111111',
    'Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1',
    'Lộ trình cá nhân hóa tập luyện và dinh dưỡng chuẩn thể hình',
    2500000,
    30,
    12,
    16,
    'Nam/Nữ muốn tăng cơ nét người',
    TRUE,
    '2026-09-05 08:00:00'
),
(
    'k2222222-0000-0000-0000-000000000002',
    'c2222222-2222-2222-2222-222222222222',
    'Gói Yoga & Thon Gọn Cơ Thể Dẻo Dai',
    'Yoga phục hồi trị liệu cổ vai gáy và săn chắc eo bụng',
    1800000,
    30,
    10,
    15,
    'Nhân viên văn phòng, phụ nữ sau sinh',
    TRUE,
    '2026-09-05 08:00:00'
),
(
    'k3333333-0000-0000-0000-000000000003',
    'c3333333-3333-3333-3333-333333333333',
    'Gói Boxing & Kickfit Đốt Mỡ Cấp Tốc',
    'Đốt cháy 800-1000 calories mỗi buổi tập, giải tỏa stress',
    3200000,
    45,
    15,
    18,
    'Người bận rộn muốn giảm cân nhanh',
    TRUE,
    '2026-09-05 08:00:00'
),
(
    'k4444444-0000-0000-0000-000000000004',
    'c1111111-1111-1111-1111-111111111111',
    'Gói Huấn Luyện Thể Lực VIP 90 Ngày',
    'Chương trình toàn diện nâng tầm sức mạnh và vóc dáng',
    6500000,
    90,
    36,
    18,
    'Học viên theo đuổi phong cách sống thể thao',
    TRUE,
    '2026-09-05 08:00:00'
)
ON CONFLICT (PackageID) DO NOTHING;

-- ==============================================================================
-- 6. TẠO CÁC ĐƠN HÀNG (ORDERS) & CHI TIẾT (ORDERDETAILS)
-- Dữ liệu được dàn trải trong Tháng 10/2026 (Kỳ này) và Tháng 9/2026 (Kỳ trước)
-- ==============================================================================

-- [Đơn 1: Coach Hùng mua gói thuê bao 12 tháng - Doanh thu sàn]
INSERT INTO Orders (OrderID, BuyerID, CoachID, TotalAmount, OrderStatus, OrderType, CreatedAt)
VALUES ('o1111111-0001-0000-0000-000000000001', 'c1111111-1111-1111-1111-111111111111', 'c1111111-1111-1111-1111-111111111111', 3900000, 'PAID', 'COACH_ACTIVATION', '2026-10-01 09:15:00')
ON CONFLICT (OrderID) DO NOTHING;

INSERT INTO OrderDetails (OrderDetailsID, OrderID, CoachSubscriptionPlansID, PackagePrice, PackageTitle, PackageDurationDays, CoachName, CreatedAt)
VALUES ('d1111111-0001-0000-0000-000000000001', 'o1111111-0001-0000-0000-000000000001', 'p3333333-0000-0000-0000-000000000003', 3900000, 'Coach Diamond Master (12 Tháng)', 365, 'Nguyễn Văn Hùng (Coach)', '2026-10-01 09:15:00')
ON CONFLICT (OrderDetailsID) DO NOTHING;

INSERT INTO Payments (PaymentID, OrderID, Amount, Currency, Method, Status, ProcessedAt, CreatedAt)
VALUES ('m1111111-0001-0000-0000-000000000001', 'o1111111-0001-0000-0000-000000000001', 3900000, 'VND', 'PAYOS', 'SUCCESS', '2026-10-01 09:16:00', '2026-10-01 09:15:00')
ON CONFLICT (PaymentID) DO NOTHING;

-- [Đơn 2: Coach Mai mua gói thuê bao 3 tháng - Doanh thu sàn]
INSERT INTO Orders (OrderID, BuyerID, CoachID, TotalAmount, OrderStatus, OrderType, CreatedAt)
VALUES ('o1111111-0002-0000-0000-000000000002', 'c2222222-2222-2222-2222-222222222222', 'c2222222-2222-2222-2222-222222222222', 1200000, 'PAID', 'COACH_ACTIVATION', '2026-10-02 11:30:00')
ON CONFLICT (OrderID) DO NOTHING;

INSERT INTO OrderDetails (OrderDetailsID, OrderID, CoachSubscriptionPlansID, PackagePrice, PackageTitle, PackageDurationDays, CoachName, CreatedAt)
VALUES ('d1111111-0002-0000-0000-000000000002', 'o1111111-0002-0000-0000-000000000002', 'p2222222-0000-0000-0000-000000000002', 1200000, 'Coach Professional Tier (3 Tháng)', 90, 'Trần Thị Mai (Coach)', '2026-10-02 11:30:00')
ON CONFLICT (OrderDetailsID) DO NOTHING;

INSERT INTO Payments (PaymentID, OrderID, Amount, Currency, Method, Status, ProcessedAt, CreatedAt)
VALUES ('m1111111-0002-0000-0000-000000000002', 'o1111111-0002-0000-0000-000000000002', 1200000, 'VND', 'PAYOS', 'SUCCESS', '2026-10-02 11:31:00', '2026-10-02 11:30:00')
ON CONFLICT (PaymentID) DO NOTHING;

-- [Đơn 3: Trainee Bảo mua Gói Gym HLV Hùng - 2.500.000 VND]
INSERT INTO Orders (OrderID, BuyerID, CoachID, TotalAmount, OrderStatus, OrderType, CreatedAt)
VALUES ('o1111111-0003-0000-0000-000000000003', 't1111111-1111-1111-1111-111111111111', 'c1111111-1111-1111-1111-111111111111', 2500000, 'COMPLETED', 'PACKAGE', '2026-10-02 14:20:00')
ON CONFLICT (OrderID) DO NOTHING;

INSERT INTO OrderDetails (OrderDetailsID, OrderID, PackageID, PackagePrice, PackageTitle, PackageDurationDays, CoachName, CreatedAt)
VALUES ('d1111111-0003-0000-0000-000000000003', 'o1111111-0003-0000-0000-000000000003', 'k1111111-0000-0000-0000-000000000001', 2500000, 'Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1', 30, 'Nguyễn Văn Hùng (Coach)', '2026-10-02 14:20:00')
ON CONFLICT (OrderDetailsID) DO NOTHING;

INSERT INTO Payments (PaymentID, OrderID, Amount, Currency, Method, Status, ProcessedAt, CreatedAt)
VALUES ('m1111111-0003-0000-0000-000000000003', 'o1111111-0003-0000-0000-000000000003', 2500000, 'VND', 'PAYOS', 'SUCCESS', '2026-10-02 14:21:00', '2026-10-02 14:20:00')
ON CONFLICT (PaymentID) DO NOTHING;

-- [Đơn 4: Trainee Vy mua Gói Yoga HLV Mai - 1.800.000 VND]
INSERT INTO Orders (OrderID, BuyerID, CoachID, TotalAmount, OrderStatus, OrderType, CreatedAt)
VALUES ('o1111111-0004-0000-0000-000000000004', 't2222222-2222-2222-2222-222222222222', 'c2222222-2222-2222-2222-222222222222', 1800000, 'COMPLETED', 'PACKAGE', '2026-10-03 16:45:00')
ON CONFLICT (OrderID) DO NOTHING;

INSERT INTO OrderDetails (OrderDetailsID, OrderID, PackageID, PackagePrice, PackageTitle, PackageDurationDays, CoachName, CreatedAt)
VALUES ('d1111111-0004-0000-0000-000000000004', 'o1111111-0004-0000-0000-000000000004', 'k2222222-0000-0000-0000-000000000002', 1800000, 'Gói Yoga & Thon Gọn Cơ Thể Dẻo Dai', 30, 'Trần Thị Mai (Coach)', '2026-10-03 16:45:00')
ON CONFLICT (OrderDetailsID) DO NOTHING;

INSERT INTO Payments (PaymentID, OrderID, Amount, Currency, Method, Status, ProcessedAt, CreatedAt)
VALUES ('m1111111-0004-0000-0000-000000000004', 'o1111111-0004-0000-0000-000000000004', 1800000, 'VND', 'PAYOS', 'SUCCESS', '2026-10-03 16:46:00', '2026-10-03 16:45:00')
ON CONFLICT (PaymentID) DO NOTHING;

-- [Đơn 5: Trainee Minh mua Gói Boxing HLV Nam - 3.200.000 VND]
INSERT INTO Orders (OrderID, BuyerID, CoachID, TotalAmount, OrderStatus, OrderType, CreatedAt)
VALUES ('o1111111-0005-0000-0000-000000000005', 't3333333-3333-3333-3333-333333333333', 'c3333333-3333-3333-3333-333333333333', 3200000, 'COMPLETED', 'PACKAGE', '2026-10-04 10:00:00')
ON CONFLICT (OrderID) DO NOTHING;

INSERT INTO OrderDetails (OrderDetailsID, OrderID, PackageID, PackagePrice, PackageTitle, PackageDurationDays, CoachName, CreatedAt)
VALUES ('d1111111-0005-0000-0000-000000000005', 'o1111111-0005-0000-0000-000000000005', 'k3333333-0000-0000-0000-000000000003', 3200000, 'Gói Boxing & Kickfit Đốt Mỡ Cấp Tốc', 45, 'Lê Hoàng Nam (Coach)', '2026-10-04 10:00:00')
ON CONFLICT (OrderDetailsID) DO NOTHING;

INSERT INTO Payments (PaymentID, OrderID, Amount, Currency, Method, Status, ProcessedAt, CreatedAt)
VALUES ('m1111111-0005-0000-0000-000000000005', 'o1111111-0005-0000-0000-000000000005', 3200000, 'VND', 'PAYOS', 'SUCCESS', '2026-10-04 10:01:00', '2026-10-04 10:00:00')
ON CONFLICT (PaymentID) DO NOTHING;

-- [Đơn 6: Trainee Bảo mua thêm Gói VIP 90 Ngày HLV Hùng - 6.500.000 VND]
INSERT INTO Orders (OrderID, BuyerID, CoachID, TotalAmount, OrderStatus, OrderType, CreatedAt)
VALUES ('o1111111-0006-0000-0000-000000000006', 't1111111-1111-1111-1111-111111111111', 'c1111111-1111-1111-1111-111111111111', 6500000, 'COMPLETED', 'PACKAGE', '2026-10-05 18:30:00')
ON CONFLICT (OrderID) DO NOTHING;

INSERT INTO OrderDetails (OrderDetailsID, OrderID, PackageID, PackagePrice, PackageTitle, PackageDurationDays, CoachName, CreatedAt)
VALUES ('d1111111-0006-0000-0000-000000000006', 'o1111111-0006-0000-0000-000000000006', 'k4444444-0000-0000-0000-000000000004', 6500000, 'Gói Huấn Luyện Thể Lực VIP 90 Ngày', 90, 'Nguyễn Văn Hùng (Coach)', '2026-10-05 18:30:00')
ON CONFLICT (OrderDetailsID) DO NOTHING;

INSERT INTO Payments (PaymentID, OrderID, Amount, Currency, Method, Status, ProcessedAt, CreatedAt)
VALUES ('m1111111-0006-0000-0000-000000000006', 'o1111111-0006-0000-0000-000000000006', 6500000, 'VND', 'PAYOS', 'SUCCESS', '2026-10-05 18:31:00', '2026-10-05 18:30:00')
ON CONFLICT (PaymentID) DO NOTHING;

-- [Đơn 7: Đơn Tháng 9/2026 để tính so sánh tăng trưởng MoM]
INSERT INTO Orders (OrderID, BuyerID, CoachID, TotalAmount, OrderStatus, OrderType, CreatedAt)
VALUES ('o1111111-0007-0000-0000-000000000007', 't3333333-3333-3333-3333-333333333333', 'c1111111-1111-1111-1111-111111111111', 2500000, 'COMPLETED', 'PACKAGE', '2026-09-15 15:00:00')
ON CONFLICT (OrderID) DO NOTHING;

INSERT INTO OrderDetails (OrderDetailsID, OrderID, PackageID, PackagePrice, PackageTitle, PackageDurationDays, CoachName, CreatedAt)
VALUES ('d1111111-0007-0000-0000-000000000007', 'o1111111-0007-0000-0000-000000000007', 'k1111111-0000-0000-0000-000000000001', 2500000, 'Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1', 30, 'Nguyễn Văn Hùng (Coach)', '2026-09-15 15:00:00')
ON CONFLICT (OrderDetailsID) DO NOTHING;

INSERT INTO Payments (PaymentID, OrderID, Amount, Currency, Method, Status, ProcessedAt, CreatedAt)
VALUES ('m1111111-0007-0000-0000-000000000007', 'o1111111-0007-0000-0000-000000000007', 2500000, 'VND', 'PAYOS', 'SUCCESS', '2026-09-15 15:01:00', '2026-09-15 15:00:00')
ON CONFLICT (PaymentID) DO NOTHING;

-- ==============================================================================
-- 7. TẠO BẢN GHI GIẢI NGÂN (PAYOUTS) CHO COACH
-- ==============================================================================

-- Payout tháng 9/2026 cho HLV Hùng (Đã xử lý PROCESSED)
INSERT INTO Payouts (PayoutID, CoachID, PayoutMonth, PayoutYear, TotalGrossAmount, SystemCommissionAmount, TaxAmount, NetPayoutAmount, Status, TransactionRef, ProcessedAt, CreatedAt)
VALUES (
    'y1111111-0001-0000-0000-000000000001',
    'c1111111-1111-1111-1111-111111111111',
    9,
    2026,
    2500000,
    0,
    0,
    2500000,
    'PROCESSED',
    'FT260930889211',
    '2026-10-01 10:00:00',
    '2026-10-01 09:30:00'
) ON CONFLICT (CoachID, PayoutMonth, PayoutYear) DO NOTHING;

INSERT INTO PayoutItems (PayoutItemID, PayoutID, OrderID, GrossAmount, CommissionRate, CommissionAmount, RefundAdjustment, TaxAmount, NetAmount, CreatedAt)
VALUES (
    'i1111111-0001-0000-0000-000000000001',
    'y1111111-0001-0000-0000-000000000001',
    'o1111111-0007-0000-0000-000000000007',
    2500000,
    0,
    0,
    0,
    0,
    2500000,
    '2026-10-01 09:30:00'
) ON CONFLICT (PayoutItemID) DO NOTHING;

-- ==============================================================================
-- 8. TẠO YÊU CẦU HOÀN TIỀN (REFUNDREQUESTS)
-- 1 yêu cầu APPROVED (500.000 VND hoàn một phần), 1 yêu cầu REJECTED
-- ==============================================================================

INSERT INTO RefundRequests (RefundRequestID, OrderID, RequestedBy, RequestedAmount, ApprovedAmount, Reason, Status, ReviewedAt, RefundedAt, CreatedAt)
VALUES (
    'r1111111-0001-0000-0000-000000000001',
    'o1111111-0003-0000-0000-000000000003',
    't1111111-1111-1111-1111-111111111111',
    500000,
    500000,
    'Học viên bận đi công tác 1 tuần, Coach đồng ý hoàn một phần buổi chưa tập',
    'APPROVED',
    '2026-10-04 14:00:00',
    '2026-10-04 14:05:00',
    '2026-10-04 11:00:00'
) ON CONFLICT (RefundRequestID) DO NOTHING;

-- ==============================================================================
-- HOÀN TẤT SEED DỮ LIỆU THÀNH CÔNG!
-- ==============================================================================
