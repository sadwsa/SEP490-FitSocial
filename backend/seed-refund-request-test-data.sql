-- ============================================================
-- Seed data for testing: Refund Request
-- Test accounts (password for all = "Test@123"):
--   Trainee : trainee_rating_test_2026@fitsocial.vn
--   Admin   : admin.test@fitsocial.vn
--
-- Key IDs:
--   Trainee user id : 9f100001-0000-4000-8000-000000000001
--   Order id        : 9f300001-0000-4000-8000-000000000001
--   Payment id      : 9fa00001-0000-4000-8000-000000000001
--   RefundRequest id: 9fd00001-0000-4000-8000-000000000001
-- ============================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 0. Admin user (for reviewing refund requests)
INSERT INTO users (userid, email, phonenumber, passwordhash, fullname, rolecode, isinternal, islocked, createdat, updatedat)
VALUES ('9fe00001-0000-4000-8000-000000000001','admin.test@fitsocial.vn','0966000001',
 '$2a$11$CTXRlMnBiH9BHhBe/2qH6emnNL3/yogBvudW.JfqcRhCrdaH5r1TC','Admin Test','ADMIN',TRUE,FALSE,NOW(),NOW())
ON CONFLICT (userid) DO NOTHING;

-- 1. Trainee user
INSERT INTO users (userid, email, phonenumber, passwordhash, fullname, rolecode, isinternal, islocked, createdat, updatedat)
VALUES ('9f100001-0000-4000-8000-000000000001','trainee_rating_test_2026@fitsocial.vn','0977654321',
 '$2a$11$CTXRlMnBiH9BHhBe/2qH6emnNL3/yogBvudW.JfqcRhCrdaH5r1TC','Trainee Rating Test','TRAINEE',FALSE,FALSE,NOW(),NOW())
ON CONFLICT (userid) DO NOTHING;

-- 2. Coach user + profile (for order FK)
INSERT INTO users (userid, email, phonenumber, passwordhash, fullname, rolecode, isinternal, islocked, createdat, updatedat)
VALUES ('9f000001-0000-4000-8000-000000000001','coach_rating_test_2026@fitsocial.vn','0977123456',
 '$2a$11$CTXRlMnBiH9BHhBe/2qH6emnNL3/yogBvudW.JfqcRhCrdaH5r1TC','Coach Rating Test','COACH',FALSE,FALSE,NOW(),NOW())
ON CONFLICT (userid) DO NOTHING;

INSERT INTO coachprofiles (coachid, experienceyears, bio, approvalstatus, approvedby, status, updatedat)
VALUES ('9f000001-0000-4000-8000-000000000001',5,'Test coach','APPROVED','a0000000-0000-0000-0000-000000000001','ACTIVE',NOW())
ON CONFLICT (coachid) DO NOTHING;

-- 3. Package
INSERT INTO trainingpackages (packageid, coachid, title, description, price, durationdays, sessioncount, minage, targetaudience, isactive, createdat, "PackageType", packagetype, averagerating, totalreviews)
VALUES ('9f200001-0000-4000-8000-000000000001','9f000001-0000-4000-8000-000000000001','Gói Test Refund','Gói tập để test refund',1000000,30,12,18,'Mọi người',TRUE,NOW(),'BOTH','BOTH',0,0)
ON CONFLICT (packageid) DO NOTHING;

-- 4. Paid order
INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('9f300001-0000-4000-8000-000000000001','9f100001-0000-4000-8000-000000000001','9f000001-0000-4000-8000-000000000001',1000000,'PAID','PACKAGE',NOW())
ON CONFLICT (orderid) DO NOTHING;

-- 5. Order detail
INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat, "PackageType", packagetype)
VALUES ('9f400001-0000-4000-8000-000000000001','9f300001-0000-4000-8000-000000000001','9f200001-0000-4000-8000-000000000001',1000000,'Gói Test Refund',30,'Coach Rating Test',NOW(),'BOTH','BOTH')
ON CONFLICT (orderdetailsid) DO NOTHING;

-- 6. Payment SUCCESS
INSERT INTO payments (paymentid, orderid, amount, currency, method, transactionref, transactiontype, gatewaytransactionid, status, processedat, createdat, updatedat)
VALUES ('9fa00001-0000-4000-8000-000000000001','9f300001-0000-4000-8000-000000000001',1000000,'VND','VNPAY','TXN-REFUND-001','PACKAGE_PURCHASE','GW-REFUND-001','SUCCESS',NOW(),NOW(),NOW())
ON CONFLICT (paymentid) DO NOTHING;

-- 7. Refund request PENDING (trainee requests refund)
INSERT INTO refundrequests (refundrequestid, orderid, paymentid, requestedby, termid, requestedamount, reason, evidenceurls, status, requestedat, createdat, updatedat)
VALUES ('9fd00001-0000-4000-8000-000000000001','9f300001-0000-4000-8000-000000000001','9fa00001-0000-4000-8000-000000000001',
 '9f100001-0000-4000-8000-000000000001','174514e1-0071-41d6-ba55-806a76ee1ba2',1000000,'Test refund request','[]','PENDING',NOW(),NOW(),NOW())
ON CONFLICT (refundrequestid) DO NOTHING;