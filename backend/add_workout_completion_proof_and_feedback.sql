-- Add proof media and coach feedback columns to WorkoutCompletionLog table
ALTER TABLE WorkoutCompletionLog 
ADD COLUMN IF NOT EXISTS MediaUrl VARCHAR(500),
ADD COLUMN IF NOT EXISTS CoachFeedback VARCHAR(500),
ADD COLUMN IF NOT EXISTS ReviewedBy UUID REFERENCES Users(UserID),
ADD COLUMN IF NOT EXISTS ReviewedAt TIMESTAMP;
