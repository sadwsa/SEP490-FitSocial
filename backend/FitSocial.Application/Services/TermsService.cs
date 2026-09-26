using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Application.Services;

public class TermsService : ITermsService
{
    private readonly ITermsAndPolicyRepository _terms;

    public TermsService(ITermsAndPolicyRepository terms)
    {
        _terms = terms;
    }

    public async Task<TermsDto?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var term = await _terms.GetLatestAsync(cancellationToken);
        if (term == null)
        {
            return null;
        }

        return new TermsDto
        {
            TermId = term.TermId,
            Version = term.Version,
            Title = term.Title,
            Content = term.Content,
            EffectiveDate = term.EffectiveDate
        };
    }
}
