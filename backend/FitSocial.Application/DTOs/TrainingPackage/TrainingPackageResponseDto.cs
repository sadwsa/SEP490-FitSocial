using System;

namespace FitSocial.Application.DTOs.TrainingPackage
{
    public class TrainingPackageResponseDto
    {
        public Guid PackageId { get; set; }
        public Guid CoachId { get; set; }
        // Thêm tên của Coach để hiển thị lên UI cho đẹp
        public string? CoachName { get; set; }
        public string? Title { get; set; }
        public decimal? Price { get; set; }
        public int? DurationDays { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
