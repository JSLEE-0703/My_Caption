using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyCaption.Core.Models;

namespace MyCaption.Core.Lookup
{
    internal interface ILookupProviderStatus
    {
        string StatusSummary { get; }

        string DictionaryFilePath { get; }
    }

    internal interface IMdictLookupProviderStatus
    {
        string MdictExecutablePath { get; }
    }

    public interface ILookupProviderFactory
    {
        ILookupProvider Create(DictionarySettings settings);
    }

    public sealed class LookupProviderFactory : ILookupProviderFactory
    {
        public ILookupProvider Create(DictionarySettings settings)
        {
            if (settings == null)
            {
                settings = new DictionarySettings();
                settings.ApplyDefaults();
            }

            string providerName = string.IsNullOrWhiteSpace(settings.ProviderName) ? "JsonFile" : settings.ProviderName;
            string dictionaryFilePath = NormalizeDictionaryFilePath(providerName, settings.DictionaryFilePath);
            string mdictExecutablePath = NormalizeOptionalPath(settings.MdictExecutablePath);

            if (string.Equals(providerName, "JsonFile", StringComparison.OrdinalIgnoreCase))
            {
                return new JsonFileLookupProvider(dictionaryFilePath);
            }

            if (string.Equals(providerName, "MdictCli", StringComparison.OrdinalIgnoreCase))
            {
                return new MdictLookupProvider(dictionaryFilePath, mdictExecutablePath);
            }

            return new JsonFileLookupProvider(dictionaryFilePath);
        }

        internal static string NormalizeDictionaryFilePath(string providerName, string dictionaryFilePath)
        {
            string path = dictionaryFilePath;
            if (string.IsNullOrWhiteSpace(path) && string.Equals(providerName, "JsonFile", StringComparison.OrdinalIgnoreCase))
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dictionary.json");
            }

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
    }

    public sealed class LookupProviderHost : ILookupProvider
    {
        private readonly object _syncRoot;
        private readonly ILookupProviderFactory _factory;
        private readonly DictionarySettings _settings;
        private ILookupProvider _provider;
        private string _statusText;
        private string _dictionaryFilePath;
        private string _mdictExecutablePath;

        public LookupProviderHost(ILookupProviderFactory factory, DictionarySettings settings)
        {
            _syncRoot = new object();
            _factory = factory;
            _settings = settings ?? new DictionarySettings();
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

        public string DictionaryFilePath
        {
            get
            {
                lock (_syncRoot)
                {
                    return _dictionaryFilePath;
                }
            }
        }

        public string MdictExecutablePath
        {
            get
            {
                lock (_syncRoot)
                {
                    return _mdictExecutablePath;
                }
            }
        }

        public Task<LookupResult> LookupAsync(string word, CancellationToken cancellationToken)
        {
            ILookupProvider provider;
            lock (_syncRoot)
            {
                provider = _provider;
            }

            return provider.LookupAsync(word, cancellationToken);
        }

        public void UpdateDictionaryFilePath(string dictionaryFilePath)
        {
            string normalizedPath = LookupProviderFactory.NormalizeDictionaryFilePath(_settings.ProviderName, dictionaryFilePath);

            lock (_syncRoot)
            {
                if (string.Equals(_settings.DictionaryFilePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _settings.DictionaryFilePath = normalizedPath;
            }

            Reload();
        }

        public void UpdateProviderName(string providerName)
        {
            string normalizedProviderName = string.IsNullOrWhiteSpace(providerName) ? "JsonFile" : providerName.Trim();

            lock (_syncRoot)
            {
                if (string.Equals(_settings.ProviderName, normalizedProviderName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _settings.ProviderName = normalizedProviderName;
                _settings.DictionaryFilePath = LookupProviderFactory.NormalizeDictionaryFilePath(_settings.ProviderName, _settings.DictionaryFilePath);
            }

            Reload();
        }

        public void UpdateMdictExecutablePath(string mdictExecutablePath)
        {
            string normalizedPath = LookupProviderFactory.NormalizeOptionalPath(mdictExecutablePath);

            lock (_syncRoot)
            {
                if (string.Equals(_settings.MdictExecutablePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _settings.MdictExecutablePath = normalizedPath;
            }

            Reload();
        }

        public void Reload()
        {
            ILookupProvider provider = _factory.Create(_settings);
            ILookupProviderStatus status = provider as ILookupProviderStatus;
            string statusText = status != null ? status.StatusSummary : provider.DisplayName;
            string dictionaryFilePath = status != null ? status.DictionaryFilePath : _settings.DictionaryFilePath;
            string mdictExecutablePath = provider is IMdictLookupProviderStatus
                ? ((IMdictLookupProviderStatus)provider).MdictExecutablePath
                : _settings.MdictExecutablePath;

            lock (_syncRoot)
            {
                _provider = provider;
                _statusText = statusText;
                _dictionaryFilePath = dictionaryFilePath ?? string.Empty;
                _mdictExecutablePath = mdictExecutablePath ?? string.Empty;
                _settings.DictionaryFilePath = _dictionaryFilePath;
                _settings.MdictExecutablePath = _mdictExecutablePath;
            }

            EventHandler handler = ProviderStatusChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
