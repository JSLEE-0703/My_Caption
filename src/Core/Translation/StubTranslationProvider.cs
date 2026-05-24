using System.Threading;
using System.Threading.Tasks;
using MyCaption.Core.Models;

namespace MyCaption.Core.Translation
{
    public sealed class StubTranslationProvider : ITranslationProvider, ITranslationProviderStatus
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

        public string StatusSummary
        {
            get { return "Stub provider active. Translation output mirrors the source text."; }
        }

        public string ExecutablePath
        {
            get { return string.Empty; }
        }

        public string ArgumentsTemplate
        {
            get { return string.Empty; }
        }

        public string ApiUrl
        {
            get { return string.Empty; }
        }

        public string ApiKey
        {
            get { return string.Empty; }
        }

        public string ApiRegion
        {
            get { return string.Empty; }
        }

        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TranslationResult(request.SourceText, request.SourceText, request.IsCommitted));
        }
    }
}
