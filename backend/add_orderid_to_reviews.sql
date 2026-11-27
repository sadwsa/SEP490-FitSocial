-- Apply this script to the Neon/PostgreSQL database to support per-order reviews.
-- Safe to run multiple times (uses IF NOT EXISTS).

-- 1. Add orderid column to reviews
ALTER TABLE reviews
    ADD COLUMN IF NOT EXISTS orderid uuid NULL;

-- 2. Add FK to orders (set null on delete)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'reviews_orderid_fkey' OR conname = 'Reviews_OrderID_fkey'
    ) THEN
        ALTER TABLE reviews
            ADD CONSTRAINT "Reviews_OrderID_fkey"
            FOREIGN KEY (orderid) REFERENCES orders(orderid) ON DELETE SET NULL;
    END IF;
END $$;

-- 3. Drop old unique index (PackageID, TraineeID) if exists
DROP INDEX IF EXISTS "Reviews_PackageID_TraineeID_key";
DROP INDEX IF EXISTS reviews_packageid_traineeid_key;

-- 4. Add new unique index (OrderID, TraineeID) for non-null OrderID
CREATE UNIQUE INDEX IF NOT EXISTS "Reviews_OrderID_TraineeID_key"
    ON reviews (orderid, traineeid)
    WHERE orderid IS NOT NULL;
