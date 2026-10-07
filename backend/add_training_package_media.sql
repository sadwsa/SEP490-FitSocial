-- =========================================================================
-- MIGRATION SCRIPT: ADD TABLE TrainingPackageMedia
-- Hỗ trợ lưu trữ nhiều hình ảnh/media cho Training Packages (FitSocial)
-- =========================================================================

-- 1. Tạo bảng TrainingPackageMedia
CREATE TABLE IF NOT EXISTS TrainingPackageMedia (
    MediaID UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    PackageID UUID NOT NULL,
    MediaUrl VARCHAR(2048) NOT NULL,
    MediaType VARCHAR(20) DEFAULT 'IMAGE',
    SortOrder SMALLINT DEFAULT 0,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_training_package_media_package 
        FOREIGN KEY (PackageID) 
        REFERENCES TrainingPackages(PackageID) 
        ON DELETE CASCADE
);

-- 2. Đánh chỉ mục tìm kiếm theo PackageID để tối ưu hiệu năng query
CREATE INDEX IF NOT EXISTS idx_training_package_media_package_id 
    ON TrainingPackageMedia(PackageID);

-- 3. Đánh chỉ mục theo SortOrder
CREATE INDEX IF NOT EXISTS idx_training_package_media_sort_order 
    ON TrainingPackageMedia(PackageID, SortOrder);
