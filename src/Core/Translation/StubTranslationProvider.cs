using System.Threading;
using System.Threading.Tasks;
using MyCaption.Core.Models;

namespace MyCaption.Core.Translation
{
    public sealed class StubTranslationProvider : ITranslationProvider
    {
        private readonly TranslationSettings _settings;

        public StubTranslationProvider(TranslationSettings settings)
        {
            _settings = settings;
        }

        public string DisplayName
        {
            get { return _settings.ProviderName; }
        }

        public string Description
        {
            get { return "Pluggable translation stub. Replace this with a real local provider later."; }
        }

        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TranslationResult(request.SourceText, request.SourceText));
        }
    }
}
