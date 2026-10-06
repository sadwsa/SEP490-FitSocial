using System.Collections.Generic;

namespace FitSocial.Application.DTOs.TrainingPackage
{
    public class UpdateTrainingPackageDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public int? DurationDays { get; set; }
        public short? SessionCount { get; set; }
        public short? MinAge { get; set; }
        public string? TargetAudience { get; set; }
        public bool? IsActive { get; set; }
        public List<string>? ImageUrls { get; set; }
    }
}
