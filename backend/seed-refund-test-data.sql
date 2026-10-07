-- ==============================================================================
-- FITSOCIAL - SCRIPT SEED TEST DATA CHO CHỨC NĂNG REFUND REQUEST (ADMIN / STAFF)
-- Sử dụng để test: View Refund List, Pagination, Filter, Detail, Approve, Reject
-- ==============================================================================
-- Lưu ý: Chạy trực tiếp trong PostgreSQL (psql hoặc pgAdmin Query Tool)
-- Script được bọc trong TRANSACTION và đảm bảo tính IDEMPOTENT (chạy lại an toàn)
-- ==============================================================================

BEGIN;

-- 1. BẬT EXTENSION UUID (Nếu chưa có)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ==============================================================================
-- 2. TẠO HOẶC ĐẢM BẢO TỒN TẠI CÁC TÀI KHOẢN USERS (Admin, Staff, Coaches, Trainees)
-- ==============================================================================

-- 2.1 Admin: Phan Phi Pham
INSERT INTO users (userid, email, phonenumber, passwordhash, fullname, rolecode, isinternal, islocked, createdat, updatedat)
VALUES (
    'a0000000-0000-0000-0000-000000000001',
    'admin@fitsocial.com',
    '0901000001',
    '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    'Phan Phi Pham (Admin)',
    'ADMIN',
    TRUE,
    FALSE,
    NOW() - INTERVAL '30 days',
    NOW() - INTERVAL '30 days'
) ON CONFLICT (userid) DO NOTHING;

-- 2.2 Staff: Lê Minh Tuấn
INSERT INTO users (userid, email, phonenumber, passwordhash, fullname, rolecode, isinternal, islocked, createdat, updatedat)
VALUES (
    's0000000-0000-0000-0000-000000000001',
    'staff@fitsocial.com',
    '0901000002',
    '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    'Lê Minh Tuấn (Staff)',
    'STAFF',
    TRUE,
    FALSE,
    NOW() - INTERVAL '30 days',
    NOW() - INTERVAL '30 days'
) ON CONFLICT (userid) DO NOTHING;

-- 2.3 Coach 1: HLV Nguyễn Văn Hùng
INSERT INTO users (userid, email, phonenumber, passwordhash, fullname, rolecode, isinternal, islocked, avatarurl, createdat, updatedat)
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
    NOW() - INTERVAL '30 days',
    NOW() - INTERVAL '30 days'
) ON CONFLICT (userid) DO NOTHING;

-- 2.4 Coach 2: HLV Trần Thị Mai
INSERT INTO users (userid, email, phonenumber, passwordhash, fullname, rolecode, isinternal, islocked, avatarurl, createdat, updatedat)
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
    NOW() - INTERVAL '30 days',
    NOW() - INTERVAL '30 days'
) ON CONFLICT (userid) DO NOTHING;

-- 2.5 Trainee 1: Lê Quốc Bảo
INSERT INTO users (userid, email, phonenumber, passwordhash, fullname, rolecode, isinternal, islocked, createdat, updatedat)
VALUES (
    't1111111-1111-1111-1111-111111111111',
    'bao.le@gmail.com',
    '0903000001',
    '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    'Lê Quốc Bảo',
    'TRAINEE',
    FALSE,
    FALSE,
    NOW() - INTERVAL '30 days',
    NOW() - INTERVAL '30 days'
) ON CONFLICT (userid) DO NOTHING;

-- 2.6 Trainee 2: Phạm Thảo Vy
INSERT INTO users (userid, email, phonenumber, passwordhash, fullname, rolecode, isinternal, islocked, createdat, updatedat)
VALUES (
    't2222222-2222-2222-2222-222222222222',
    'vy.pham@gmail.com',
    '0903000002',
    '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    'Phạm Thảo Vy',
    'TRAINEE',
    FALSE,
    FALSE,
    NOW() - INTERVAL '30 days',
    NOW() - INTERVAL '30 days'
) ON CONFLICT (userid) DO NOTHING;

-- 2.7 Trainee 3: Hoàng Đức Minh
INSERT INTO users (userid, email, phonenumber, passwordhash, fullname, rolecode, isinternal, islocked, createdat, updatedat)
VALUES (
    't3333333-3333-3333-3333-333333333333',
    'minh.hoang@gmail.com',
    '0903000003',
    '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
    'Hoàng Đức Minh',
    'TRAINEE',
    FALSE,
    FALSE,
    NOW() - INTERVAL '30 days',
    NOW() - INTERVAL '30 days'
) ON CONFLICT (userid) DO NOTHING;

-- ==============================================================================
-- 3. HỒ SƠ COACH PROFILES
-- ==============================================================================

INSERT INTO coachprofiles (coachid, experienceyears, bio, approvalstatus, status)
VALUES 
('c1111111-1111-1111-1111-111111111111', 5, 'HLV Thể hình chuyên nghiệp, chứng chỉ NASM CPT.', 'APPROVED', 'ACTIVE'),
('c2222222-2222-2222-2222-222222222222', 4, 'Chuyên gia Yoga & Phục hồi cơ xương khớp.', 'APPROVED', 'ACTIVE')
ON CONFLICT (coachid) DO NOTHING;

-- ==============================================================================
-- 4. ĐIỀU KHOẢN VÀ CHÍNH SÁCH (TERMS AND POLICIES)
-- ==============================================================================

INSERT INTO termsandpolicies (termid, title, content, version, effectivedate, createdat)
VALUES (
    'e1111111-0000-0000-0000-000000000001',
    'Chính sách hoàn tiền FitSocial (Refund Policy 7 Days)',
    'Học viên được quyền yêu cầu hoàn tiền trong vòng 7 ngày đầu kể từ thời điểm thanh toán thành công nếu chưa hoàn thành quá 50% số buổi tập.',
    'v1.0',
    NOW() - INTERVAL '60 days',
    NOW() - INTERVAL '60 days'
) ON CONFLICT (termid) DO NOTHING;

-- ==============================================================================
-- 5. CÁC GÓI TẬP (TRAINING PACKAGES)
-- ==============================================================================

-- Gói Gym 1-1 của HLV Hùng (Giá chuẩn 2.500.000 VND)
INSERT INTO trainingpackages (packageid, coachid, title, description, price, durationdays, sessioncount, minage, targetaudience, isactive, createdat)
VALUES (
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
    NOW() - INTERVAL '30 days'
) ON CONFLICT (packageid) DO NOTHING;

-- Gói Yoga của HLV Mai (Giá chuẩn 1.800.000 VND)
INSERT INTO trainingpackages (packageid, coachid, title, description, price, durationdays, sessioncount, minage, targetaudience, isactive, createdat)
VALUES (
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
    NOW() - INTERVAL '30 days'
) ON CONFLICT (packageid) DO NOTHING;

-- Gói Test Case 6: Gói tăng giá (Giá hiện tại là 1.000.000 VND nhưng lúc mua là 500.000 VND)
INSERT INTO trainingpackages (packageid, coachid, title, description, price, durationdays, sessioncount, minage, targetaudience, isactive, createdat)
VALUES (
    'k0000006-0001-0000-0000-000000000001',
    'c1111111-1111-1111-1111-111111111111',
    'Gói Huấn Luyện Thể Lực Khởi Động (Test Snapshot Price)',
    'Gói tập dùng để kiểm tra việc HLV tăng giá gói từ 500k lên 1 triệu',
    1000000, -- Giá hiện tại sau khi tăng giá
    30,
    8,
    15,
    'Người mới bắt đầu tập gym',
    TRUE,
    NOW() - INTERVAL '30 days'
) ON CONFLICT (packageid) DO UPDATE SET price = 1000000;

-- ==============================================================================
-- 6. DỌN DẸP DỮ LIỆU TEST CŨ ĐỂ ĐẢM BẢO TÍNH IDEMPOTENT (CHẠY LẠI KHÔNG BỊ TRÙNG)
-- ==============================================================================

DELETE FROM auditlogs WHERE entityid LIKE 'rf00000%';
DELETE FROM notifications WHERE id LIKE 'nf00000%';
DELETE FROM refundrequests WHERE refundrequestid LIKE 'rf00000%';
DELETE FROM payoutitems WHERE payoutitemid LIKE 'pi00000%';
DELETE FROM payouts WHERE payoutid LIKE 'po00000%';
DELETE FROM trainingplan WHERE trainingplanid LIKE 'tp00000%';
DELETE FROM payments WHERE paymentid LIKE 'py00000%';
DELETE FROM orderdetails WHERE orderdetailsid LIKE 'od00000%';
DELETE FROM orders WHERE orderid LIKE 'or00000%';

-- ==============================================================================
-- 7. TẠO 5 YÊU CẦU HOÀN TIỀN PENDING (HỢP LỆ TRONG VÒNG 7 NGÀY ĐẦU)
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- PENDING #1: Trainee Bảo hoàn đơn Gym (2.500.000 VND) - Mua 2 ngày trước
-- ------------------------------------------------------------------------------
INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('or000001-0001-0000-0000-000000000001', 't1111111-1111-1111-1111-111111111111', 'c1111111-1111-1111-1111-111111111111', 2500000, 'COMPLETED', 'PACKAGE', NOW() - INTERVAL '2 days');

INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat)
VALUES ('od000001-0001-0000-0000-000000000001', 'or000001-0001-0000-0000-000000000001', 'k1111111-0000-0000-0000-000000000001', 2500000, 'Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1', 30, 'Nguyễn Văn Hùng (Coach)', NOW() - INTERVAL '2 days');

INSERT INTO payments (paymentid, orderid, amount, currency, method, transactionref, status, processedat, createdat, updatedat)
VALUES ('py000001-0001-0000-0000-000000000001', 'or000001-0001-0000-0000-000000000001', 2500000, 'VND', 'VietQR', 'PAYOS-20261005-01', 'SUCCESS', NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days');

INSERT INTO trainingplan (trainingplanid, coachid, orderdetailsid, title, description, startdate, enddate, status, createdat, updatedat)
VALUES ('tp000001-0001-0000-0000-000000000001', 'c1111111-1111-1111-1111-111111111111', 'od000001-0001-0000-0000-000000000001', 'Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1', 'Kế hoạch tập gym kích hoạt', NOW() - INTERVAL '2 days', NOW() + INTERVAL '28 days', 'ACTIVE', NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days');

INSERT INTO refundrequests (refundrequestid, orderid, paymentid, requestedby, termid, requestedamount, approvedamount, reason, evidenceurls, status, requestedat, createdat, updatedat)
VALUES (
    'rf000001-0001-0000-0000-000000000001',
    'or000001-0001-0000-0000-000000000001',
    'py000001-0001-0000-0000-000000000001',
    't1111111-1111-1111-1111-111111111111',
    'e1111111-0000-0000-0000-000000000001',
    2500000,
    NULL,
    'Bận đi công tác đột xuất ở nước ngoài 2 tháng, không thể tiếp tục lịch tập 1-1.',
    'https://images.unsplash.com/photo-1540420773420-3366772f4999?w=300;https://images.unsplash.com/photo-1517838277536-f5f99be501cd?w=300',
    'PENDING',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '1 day'
);

-- ------------------------------------------------------------------------------
-- PENDING #2: Trainee Vy hoàn đơn Yoga (1.800.000 VND) - Mua 3 ngày trước
-- ------------------------------------------------------------------------------
INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('or000001-0002-0000-0000-000000000002', 't2222222-2222-2222-2222-222222222222', 'c2222222-2222-2222-2222-222222222222', 1800000, 'COMPLETED', 'PACKAGE', NOW() - INTERVAL '3 days');

INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat)
VALUES ('od000001-0002-0000-0000-000000000002', 'or000001-0002-0000-0000-000000000002', 'k2222222-0000-0000-0000-000000000002', 1800000, 'Gói Yoga & Thon Gọn Cơ Thể Dẻo Dai', 30, 'Trần Thị Mai (Coach)', NOW() - INTERVAL '3 days');

INSERT INTO payments (paymentid, orderid, amount, currency, method, transactionref, status, processedat, createdat, updatedat)
VALUES ('py000001-0002-0000-0000-000000000002', 'or000001-0002-0000-0000-000000000002', 1800000, 'VND', 'VietQR', 'PAYOS-20261004-02', 'SUCCESS', NOW() - INTERVAL '3 days', NOW() - INTERVAL '3 days', NOW() - INTERVAL '3 days');

INSERT INTO refundrequests (refundrequestid, orderid, paymentid, requestedby, termid, requestedamount, approvedamount, reason, evidenceurls, status, requestedat, createdat, updatedat)
VALUES (
    'rf000001-0002-0000-0000-000000000002',
    'or000001-0002-0000-0000-000000000002',
    'py000001-0002-0000-0000-000000000002',
    't2222222-2222-2222-2222-222222222222',
    'e1111111-0000-0000-0000-000000000001',
    1800000,
    NULL,
    'Lịch làm việc của công ty đổi sang ca đêm, không thể tham gia lớp yoga buổi sáng của HLV.',
    NULL,
    'PENDING',
    NOW() - INTERVAL '2 days',
    NOW() - INTERVAL '2 days',
    NOW() - INTERVAL '2 days'
);

-- ------------------------------------------------------------------------------
-- PENDING #3: Trainee Minh hoàn đơn Gym (2.500.000 VND) - Mua 1 ngày trước
-- ------------------------------------------------------------------------------
INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('or000001-0003-0000-0000-000000000003', 't3333333-3333-3333-3333-333333333333', 'c1111111-1111-1111-1111-111111111111', 2500000, 'COMPLETED', 'PACKAGE', NOW() - INTERVAL '1 day');

INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat)
VALUES ('od000001-0003-0000-0000-000000000003', 'or000001-0003-0000-0000-000000000003', 'k1111111-0000-0000-0000-000000000001', 2500000, 'Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1', 30, 'Nguyễn Văn Hùng (Coach)', NOW() - INTERVAL '1 day');

INSERT INTO payments (paymentid, orderid, amount, currency, method, transactionref, status, processedat, createdat, updatedat)
VALUES ('py000001-0003-0000-0000-000000000003', 'or000001-0003-0000-0000-000000000003', 2500000, 'VND', 'VietQR', 'PAYOS-20261006-03', 'SUCCESS', NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day');

INSERT INTO refundrequests (refundrequestid, orderid, paymentid, requestedby, termid, requestedamount, approvedamount, reason, evidenceurls, status, requestedat, createdat, updatedat)
VALUES (
    'rf000001-0003-0000-0000-000000000003',
    'or000001-0003-0000-0000-000000000003',
    'py000001-0003-0000-0000-000000000003',
    't3333333-3333-3333-3333-333333333333',
    'e1111111-0000-0000-0000-000000000001',
    2500000,
    NULL,
    'Bị bong gân cổ chân khi đá bóng cuối tuần, bác sĩ yêu cầu bó bột bất động 4 tuần.',
    'https://images.unsplash.com/photo-1579684385127-1ef15d508118?w=300',
    'PENDING',
    NOW() - INTERVAL '12 hours',
    NOW() - INTERVAL '12 hours',
    NOW() - INTERVAL '12 hours'
);

-- ------------------------------------------------------------------------------
-- PENDING #4: Trainee Vy hoàn gói Gym (2.500.000 VND) - Mua 4 ngày trước
-- ------------------------------------------------------------------------------
INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('or000001-0004-0000-0000-000000000004', 't2222222-2222-2222-2222-222222222222', 'c1111111-1111-1111-1111-111111111111', 2500000, 'COMPLETED', 'PACKAGE', NOW() - INTERVAL '4 days');

INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat)
VALUES ('od000001-0004-0000-0000-000000000004', 'or000001-0004-0000-0000-000000000004', 'k1111111-0000-0000-0000-000000000001', 2500000, 'Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1', 30, 'Nguyễn Văn Hùng (Coach)', NOW() - INTERVAL '4 days');

INSERT INTO payments (paymentid, orderid, amount, currency, method, transactionref, status, processedat, createdat, updatedat)
VALUES ('py000001-0004-0000-0000-000000000004', 'or000001-0004-0000-0000-000000000004', 2500000, 'VND', 'VietQR', 'PAYOS-20261003-04', 'SUCCESS', NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days');

INSERT INTO refundrequests (refundrequestid, orderid, paymentid, requestedby, termid, requestedamount, approvedamount, reason, evidenceurls, status, requestedat, createdat, updatedat)
VALUES (
    'rf000001-0004-0000-0000-000000000004',
    'or000001-0004-0000-0000-000000000004',
    'py000001-0004-0000-0000-000000000004',
    't2222222-2222-2222-2222-222222222222',
    'e1111111-0000-0000-0000-000000000001',
    2500000,
    NULL,
    'HLV liên tục thay đổi giờ tập mà không báo trước 24h theo thỏa thuận.',
    NULL,
    'PENDING',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '1 day'
);

-- ------------------------------------------------------------------------------
-- PENDING #5: Trainee Bảo hoàn đơn Yoga (1.800.000 VND) - Mua 5 ngày trước
-- ------------------------------------------------------------------------------
INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('or000001-0005-0000-0000-000000000005', 't1111111-1111-1111-1111-111111111111', 'c2222222-2222-2222-2222-222222222222', 1800000, 'COMPLETED', 'PACKAGE', NOW() - INTERVAL '5 days');

INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat)
VALUES ('od000001-0005-0000-0000-000000000005', 'or000001-0005-0000-0000-000000000005', 'k2222222-0000-0000-0000-000000000002', 1800000, 'Gói Yoga & Thon Gọn Cơ Thể Dẻo Dai', 30, 'Trần Thị Mai (Coach)', NOW() - INTERVAL '5 days');

INSERT INTO payments (paymentid, orderid, amount, currency, method, transactionref, status, processedat, createdat, updatedat)
VALUES ('py000001-0005-0000-0000-000000000005', 'or000001-0005-0000-0000-000000000005', 1800000, 'VND', 'VietQR', 'PAYOS-20261002-05', 'SUCCESS', NOW() - INTERVAL '5 days', NOW() - INTERVAL '5 days', NOW() - INTERVAL '5 days');

INSERT INTO refundrequests (refundrequestid, orderid, paymentid, requestedby, termid, requestedamount, approvedamount, reason, evidenceurls, status, requestedat, createdat, updatedat)
VALUES (
    'rf000001-0005-0000-0000-000000000005',
    'or000001-0005-0000-0000-000000000005',
    'py000001-0005-0000-0000-000000000005',
    't1111111-1111-1111-1111-111111111111',
    'e1111111-0000-0000-0000-000000000001',
    1800000,
    NULL,
    'Sau 2 buổi tập nhận thấy cường độ bài tập yoga không phù hợp với thể trạng cá nhân.',
    NULL,
    'PENDING',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '1 day'
);

-- ==============================================================================
-- 8. TẠO 1 YÊU CẦU HOÀN TIỀN ĐÃ ĐƯỢC DUYỆT (STATUS = APPROVED)
-- Order và Payment phải ở trạng thái 'REFUNDED'
-- ==============================================================================

INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('or000002-0001-0000-0000-000000000001', 't1111111-1111-1111-1111-111111111111', 'c1111111-1111-1111-1111-111111111111', 2500000, 'REFUNDED', 'PACKAGE', NOW() - INTERVAL '5 days');

INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat)
VALUES ('od000002-0001-0000-0000-000000000001', 'or000002-0001-0000-0000-000000000001', 'k1111111-0000-0000-0000-000000000001', 2500000, 'Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1', 30, 'Nguyễn Văn Hùng (Coach)', NOW() - INTERVAL '5 days');

INSERT INTO payments (paymentid, orderid, amount, currency, method, transactionref, status, processedat, createdat, updatedat)
VALUES ('py000002-0001-0000-0000-000000000001', 'or000002-0001-0000-0000-000000000001', 2500000, 'VND', 'VietQR', 'PAYOS-20261002-APP01', 'REFUNDED', NOW() - INTERVAL '5 days', NOW() - INTERVAL '5 days', NOW() - INTERVAL '1 day');

INSERT INTO refundrequests (refundrequestid, orderid, paymentid, requestedby, termid, requestedamount, approvedamount, reason, evidenceurls, status, reviewedby, reviewedat, staffnote, refundtransactionref, requestedat, refundedat, createdat, updatedat)
VALUES (
    'rf000002-0001-0000-0000-000000000001',
    'or000002-0001-0000-0000-000000000001',
    'py000002-0001-0000-0000-000000000001',
    't1111111-1111-1111-1111-111111111111',
    'e1111111-0000-0000-0000-000000000001',
    2500000,
    2500000,
    'Học viên chuyển chỗ ở sang tỉnh khác, HLV đồng ý hoàn 100% tiền gói tập.',
    'https://images.unsplash.com/photo-1540420773420-3366772f4999?w=300',
    'APPROVED',
    's0000000-0000-0000-0000-000000000001', -- Processed by Staff Lê Minh Tuấn
    NOW() - INTERVAL '1 day',
    'Đã đối soát với HLV Hùng và học viên Bảo, xác nhận trong hạn 7 ngày đầu, duyệt hoàn tiền 100%.',
    'REF-20261005-APP01',
    NOW() - INTERVAL '2 days',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '2 days',
    NOW() - INTERVAL '1 day'
);

-- ==============================================================================
-- 9. TẠO 1 YÊU CẦU HOÀN TIỀN ĐÃ BỊ TỪ CHỐI (STATUS = REJECTED)
-- Order và Payment GIỮ NGUYÊN trạng thái (COMPLETED / SUCCESS)
-- ==============================================================================

INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('or000003-0001-0000-0000-000000000001', 't3333333-3333-3333-3333-333333333333', 'c1111111-1111-1111-1111-111111111111', 2500000, 'COMPLETED', 'PACKAGE', NOW() - INTERVAL '4 days');

INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat)
VALUES ('od000003-0001-0000-0000-000000000001', 'or000003-0001-0000-0000-000000000001', 'k1111111-0000-0000-0000-000000000001', 2500000, 'Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1', 30, 'Nguyễn Văn Hùng (Coach)', NOW() - INTERVAL '4 days');

INSERT INTO payments (paymentid, orderid, amount, currency, method, transactionref, status, processedat, createdat, updatedat)
VALUES ('py000003-0001-0000-0000-000000000001', 'or000003-0001-0000-0000-000000000001', 2500000, 'VND', 'VietQR', 'PAYOS-20261003-REJ01', 'SUCCESS', NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days');

INSERT INTO refundrequests (refundrequestid, orderid, paymentid, requestedby, termid, requestedamount, approvedamount, reason, evidenceurls, status, reviewedby, reviewedat, staffnote, requestedat, createdat, updatedat)
VALUES (
    'rf000003-0001-0000-0000-000000000001',
    'or000003-0001-0000-0000-000000000001',
    'py000003-0001-0000-0000-000000000001',
    't3333333-3333-3333-3333-333333333333',
    'e1111111-0000-0000-0000-000000000001',
    2500000,
    NULL,
    'Tập được 7 buổi thấy mệt quá nên muốn hủy gói lấy lại tiền.',
    NULL,
    'REJECTED',
    's0000000-0000-0000-0000-000000000001', -- Processed by Staff Lê Minh Tuấn
    NOW() - INTERVAL '1 day',
    'Học viên đã tham gia hơn 50% số buổi tập và không có chỉ định y tế bất khả kháng. Từ chối hoàn tiền theo điều 4.2 của chính sách.',
    NOW() - INTERVAL '2 days',
    NOW() - INTERVAL '2 days',
    NOW() - INTERVAL '1 day'
);

-- ==============================================================================
-- 10. TEST CASE TRANH CHẤP: PENDING + PAYOUT ĐÃ GIẢI NGÂN (STATUS = PROCESSED)
-- Khi Admin bấm Duyệt trên UI, backend phải CHẶN với lỗi:
-- "This transaction is no longer eligible for refund because the payout to the coach has already been processed."
-- ==============================================================================

INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('or000004-0001-0000-0000-000000000001', 't1111111-1111-1111-1111-111111111111', 'c1111111-1111-1111-1111-111111111111', 2500000, 'COMPLETED', 'PACKAGE', NOW() - INTERVAL '6 days');

INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat)
VALUES ('od000004-0001-0000-0000-000000000001', 'or000004-0001-0000-0000-000000000001', 'k1111111-0000-0000-0000-000000000001', 2500000, 'Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1', 30, 'Nguyễn Văn Hùng (Coach)', NOW() - INTERVAL '6 days');

INSERT INTO payments (paymentid, orderid, amount, currency, method, transactionref, status, processedat, createdat, updatedat)
VALUES ('py000004-0001-0000-0000-000000000001', 'or000004-0001-0000-0000-000000000001', 2500000, 'VND', 'VietQR', 'PAYOS-20261001-PO01', 'SUCCESS', NOW() - INTERVAL '6 days', NOW() - INTERVAL '6 days', NOW() - INTERVAL '6 days');

-- Payout đã giải ngân thành công cho Coach Hùng
INSERT INTO payouts (payoutid, coachid, payoutmonth, payoutyear, totalgrossamount, systemcommissionamount, taxamount, netpayoutamount, status, transactionref, processedby, processedat, createdat)
VALUES (
    'po000004-0001-0000-0000-000000000001',
    'c1111111-1111-1111-1111-111111111111',
    10,
    2026,
    2500000,
    0,
    0,
    2500000,
    'PROCESSED', -- ĐÃ GIẢI NGÂN
    'BANK-DISBURSE-9921',
    'a0000000-0000-0000-0000-000000000001',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '2 days'
) ON CONFLICT (payoutid) DO NOTHING;

INSERT INTO payoutitems (payoutitemid, payoutid, orderid, grossamount, commissionrate, commissionamount, refundadjustment, taxamount, netamount, createdat)
VALUES (
    'pi000004-0001-0000-0000-000000000001',
    'po000004-0001-0000-0000-000000000001',
    'or000004-0001-0000-0000-000000000001',
    2500000,
    0,
    0,
    0,
    0,
    2500000,
    NOW() - INTERVAL '2 days'
) ON CONFLICT (payoutitemid) DO NOTHING;

-- Yêu cầu hoàn tiền vẫn ở trạng thái PENDING
INSERT INTO refundrequests (refundrequestid, orderid, paymentid, requestedby, termid, requestedamount, approvedamount, reason, evidenceurls, status, requestedat, createdat, updatedat)
VALUES (
    'rf000004-0001-0000-0000-000000000001',
    'or000004-0001-0000-0000-000000000001',
    'py000004-0001-0000-0000-000000000001',
    't1111111-1111-1111-1111-111111111111',
    'e1111111-0000-0000-0000-000000000001',
    2500000,
    NULL,
    'Yêu cầu hoàn tiền test trường hợp tiền đã được chi trả cho HLV (Payout Conflict).',
    NULL,
    'PENDING',
    NOW() - INTERVAL '12 hours',
    NOW() - INTERVAL '12 hours',
    NOW() - INTERVAL '12 hours'
);

-- ==============================================================================
-- 11. TEST CASE QUÁ HẠN 7 NGÀY: PURCHASEDATE = 10 NGÀY TRƯỚC, STATUS = PENDING
-- Dùng để test hiển thị trên UI và kiểm tra Admin xem chi tiết từ chối yêu cầu
-- ==============================================================================

INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('or000005-0001-0000-0000-000000000001', 't3333333-3333-3333-3333-333333333333', 'c2222222-2222-2222-2222-222222222222', 1800000, 'COMPLETED', 'PACKAGE', NOW() - INTERVAL '10 days');

INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat)
VALUES ('od000005-0001-0000-0000-000000000001', 'or000005-0001-0000-0000-000000000001', 'k2222222-0000-0000-0000-000000000002', 1800000, 'Gói Yoga & Thon Gọn Cơ Thể Dẻo Dai', 30, 'Trần Thị Mai (Coach)', NOW() - INTERVAL '10 days');

INSERT INTO payments (paymentid, orderid, amount, currency, method, transactionref, status, processedat, createdat, updatedat)
VALUES ('py000005-0001-0000-0000-000000000001', 'or000005-0001-0000-0000-000000000001', 1800000, 'VND', 'VietQR', 'PAYOS-20260927-EXP01', 'SUCCESS', NOW() - INTERVAL '10 days', NOW() - INTERVAL '10 days', NOW() - INTERVAL '10 days');

INSERT INTO refundrequests (refundrequestid, orderid, paymentid, requestedby, termid, requestedamount, approvedamount, reason, evidenceurls, status, requestedat, createdat, updatedat)
VALUES (
    'rf000005-0001-0000-0000-000000000001',
    'or000005-0001-0000-0000-000000000001',
    'py000005-0001-0000-0000-000000000001',
    't3333333-3333-3333-3333-333333333333',
    'e1111111-0000-0000-0000-000000000001',
    1800000,
    NULL,
    'Yêu cầu hoàn tiền gửi muộn sau 10 ngày mua gói tập (Test Case Expired).',
    NULL,
    'PENDING',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '1 day'
);

-- ==============================================================================
-- 12. TEST CASE SNAPSHOT PRICE: GÓI TẬP HIỆN TẠI GIÁ 1.000.000 VND, LÚC MUA 500.000 VND
-- ==============================================================================

INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('or000006-0001-0000-0000-000000000001', 't1111111-1111-1111-1111-111111111111', 'c1111111-1111-1111-1111-111111111111', 500000, 'COMPLETED', 'PACKAGE', NOW() - INTERVAL '2 days');

INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat)
VALUES ('od000006-0001-0000-0000-000000000001', 'or000006-0001-0000-0000-000000000001', 'k0000006-0001-0000-0000-000000000001', 500000, 'Gói Huấn Luyện Thể Lực Khởi Động (Test Snapshot Price)', 30, 'Nguyễn Văn Hùng (Coach)', NOW() - INTERVAL '2 days');

INSERT INTO payments (paymentid, orderid, amount, currency, method, transactionref, status, processedat, createdat, updatedat)
VALUES ('py000006-0001-0000-0000-000000000001', 'or000006-0001-0000-0000-000000000001', 500000, 'VND', 'VietQR', 'PAYOS-20261005-SNAP01', 'SUCCESS', NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days');

INSERT INTO refundrequests (refundrequestid, orderid, paymentid, requestedby, termid, requestedamount, approvedamount, reason, evidenceurls, status, requestedat, createdat, updatedat)
VALUES (
    'rf000006-0001-0000-0000-000000000001',
    'or000006-0001-0000-0000-000000000001',
    'py000006-0001-0000-0000-000000000001',
    't1111111-1111-1111-1111-111111111111',
    'e1111111-0000-0000-0000-000000000001',
    500000, -- Snapshot lúc mua: 500.000 VND (dù giá gói hiện tại là 1.000.000 VND)
    NULL,
    'Test snapshot price: gói tập sau khi mua đã tăng giá lên 1 triệu, nhưng tiền hoàn phải là 500k.',
    NULL,
    'PENDING',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '1 day'
);

-- ==============================================================================
-- 13. TẠO TEST DỮ LIỆU BẢNG NOTIFICATIONS (ENGLISH NOTIFICATIONS FOR BUYER & SELLER)
-- Chú ý: Column name trong PostgreSQL schema là 'desciption' (không có chữ 'r')
-- ==============================================================================

-- 13.1 Thông báo Approved gửi cho Buyer (Trainee Bảo)
INSERT INTO notifications (id, userid, actorid, referenceid, type, desciption, isread, createdat)
VALUES (
    'nf000001-0001-0000-0000-000000000001',
    't1111111-1111-1111-1111-111111111111',
    's0000000-0000-0000-0000-000000000001',
    'rf000002-0001-0000-0000-000000000001',
    'REFUND_APPROVED',
    'Your refund request for "Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1" has been approved. The refund amount is 2,500,000 VND.',
    FALSE,
    NOW() - INTERVAL '1 day'
);

-- 13.2 Thông báo Approved gửi cho Seller (Coach Hùng)
INSERT INTO notifications (id, userid, actorid, referenceid, type, desciption, isread, createdat)
VALUES (
    'nf000001-0003-0000-0000-000000000003',
    'c1111111-1111-1111-1111-111111111111',
    's0000000-0000-0000-0000-000000000001',
    'rf000002-0001-0000-0000-000000000001',
    'REFUND_APPROVED',
    'A refund request for your training package "Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1" has been approved. The refund amount is 2,500,000 VND.',
    FALSE,
    NOW() - INTERVAL '1 day'
);

-- 13.3 Thông báo Rejected gửi cho Buyer (Trainee Minh)
INSERT INTO notifications (id, userid, actorid, referenceid, type, desciption, isread, createdat)
VALUES (
    'nf000001-0002-0000-0000-000000000002',
    't3333333-3333-3333-3333-333333333333',
    's0000000-0000-0000-0000-000000000001',
    'rf000003-0001-0000-0000-000000000001',
    'REFUND_REJECTED',
    'Your refund request for "Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1" has been rejected. Reason: Trainee has already completed over 50% of the training package sessions.',
    FALSE,
    NOW() - INTERVAL '1 day'
);

-- 13.4 Thông báo Rejected gửi cho Seller (Coach Hùng)
INSERT INTO notifications (id, userid, actorid, referenceid, type, desciption, isread, createdat)
VALUES (
    'nf000001-0004-0000-0000-000000000004',
    'c1111111-1111-1111-1111-111111111111',
    's0000000-0000-0000-0000-000000000001',
    'rf000003-0001-0000-0000-000000000001',
    'REFUND_REJECTED',
    'The refund request for your training package "Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1" has been rejected. Reason: Trainee has already completed over 50% of the training package sessions.',
    FALSE,
    NOW() - INTERVAL '1 day'
);

-- ==============================================================================
-- 14. TẠO TEST DỮ LIỆU BẢNG AUDITLOGS (ENGLISH AUDIT LOG ENTRIES)
-- ==============================================================================

-- 14.1 Audit log cho thao tác Approve Refund
INSERT INTO auditlogs (auditlogid, actoraccountid, action, entitytype, entityid, oldvalue, newvalue, ipaddress, createdat)
VALUES (
    'al000001-0001-0000-0000-000000000001',
    's0000000-0000-0000-0000-000000000001',
    'APPROVE_REFUND',
    'RefundRequest',
    'rf000002-0001-0000-0000-000000000001',
    '{"status": "PENDING", "requestedAmount": 2500000}'::jsonb,
    jsonb_build_object(
        'description', E'Refund request approved.\nBuyer: Lê Quốc Bảo\nSeller: Nguyễn Văn Hùng\nTraining Package: Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1\nOrder: #ORD-OR000002\nOriginal Paid Amount: 2,500,000 VND\nRefund Amount: 2,500,000 VND\nPurchase Date: 05/10/2026 10:00\nRequested At: 05/10/2026 15:30\nProcessed At: 06/10/2026 09:00\nPrevious Status: PENDING\nNew Status: APPROVED\nProcessed By: Lê Minh Tuấn (Staff)',
        'status', 'APPROVED',
        'approvedAmount', 2500000,
        'refundTransactionRef', 'REF-20261005-APP01',
        'staffNote', 'Confirmed within 7-day policy period; approved 100% full refund.',
        'processedBy', 'Lê Minh Tuấn (Staff)'
    ),
    '127.0.0.1',
    NOW() - INTERVAL '1 day'
);

-- 14.2 Audit log cho thao tác Reject Refund
INSERT INTO auditlogs (auditlogid, actoraccountid, action, entitytype, entityid, oldvalue, newvalue, ipaddress, createdat)
VALUES (
    'al000001-0002-0000-0000-000000000002',
    's0000000-0000-0000-0000-000000000001',
    'REJECT_REFUND',
    'RefundRequest',
    'rf000003-0001-0000-0000-000000000001',
    '{"status": "PENDING"}'::jsonb,
    jsonb_build_object(
        'description', E'Refund request rejected.\nBuyer: Hoàng Đức Minh\nSeller: Nguyễn Văn Hùng\nTraining Package: Gói Gym Tăng Cơ Giảm Mỡ Chuyên Sâu 1-1\nOrder: #ORD-OR000003\nOriginal Paid Amount: 2,500,000 VND\nPurchase Date: 03/10/2026 09:00\nRequested At: 05/10/2026 14:00\nProcessed At: 06/10/2026 10:30\nPrevious Status: PENDING\nNew Status: REJECTED\nProcessed By: Lê Minh Tuấn (Staff)\nRejection Reason: Trainee has already completed over 50% of the training package sessions.',
        'status', 'REJECTED',
        'staffNote', 'Trainee has already completed over 50% of the training package sessions.',
        'processedBy', 'Lê Minh Tuấn (Staff)',
        'rejectionReason', 'Trainee has already completed over 50% of the training package sessions.'
    ),
    '127.0.0.1',
    NOW() - INTERVAL '1 day'
);

COMMIT;
