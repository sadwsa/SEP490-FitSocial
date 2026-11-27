-- Chạy script này trên Neon SQL Editor để hỗ trợ tính năng Review có hình ảnh

-- 1. Thêm cột RowVersion vào bảng Reviews (nếu chưa có)
ALTER TABLE "Reviews" 
    ADD COLUMN IF NOT EXISTS "RowVersion" bytea NULL;

-- 2. Tạo bảng ReviewMedia (nếu chưa có)
CREATE TABLE IF NOT EXISTS "ReviewMedia" (
    "ReviewMediaID" uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    "ReviewID" uuid NOT NULL,
    "MediaUrl" text NOT NULL,
    "MediaType" character varying(20) NOT NULL DEFAULT 'image',
    "SortOrder" integer NOT NULL DEFAULT 0,
    "created_at" timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "ReviewMedia_ReviewID_fkey" 
        FOREIGN KEY ("ReviewID") REFERENCES "Reviews"("ReviewID") ON DELETE CASCADE
);

-- 3. Tạo index trên ReviewID để truy vấn nhanh
CREATE INDEX IF NOT EXISTS "IX_ReviewMedia_ReviewID" 
    ON "ReviewMedia" ("ReviewID");

-- 4. Kiểm tra
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'Reviews' AND column_name = 'RowVersion';

SELECT table_name 
FROM information_schema.tables 
WHERE table_name = 'ReviewMedia';
