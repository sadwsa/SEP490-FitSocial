-- ============================================================
-- Seed data for testing:
--   1) Mark Daily Workout
--   2) View Training Progress
--   3) Monitor Trainee Workout Progress
--
-- Test accounts (password for both = "Test@123"):
--   Coach   : coach_rating_test_2026@fitsocial.vn
--   Trainee : trainee_rating_test_2026@fitsocial.vn
--
-- Key IDs:
--   Coach user id      : 9f000001-0000-4000-8000-000000000001
--   Trainee user id    : 9f100001-0000-4000-8000-000000000001
--   Package id         : 9f200001-0000-4000-8000-000000000001
--   Order id           : 9f300001-0000-4000-8000-000000000001
--   OrderDetail id     : 9f400001-0000-4000-8000-000000000001
--   TrainingPlan id    : 9f700001-0000-4000-8000-000000000001
--   Exercise ids       : 9f800001..., 9f800002..., 9f800003...
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
VALUES ('9f200001-0000-4000-8000-000000000001','9f000001-0000-4000-8000-000000000001','Gói Test Workout','Gói tập để test workout',1000000,30,12,18,'Mọi người',TRUE,NOW(),'BOTH','BOTH',0,0)
ON CONFLICT (packageid) DO NOTHING;

-- 5. Paid order
INSERT INTO orders (orderid, buyerid, coachid, totalamount, orderstatus, ordertype, createdat)
VALUES ('9f300001-0000-4000-8000-000000000001','9f100001-0000-4000-8000-000000000001','9f000001-0000-4000-8000-000000000001',1000000,'PAID','PACKAGE',NOW())
ON CONFLICT (orderid) DO NOTHING;

-- 6. Order detail
INSERT INTO orderdetails (orderdetailsid, orderid, packageid, packageprice, packagetitle, packagedurationdays, coachname, createdat, "PackageType", packagetype)
VALUES ('9f400001-0000-4000-8000-000000000001','9f300001-0000-4000-8000-000000000001','9f200001-0000-4000-8000-000000000001',1000000,'Gói Test Workout',30,'Coach Rating Test',NOW(),'BOTH','BOTH')
ON CONFLICT (orderdetailsid) DO NOTHING;

-- 7. Training plan (ACTIVE) linked to order detail
INSERT INTO trainingplan (trainingplanid, coachid, orderdetailsid, title, description, startdate, enddate, status, publishedat, createdat, updatedat)
VALUES ('9f700001-0000-4000-8000-000000000001','9f000001-0000-4000-8000-000000000001','9f400001-0000-4000-8000-000000000001',
 'Kế hoạch tập tuần 1','Kế hoạch mẫu để test workout',NOW(),NOW()+INTERVAL '30 days','ACTIVE',NOW(),NOW(),NOW())
ON CONFLICT (trainingplanid) DO NOTHING;

-- 8. Exercises (3 items) in week 1
INSERT INTO trainingplanexercise (trainingplanexerciseid, trainingplanid, weeknumber, daynumber, exercisename, sets, reps, durationminutes, restseconds, sortorder, notes)
VALUES
 ('9f800001-0000-4000-8000-000000000001','9f700001-0000-4000-8000-000000000001',1,1,'Push-up',3,12,10,60,1,'Tay rộng'),
 ('9f800002-0000-4000-8000-000000000002','9f700001-0000-4000-8000-000000000001',1,1,'Squat',4,15,12,60,2,'Chân rộng'),
 ('9f800003-0000-4000-8000-000000000003','9f700001-0000-4000-8000-000000000001',1,2,'Plank',3,1,5,30,3,'Giữ tư thế')
ON CONFLICT (trainingplanexerciseid) DO NOTHING;

-- 9. One completed workout log for first exercise (to test progress)
INSERT INTO workoutcompletionlog (workoutcompletionlogid, traineeid, trainingplanexerciseid, logdate, status, notes, createdat)
VALUES ('9f900001-0000-4000-8000-000000000001','9f100001-0000-4000-8000-000000000001','9f800001-0000-4000-8000-000000000001',NOW(),'COMPLETED','Hoàn thành tốt',NOW())
ON CONFLICT (workoutcompletionlogid) DO NOTHING;