-- 1. Fix column lengths to match EF Core DbContext configuration (Fixes 22001 value too long for varchar(26))
ALTER TABLE Notifications ALTER COLUMN Type TYPE VARCHAR(50);
ALTER TABLE WorkoutCompletionLog ALTER COLUMN Status TYPE VARCHAR(30);
ALTER TABLE MealPlan ALTER COLUMN Status TYPE VARCHAR(30);

-- 2. Create MealCompletionLog table for meal photo proof & coach review feedback
CREATE TABLE IF NOT EXISTS MealCompletionLog (
    MealCompletionLogID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    TraineeID UUID NOT NULL REFERENCES Users(UserID),
    MealPlanItemID UUID NOT NULL REFERENCES MealPlanItem(MealPlanItemID) ON DELETE CASCADE,
    LogDate TIMESTAMP WITHOUT TIME ZONE,
    Status VARCHAR(30),
    Notes VARCHAR(300),
    MediaUrl VARCHAR(500),
    CoachFeedback VARCHAR(500),
    ReviewedBy UUID REFERENCES Users(UserID),
    ReviewedAt TIMESTAMP WITHOUT TIME ZONE,
    CreatedAt TIMESTAMP WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP
);
