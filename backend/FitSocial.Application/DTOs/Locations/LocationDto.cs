namespace FitSocial.Application.DTOs.Locations;

public class LocationDto
{
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string? Address { get; set; }
}
