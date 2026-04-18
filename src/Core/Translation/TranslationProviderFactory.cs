using MyCaption.Core.Models;

namespace MyCaption.Core.Translation
{
    public interface ITranslationProviderFactory
    {
        ITranslationProvider Create(TranslationSettings settings);
    }

    public sealed class TranslationProviderFactory : ITranslationProviderFactory
    {
        public ITranslationProvider Create(TranslationSettings settings)
        {
            return new StubTranslationProvider(settings);
        }
    }
}
