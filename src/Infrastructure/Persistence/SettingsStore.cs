using System;
using System.IO;
using System.Runtime.Serialization.Json;
using MyCaption.Core.Models;

namespace MyCaption.Infrastructure.Persistence
{
    public sealed class SettingsStore
    {
        private readonly string _settingsPath;

        public SettingsStore()
        {
            _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        }

        public string SettingsPath
        {
            get { return _settingsPath; }
        }

        public AppSettings Load()
        {
            try
            {
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
                return new AppSettings();
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
    }
}
