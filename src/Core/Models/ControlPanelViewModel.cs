namespace MyCaption.Core.Models
{
    public sealed class ControlPanelViewModel : BindableBase
    {
        private string _statusText;
        private string _providerText;
        private string _lookupProviderText;
        private string _lookupStatusText;
        private string _translationStatusText;
        private string _translationProviderName;
        private string _translationSourceLanguage;
        private string _translationTargetLanguage;
        private string _translationExecutablePath;
        private string _translationArgumentsTemplate;
        private string _translationApiUrl;
        private string _translationApiKey;
        private string _translationApiRegion;
        private string _dictionaryProviderName;
        private string _dictionaryFilePath;
        private string _mdictExecutablePath;
        private bool _isRunning;
        private CaptureState _captureState;
        private bool _overlayVisible;
        private bool _originalOnTop;
        private bool _hideOriginalLiveCaptions;
        private bool _translationEnabled;
        private bool _showTranslationText;
        private double _fontSize;
        private double _backgroundOpacity;
        private string _previewOriginal;
        private string _previewTranslation;

        public string StatusText
        {
            get { return _statusText; }
            set { SetProperty(ref _statusText, value, "StatusText"); }
        }

        public string ProviderText
        {
            get { return _providerText; }
            set { SetProperty(ref _providerText, value, "ProviderText"); }
        }

        public string LookupProviderText
        {
            get { return _lookupProviderText; }
            set { SetProperty(ref _lookupProviderText, value, "LookupProviderText"); }
        }

        public string LookupStatusText
        {
            get { return _lookupStatusText; }
            set { SetProperty(ref _lookupStatusText, value, "LookupStatusText"); }
        }

        public string TranslationStatusText
        {
            get { return _translationStatusText; }
            set { SetProperty(ref _translationStatusText, value, "TranslationStatusText"); }
        }

        public string TranslationProviderName
        {
            get { return _translationProviderName; }
            set { SetProperty(ref _translationProviderName, value, "TranslationProviderName"); }
        }

        public string TranslationSourceLanguage
        {
            get { return _translationSourceLanguage; }
            set { SetProperty(ref _translationSourceLanguage, value, "TranslationSourceLanguage"); }
        }

        public string TranslationTargetLanguage
        {
            get { return _translationTargetLanguage; }
            set { SetProperty(ref _translationTargetLanguage, value, "TranslationTargetLanguage"); }
        }

        public string TranslationExecutablePath
        {
            get { return _translationExecutablePath; }
            set { SetProperty(ref _translationExecutablePath, value, "TranslationExecutablePath"); }
        }

        public string TranslationArgumentsTemplate
        {
            get { return _translationArgumentsTemplate; }
            set { SetProperty(ref _translationArgumentsTemplate, value, "TranslationArgumentsTemplate"); }
        }

        public string TranslationApiUrl
        {
            get { return _translationApiUrl; }
            set { SetProperty(ref _translationApiUrl, value, "TranslationApiUrl"); }
        }

        public string TranslationApiKey
        {
            get { return _translationApiKey; }
            set { SetProperty(ref _translationApiKey, value, "TranslationApiKey"); }
        }

        public string TranslationApiRegion
        {
            get { return _translationApiRegion; }
            set { SetProperty(ref _translationApiRegion, value, "TranslationApiRegion"); }
        }

        public string DictionaryProviderName
        {
            get { return _dictionaryProviderName; }
            set { SetProperty(ref _dictionaryProviderName, value, "DictionaryProviderName"); }
        }

        public string DictionaryFilePath
        {
            get { return _dictionaryFilePath; }
            set { SetProperty(ref _dictionaryFilePath, value, "DictionaryFilePath"); }
        }

        public string MdictExecutablePath
        {
            get { return _mdictExecutablePath; }
            set { SetProperty(ref _mdictExecutablePath, value, "MdictExecutablePath"); }
        }

        public bool IsRunning
        {
            get { return _isRunning; }
            set
            {
                if (SetProperty(ref _isRunning, value, "IsRunning"))
                {
                    OnPropertyChanged("StartStopButtonText");
                    OnPropertyChanged("CanResumeCapture");
                }
            }
        }

        public CaptureState CaptureState
        {
            get { return _captureState; }
            set
            {
                if (SetProperty(ref _captureState, value, "CaptureState"))
                {
                    OnPropertyChanged("StartStopButtonText");
                    OnPropertyChanged("CanResumeCapture");
                    OnPropertyChanged("StatusBadgeText");
                }
            }
        }

        public bool OverlayVisible
        {
            get { return _overlayVisible; }
            set { SetProperty(ref _overlayVisible, value, "OverlayVisible"); }
        }

        public bool OriginalOnTop
        {
            get { return _originalOnTop; }
            set { SetProperty(ref _originalOnTop, value, "OriginalOnTop"); }
        }

        public bool HideOriginalLiveCaptions
        {
            get { return _hideOriginalLiveCaptions; }
            set { SetProperty(ref _hideOriginalLiveCaptions, value, "HideOriginalLiveCaptions"); }
        }

        public bool TranslationEnabled
        {
            get { return _translationEnabled; }
            set { SetProperty(ref _translationEnabled, value, "TranslationEnabled"); }
        }

        public bool ShowTranslationText
        {
            get { return _showTranslationText; }
            set { SetProperty(ref _showTranslationText, value, "ShowTranslationText"); }
        }

        public double FontSize
        {
            get { return _fontSize; }
            set { SetProperty(ref _fontSize, value, "FontSize"); }
        }

        public double BackgroundOpacity
        {
            get { return _backgroundOpacity; }
            set { SetProperty(ref _backgroundOpacity, value, "BackgroundOpacity"); }
        }

        public string PreviewOriginal
        {
            get { return _previewOriginal; }
            set { SetProperty(ref _previewOriginal, value, "PreviewOriginal"); }
        }

        public string PreviewTranslation
        {
            get { return _previewTranslation; }
            set { SetProperty(ref _previewTranslation, value, "PreviewTranslation"); }
        }

        public string StartStopButtonText
        {
            get { return CaptureState == CaptureState.Paused ? "Resume Capture" : "Pause Capture"; }
        }

        public bool CanResumeCapture
        {
            get { return CaptureState == CaptureState.Paused; }
        }

        public string StatusBadgeText
        {
            get
            {
                switch (CaptureState)
                {
                    case CaptureState.Starting:
                        return "Starting";
                    case CaptureState.ConnectedIdle:
                        return "Ready";
                    case CaptureState.Running:
                        return "Capturing";
                    case CaptureState.WaitingForSource:
                        return "Waiting";
                    case CaptureState.Paused:
                    default:
                        return "Paused";
                }
            }
        }
    }
}
