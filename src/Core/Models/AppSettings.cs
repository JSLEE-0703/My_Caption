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
        private bool? _autoLaunchIfMissingValue;
        private bool? _hideOriginalWindowValue;

        public LiveCaptionsSettings()
        {
            _autoLaunchIfMissingValue = true;
            _hideOriginalWindowValue = true;
        }

        public bool AutoLaunchIfMissing
        {
            get { return !_autoLaunchIfMissingValue.HasValue || _autoLaunchIfMissingValue.Value; }
            set { _autoLaunchIfMissingValue = value; }
        }

        [DataMember(Name = "AutoLaunchIfMissing", EmitDefaultValue = false)]
        private bool? AutoLaunchIfMissingValue
        {
            get { return _autoLaunchIfMissingValue; }
            set { _autoLaunchIfMissingValue = value; }
        }

        public bool HideOriginalWindow
        {
            get { return !_hideOriginalWindowValue.HasValue || _hideOriginalWindowValue.Value; }
            set { _hideOriginalWindowValue = value; }
        }

        [DataMember(Name = "HideOriginalWindow", EmitDefaultValue = false)]
        private bool? HideOriginalWindowValue
        {
            get { return _hideOriginalWindowValue; }
            set { _hideOriginalWindowValue = value; }
        }

        [DataMember]
        public int PollIntervalMs { get; set; }

        [DataMember]
        public int SyncCommitThreshold { get; set; }

        [DataMember]
        public int IdleCommitThreshold { get; set; }

        public void ApplyDefaults()
        {
            if (!_autoLaunchIfMissingValue.HasValue)
            {
                _autoLaunchIfMissingValue = true;
            }

            if (!_hideOriginalWindowValue.HasValue)
            {
                _hideOriginalWindowValue = true;
            }

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
            ExecutablePath = string.Empty;
            ArgumentsTemplate = string.Empty;
            ApiUrl = string.Empty;
            ApiKey = string.Empty;
            ApiRegion = string.Empty;
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

        [DataMember]
        public string ExecutablePath { get; set; }

        [DataMember]
        public string ArgumentsTemplate { get; set; }

        [DataMember]
        public string ApiUrl { get; set; }

        [DataMember]
        public string ApiKey { get; set; }

        [DataMember]
        public string ApiRegion { get; set; }

        public void ApplyDefaults()
        {
            if (!_enabledValue.HasValue)
            {
                _enabledValue = true;
            }

            if (string.IsNullOrWhiteSpace(ProviderName))
            {
                ProviderName = "ExternalCli";
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

            if (ExecutablePath == null)
            {
                ExecutablePath = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(ExecutablePath) || ShouldUseBundledTranslationExecutable(ExecutablePath))
            {
                ExecutablePath = ResolveDefaultTranslationExecutablePath();
            }

            if (ArgumentsTemplate == null)
            {
                ArgumentsTemplate = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(ArgumentsTemplate) || ShouldUseDefaultArgosArgumentsTemplate(ArgumentsTemplate))
            {
                ArgumentsTemplate = ResolveDefaultTranslationArgumentsTemplate();
            }

            if (ApiUrl == null)
            {
                ApiUrl = string.Empty;
            }

            if (ApiKey == null)
            {
                ApiKey = string.Empty;
            }

            if (ApiRegion == null)
            {
                ApiRegion = string.Empty;
            }
        }

        private static string ResolveDefaultTranslationExecutablePath()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string bundledPythonPath = Path.Combine(baseDirectory, "runtime", "python", "python.exe");
            if (File.Exists(bundledPythonPath))
            {
                return bundledPythonPath;
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                string knownEnvironmentPythonPath = Path.Combine(userProfile, ".conda", "envs", "herobot_env", "python.exe");
                if (File.Exists(knownEnvironmentPythonPath))
                {
                    return knownEnvironmentPythonPath;
                }
            }

            return bundledPythonPath;
        }

        private static bool ShouldUseBundledTranslationExecutable(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || File.Exists(executablePath))
            {
                return false;
            }

            string bundledPythonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "python", "python.exe");
            return File.Exists(bundledPythonPath);
        }

        private static string ResolveDefaultTranslationArgumentsTemplate()
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "argos_translate_stdin.py");
            return "\"" + scriptPath + "\" --from {from} --to {to}";
        }

        private static bool ShouldUseDefaultArgosArgumentsTemplate(string argumentsTemplate)
        {
            if (string.IsNullOrWhiteSpace(argumentsTemplate) ||
                argumentsTemplate.IndexOf("argos_translate_stdin.py", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            string scriptPath = ExtractArgosScriptPath(argumentsTemplate);
            return !string.IsNullOrWhiteSpace(scriptPath) &&
                !File.Exists(scriptPath) &&
                File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "argos_translate_stdin.py"));
        }

        private static string ExtractArgosScriptPath(string argumentsTemplate)
        {
            string text = argumentsTemplate == null ? string.Empty : argumentsTemplate.Trim();
            int scriptIndex = text.IndexOf("argos_translate_stdin.py", StringComparison.OrdinalIgnoreCase);
            if (scriptIndex < 0)
            {
                return string.Empty;
            }

            int start = scriptIndex;
            while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
            {
                start--;
            }

            string path = text.Substring(start, scriptIndex - start + "argos_translate_stdin.py".Length).Trim().Trim('"');
            return Path.IsPathRooted(path)
                ? path
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
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
                ProviderName = "MdictCli";
            }

            if (string.IsNullOrWhiteSpace(DictionaryFilePath))
            {
                DictionaryFilePath = ResolveDefaultDictionaryFilePath();
            }

            if (MdictExecutablePath == null)
            {
                MdictExecutablePath = string.Empty;
            }
        }

        private static string ResolveDefaultDictionaryFilePath()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDirectory, "dictionary", "default.mdx"),
                Path.Combine(baseDirectory, "dictionary.mdx")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return string.Empty;
        }
    }
}
