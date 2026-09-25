namespace FitSocial.Application.DTOs.Locations;

public class CreateLocationDto
{
    public string LocationName { get; set; } = string.Empty;
    public string? Address { get; set; }
}
