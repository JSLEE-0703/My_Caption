namespace MyCaption.Core.Models
{
    public sealed class ControlPanelViewModel : BindableBase
    {
        private string _statusText;
        private string _providerText;
        private string _lookupProviderText;
        private bool _isRunning;
        private CaptureState _captureState;
        private bool _overlayVisible;
        private bool _originalOnTop;
        private bool _hideOriginalLiveCaptions;
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
