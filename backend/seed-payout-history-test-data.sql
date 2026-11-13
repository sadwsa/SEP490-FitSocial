-- ============================================================
-- Seed data for testing: Payout History (coach payouts)
-- Test accounts (password for both = "Test@123"):
--   Coach   : coach_rating_test_2026@fitsocial.vn
--   Admin   : admin@fitsocial.com (password from seed-refund-test-data: "password"?)
--
-- Key IDs:
--   Coach user id   : 9f000001-0000-4000-8000-000000000001
--   Order id        : 9f300001-0000-4000-8000-000000000001
--   Payout id       : 9fb00001-0000-4000-8000-000000000001
--   PayoutItem id   : 9fc00001-0000-4000-8000-000000000001
-- ============================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. Coach user
INSERT INTO users (userid, email, phonenumber, passwordhash, fullname, rolecode, isinternal, islocked, createdat, updatedat)
VALUES ('9f000001-0000-4000-8000-000000000001','coach_rating_test_2026@fitsocial.vn','0977123456',
 '$2a$11$CTXRlMnBiH9BHhBe/2qH6emnNL3/yogBvudW.JfqcRhCrdaH5r1TC','Coach Rating Test','COACH',FALSE,FALSE,NOW(),NOW())
ON CONFLICT (userid) DO NOTHING;

-- 2. Coach profile
INSERT INTO coachprofiles (coachid, experienceyears, bio, approvalstatus, approvedby, status, updatedat)
VALUES ('9f000001-0000-4000-8000-000000000001',5,'Test coach','APPROVED','a0000000-0000-0000-0000-000000000001','ACTIVE',NOW())
ON CONFLICT (coachid) DO NOTHING;

-- 3. Payout (monthly cycle, completed)
INSERT INTO payouts (payoutid, coachid, payoutmonth, payoutyear, totalgrossamount, systemcommissionamount, taxamount, netpayoutamount, status, processedby, processedat, createdat, transactionref)
VALUES ('9fb00001-0000-4000-8000-000000000001','9f000001-0000-4000-8000-000000000001',11,2026,1000000,100000,0,900000,'COMPLETED','a0000000-0000-0000-0000-000000000001',NOW(),NOW(),'PAYOUT-TEST-001')
ON CONFLICT (payoutid) DO NOTHING;

-- 4. Payout item linked to order
INSERT INTO payoutitems (payoutitemid, payoutid, orderid, grossamount, commissionrate, commissionamount, refundadjustment, taxamount, netamount, createdat, status, processedat, transactionref, gatewayref)
VALUES ('9fc00001-0000-4000-8000-000000000001','9fb00001-0000-4000-8000-000000000001','9f300001-0000-4000-8000-000000000001',1000000,0.10,100000,0,0,900000,NOW(),'COMPLETED',NOW(),'PAYOUT-ITEM-TEST-001','GW-PAYOUT-001')
ON CONFLICT (payoutitemid) DO NOTHING;