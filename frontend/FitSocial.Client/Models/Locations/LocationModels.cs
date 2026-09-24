namespace FitSocial.Client.Models.Locations;

public class LocationDto
{
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string? Address { get; set; }
}

public class CreateLocationDto
{
    public string LocationName { get; set; } = string.Empty;
    public string? Address { get; set; }
}
