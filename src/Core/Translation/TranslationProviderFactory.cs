using MyCaption.Core.Models;
using System;
using System.IO;

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
            if (settings == null)
            {
                settings = new TranslationSettings();
                settings.ApplyDefaults();
            }

            string providerName = string.IsNullOrWhiteSpace(settings.ProviderName) ? "Stub" : settings.ProviderName.Trim();
            settings.ExecutablePath = NormalizeOptionalPath(settings.ExecutablePath);
            settings.ApiUrl = NormalizeApiUrl(providerName, settings.ApiUrl);

            if (string.Equals(providerName, "ExternalCli", StringComparison.OrdinalIgnoreCase))
            {
                return new ExternalCliTranslationProvider(settings);
            }

            if (string.Equals(providerName, "DeepL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(providerName, "AzureTranslator", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(providerName, "GoogleCloud", StringComparison.OrdinalIgnoreCase))
            {
                return new OfficialApiTranslationProvider(settings);
            }

            return new StubTranslationProvider(settings);
        }

        internal static string NormalizeOptionalPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            path = path.Trim();

            try
            {
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                }

                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        internal static string NormalizeApiUrl(string providerName, string apiUrl)
        {
            string normalizedUrl = apiUrl == null ? string.Empty : apiUrl.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedUrl))
            {
                return normalizedUrl;
            }

            if (string.Equals(providerName, "DeepL", StringComparison.OrdinalIgnoreCase))
            {
                return "https://api-free.deepl.com/v2/translate";
            }

            if (string.Equals(providerName, "AzureTranslator", StringComparison.OrdinalIgnoreCase))
            {
                return "https://api.cognitive.microsofttranslator.com";
            }

            if (string.Equals(providerName, "GoogleCloud", StringComparison.OrdinalIgnoreCase))
            {
                return "https://translation.googleapis.com/language/translate/v2";
            }

            return string.Empty;
        }
    }
}
