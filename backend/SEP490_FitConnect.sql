-- ==========================================
-- SETUP: Cài đặt extension để tự động sinh UUID
-- ==========================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ==========================================
-- 1. TẠO CÁC BẢNG ĐỘC LẬP (BẢNG CHA)
-- ==========================================


CREATE TABLE Locations (
    LocationID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    LocationName VARCHAR(200) NOT NULL,
    Address VARCHAR(200)
);

CREATE TABLE TermsAndPolicies (
    TermID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    Version VARCHAR(50),
    Title VARCHAR(255),
    Content TEXT,
    EffectiveDate TIMESTAMP,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE PaymentGatewayConfigs (
    GatewayID UUID PRIMARY KEY DEFAULT uuid_generate_v4(), 
    GatewayName VARCHAR(50),
    ClientID VARCHAR(255),
    EncryptedApiKey BYTEA,          -- Đã có tiền tố Encrypted
    EncryptedChecksumKey BYTEA,     -- Đã có tiền tố Encrypted
    WebhookUrl VARCHAR(2048),
    IsActive BOOLEAN DEFAULT TRUE,
    UpdatedBy UUID, 
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Price (
    PriceID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    Amount NUMERIC(18,2) NOT NULL,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ==========================================
-- 2. TẠO BẢNG USERS VÀ CÁC THỰC THỂ PROFILE
-- ==========================================

CREATE TABLE Users (
    UserID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    Email VARCHAR(320) UNIQUE NOT NULL,
    PhoneNumber VARCHAR(20) UNIQUE,
    PasswordHash VARCHAR(255),
    GoogleProviderID VARCHAR(255) UNIQUE,
    FullName VARCHAR(100),
    RoleCode VARCHAR(8),
    IsInternal BOOLEAN DEFAULT FALSE,
    AvatarUrl VARCHAR(2048),
    DateOfBirth DATE,
    Gender VARCHAR(6),
    IsLocked BOOLEAN DEFAULT FALSE,
    LockedBy UUID REFERENCES Users(UserID),
    LastActiveAt TIMESTAMP,
    TokenVersion INT DEFAULT 1,
    WarningCount INT DEFAULT 0,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE PaymentGatewayConfigs ADD CONSTRAINT fk_gateway_updatedby FOREIGN KEY (UpdatedBy) REFERENCES Users(UserID);

CREATE TABLE CoachProfiles (
    CoachID UUID PRIMARY KEY REFERENCES Users(UserID),
    ExperienceYears INT,
    Bio TEXT,
    IdentityCardUrl VARCHAR(2048),
    CertificateUrl VARCHAR(2048),
    ApprovalStatus VARCHAR(50),
    ApprovedBy UUID REFERENCES Users(UserID),
    Status VARCHAR(20),
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ==========================================
-- 3. TẠO CÁC BẢNG BẢO MẬT: EKYC, TÀI KHOẢN, CHỨNG CHỈ
-- ==========================================

CREATE TABLE CoachBankAccounts (
    BankID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    CoachID UUID REFERENCES CoachProfiles(CoachID),
    BankName VARCHAR(255),
    BankCode VARCHAR(50),
    AccountName VARCHAR(255),
    EncryptedAccountNumber BYTEA, -- Đã có tiền tố Encrypted
    Branch VARCHAR(255),
    IsDefault BOOLEAN DEFAULT FALSE,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE CoachEkycVerifications (
    EkycID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    CoachID UUID REFERENCES CoachProfiles(CoachID),
    EncryptedIdCardNumber BYTEA,         -- Đã có tiền tố Encrypted
    FullNameOnCard VARCHAR(100),
    DateOfBirthOnCard DATE,
    Sex VARCHAR(10),
    Nationality VARCHAR(50),
    Ethnicity VARCHAR(50),
    Religion VARCHAR(50),
    Birthplace VARCHAR(255),
    Address VARCHAR(500),
    Province VARCHAR(100),
    District VARCHAR(100),
    Ward VARCHAR(100),
    ProvinceCode VARCHAR(10),
    DistrictCode VARCHAR(10),
    WardCode VARCHAR(10),
    Street VARCHAR(255),
    DocumentType VARCHAR(50),
    IssueDate VARCHAR(20),
    Expiry VARCHAR(20),
    IssueBy TEXT,
    Feature TEXT,
    FrontCardUrl VARCHAR(2048), 
    BackCardUrl VARCHAR(2048),
    FaceImageUrl VARCHAR(2048),
    LivenessScore NUMERIC(5,2),
    FaceMatchConfidence NUMERIC(5,2),
    VerificationStatus VARCHAR(50),
    EncryptedRawInformationJson BYTEA,   -- [MỚI CẬP NHẬT] Đã thêm tiền tố Encrypted
    FailureReason TEXT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE CoachCertificates (
    CertificateID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    CoachID UUID REFERENCES CoachProfiles(CoachID),
    CertificateName VARCHAR(255),
    CertificateUrl VARCHAR(2048),
    IssuedBy VARCHAR(255),
    IssuedDate DATE,
    ExpiryDate DATE,
    VerificationStatus VARCHAR(50),
    VerifiedBy UUID REFERENCES Users(UserID),
    VerifiedAt TIMESTAMP,
    RejectedReason TEXT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE CoachLocations (
    CoachID UUID REFERENCES CoachProfiles(CoachID),
    LocationID UUID REFERENCES Locations(LocationID),
    PRIMARY KEY (CoachID, LocationID)
);

-- ==========================================
-- 4. TẠO CÁC BẢNG LOG VÀ USER ACTIVITY
-- ==========================================

CREATE TABLE RefreshTokens (
    TokenID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    UserID UUID REFERENCES Users(UserID),
    TokenHash VARCHAR(255) UNIQUE,
    DeviceInfo VARCHAR(255),
    ExpiresAt TIMESTAMP,
    RevokedAt TIMESTAMP,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE OTPLogs (
    OtpID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    UserID UUID REFERENCES Users(UserID),
    Email VARCHAR(320),
    OtpHash VARCHAR(255),
    Purpose VARCHAR(50),
    AttemptCount INT DEFAULT 0,
    ExpiresAt TIMESTAMP,
    VerifiedAt TIMESTAMP,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE UserAgreements (
    AgreementID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    UserID UUID REFERENCES Users(UserID),
    TermID UUID REFERENCES TermsAndPolicies(TermID),
    IpAddress VARCHAR(45),
    AcceptedAt TIMESTAMP
);

CREATE TABLE UserBlocks (
    ID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    BlockerID UUID REFERENCES Users(UserID),
    BlockedID UUID REFERENCES Users(UserID),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(BlockerID, BlockedID)
);

-- ==========================================
-- 5. TẠO CÁC BẢNG SOCIAL, MẠNG XÃ HỘI, CHAT VÀ REPORT
-- ==========================================

CREATE TABLE Posts (
    ID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    AuthorID UUID REFERENCES Users(UserID),
    LocationID UUID REFERENCES Locations(LocationID),
    Content TEXT,
    IsDeleted BOOLEAN DEFAULT FALSE,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE PostMedia (
    ID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    PostID UUID REFERENCES Posts(ID),
    MediaURL VARCHAR(2048),
    MediaType VARCHAR(20), 
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Comments (
    ID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    PostID UUID REFERENCES Posts(ID),
    AuthorID UUID REFERENCES Users(UserID),
    ParentCommentID UUID REFERENCES Comments(ID),
    Content VARCHAR(500),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE PostInteractions (
    PostID UUID REFERENCES Posts(ID),
    UserID UUID REFERENCES Users(UserID),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (PostID, UserID)
);

CREATE TABLE Conversations (
    ConversationID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    User1ID UUID REFERENCES Users(UserID),
    User2ID UUID REFERENCES Users(UserID),
    LastMessageContent TEXT,
    LastMessageSenderID UUID REFERENCES Users(UserID),
    LastMessageAt TIMESTAMP,
    User1DeletedHistoryAt TIMESTAMP,
    User2DeletedHistoryAt TIMESTAMP,
    User1LastReadMessageID UUID, 
    User2LastReadMessageID UUID,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Messages (
    MessageID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ConversationID UUID REFERENCES Conversations(ConversationID),
    SenderID UUID REFERENCES Users(UserID),
    Content TEXT,
    MessageType VARCHAR(50),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE Conversations ADD CONSTRAINT fk_user1_lastmsg FOREIGN KEY (User1LastReadMessageID) REFERENCES Messages(MessageID);
ALTER TABLE Conversations ADD CONSTRAINT fk_user2_lastmsg FOREIGN KEY (User2LastReadMessageID) REFERENCES Messages(MessageID);

CREATE TABLE MessageAttachments (
    AttachmentID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    MessageID UUID REFERENCES Messages(MessageID),
    MediaUrl VARCHAR(2048),
    ThumbnailUrl VARCHAR(2048),
    MediaType VARCHAR(50),
    FileSize BIGINT,
    DurationSeconds INT,
    Width INT,
    Height INT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Reports (
    ReportID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ReporterID UUID REFERENCES Users(UserID),
    ReportedUserID UUID REFERENCES Users(UserID),
    ReportedPostID UUID REFERENCES Posts(ID),
    Type VARCHAR(30),
    Reason VARCHAR(500),
    Description VARCHAR(500),
    Status VARCHAR(30),
    ResolvedBy UUID REFERENCES Users(UserID),
    ResolvedAt TIMESTAMP,
    AppealStatus VARCHAR(30),
    AppealContent TEXT,
    AppealedAt TIMESTAMP,
    AppealReviewedBy UUID REFERENCES Users(UserID),
    AppealReviewedAt TIMESTAMP,
    AppealReviewNote TEXT,
    NotifiedReportedUserAt TIMESTAMP,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE ReportMedia (
    MediaID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ReportID UUID REFERENCES Reports(ReportID),
    MediaUrl VARCHAR(2048),   
    MediaType VARCHAR(10),    
    MediaFor VARCHAR(10),     
    SortOrder SMALLINT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ==========================================
-- 6. TẠO CÁC BẢNG E-COMMERCE: GÓI TẬP, ORDER, PAYMENT
-- ==========================================

CREATE TABLE CoachSubscriptionPlans (
    CoachSubscriptionPlansID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    PriceID UUID REFERENCES Price(PriceID),
    Amount NUMERIC(18,2),
    Currency VARCHAR(10),
    Description VARCHAR(500),
    SubscriptionDuration INT,
    TrainingPackageDuration INT,
    ImageUrl VARCHAR(2048),
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE TrainingPackages (
    PackageID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    CoachID UUID REFERENCES CoachProfiles(CoachID),
    Title VARCHAR(255),
    Description VARCHAR(500),
    Price NUMERIC(18,2),
    DurationDays INT,
    SessionCount SMALLINT,
    MinAge SMALLINT,
    TargetAudience VARCHAR(100),
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Orders (
    OrderID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    BuyerID UUID REFERENCES Users(UserID),
    CoachID UUID REFERENCES CoachProfiles(CoachID),
    TotalAmount NUMERIC(18,2),
    OrderStatus VARCHAR(50),
    OrderType VARCHAR(50),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE OrderDetails (
    OrderDetailsID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    OrderID UUID REFERENCES Orders(OrderID),
    PackageID UUID REFERENCES TrainingPackages(PackageID),
    CoachSubscriptionPlansID UUID REFERENCES CoachSubscriptionPlans(CoachSubscriptionPlansID),
    PackagePrice NUMERIC(18,2),
    PackageTitle VARCHAR(255),
    PackageDurationDays INT,
    CoachName VARCHAR(100),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Payments (
    PaymentID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    OrderID UUID REFERENCES Orders(OrderID),
    GatewayID UUID REFERENCES PaymentGatewayConfigs(GatewayID),
    Amount NUMERIC(18,2),
    Currency VARCHAR(10),
    Method VARCHAR(50),
    TransactionRef VARCHAR(100),
    TransactionType VARCHAR(30),
    GatewayTransactionID VARCHAR(100),
    Status VARCHAR(20),
    FailureReason TEXT,
    ProcessedAt TIMESTAMP,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Carts (
    CartID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    UserID UUID REFERENCES Users(UserID),
    PackageID UUID REFERENCES TrainingPackages(PackageID),
    Quantity INT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE CoachUpgrades (
    UpgradeID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    CoachID UUID REFERENCES CoachProfiles(CoachID),
    CoachSubscriptionPlansID UUID REFERENCES CoachSubscriptionPlans(CoachSubscriptionPlansID),
    OrderID UUID UNIQUE REFERENCES Orders(OrderID),
    Status VARCHAR(50),
    EndDay TIMESTAMP,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ==========================================
-- 7. TẠO CÁC BẢNG WORKOUT (CHƯƠNG TRÌNH TẬP LUYỆN, DINH DƯỠNG)
-- ==========================================

CREATE TABLE VideoTutorial (
    VideoTutorialID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    CoachID UUID REFERENCES CoachProfiles(CoachID),
    Title VARCHAR(200),
    Description VARCHAR(500),
    VideoUrl VARCHAR(2048),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE TrainingPlan (
    TrainingPlanID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    CoachID UUID REFERENCES CoachProfiles(CoachID),
    OrderDetailsID UUID REFERENCES OrderDetails(OrderDetailsID),
    Title VARCHAR(500),
    Description VARCHAR(500),
    StartDate TIMESTAMP,
    EndDate TIMESTAMP,
    Status VARCHAR(30),
    PublishedAt TIMESTAMP,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE TrainingPlanExercise (
    TrainingPlanExerciseID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    TrainingPlanID UUID REFERENCES TrainingPlan(TrainingPlanID),
    VideoTutorialID UUID REFERENCES VideoTutorial(VideoTutorialID),
    WeekNumber SMALLINT,
    DayNumber SMALLINT,
    ExerciseName VARCHAR(200),
    Sets SMALLINT,
    Reps SMALLINT,
    DurationMinutes SMALLINT,
    RestSeconds SMALLINT,
    SortOrder SMALLINT,
    Notes VARCHAR(500)
);

CREATE TABLE MealPlan (
    MealPlanID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    CoachID UUID REFERENCES CoachProfiles(CoachID),
    OrderDetailsID UUID REFERENCES OrderDetails(OrderDetailsID),
    Title VARCHAR(200),
    Description VARCHAR(500),
    StartDate TIMESTAMP,
    EndDate TIMESTAMP,
    Status VARCHAR(30),
    PublishedAt TIMESTAMP,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE MealPlanItem (
    MealPlanItemID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    MealPlanID UUID REFERENCES MealPlan(MealPlanID),
    WeekNumber SMALLINT,
    DayNumber SMALLINT,
    MealType VARCHAR(100),
    FoodDescription VARCHAR(500),
    Calories INT,
    SortOrder SMALLINT
);

CREATE TABLE BodyMetricLog (
    BodyMetricLogID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    TraineeID UUID REFERENCES Users(UserID),
    LogDate TIMESTAMP,
    BodyWeightKg NUMERIC(5,2),
    CalorieIntake INT,
    Notes VARCHAR(500),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE BodyMeasurementDetail (
    BodyMeasurementDetailID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    BodyMetricLogID UUID REFERENCES BodyMetricLog(BodyMetricLogID),
    MeasurementType VARCHAR(200),
    ValueCm NUMERIC(5,2)
);

CREATE TABLE WorkoutCompletionLog (
    WorkoutCompletionLogID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    TraineeID UUID REFERENCES Users(UserID),
    TrainingPlanExerciseID UUID REFERENCES TrainingPlanExercise(TrainingPlanExerciseID),
    LogDate TIMESTAMP,
    Status VARCHAR(30),
    Notes VARCHAR(300),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ==========================================
-- 8. TẠO CÁC BẢNG REVIEWS, PAYOUTS, REFUNDS, AUDIT
-- ==========================================

CREATE TABLE Reviews (
    ReviewID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    TraineeID UUID REFERENCES Users(UserID),
    CoachID UUID REFERENCES CoachProfiles(CoachID),
    Rating INTEGER,
    Comment TEXT,
    Reply TEXT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Payouts (
    PayoutID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    CoachID UUID REFERENCES CoachProfiles(CoachID),
    PayoutMonth INTEGER,
    PayoutYear INTEGER,
    TotalGrossAmount NUMERIC(18,2),
    SystemCommissionAmount NUMERIC(18,2),
    TaxAmount NUMERIC(18,2),
    NetPayoutAmount NUMERIC(18,2),
    Status VARCHAR(50),
    TransactionRef VARCHAR(100),
    ProcessedBy UUID REFERENCES Users(UserID),
    ProcessedAt TIMESTAMP,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(CoachID, PayoutMonth, PayoutYear)
);

CREATE TABLE PayoutItems (
    PayoutItemID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    PayoutID UUID REFERENCES Payouts(PayoutID),
    OrderID UUID REFERENCES Orders(OrderID),
    GrossAmount NUMERIC(18,2),
    CommissionRate NUMERIC(5,2),
    CommissionAmount NUMERIC(18,2),
    RefundAdjustment NUMERIC(18,2),
    TaxAmount NUMERIC(18,2),
    NetAmount NUMERIC(18,2),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE RefundRequests (
    RefundRequestID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    OrderID UUID REFERENCES Orders(OrderID),
    PaymentID UUID REFERENCES Payments(PaymentID),
    RequestedBy UUID REFERENCES Users(UserID),
    TermID UUID REFERENCES TermsAndPolicies(TermID),
    RequestedAmount NUMERIC(18,2),
    ApprovedAmount NUMERIC(18,2),
    Reason TEXT,
    EvidenceUrls TEXT,
    Status VARCHAR(30),
    ReviewedBy UUID REFERENCES Users(UserID),
    ReviewedAt TIMESTAMP,
    StaffNote TEXT,
    RefundTransactionRef VARCHAR(100),
    RequestedAt TIMESTAMP,
    RefundedAt TIMESTAMP,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Notifications (
    ID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    UserID UUID REFERENCES Users(UserID),
    ActorID UUID REFERENCES Users(UserID),
    ReferenceID UUID,
    Type VARCHAR(50),
    Desciption VARCHAR(500),
    IsRead BOOLEAN DEFAULT FALSE,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE AuditLogs (
    AuditLogID BIGSERIAL PRIMARY KEY,
    ActorAccountID UUID REFERENCES Users(UserID),
    Action VARCHAR(100),
    EntityType VARCHAR(100),
    EntityID VARCHAR(100),
    OldValue JSONB,
    NewValue JSONB,
    IPAddress VARCHAR(45),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);