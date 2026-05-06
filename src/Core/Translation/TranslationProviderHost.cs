using System;
using System.Threading;
using System.Threading.Tasks;
using MyCaption.Core.Models;

namespace MyCaption.Core.Translation
{
    internal interface ITranslationProviderStatus
    {
        string StatusSummary { get; }

        string ExecutablePath { get; }

        string ArgumentsTemplate { get; }

        string ApiUrl { get; }

        string ApiKey { get; }

        string ApiRegion { get; }
    }

    public sealed class TranslationProviderHost : ITranslationProvider, IDisposable
    {
        private readonly object _syncRoot;
        private readonly ITranslationProviderFactory _factory;
        private readonly TranslationSettings _settings;
        private ITranslationProvider _provider;
        private string _statusText;
        private string _executablePath;
        private string _argumentsTemplate;
        private string _apiUrl;
        private string _apiKey;
        private string _apiRegion;

        public TranslationProviderHost(ITranslationProviderFactory factory, TranslationSettings settings)
        {
            _syncRoot = new object();
            _factory = factory;
            _settings = settings ?? new TranslationSettings();
            _settings.ApplyDefaults();
            Reload();
        }

        public event EventHandler ProviderStatusChanged;

        public string DisplayName
        {
            get
            {
                lock (_syncRoot)
                {
                    return _provider != null ? _provider.DisplayName : _settings.ProviderName;
                }
            }
        }

        public string Description
        {
            get
            {
                lock (_syncRoot)
                {
                    return _provider != null ? _provider.Description : _settings.ProviderName;
                }
            }
        }

        public string StatusText
        {
            get
            {
                lock (_syncRoot)
                {
                    return _statusText;
                }
            }
        }

        public string ExecutablePath
        {
            get
            {
                lock (_syncRoot)
                {
                    return _executablePath;
                }
            }
        }

        public string ArgumentsTemplate
        {
            get
            {
                lock (_syncRoot)
                {
                    return _argumentsTemplate;
                }
            }
        }

        public string ApiUrl
        {
            get
            {
                lock (_syncRoot)
                {
                    return _apiUrl;
                }
            }
        }

        public string ApiKey
        {
            get
            {
                lock (_syncRoot)
                {
                    return _apiKey;
                }
            }
        }

        public string ApiRegion
        {
            get
            {
                lock (_syncRoot)
                {
                    return _apiRegion;
                }
            }
        }

        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken)
        {
            ITranslationProvider provider;
            lock (_syncRoot)
            {
                provider = _provider;
            }

            return provider.TranslateAsync(request, cancellationToken);
        }

        public void UpdateProviderName(string providerName)
        {
            string normalizedProviderName = string.IsNullOrWhiteSpace(providerName) ? "Stub" : providerName.Trim();

            lock (_syncRoot)
            {
                if (string.Equals(_settings.ProviderName, normalizedProviderName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _settings.ProviderName = normalizedProviderName;
            }

            Reload();
        }

        public void UpdateExecutablePath(string executablePath)
        {
            string normalizedPath = TranslationProviderFactory.NormalizeOptionalPath(executablePath);

            lock (_syncRoot)
            {
                if (string.Equals(_settings.ExecutablePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _settings.ExecutablePath = normalizedPath;
            }

            Reload();
        }

        public void UpdateArgumentsTemplate(string argumentsTemplate)
        {
            string normalizedTemplate = argumentsTemplate == null ? string.Empty : argumentsTemplate.Trim();

            lock (_syncRoot)
            {
                if (string.Equals(_settings.ArgumentsTemplate, normalizedTemplate, StringComparison.Ordinal))
                {
                    return;
                }

                _settings.ArgumentsTemplate = normalizedTemplate;
            }

            Reload();
        }

        public void UpdateApiUrl(string apiUrl)
        {
            string normalizedUrl = TranslationProviderFactory.NormalizeApiUrl(_settings.ProviderName, apiUrl);

            lock (_syncRoot)
            {
                if (string.Equals(_settings.ApiUrl, normalizedUrl, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _settings.ApiUrl = normalizedUrl;
            }

            Reload();
        }

        public void UpdateApiKey(string apiKey)
        {
            string normalizedKey = apiKey == null ? string.Empty : apiKey.Trim();

            lock (_syncRoot)
            {
                if (string.Equals(_settings.ApiKey, normalizedKey, StringComparison.Ordinal))
                {
                    return;
                }

                _settings.ApiKey = normalizedKey;
            }

            Reload();
        }

        public void UpdateApiRegion(string apiRegion)
        {
            string normalizedRegion = apiRegion == null ? string.Empty : apiRegion.Trim();

            lock (_syncRoot)
            {
                if (string.Equals(_settings.ApiRegion, normalizedRegion, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _settings.ApiRegion = normalizedRegion;
            }

            Reload();
        }

        public void Reload()
        {
            ITranslationProvider provider = _factory.Create(_settings);
            ITranslationProvider previousProvider;
            ITranslationProviderStatus status = provider as ITranslationProviderStatus;
            string statusText = status != null ? status.StatusSummary : provider.Description;
            string executablePath = status != null ? status.ExecutablePath : _settings.ExecutablePath;
            string argumentsTemplate = status != null ? status.ArgumentsTemplate : _settings.ArgumentsTemplate;
            string apiUrl = status != null ? status.ApiUrl : _settings.ApiUrl;
            string apiKey = status != null ? status.ApiKey : _settings.ApiKey;
            string apiRegion = status != null ? status.ApiRegion : _settings.ApiRegion;

            lock (_syncRoot)
            {
                previousProvider = _provider;
                _provider = provider;
                _statusText = statusText ?? string.Empty;
                _executablePath = executablePath ?? string.Empty;
                _argumentsTemplate = argumentsTemplate ?? string.Empty;
                _apiUrl = apiUrl ?? string.Empty;
                _apiKey = apiKey ?? string.Empty;
                _apiRegion = apiRegion ?? string.Empty;
                _settings.ExecutablePath = _executablePath;
                _settings.ArgumentsTemplate = _argumentsTemplate;
                _settings.ApiUrl = _apiUrl;
                _settings.ApiKey = _apiKey;
                _settings.ApiRegion = _apiRegion;
            }

            DisposeProvider(previousProvider, provider);

            EventHandler handler = ProviderStatusChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            ITranslationProvider provider;

            lock (_syncRoot)
            {
                provider = _provider;
                _provider = null;
            }

            DisposeProvider(provider, null);
        }

        private static void DisposeProvider(ITranslationProvider provider, ITranslationProvider replacement)
        {
            if (provider == null || object.ReferenceEquals(provider, replacement))
            {
                return;
            }

            IDisposable disposable = provider as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }
    }
}
