namespace FitSocial.Client.Models.Terms;

public class TermsDto
{
    public Guid TermId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
}
