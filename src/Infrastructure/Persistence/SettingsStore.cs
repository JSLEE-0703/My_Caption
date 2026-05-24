using System;
using System.IO;
using System.Runtime.Serialization.Json;
using MyCaption.Core.Models;

namespace MyCaption.Infrastructure.Persistence
{
    public sealed class SettingsStore
    {
        private const string SettingsFileName = "settings.json";

        private readonly string _settingsPath;
        private readonly string _legacySettingsPath;

        public SettingsStore()
        {
            _legacySettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
            _settingsPath = ResolveSettingsPath(_legacySettingsPath);
        }

        public string SettingsPath
        {
            get { return _settingsPath; }
        }

        public AppSettings Load()
        {
            try
            {
                EnsureMigratedSettingsFile();

                if (!File.Exists(_settingsPath))
                {
                    AppSettings defaults = new AppSettings();
                    Save(defaults);
                    return defaults;
                }

                using (Stream stream = File.OpenRead(_settingsPath))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    AppSettings settings = serializer.ReadObject(stream) as AppSettings;
                    if (settings == null)
                    {
                        settings = new AppSettings();
                    }

                    settings.ApplyDefaults();
                    return settings;
                }
            }
            catch
            {
                return LoadLegacyOrDefault();
            }
        }

        public void Save(AppSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.ApplyDefaults();

            string directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (Stream stream = File.Create(_settingsPath))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppSettings));
                serializer.WriteObject(stream, settings);
            }
        }

        private static string ResolveSettingsPath(string fallbackPath)
        {
            string applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(applicationDataPath))
            {
                return fallbackPath;
            }

            return Path.Combine(applicationDataPath, "My Caption", SettingsFileName);
        }

        private void EnsureMigratedSettingsFile()
        {
            if (string.Equals(_settingsPath, _legacySettingsPath, StringComparison.OrdinalIgnoreCase) ||
                File.Exists(_settingsPath) ||
                !File.Exists(_legacySettingsPath))
            {
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(_legacySettingsPath, _settingsPath, false);
            }
            catch
            {
            }
        }

        private AppSettings LoadLegacyOrDefault()
        {
            if (string.Equals(_settingsPath, _legacySettingsPath, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(_legacySettingsPath))
            {
                return new AppSettings();
            }

            try
            {
                using (Stream stream = File.OpenRead(_legacySettingsPath))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    AppSettings settings = serializer.ReadObject(stream) as AppSettings;
                    if (settings == null)
                    {
                        settings = new AppSettings();
                    }

                    settings.ApplyDefaults();
                    return settings;
                }
            }
            catch
            {
                return new AppSettings();
            }
        }
    }
}
