namespace FitSocial.Client.Models.Coaches;

public class UpdateReviewDto
{
    public int Rating { get; set; } = 5;
    public string? Comment { get; set; }
}
