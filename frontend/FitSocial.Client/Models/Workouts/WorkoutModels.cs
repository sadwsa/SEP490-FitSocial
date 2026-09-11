namespace FitSocial.Client.Models.Workouts;

public class WorkoutDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Beginner"; // Beginner, Intermediate, Advanced
    public int DurationMinutes { get; set; }
    public int CaloriesBurnedEstimate { get; set; }
    public string Category { get; set; } = "Gym"; // Gym, Cardio, Calisthenics, Yoga, Running
    public List<string> Tags { get; set; } = new();
}
