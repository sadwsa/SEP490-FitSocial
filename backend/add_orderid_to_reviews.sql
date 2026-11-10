-- Apply this script to the Neon/PostgreSQL database to support per-order reviews.
-- Safe to run multiple times (uses IF NOT EXISTS).

-- 1. Add OrderID column to Reviews
ALTER TABLE "Reviews"
    ADD COLUMN IF NOT EXISTS "OrderID" uuid NULL;

-- 2. Add FK to Orders (set null on delete)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'Reviews_OrderID_fkey'
    ) THEN
        ALTER TABLE "Reviews"
            ADD CONSTRAINT "Reviews_OrderID_fkey"
            FOREIGN KEY ("OrderID") REFERENCES "Orders"("OrderID") ON DELETE SET NULL;
    END IF;
END $$;

-- 3. Drop old unique index (PackageID, TraineeID) if exists
DROP INDEX IF EXISTS "Reviews_PackageID_TraineeID_key";

-- 4. Add new unique index (OrderID, TraineeID) for non-null OrderID
CREATE UNIQUE INDEX IF NOT EXISTS "Reviews_OrderID_TraineeID_key"
    ON "Reviews" ("OrderID", "TraineeID")
    WHERE "OrderID" IS NOT NULL;
