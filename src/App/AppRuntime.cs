using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using MyCaption.Core.Capture;
using MyCaption.Core.Lookup;
using MyCaption.Core.Models;
using MyCaption.Core.Stabilization;
using MyCaption.Core.Translation;
using MyCaption.Infrastructure.Persistence;
using MyCaption.Infrastructure.Windows;

namespace MyCaption.Runtime
{
    public sealed class AppRuntime : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly SettingsStore _settingsStore;
        private readonly LiveCaptionsCaptureService _captureService;
        private readonly CaptionStabilizer _stabilizer;
        private readonly TranslationDispatcher _translationDispatcher;
        private readonly ILookupProvider _lookupProvider;
        private readonly AltKeyMonitor _altMonitor;
        private readonly Dispatcher _dispatcher;

        public AppRuntime(
            AppSettings settings,
            SettingsStore settingsStore,
            LiveCaptionsCaptureService captureService,
            CaptionStabilizer stabilizer,
            TranslationDispatcher translationDispatcher,
            ILookupProvider lookupProvider,
            AltKeyMonitor altMonitor,
            Dispatcher dispatcher)
        {
            _settings = settings;
            _settingsStore = settingsStore;
            _captureService = captureService;
            _stabilizer = stabilizer;
            _translationDispatcher = translationDispatcher;
            _lookupProvider = lookupProvider;
            _altMonitor = altMonitor;
            _dispatcher = dispatcher;

            Overlay = new OverlayViewModel();
            Overlay.OriginalFontSize = _settings.Overlay.OriginalFontSize;
            Overlay.TranslationFontSize = _settings.Overlay.TranslationFontSize;
            Overlay.BackgroundOpacity = _settings.Overlay.BackgroundOpacity;
            Panel = new ControlPanelViewModel();
            Panel.CaptureState = CaptureState.Paused;
            Panel.StatusText = "Press Resume Capture to start listening for Windows Live Captions.";
            Panel.ProviderText = string.Format("Translation: {0} - {1}", _translationDispatcher.Provider.DisplayName, _translationDispatcher.Provider.Description);
            Panel.LookupProviderText = string.Format("Lookup: {0}", _lookupProvider.DisplayName);
            Panel.OverlayVisible = true;
            Panel.OriginalOnTop = _settings.Overlay.OriginalOnTop;
            Panel.HideOriginalLiveCaptions = _settings.LiveCaptions.HideOriginalWindow;
            Panel.FontSize = _settings.Overlay.OriginalFontSize;
            Panel.BackgroundOpacity = _settings.Overlay.BackgroundOpacity;

            _captureService.SnapshotCaptured += OnSnapshotCaptured;
            _captureService.StateChanged += OnCaptureStateChanged;
            _translationDispatcher.TranslationCompleted += OnTranslationCompleted;
            _altMonitor.AltStateChanged += OnAltStateChanged;
        }

        public event EventHandler InteractiveModeChanged;
        public event EventHandler OverlaySettingsChanged;

        public OverlayViewModel Overlay { get; private set; }

        public ControlPanelViewModel Panel { get; private set; }

        public AppSettings Settings
        {
            get { return _settings; }
        }

        public void Start()
        {
            Panel.IsRunning = true;
            Panel.CaptureState = CaptureState.Starting;
            Panel.StatusText = "Looking for Windows Live Captions...";
            _captureService.Start();
            _altMonitor.Start();
        }

        public void Stop()
        {
            Panel.IsRunning = false;
            Panel.CaptureState = CaptureState.Paused;
            Panel.StatusText = "Capture paused.";
            _captureService.Stop();
            _altMonitor.Stop();
            Overlay.UpdateLookup(null);
        }

        public void SaveSettings()
        {
            _settingsStore.Save(_settings);
        }

        public void SaveOverlayBounds(Rect bounds)
        {
            _settings.Overlay.Left = bounds.Left;
            _settings.Overlay.Top = bounds.Top;
            _settings.Overlay.Width = bounds.Width;
            _settings.Overlay.Height = bounds.Height;
            SaveSettings();
        }

        public void ResetOverlayBounds()
        {
            _settings.Overlay = new OverlaySettings();
            _settings.Overlay.ApplyDefaults();
            Overlay.OriginalFontSize = _settings.Overlay.OriginalFontSize;
            Overlay.TranslationFontSize = _settings.Overlay.TranslationFontSize;
            Overlay.BackgroundOpacity = _settings.Overlay.BackgroundOpacity;
            Panel.FontSize = _settings.Overlay.OriginalFontSize;
            Panel.BackgroundOpacity = _settings.Overlay.BackgroundOpacity;
            SaveSettings();
            RaiseOverlaySettingsChanged();
        }

        public void UpdateOverlayVisible(bool isVisible)
        {
            Panel.OverlayVisible = isVisible;
        }

        public void UpdateOriginalOrder(bool originalOnTop)
        {
            _settings.Overlay.OriginalOnTop = originalOnTop;
            Panel.OriginalOnTop = originalOnTop;
            SaveSettings();
            RaiseOverlaySettingsChanged();
        }

        public void UpdateHideOriginalWindow(bool hideOriginalWindow)
        {
            _settings.LiveCaptions.HideOriginalWindow = hideOriginalWindow;
            Panel.HideOriginalLiveCaptions = hideOriginalWindow;
            _captureService.UpdateOriginalWindowVisibility(hideOriginalWindow);
            SaveSettings();
        }

        public void UpdateFontSize(double fontSize)
        {
            _settings.Overlay.OriginalFontSize = fontSize;
            _settings.Overlay.TranslationFontSize = Math.Max(18.0, fontSize - 2.0);
            Overlay.OriginalFontSize = _settings.Overlay.OriginalFontSize;
            Overlay.TranslationFontSize = _settings.Overlay.TranslationFontSize;
            Panel.FontSize = fontSize;
            SaveSettings();
            RaiseOverlaySettingsChanged();
        }

        public void UpdateBackgroundOpacity(double opacity)
        {
            _settings.Overlay.BackgroundOpacity = opacity;
            Overlay.BackgroundOpacity = opacity;
            Panel.BackgroundOpacity = opacity;
            SaveSettings();
            RaiseOverlaySettingsChanged();
        }

        public async void LookupAsync(WordTokenViewModel token)
        {
            if (token == null || !token.CanClick)
            {
                return;
            }

            LookupResult result = await _lookupProvider.LookupAsync(token.LookupKey, CancellationToken.None);
            Overlay.UpdateLookup(result);
        }

        private void OnCaptureStateChanged(object sender, CaptureStateChangedEventArgs e)
        {
            _dispatcher.BeginInvoke(new Action(delegate
            {
                Panel.IsRunning = e.IsCaptureActive;
                Panel.CaptureState = e.State;
                Panel.StatusText = e.StatusText;
            }));
        }

        private void OnSnapshotCaptured(object sender, LiveCaptionsSnapshotEventArgs e)
        {
            StabilizedCaptionUpdate update = _stabilizer.Process(e.Snapshot);
            if (update == null)
            {
                return;
            }

            var tokens = CaptionStabilizer.Tokenize(update.DisplayText);

            _dispatcher.BeginInvoke(new Action(delegate
            {
                Overlay.UpdateOriginalText(update.DisplayText, tokens);
                Panel.PreviewOriginal = update.DisplayText;
            }));

            if (!string.IsNullOrWhiteSpace(update.TranslationRequestText))
            {
                _translationDispatcher.Request(new TranslationRequest(
                    update.TranslationRequestText,
                    _settings.Translation.SourceLanguage,
                    _settings.Translation.TargetLanguage));
            }
        }

        private void OnTranslationCompleted(object sender, TranslationCompletedEventArgs e)
        {
            _dispatcher.BeginInvoke(new Action(delegate
            {
                Overlay.UpdateTranslationText(e.Result.TranslatedText);
                Panel.PreviewTranslation = e.Result.TranslatedText;
            }));
        }

        private void OnAltStateChanged(object sender, AltStateChangedEventArgs e)
        {
            if (!_settings.Interaction.HoldAltToInteract)
            {
                return;
            }

            Overlay.IsInteractive = e.IsAltPressed;
            if (!e.IsAltPressed)
            {
                Overlay.UpdateLookup(null);
            }

            EventHandler handler = InteractiveModeChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void RaiseOverlaySettingsChanged()
        {
            EventHandler handler = OverlaySettingsChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            Stop();
            _captureService.SnapshotCaptured -= OnSnapshotCaptured;
            _captureService.StateChanged -= OnCaptureStateChanged;
            _translationDispatcher.TranslationCompleted -= OnTranslationCompleted;
            _altMonitor.AltStateChanged -= OnAltStateChanged;
            _translationDispatcher.Dispose();
            _altMonitor.Dispose();
            SaveSettings();
        }
    }
}
