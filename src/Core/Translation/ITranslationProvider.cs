using System.Threading;
using System.Threading.Tasks;
using MyCaption.Core.Models;

namespace MyCaption.Core.Translation
{
    public interface ITranslationProvider
    {
        string DisplayName { get; }

        string Description { get; }

        Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken);
    }
}
