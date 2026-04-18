using System;
using System.IO;
using System.Runtime.Serialization;
using System.Windows;

namespace MyCaption.Core.Models
{
    [DataContract]
    public sealed class AppSettings
    {
        [DataMember]
        public OverlaySettings Overlay { get; set; }

        [DataMember]
        public LiveCaptionsSettings LiveCaptions { get; set; }

        [DataMember]
        public InteractionSettings Interaction { get; set; }

        [DataMember]
        public TranslationSettings Translation { get; set; }

        [DataMember]
        public DictionarySettings Dictionary { get; set; }

        public AppSettings()
        {
            ApplyDefaults();
        }

        public void ApplyDefaults()
        {
            if (Overlay == null)
            {
                Overlay = new OverlaySettings();
            }

            if (LiveCaptions == null)
            {
                LiveCaptions = new LiveCaptionsSettings();
            }

            if (Interaction == null)
            {
                Interaction = new InteractionSettings();
            }

            if (Translation == null)
            {
                Translation = new TranslationSettings();
            }

            if (Dictionary == null)
            {
                Dictionary = new DictionarySettings();
            }

            Overlay.ApplyDefaults();
            LiveCaptions.ApplyDefaults();
            Interaction.ApplyDefaults();
            Translation.ApplyDefaults();
            Dictionary.ApplyDefaults();
        }
    }

    [DataContract]
    public sealed class OverlaySettings
    {
        private bool? _showTranslationTextValue;

        public OverlaySettings()
        {
            _showTranslationTextValue = true;
        }

        [DataMember]
        public double Left { get; set; }

        [DataMember]
        public double Top { get; set; }

        [DataMember]
        public double Width { get; set; }

        [DataMember]
        public double Height { get; set; }

        [DataMember]
        public double OriginalFontSize { get; set; }

        [DataMember]
        public double TranslationFontSize { get; set; }

        [DataMember]
        public double BackgroundOpacity { get; set; }

        [DataMember]
        public bool OriginalOnTop { get; set; }

        [DataMember]
        public bool Topmost { get; set; }

        public bool ShowTranslationText
        {
            get { return !_showTranslationTextValue.HasValue || _showTranslationTextValue.Value; }
            set { _showTranslationTextValue = value; }
        }

        [DataMember(Name = "ShowTranslationText", EmitDefaultValue = false)]
        private bool? ShowTranslationTextValue
        {
            get { return _showTranslationTextValue; }
            set { _showTranslationTextValue = value; }
        }

        public void ApplyDefaults()
        {
            if (Width <= 0)
            {
                Width = 840;
            }

            if (Height <= 0)
            {
                Height = 190;
            }

            if (OriginalFontSize <= 0)
            {
                OriginalFontSize = 26;
            }

            if (TranslationFontSize <= 0)
            {
                TranslationFontSize = 23;
            }

            if (BackgroundOpacity <= 0)
            {
                BackgroundOpacity = 0.88;
            }

            if (!_showTranslationTextValue.HasValue)
            {
                _showTranslationTextValue = true;
            }

            Topmost = true;

            if (Left <= 0)
            {
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2.0;
            }

            if (Top <= 0)
            {
                Top = SystemParameters.PrimaryScreenHeight - Height - 96;
            }
        }
    }

    [DataContract]
    public sealed class LiveCaptionsSettings
    {
        [DataMember]
        public bool AutoLaunchIfMissing { get; set; }

        [DataMember]
        public bool HideOriginalWindow { get; set; }

        [DataMember]
        public int PollIntervalMs { get; set; }

        [DataMember]
        public int SyncCommitThreshold { get; set; }

        [DataMember]
        public int IdleCommitThreshold { get; set; }

        public void ApplyDefaults()
        {
            AutoLaunchIfMissing = true;

            if (PollIntervalMs <= 0)
            {
                PollIntervalMs = 35;
            }

            if (SyncCommitThreshold <= 0)
            {
                SyncCommitThreshold = 3;
            }

            if (IdleCommitThreshold <= 0)
            {
                IdleCommitThreshold = 18;
            }
        }
    }

    [DataContract]
    public sealed class InteractionSettings
    {
        [DataMember]
        public bool HoldAltToInteract { get; set; }

        [DataMember]
        public bool StartClickThrough { get; set; }

        public void ApplyDefaults()
        {
            HoldAltToInteract = true;
            StartClickThrough = true;
        }
    }

    [DataContract]
    public sealed class TranslationSettings
    {
        private bool? _enabledValue;

        public TranslationSettings()
        {
            _enabledValue = true;
            ConfigPath = string.Empty;
        }

        public bool Enabled
        {
            get { return !_enabledValue.HasValue || _enabledValue.Value; }
            set { _enabledValue = value; }
        }

        [DataMember(Name = "Enabled", EmitDefaultValue = false)]
        private bool? EnabledValue
        {
            get { return _enabledValue; }
            set { _enabledValue = value; }
        }

        [DataMember]
        public string ProviderName { get; set; }

        [DataMember]
        public string SourceLanguage { get; set; }

        [DataMember]
        public string TargetLanguage { get; set; }

        [DataMember]
        public string ConfigPath { get; set; }

        public void ApplyDefaults()
        {
            if (!_enabledValue.HasValue)
            {
                _enabledValue = true;
            }

            if (string.IsNullOrWhiteSpace(ProviderName))
            {
                ProviderName = "Stub";
            }

            if (string.IsNullOrWhiteSpace(SourceLanguage))
            {
                SourceLanguage = "auto";
            }

            if (string.IsNullOrWhiteSpace(TargetLanguage))
            {
                TargetLanguage = "zh-CN";
            }

            if (ConfigPath == null)
            {
                ConfigPath = string.Empty;
            }
        }
    }

    [DataContract]
    public sealed class DictionarySettings
    {
        [DataMember]
        public string ProviderName { get; set; }

        [DataMember]
        public string DictionaryFilePath { get; set; }

        [DataMember]
        public string MdictExecutablePath { get; set; }

        public void ApplyDefaults()
        {
            if (string.IsNullOrWhiteSpace(ProviderName))
            {
                ProviderName = "JsonFile";
            }

            if (string.IsNullOrWhiteSpace(DictionaryFilePath))
            {
                DictionaryFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dictionary.json");
            }

            if (MdictExecutablePath == null)
            {
                MdictExecutablePath = string.Empty;
            }
        }
    }
}
