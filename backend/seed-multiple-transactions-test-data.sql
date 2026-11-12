-- ============================================================
-- Seed data for testing: Multiple Transaction History entries
-- Test accounts (password for both = "Test@123"):
--   Coach   : coach_rating_test_2026@fitsocial.vn
--   Trainee : trainee_rating_test_2026@fitsocial.vn
--
-- Creates 4 paid orders (PACKAGE) with payments SUCCESS.
-- Trainee will see 4 negative transactions; Coach will see 4 positive.
-- ============================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. Coach user
INSERT INTO users (userid, email, phonenumber, passwordhash, fullname, rolecode, isinternal, islocked, createdat, updatedat)
VALUES ('9f000001-0000-4000-8000-000000000001','coach_rating_test_2026@fitsocial.vn','0977123456',
 '$2a$11$CTXRlMnBiH9BHhBe/2qH6emnNL3/yogBvudW.JfqcRhCrdaH5r1TC','Coach Rating Test','COACH',FALSE,FALSE,NOW(),NOW())
ON CONFLICT (userid) DO NOTHING;

-- 2. Trainee user
INSERT INTO users (userid, email, phonenumber, passwordhash, fullname, rolecode, isinternal, islocked, createdat, updatedat)
VALUES ('9f100001-0000-4000-8000-000000000001','trainee_rating_test_2026@fitsocial.vn','0977654321',
 '$2a$11$CTXRlMnBiH9BHhBe/2qH6emnNL3/yogBvudW.JfqcRhCrdaH5r1TC','Trainee Rating Test','TRAINEE',FALSE,FALSE,NOW(),NOW())
ON CONFLICT (userid) DO NOTHING;

-- 3. Coach profile
INSERT INTO coachprofiles (coachid, experienceyears, bio, approvalstatus, approvedby, status, updatedat)
VALUES ('9f000001-0000-4000-8000-000000000001',5,'Test coach','APPROVED','a0000000-0000-0000-0000-000000000001','ACTIVE',NOW())
ON CONFLICT (coachid) DO NOTHING;

-- 4. Training package
INSERT INTO trainingpackages (packageid, coachid, title, description, price, durationdays, sessioncount, minage, targetaudience, isactive, createdat, "PackageType", packagetype, averagerating, totalreviews)
VALUES ('9f200001-0000-4000-8000-000000000001','9f000001-0000-4000-8000-000000000001','Gói Test Multi Transaction','Gói tập để test nhiều giao dịch',1000000,30,12,18,'Mọi người',TRUE,NOW(),'BOTH','BOTH',0,0)
ON CONFLICT (packageid) DO NOTHING;

-- 5. Orders + details + payments (4 orders)
-- Order 1
INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('9f300001-0000-4000-8000-000000000001','9f100001-0000-4000-8000-000000000001','9f000001-0000-4000-8000-000000000001',500000,'PAID','PACKAGE',NOW()-INTERVAL '3 days')
ON CONFLICT (orderid) DO NOTHING;
INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat, "PackageType", packagetype)
VALUES ('9f400001-0000-4000-8000-000000000001','9f300001-0000-4000-8000-000000000001','9f200001-0000-4000-8000-000000000001',500000,'Gói Test Multi Transaction',30,'Coach Rating Test',NOW()-INTERVAL '3 days','BOTH','BOTH')
ON CONFLICT (orderdetailsid) DO NOTHING;
INSERT INTO payments (paymentid, orderid, amount, currency, method, transactionref, transactiontype, gatewaytransactionid, status, processedat, createdat, updatedat)
VALUES ('9fa00001-0000-4000-8000-000000000001','9f300001-0000-4000-8000-000000000001',500000,'VND','VNPAY','TXN-MULTI-001','PACKAGE_PURCHASE','GW-MULTI-001','SUCCESS',NOW()-INTERVAL '3 days',NOW()-INTERVAL '3 days',NOW()-INTERVAL '3 days')
ON CONFLICT (paymentid) DO NOTHING;

-- Order 2
INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('9f300002-0000-4000-8000-000000000002','9f100001-0000-4000-8000-000000000001','9f000001-0000-4000-8000-000000000001',750000,'COMPLETED','PACKAGE',NOW()-INTERVAL '2 days')
ON CONFLICT (orderid) DO NOTHING;
INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat, "PackageType", packagetype)
VALUES ('9f400002-0000-4000-8000-000000000002','9f300002-0000-4000-8000-000000000002','9f200001-0000-4000-8000-000000000001',750000,'Gói Test Multi Transaction',30,'Coach Rating Test',NOW()-INTERVAL '2 days','BOTH','BOTH')
ON CONFLICT (orderdetailsid) DO NOTHING;
INSERT INTO payments (paymentid, orderid, amount, currency, method, transactionref, transactiontype, gatewaytransactionid, status, processedat, createdat, updatedat)
VALUES ('9fa00002-0000-4000-8000-000000000002','9f300002-0000-4000-8000-000000000002',750000,'VND','MOMO','TXN-MULTI-002','PACKAGE_PURCHASE','GW-MULTI-002','SUCCESS',NOW()-INTERVAL '2 days',NOW()-INTERVAL '2 days',NOW()-INTERVAL '2 days')
ON CONFLICT (paymentid) DO NOTHING;

-- Order 3
INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('9f300003-0000-4000-8000-000000000003','9f100001-0000-4000-8000-000000000001','9f000001-0000-4000-8000-000000000001',1200000,'ACTIVE','PACKAGE',NOW()-INTERVAL '1 day')
ON CONFLICT (orderid) DO NOTHING;
INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat, "PackageType", packagetype)
VALUES ('9f400003-0000-4000-8000-000000000003','9f300003-0000-4000-8000-000000000003','9f200001-0000-4000-8000-000000000001',1200000,'Gói Test Multi Transaction',30,'Coach Rating Test',NOW()-INTERVAL '1 day','BOTH','BOTH')
ON CONFLICT (orderdetailsid) DO NOTHING;
INSERT INTO payments (paymentid, orderid, amount, currency, method, transactionref, transactiontype, gatewaytransactionid, status, processedat, createdat, updatedat)
VALUES ('9fa00003-0000-4000-8000-000000000003','9f300003-0000-4000-8000-000000000003',1200000,'VND','BANKING','TXN-MULTI-003','PACKAGE_PURCHASE','GW-MULTI-003','SUCCESS',NOW()-INTERVAL '1 day',NOW()-INTERVAL '1 day',NOW()-INTERVAL '1 day')
ON CONFLICT (paymentid) DO NOTHING;

-- Order 4 (no payment -> TransactionId will be OrderId)
INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('9f300004-0000-4000-8000-000000000004','9f100001-0000-4000-8000-000000000001','9f000001-0000-4000-8000-000000000001',300000,'PAID','PACKAGE',NOW())
ON CONFLICT (orderid) DO NOTHING;
INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat, "PackageType", packagetype)
VALUES ('9f400004-0000-4000-8000-000000000004','9f300004-0000-4000-8000-000000000004','9f200001-0000-4000-8000-000000000001',300000,'Gói Test Multi Transaction',30,'Coach Rating Test',NOW(),'BOTH','BOTH')
ON CONFLICT (orderdetailsid) DO NOTHING;