using System;

namespace FitSocial.Application.DTOs.TrainingPackage
{
    public class TrainingPackageMediaDto
    {
        public Guid MediaId { get; set; }
        public Guid PackageId { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        public string? MediaType { get; set; } = "IMAGE";
        public short? SortOrder { get; set; } = 0;
        public DateTime? CreatedAt { get; set; }
    }
}
