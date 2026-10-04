namespace FitSocial.Client.Models.Coaches;

public class CreateReviewDto
{
    public int Rating { get; set; } = 5; // Mặc định là 5 sao
    public string? Comment { get; set; }
}
