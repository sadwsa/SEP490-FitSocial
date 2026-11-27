using FitSocial.Client.Models.Auth;

namespace FitSocial.Client.Models.Coach;

/// <summary>
/// Holds the coach registration draft while the user pays on /coach-payment.
/// Account is only created (register-coach) after successful payment.
/// </summary>
public class PendingCoachRegistration
{
    public RegisterCoachRequest? Draft { get; set; }
    public List<Guid> SportIds { get; set; } = new();
    public List<Guid> SpecialtyIds { get; set; } = new();
    public List<string> SpecialtyNames { get; set; } = new();

    public bool HasDraft => Draft != null;

    public void Clear()
    {
        Draft = null;
        SportIds = new List<Guid>();
        SpecialtyIds = new List<Guid>();
        SpecialtyNames = new List<string>();
    }
}
