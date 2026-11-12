using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Application.Interfaces;

public interface IImageService
{
    Task<bool> DeleteImageAsync(string imageUrl, CancellationToken cancellationToken = default);
}
