using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Common;

public class TermsDto
{
    public Guid TermId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
}
