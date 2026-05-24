using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyCaption.Core.Models;

namespace MyCaption.Core.Translation
{
    public sealed class OfficialApiTranslationProvider : ITranslationProvider, ITranslationProviderStatus
    {
        private static readonly HttpClient Client = new HttpClient();

        private readonly string _providerName;
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private readonly string _apiRegion;
        private readonly string _statusSummary;
        private readonly string _initializationError;

        public OfficialApiTranslationProvider(TranslationSettings settings)
        {
            settings = settings ?? new TranslationSettings();
            settings.ApplyDefaults();

            _providerName = string.IsNullOrWhiteSpace(settings.ProviderName) ? "Stub" : settings.ProviderName.Trim();
            _apiUrl = TranslationProviderFactory.NormalizeApiUrl(_providerName, settings.ApiUrl);
            _apiKey = settings.ApiKey == null ? string.Empty : settings.ApiKey.Trim();
            _apiRegion = settings.ApiRegion == null ? string.Empty : settings.ApiRegion.Trim();
            _statusSummary = InitializeStatus(out _initializationError);
        }

        public string DisplayName
        {
            get
            {
                if (string.Equals(_providerName, "DeepL", StringComparison.OrdinalIgnoreCase))
                {
                    return "DeepL API";
                }

                if (string.Equals(_providerName, "AzureTranslator", StringComparison.OrdinalIgnoreCase))
                {
                    return "Azure Translator";
                }

                if (string.Equals(_providerName, "GoogleCloud", StringComparison.OrdinalIgnoreCase))
                {
                    return "Google Cloud Translation";
                }

                return _providerName;
            }
        }

        public string Description
        {
            get { return "Calls the selected official translation HTTP API directly."; }
        }

        public string StatusSummary
        {
            get { return _statusSummary; }
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
            get { return _apiUrl; }
        }

        public string ApiKey
        {
            get { return _apiKey; }
        }

        public string ApiRegion
        {
            get { return _apiRegion; }
        }

        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (!string.IsNullOrWhiteSpace(_initializationError))
            {
                return Task.FromResult(new TranslationResult(request.SourceText, "[translation error] " + _initializationError, request.IsCommitted));
            }

            return TranslateCoreAsync(request, cancellationToken);
        }

        private async Task<TranslationResult> TranslateCoreAsync(TranslationRequest request, CancellationToken cancellationToken)
        {
            string translatedText;

            if (string.Equals(_providerName, "DeepL", StringComparison.OrdinalIgnoreCase))
            {
                translatedText = await TranslateWithDeepLAsync(request, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(_providerName, "AzureTranslator", StringComparison.OrdinalIgnoreCase))
            {
                translatedText = await TranslateWithAzureAsync(request, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(_providerName, "GoogleCloud", StringComparison.OrdinalIgnoreCase))
            {
                translatedText = await TranslateWithGoogleCloudAsync(request, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException("Unsupported official translation provider.");
            }

            return new TranslationResult(request.SourceText, translatedText, request.IsCommitted);
        }

        private string InitializeStatus(out string initializationError)
        {
            initializationError = string.Empty;

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                initializationError = DisplayName + " unavailable: API key is empty.";
                return initializationError;
            }

            if (string.IsNullOrWhiteSpace(_apiUrl))
            {
                initializationError = DisplayName + " unavailable: API URL is empty.";
                return initializationError;
            }

            if (string.Equals(_providerName, "AzureTranslator", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(_apiRegion))
            {
                return "Azure Translator ready. Add region if your resource is regional or multi-service.";
            }

            return DisplayName + " ready.";
        }

        private async Task<string> TranslateWithDeepLAsync(TranslationRequest request, CancellationToken cancellationToken)
        {
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, _apiUrl);
            httpRequest.Headers.Add("Authorization", "DeepL-Auth-Key " + _apiKey);
            httpRequest.Content = new StringContent(BuildDeepLBody(request), Encoding.UTF8, "application/json");

            using (HttpResponseMessage response = await Client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false))
            {
                string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                EnsureSuccessStatusCode(response, responseText);

                DeepLTranslateResponse payload = DeserializeJson<DeepLTranslateResponse>(responseText);
                if (payload == null || payload.Translations == null || payload.Translations.Length == 0)
                {
                    throw new InvalidOperationException("DeepL response did not contain translated text.");
                }

                return payload.Translations[0].Text ?? string.Empty;
            }
        }

        private async Task<string> TranslateWithAzureAsync(TranslationRequest request, CancellationToken cancellationToken)
        {
            string requestUrl = BuildAzureRequestUrl(request);
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            httpRequest.Headers.Add("Ocp-Apim-Subscription-Key", _apiKey);
            if (!string.IsNullOrWhiteSpace(_apiRegion))
            {
                httpRequest.Headers.Add("Ocp-Apim-Subscription-Region", _apiRegion);
            }

            httpRequest.Content = new StringContent(BuildAzureBody(request), Encoding.UTF8, "application/json");

            using (HttpResponseMessage response = await Client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false))
            {
                string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                EnsureSuccessStatusCode(response, responseText);

                AzureTranslateResponse[] payload = DeserializeJson<AzureTranslateResponse[]>(responseText);
                if (payload == null || payload.Length == 0 || payload[0].Translations == null || payload[0].Translations.Length == 0)
                {
                    throw new InvalidOperationException("Azure Translator response did not contain translated text.");
                }

                return payload[0].Translations[0].Text ?? string.Empty;
            }
        }

        private async Task<string> TranslateWithGoogleCloudAsync(TranslationRequest request, CancellationToken cancellationToken)
        {
            string requestUrl = BuildGoogleCloudRequestUrl(request);
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUrl);

            using (HttpResponseMessage response = await Client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false))
            {
                string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                EnsureSuccessStatusCode(response, responseText);

                GoogleCloudTranslateResponse payload = DeserializeJson<GoogleCloudTranslateResponse>(responseText);
                if (payload == null || payload.Data == null || payload.Data.Translations == null || payload.Data.Translations.Length == 0)
                {
                    throw new InvalidOperationException("Google Cloud Translation response did not contain translated text.");
                }

                return WebUtility.HtmlDecode(payload.Data.Translations[0].TranslatedText ?? string.Empty);
            }
        }

        private string BuildDeepLBody(TranslationRequest request)
        {
            string targetLanguage = NormalizeDeepLTargetLanguage(request.TargetLanguage);
            string sourceLanguage = NormalizeDeepLSourceLanguage(request.SourceLanguage);
            StringBuilder builder = new StringBuilder();
            builder.Append("{\"text\":[\"");
            builder.Append(EscapeJson(request.SourceText ?? string.Empty));
            builder.Append("\"],\"target_lang\":\"");
            builder.Append(EscapeJson(targetLanguage));
            builder.Append("\"");

            if (!string.IsNullOrWhiteSpace(sourceLanguage))
            {
                builder.Append(",\"source_lang\":\"");
                builder.Append(EscapeJson(sourceLanguage));
                builder.Append("\"");
            }

            builder.Append("}");
            return builder.ToString();
        }

        private string BuildAzureRequestUrl(TranslationRequest request)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(_apiUrl.TrimEnd('/'));
            builder.Append("/translate?api-version=3.0");
            builder.Append("&to=");
            builder.Append(Uri.EscapeDataString(NormalizeAzureLanguage(request.TargetLanguage)));

            string sourceLanguage = NormalizeAzureLanguage(request.SourceLanguage);
            if (!string.IsNullOrWhiteSpace(sourceLanguage) && !string.Equals(sourceLanguage, "auto", StringComparison.OrdinalIgnoreCase))
            {
                builder.Append("&from=");
                builder.Append(Uri.EscapeDataString(sourceLanguage));
            }

            return builder.ToString();
        }

        private string BuildAzureBody(TranslationRequest request)
        {
            return "[{\"Text\":\"" + EscapeJson(request.SourceText ?? string.Empty) + "\"}]";
        }

        private string BuildGoogleCloudRequestUrl(TranslationRequest request)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(_apiUrl);
            builder.Append("?key=");
            builder.Append(Uri.EscapeDataString(_apiKey));
            builder.Append("&target=");
            builder.Append(Uri.EscapeDataString(NormalizeGoogleLanguage(request.TargetLanguage)));
            builder.Append("&format=text");
            builder.Append("&q=");
            builder.Append(Uri.EscapeDataString(request.SourceText ?? string.Empty));

            string sourceLanguage = NormalizeGoogleLanguage(request.SourceLanguage);
            if (!string.IsNullOrWhiteSpace(sourceLanguage) && !string.Equals(sourceLanguage, "auto", StringComparison.OrdinalIgnoreCase))
            {
                builder.Append("&source=");
                builder.Append(Uri.EscapeDataString(sourceLanguage));
            }

            return builder.ToString();
        }

        private static void EnsureSuccessStatusCode(HttpResponseMessage response, string responseText)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            string errorText = CleanupCommandOutput(responseText);
            if (string.IsNullOrWhiteSpace(errorText))
            {
                errorText = "HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase;
            }

            throw new InvalidOperationException(errorText);
        }

        private static string CleanupCommandOutput(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Replace("\0", string.Empty).Trim();
        }

        private static T DeserializeJson<T>(string json)
        {
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? string.Empty)))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                object value = serializer.ReadObject(stream);
                return value is T ? (T)value : default(T);
            }
        }

        private static string NormalizeDeepLTargetLanguage(string language)
        {
            string normalized = NormalizeLanguage(language);
            if (string.Equals(normalized, "zh-cn", StringComparison.OrdinalIgnoreCase))
            {
                return "ZH-HANS";
            }

            if (string.Equals(normalized, "zh-tw", StringComparison.OrdinalIgnoreCase))
            {
                return "ZH-HANT";
            }

            if (string.Equals(normalized, "en-us", StringComparison.OrdinalIgnoreCase))
            {
                return "EN-US";
            }

            if (string.Equals(normalized, "en-gb", StringComparison.OrdinalIgnoreCase))
            {
                return "EN-GB";
            }

            if (normalized.Length >= 2)
            {
                if (string.Equals(normalized, "pt-br", StringComparison.OrdinalIgnoreCase))
                {
                    return "PT-BR";
                }

                return normalized.Length > 2 ? normalized.Substring(0, 2).ToUpperInvariant() : normalized.ToUpperInvariant();
            }

            return "ZH-HANS";
        }

        private static string NormalizeDeepLSourceLanguage(string language)
        {
            string normalized = NormalizeLanguage(language);
            if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (normalized.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
            {
                return "EN";
            }

            if (normalized.StartsWith("zh-", StringComparison.OrdinalIgnoreCase))
            {
                return "ZH";
            }

            return normalized.Length > 2 ? normalized.Substring(0, 2).ToUpperInvariant() : normalized.ToUpperInvariant();
        }

        private static string NormalizeAzureLanguage(string language)
        {
            string normalized = NormalizeLanguage(language);
            if (string.Equals(normalized, "zh-cn", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-Hans";
            }

            if (string.Equals(normalized, "zh-tw", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-Hant";
            }

            return string.IsNullOrWhiteSpace(normalized) ? "auto" : normalized;
        }

        private static string NormalizeGoogleLanguage(string language)
        {
            string normalized = NormalizeLanguage(language);
            return string.IsNullOrWhiteSpace(normalized) ? "auto" : normalized;
        }

        private static string NormalizeLanguage(string language)
        {
            return string.IsNullOrWhiteSpace(language) ? string.Empty : language.Trim();
        }

        private static string EscapeJson(string value)
        {
            StringBuilder builder = new StringBuilder();
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                switch (ch)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(ch))
                        {
                            builder.Append("\\u");
                            builder.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(ch);
                        }

                        break;
                }
            }

            return builder.ToString();
        }

        [DataContract]
        private sealed class DeepLTranslateResponse
        {
            [DataMember(Name = "translations")]
            public DeepLTranslation[] Translations { get; set; }
        }

        [DataContract]
        private sealed class DeepLTranslation
        {
            [DataMember(Name = "text")]
            public string Text { get; set; }
        }

        [DataContract]
        private sealed class AzureTranslateResponse
        {
            [DataMember(Name = "translations")]
            public AzureTranslation[] Translations { get; set; }
        }

        [DataContract]
        private sealed class AzureTranslation
        {
            [DataMember(Name = "text")]
            public string Text { get; set; }
        }

        [DataContract]
        private sealed class GoogleCloudTranslateResponse
        {
            [DataMember(Name = "data")]
            public GoogleCloudTranslateData Data { get; set; }
        }

        [DataContract]
        private sealed class GoogleCloudTranslateData
        {
            [DataMember(Name = "translations")]
            public GoogleCloudTranslation[] Translations { get; set; }
        }

        [DataContract]
        private sealed class GoogleCloudTranslation
        {
            [DataMember(Name = "translatedText")]
            public string TranslatedText { get; set; }
        }
    }
}
