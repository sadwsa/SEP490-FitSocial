-- Kích hoạt extension hỗ trợ sinh UUID tự động (nếu bản PostgreSQL cũ)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ==============================================================================
-- PHẦN 1: TẠO BẢNG & KHÓA CHÍNH (PRIMARY KEYS), KHÓA ĐỘC NHẤT (UNIQUE KEYS)
-- ==============================================================================

-- 1. Users
CREATE TABLE "Users" (
    "UserID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Email" VARCHAR(320) UNIQUE NOT NULL,
    "PhoneNumber" VARCHAR(20) UNIQUE,
    "PasswordHash" VARCHAR(255),
    "GoogleProviderID" VARCHAR(255) UNIQUE,
    "FullName" VARCHAR(100),
    "RoleCode" VARCHAR(8),
    "IsInternal" BOOLEAN DEFAULT FALSE,
    "AvatarUrl" VARCHAR(2048),
    "DateOfBirth" DATE,
    "Gender" VARCHAR(6),
    "IsLocked" BOOLEAN DEFAULT FALSE,
    "LockedBy" UUID,
    "LastActiveAt" TIMESTAMP,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 2. CoachProfiles
CREATE TABLE "CoachProfiles" (
    "CoachID" UUID PRIMARY KEY,
    "ExperienceYears" INT,
    "Bio" TEXT,
    "IdentityCardUrl" VARCHAR(2048),
    "CertificateUrl" VARCHAR(2048),
    "ApprovalStatus" VARCHAR(50),
    "ApprovedBy" UUID,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 3. RefreshTokens
CREATE TABLE "RefreshTokens" (
    "TokenID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserID" UUID NOT NULL,
    "TokenHash" VARCHAR(255) UNIQUE NOT NULL,
    "ExpiresAt" TIMESTAMP NOT NULL,
    "RevokedAt" TIMESTAMP,
    "DeviceInfo" VARCHAR(255),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 4. OTPLogs
CREATE TABLE "OTPLogs" (
    "OtpID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserID" UUID,
    "OtpHash" VARCHAR(255) NOT NULL,
    "Email" VARCHAR(320),
    "ExpiresAt" TIMESTAMP NOT NULL,
    "VerifiedAt" TIMESTAMP,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "Purpose" VARCHAR(50),
    "AttemptCount" INT DEFAULT 0
);

-- 5. Locations
CREATE TABLE "Locations" (
    "LocationID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "LocationName" VARCHAR(200) NOT NULL,
    "Address" VARCHAR(200)
);

-- 6. Sports
CREATE TABLE "Sports" (
    "SportID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "SportName" VARCHAR(100) NOT NULL
);

-- 7. CoachLocations
CREATE TABLE "CoachLocations" (
    "CoachID" UUID NOT NULL,
    "LocationID" UUID NOT NULL,
    PRIMARY KEY ("CoachID", "LocationID")
);

-- 8. CoachSports
CREATE TABLE "CoachSports" (
    "CoachID" UUID NOT NULL,
    "SportID" UUID NOT NULL,
    PRIMARY KEY ("CoachID", "SportID")
);

-- 9. UserFavoriteSports (Gợi ý từ node tên UserSports)
CREATE TABLE "UserFavoriteSports" (
    "UserID" UUID NOT NULL,
    "SportID" UUID NOT NULL,
    PRIMARY KEY ("UserID", "SportID")
);

-- 10. CoachBankAccounts
CREATE TABLE "CoachBankAccounts" (
    "BankID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CoachID" UUID NOT NULL,
    "BankName" VARCHAR(255),
    "BankCode" VARCHAR(50),
    "AccountName" VARCHAR(255),
    "AccountNumber" VARCHAR(50),
    "Branch" VARCHAR(255),
    "IsDefault" BOOLEAN DEFAULT FALSE,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 11. Friendships
CREATE TABLE "Friendships" (
    "RequesterID" UUID NOT NULL,
    "AddresseeID" UUID NOT NULL,
    "Status" VARCHAR(20),
    "AcceptedAt" TIMESTAMP,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY ("RequesterID", "AddresseeID")
);

-- 12. userBlocks
CREATE TABLE "userBlocks" (
    "ID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "BlockerID" UUID NOT NULL,
    "BlockedID" UUID NOT NULL,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE ("BlockerID", "BlockedID")
);

-- 13. Reports
CREATE TABLE "Reports" (
    "ReportID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ReporterID" UUID,
    "ReportedUserID" UUID,
    "ReportedPostID" UUID,
    "Reason" VARCHAR(500),
    "Status" VARCHAR(30),
    "ResolvedBy" UUID,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "ResolvedAt" TIMESTAMP
);

-- 14. Posts
CREATE TABLE "Posts" (
    "ID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "AuthorID" UUID NOT NULL,
    "Content" TEXT,
    "PostType" VARCHAR(50) NOT NULL,
    "SportID" UUID,
    "LocationID" UUID,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "IsDeleted" BOOLEAN DEFAULT FALSE
);

-- 15. PostMedia
CREATE TABLE "PostMedia" (
    "ID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "PostID" UUID NOT NULL,
    "MediaURL" VARCHAR(150),
    "MediaType" VARCHAR(50), -- Sửa lỗi BOOLEAN(12) trong JSON
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 16. PostInteractions
CREATE TABLE "PostInteractions" (
    "PostID" UUID NOT NULL,
    "UserID" UUID NOT NULL,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY ("PostID", "UserID")
);

-- 17. Comments
CREATE TABLE "Comments" (
    "ID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "PostID" UUID NOT NULL,
    "AuthorID" UUID NOT NULL,
    "ParentCommentID" UUID,
    "Content" VARCHAR(180),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 18. Conversations
CREATE TABLE "Conversations" (
    "ID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CreatorID" UUID,
    "Type" VARCHAR(10),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "IsDeleted" BOOLEAN DEFAULT FALSE
);

-- 19. Participants
CREATE TABLE "Participants" (
    "ConversationID" UUID NOT NULL,
    "UserID" UUID NOT NULL,
    "JoinedAT" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "LastReadMessageID" UUID, -- Chuẩn hóa từ LastReadMasageID
    "HistoryDeletedAt" TIMESTAMP,
    "Status" VARCHAR(7),
    PRIMARY KEY ("ConversationID", "UserID")
);

-- 20. Messages
CREATE TABLE "Messages" (
    "ID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ConversationID" UUID NOT NULL,
    "SenderID" UUID NOT NULL,
    "Content" VARCHAR(300),
    "MessageType" VARCHAR(5),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 21. VideoCalls
CREATE TABLE "VideoCalls" (
    "MessageID" UUID PRIMARY KEY,
    "DurationSeconds" INTEGER,
    "CallStatus" VARCHAR(20)
);

-- 22. Notifications
CREATE TABLE "Notifications" (
    "ID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserID" UUID NOT NULL,
    "Type" VARCHAR(50), -- Sửa lỗi TIMESTAMP(20) trong JSON
    "ReferenceID" UUID,
    "IsRead" BOOLEAN DEFAULT FALSE,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 23. BodyMetricLog
CREATE TABLE "BodyMetricLog" (
    "BodyMetricLogID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "TraineeID" UUID NOT NULL,
    "LogDate" TIMESTAMP NOT NULL,
    "BodyWeightKg" NUMERIC(5,2),
    "CalorieIntake" INT,
    "Notes" VARCHAR,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 24. BodyMeasurementDetail
CREATE TABLE "BodyMeasurementDetail" (
    "BodyMeasurementDetailID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "BodyMetricLogID" UUID NOT NULL,
    "MeasurementType" VARCHAR(200),
    "ValueCm" NUMERIC(5,2)
);

-- 25. Orders
CREATE TABLE "Orders" (
    "OrderID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "TraineeID" UUID NOT NULL,
    "CoachID" UUID,
    "TotalAmount" NUMERIC(18,2),
    "OrderStatus" VARCHAR(50),
    "OrderType" VARCHAR(50),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 26. OrderDetails
CREATE TABLE "OrderDetails" (
    "OrderDetailsID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "OrderID" UUID NOT NULL,
    "PackagePrice" NUMERIC(18,2),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 27. Payments
CREATE TABLE "Payments" (
    "PaymentID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "OrderID" UUID NOT NULL,
    "Amount" NUMERIC(18,2),
    "Currency" VARCHAR(10),
    "Method" VARCHAR(50),
    "GatewayID" VARCHAR(50),
    "TransactionRef" VARCHAR(100),
    "GatewayTransactionID" VARCHAR(100),
    "Status" VARCHAR(20),
    "FailureReason" TEXT,
    "ProcessedAt" TIMESTAMP,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 28. PaymentGatewayConfigs
CREATE TABLE "PaymentGatewayConfigs" (
    "GatewayID" SERIAL PRIMARY KEY,
    "GatewayName" VARCHAR(50),
    "ClientID" VARCHAR(255),
    "EncryptedApiKey" BYTEA,
    "EncryptedChecksumKey" BYTEA,
    "WebhookUrl" VARCHAR(2048),
    "IsActive" BOOLEAN DEFAULT TRUE,
    "UpdatedBy" UUID,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 29. TrainingPackages
CREATE TABLE "TrainingPackages" (
    "PackageID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CoachID" UUID NOT NULL,
    "Title" VARCHAR(255),
    "Price" NUMERIC(18,2),
    "DurationDays" INTEGER,
    "IsActive" BOOLEAN DEFAULT TRUE,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "OrderDetailsID" UUID
);


-- 30. Carts (BẢNG MỚI ĐƯỢC THÊM VÀO)
CREATE TABLE "Carts" (
    "CartID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserID" UUID NOT NULL,
    "PackageID" UUID NOT NULL,
    "Quantity" INT NOT NULL DEFAULT 1,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE ("UserID", "PackageID") -- Đảm bảo một người dùng không lưu lặp lại 1 gói tập trong giỏ
);

-- 30. MealPlan
CREATE TABLE "MealPlan" (
    "MealPlanID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CoachID" UUID NOT NULL,
    "OrderDetailsID" UUID,
    "Title" VARCHAR(200),
    "Description" VARCHAR(500),
    "StartDate" TIMESTAMP,
    "EndDate" TIMESTAMP,
    "Status" VARCHAR(30),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "PublishedAt" TIMESTAMP
);

-- 31. MealPlanItem
CREATE TABLE "MealPlanItem" (
    "MealPlanItemID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "MealPlanID" UUID NOT NULL,
    "WeekNumber" SMALLINT,
    "DayNumber" SMALLINT,
    "MealType" VARCHAR(100),
    "FoodDescription" VARCHAR(500),
    "Calories" INT,
    "SortOrder" SMALLINT
);

-- 32. TrainingPlan
CREATE TABLE "TrainingPlan" (
    "TrainingPlanID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CoachID" UUID NOT NULL,
    "OrderDetailsID" UUID,
    "Title" VARCHAR(500),
    "Description" VARCHAR(500),
    "StartDate" TIMESTAMP,
    "EndDate" TIMESTAMP,
    "Status" VARCHAR(30),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "PublishedAt" TIMESTAMP
);

-- 33. VideoTutorial
CREATE TABLE "VideoTutorial" (
    "VideoTutorialID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CoachID" UUID NOT NULL,
    "Title" VARCHAR(200),
    "Description" VARCHAR(500),
    "VideoUrl" VARCHAR(500),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 34. TrainingPlanExercise
CREATE TABLE "TrainingPlanExercise" (
    "TrainingPlanExerciseID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "TrainingPlanID" UUID NOT NULL,
    "VideoTutorialID" UUID,
    "WeekNumber" SMALLINT,
    "DayNumber" SMALLINT,
    "ExerciseName" VARCHAR(200),
    "Sets" SMALLINT,
    "Reps" SMALLINT,
    "DurationMinutes" SMALLINT,
    "RestSeconds" SMALLINT,
    "Notes" VARCHAR(500),
    "SortOrder" SMALLINT
);

-- 35. WorkoutCompletionLog
CREATE TABLE "WorkoutCompletionLog" (
    "WorkoutCompletionLogID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "TraineeID" UUID NOT NULL,
    "TrainingPlanExerciseID" UUID NOT NULL,
    "LogDate" TIMESTAMP NOT NULL,
    "Status" VARCHAR(30),
    "Notes" VARCHAR(300),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 36. Price
CREATE TABLE "Price" (
    "PriceID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Amount" NUMERIC(18,2),
    "Currency" VARCHAR(10),
    "IsActive" BOOLEAN DEFAULT TRUE,
    "ImageUrl" VARCHAR(2048),
    "Description" VARCHAR(500),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 37. CoachUpgrades
CREATE TABLE "CoachUpgrades" (
    "UpgradeID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "PriceID" UUID NOT NULL,
    "CoachID" UUID NOT NULL,
    "OrderID" UUID UNIQUE NOT NULL,
    "Status" VARCHAR(50),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 38. Payouts
CREATE TABLE "Payouts" (
    "PayoutID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CoachID" UUID NOT NULL,
    "PayoutMonth" INTEGER NOT NULL,
    "PayoutYear" INTEGER NOT NULL,
    "TotalGrossAmount" NUMERIC(18,2),
    "SystemCommissionAmount" NUMERIC(18,2),
    "TaxAmount" NUMERIC(18,2),
    "NetPayoutAmount" NUMERIC(18,2),
    "Status" VARCHAR(50),
    "ProcessedBy" UUID,
    "ProcessedAt" TIMESTAMP,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE ("CoachID", "PayoutMonth", "PayoutYear")
);

-- 39. PayoutItems
CREATE TABLE "PayoutItems" (
    "PayoutItemID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "PayoutID" UUID NOT NULL,
    "OrderID" UUID NOT NULL,
    "GrossAmount" NUMERIC(18,2),
    "CommissionRate" NUMERIC(5,2),
    "CommissionAmount" NUMERIC(18,2),
    "RefundAdjustment" NUMERIC(18,2),
    "TaxAmount" NUMERIC(18,2),
    "NetAmount" NUMERIC(18,2),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 40. Reviews
CREATE TABLE "Reviews" (
    "ReviewID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "TraineeID" UUID NOT NULL,
    "CoachID" UUID NOT NULL,
    "Rating" INTEGER,
    "Comment" TEXT,
    "Reply" TEXT,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 41. AuditLogs
CREATE TABLE "AuditLogs" (
    "AuditLogID" BIGSERIAL PRIMARY KEY,
    "ActorAccountID" UUID,
    "Action" VARCHAR(100),
    "EntityType" VARCHAR(100),
    "EntityID" VARCHAR(100),
    "OldValue" JSONB,
    "NewValue" JSONB,
    "IPAddress" VARCHAR(45),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ==============================================================================
-- PHẦN 2: KHAI BÁO RÀNG BUỘC KHÓA NGOẠI (FOREIGN KEYS)
-- ==============================================================================

ALTER TABLE "Users" ADD FOREIGN KEY ("LockedBy") REFERENCES "Users"("UserID");

ALTER TABLE "CoachProfiles" ADD FOREIGN KEY ("CoachID") REFERENCES "Users"("UserID");
ALTER TABLE "CoachProfiles" ADD FOREIGN KEY ("ApprovedBy") REFERENCES "Users"("UserID");

ALTER TABLE "RefreshTokens" ADD FOREIGN KEY ("UserID") REFERENCES "Users"("UserID") ON DELETE CASCADE;

ALTER TABLE "OTPLogs" ADD FOREIGN KEY ("UserID") REFERENCES "Users"("UserID") ON DELETE CASCADE;

ALTER TABLE "CoachLocations" ADD FOREIGN KEY ("CoachID") REFERENCES "CoachProfiles"("CoachID") ON DELETE CASCADE;
ALTER TABLE "CoachLocations" ADD FOREIGN KEY ("LocationID") REFERENCES "Locations"("LocationID") ON DELETE CASCADE;

ALTER TABLE "CoachSports" ADD FOREIGN KEY ("CoachID") REFERENCES "CoachProfiles"("CoachID") ON DELETE CASCADE;
ALTER TABLE "CoachSports" ADD FOREIGN KEY ("SportID") REFERENCES "Sports"("SportID") ON DELETE CASCADE;

ALTER TABLE "UserFavoriteSports" ADD FOREIGN KEY ("UserID") REFERENCES "Users"("UserID") ON DELETE CASCADE;
ALTER TABLE "UserFavoriteSports" ADD FOREIGN KEY ("SportID") REFERENCES "Sports"("SportID") ON DELETE CASCADE;

ALTER TABLE "CoachBankAccounts" ADD FOREIGN KEY ("CoachID") REFERENCES "CoachProfiles"("CoachID") ON DELETE CASCADE;

ALTER TABLE "Friendships" ADD FOREIGN KEY ("RequesterID") REFERENCES "Users"("UserID") ON DELETE CASCADE;
ALTER TABLE "Friendships" ADD FOREIGN KEY ("AddresseeID") REFERENCES "Users"("UserID") ON DELETE CASCADE;

ALTER TABLE "userBlocks" ADD FOREIGN KEY ("BlockerID") REFERENCES "Users"("UserID") ON DELETE CASCADE;
ALTER TABLE "userBlocks" ADD FOREIGN KEY ("BlockedID") REFERENCES "Users"("UserID") ON DELETE CASCADE;

ALTER TABLE "Reports" ADD FOREIGN KEY ("ReporterID") REFERENCES "Users"("UserID");
ALTER TABLE "Reports" ADD FOREIGN KEY ("ReportedUserID") REFERENCES "Users"("UserID");
ALTER TABLE "Reports" ADD FOREIGN KEY ("ReportedPostID") REFERENCES "Posts"("ID");
ALTER TABLE "Reports" ADD FOREIGN KEY ("ResolvedBy") REFERENCES "Users"("UserID");

ALTER TABLE "Posts" ADD FOREIGN KEY ("AuthorID") REFERENCES "Users"("UserID") ON DELETE CASCADE;
ALTER TABLE "Posts" ADD FOREIGN KEY ("SportID") REFERENCES "Sports"("SportID");
ALTER TABLE "Posts" ADD FOREIGN KEY ("LocationID") REFERENCES "Locations"("LocationID");

ALTER TABLE "PostMedia" ADD FOREIGN KEY ("PostID") REFERENCES "Posts"("ID") ON DELETE CASCADE;

ALTER TABLE "PostInteractions" ADD FOREIGN KEY ("PostID") REFERENCES "Posts"("ID") ON DELETE CASCADE;
ALTER TABLE "PostInteractions" ADD FOREIGN KEY ("UserID") REFERENCES "Users"("UserID") ON DELETE CASCADE;

ALTER TABLE "Comments" ADD FOREIGN KEY ("PostID") REFERENCES "Posts"("ID") ON DELETE CASCADE;
ALTER TABLE "Comments" ADD FOREIGN KEY ("AuthorID") REFERENCES "Users"("UserID") ON DELETE CASCADE;
ALTER TABLE "Comments" ADD FOREIGN KEY ("ParentCommentID") REFERENCES "Comments"("ID") ON DELETE CASCADE;

-- KHÓA NGOẠI DÀNH CHO BẢNG CARTS MỚI
ALTER TABLE "Carts" ADD FOREIGN KEY ("UserID") REFERENCES "Users"("UserID") ON DELETE CASCADE;
ALTER TABLE "Carts" ADD FOREIGN KEY ("PackageID") REFERENCES "TrainingPackages"("PackageID") ON DELETE CASCADE;

ALTER TABLE "Conversations" ADD FOREIGN KEY ("CreatorID") REFERENCES "Users"("UserID") ON DELETE SET NULL;

ALTER TABLE "Participants" ADD FOREIGN KEY ("ConversationID") REFERENCES "Conversations"("ID") ON DELETE CASCADE;
ALTER TABLE "Participants" ADD FOREIGN KEY ("UserID") REFERENCES "Users"("UserID") ON DELETE CASCADE;

ALTER TABLE "Messages" ADD FOREIGN KEY ("ConversationID") REFERENCES "Conversations"("ID") ON DELETE CASCADE;
ALTER TABLE "Messages" ADD FOREIGN KEY ("SenderID") REFERENCES "Users"("UserID") ON DELETE CASCADE;

ALTER TABLE "VideoCalls" ADD FOREIGN KEY ("MessageID") REFERENCES "Messages"("ID") ON DELETE CASCADE;

ALTER TABLE "Notifications" ADD FOREIGN KEY ("UserID") REFERENCES "Users"("UserID") ON DELETE CASCADE;

ALTER TABLE "BodyMetricLog" ADD FOREIGN KEY ("TraineeID") REFERENCES "Users"("UserID") ON DELETE CASCADE;

ALTER TABLE "BodyMeasurementDetail" ADD FOREIGN KEY ("BodyMetricLogID") REFERENCES "BodyMetricLog"("BodyMetricLogID") ON DELETE CASCADE;

ALTER TABLE "Orders" ADD FOREIGN KEY ("TraineeID") REFERENCES "Users"("UserID");
ALTER TABLE "Orders" ADD FOREIGN KEY ("CoachID") REFERENCES "CoachProfiles"("CoachID");

ALTER TABLE "OrderDetails" ADD FOREIGN KEY ("OrderID") REFERENCES "Orders"("OrderID") ON DELETE CASCADE;

ALTER TABLE "Payments" ADD FOREIGN KEY ("OrderID") REFERENCES "Orders"("OrderID") ON DELETE CASCADE;

ALTER TABLE "PaymentGatewayConfigs" ADD FOREIGN KEY ("UpdatedBy") REFERENCES "Users"("UserID");

ALTER TABLE "TrainingPackages" ADD FOREIGN KEY ("CoachID") REFERENCES "CoachProfiles"("CoachID");
ALTER TABLE "TrainingPackages" ADD FOREIGN KEY ("OrderDetailsID") REFERENCES "OrderDetails"("OrderDetailsID");

ALTER TABLE "MealPlan" ADD FOREIGN KEY ("CoachID") REFERENCES "CoachProfiles"("CoachID");
ALTER TABLE "MealPlan" ADD FOREIGN KEY ("OrderDetailsID") REFERENCES "OrderDetails"("OrderDetailsID");

ALTER TABLE "MealPlanItem" ADD FOREIGN KEY ("MealPlanID") REFERENCES "MealPlan"("MealPlanID") ON DELETE CASCADE;

ALTER TABLE "TrainingPlan" ADD FOREIGN KEY ("CoachID") REFERENCES "CoachProfiles"("CoachID");
ALTER TABLE "TrainingPlan" ADD FOREIGN KEY ("OrderDetailsID") REFERENCES "OrderDetails"("OrderDetailsID");

ALTER TABLE "VideoTutorial" ADD FOREIGN KEY ("CoachID") REFERENCES "CoachProfiles"("CoachID") ON DELETE CASCADE;

ALTER TABLE "TrainingPlanExercise" ADD FOREIGN KEY ("TrainingPlanID") REFERENCES "TrainingPlan"("TrainingPlanID") ON DELETE CASCADE;
ALTER TABLE "TrainingPlanExercise" ADD FOREIGN KEY ("VideoTutorialID") REFERENCES "VideoTutorial"("VideoTutorialID") ON DELETE SET NULL;

ALTER TABLE "WorkoutCompletionLog" ADD FOREIGN KEY ("TraineeID") REFERENCES "Users"("UserID");
ALTER TABLE "WorkoutCompletionLog" ADD FOREIGN KEY ("TrainingPlanExerciseID") REFERENCES "TrainingPlanExercise"("TrainingPlanExerciseID");

ALTER TABLE "CoachUpgrades" ADD FOREIGN KEY ("PriceID") REFERENCES "Price"("PriceID");
ALTER TABLE "CoachUpgrades" ADD FOREIGN KEY ("CoachID") REFERENCES "CoachProfiles"("CoachID");
ALTER TABLE "CoachUpgrades" ADD FOREIGN KEY ("OrderID") REFERENCES "Orders"("OrderID");

ALTER TABLE "Payouts" ADD FOREIGN KEY ("CoachID") REFERENCES "CoachProfiles"("CoachID");
ALTER TABLE "Payouts" ADD FOREIGN KEY ("ProcessedBy") REFERENCES "Users"("UserID");

ALTER TABLE "PayoutItems" ADD FOREIGN KEY ("PayoutID") REFERENCES "Payouts"("PayoutID") ON DELETE CASCADE;
-- Mặc dù JSON không nói rõ nhưng OrderID ở PayoutItems thường tham chiếu về Orders
ALTER TABLE "PayoutItems" ADD FOREIGN KEY ("OrderID") REFERENCES "Orders"("OrderID");

ALTER TABLE "Reviews" ADD FOREIGN KEY ("TraineeID") REFERENCES "Users"("UserID");
ALTER TABLE "Reviews" ADD FOREIGN KEY ("CoachID") REFERENCES "CoachProfiles"("CoachID");

ALTER TABLE "AuditLogs" ADD FOREIGN KEY ("ActorAccountID") REFERENCES "Users"("UserID") ON DELETE SET NULL;



-- ==============================================================================
-- 1. BẢNG QUẢN LÝ ĐIỀU KHOẢN VÀ SỰ ĐỒNG Ý (TERMS & AGREEMENTS)
-- ==============================================================================

CREATE TABLE "TermsAndPolicies" (
    "TermID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Version" VARCHAR(50) NOT NULL UNIQUE,
    "Title" VARCHAR(255) NOT NULL,
    "Content" TEXT NOT NULL,
    "EffectiveDate" TIMESTAMP NOT NULL,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE "UserAgreements" (
    "AgreementID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserID" UUID NOT NULL,
    "TermID" UUID NOT NULL,
    "IpAddress" VARCHAR(45),
    "AcceptedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "UQ_User_Term" UNIQUE ("UserID", "TermID")
);

ALTER TABLE "UserAgreements" ADD FOREIGN KEY ("UserID") REFERENCES "Users"("UserID") ON DELETE CASCADE;
ALTER TABLE "UserAgreements" ADD FOREIGN KEY ("TermID") REFERENCES "TermsAndPolicies"("TermID") ON DELETE CASCADE;


-- ==============================================================================
-- 2. BẢNG LƯU TRỮ DỮ LIỆU eKYC (VIETTEL eKYC & OCR & LIVENESS)
-- ==============================================================================

CREATE TABLE "CoachEkycVerifications" (
    "EkycID" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CoachID" UUID NOT NULL,
    "IdCardNumber" VARCHAR(20) UNIQUE,
    "FullNameOnCard" VARCHAR(100),
    "DateOfBirthOnCard" DATE,
    "Birthplace" VARCHAR(255),
    "Sex" VARCHAR(20),
    "Address" VARCHAR(500),
    "Province" VARCHAR(100),
    "District" VARCHAR(100),
    "Ward" VARCHAR(100),
    "ProvinceCode" VARCHAR(10),
    "DistrictCode" VARCHAR(10),
    "WardCode" VARCHAR(10),
    "Street" VARCHAR(255),
    "Nationality" VARCHAR(50),
    "Religion" VARCHAR(50),
    "Ethnicity" VARCHAR(50),
    "Expiry" VARCHAR(20),
    "Feature" VARCHAR(500),
    "IssueDate" VARCHAR(20),
    "IssueBy" VARCHAR(255),
    "DocumentType" VARCHAR(50),
    "RawInformationJson" JSONB,
    "FrontCardUrl" VARCHAR(2048) NOT NULL,
    "BackCardUrl" VARCHAR(2048) NOT NULL,
    "FaceImageUrl" VARCHAR(2048),
    "LivenessScore" NUMERIC(5,2),
    "FaceMatchConfidence" NUMERIC(5,2),
    "VerificationStatus" VARCHAR(50) DEFAULT 'Pending', -- Pending, Success, Failed
    "FailureReason" TEXT,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE "CoachEkycVerifications" ADD FOREIGN KEY ("CoachID") REFERENCES "CoachProfiles"("CoachID") ON DELETE CASCADE;


-- ==============================================================================
-- 3. MỞ RỘNG BẢNG THANH TOÁN (HỖ TRỢ LƯU LOG KHI TÍCH HỢP MOMO & PAYOS)
-- ==============================================================================

ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "GatewayResponseRaw" JSONB;


-- 1. Thêm cột tạm thời kiểu INT
ALTER TABLE "Payments" ADD COLUMN "TempGatewayID" INT;

-- 2. Map dữ liệu cũ sang ID số (Ví dụ gán ID cho PayOS là 1, MoMo là 2...)
UPDATE "Payments" SET "TempGatewayID" = 1 WHERE "GatewayID" = 'PAYOS';
UPDATE "Payments" SET "TempGatewayID" = 2 WHERE "GatewayID" = 'MOMO';
-- (Cập nhật tiếp cho các giá trị khác nếu có)

-- 3. Xóa cột GatewayID cũ và đổi tên cột tạm thành GatewayID
ALTER TABLE "Payments" DROP COLUMN "GatewayID";
ALTER TABLE "Payments" RENAME COLUMN "TempGatewayID" TO "GatewayID";

-- 4. Thêm ràng buộc khóa ngoại chuẩn
ALTER TABLE "Payments" ADD FOREIGN KEY ("GatewayID") REFERENCES "PaymentGatewayConfigs"("GatewayID");