namespace FitSocial.Application.DTOs.TrainingPackage
{
    public class CreateTrainingPackageDto
    {
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int DurationDays { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
