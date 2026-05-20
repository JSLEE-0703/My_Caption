using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Text;
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
        private readonly object _lookupSyncRoot;
        private readonly object _translationSyncRoot;
        private readonly AppSettings _settings;
        private readonly SettingsStore _settingsStore;
        private readonly LiveCaptionsCaptureService _captureService;
        private readonly CaptionStabilizer _stabilizer;
        private readonly TranslationProviderHost _translationProvider;
        private readonly TranslationDispatcher _translationDispatcher;
        private readonly LookupProviderHost _lookupProvider;
        private readonly AltKeyMonitor _altMonitor;
        private readonly Dispatcher _dispatcher;
        private CancellationTokenSource _lookupCancellationTokenSource;
        private int _lookupRequestVersion;
        private string _currentDisplayText;
        private string _lastRenderedTranslationSourceText;
        private bool _lastRenderedTranslationWasCommitted;

        public AppRuntime(
            AppSettings settings,
            SettingsStore settingsStore,
            LiveCaptionsCaptureService captureService,
            CaptionStabilizer stabilizer,
            TranslationProviderHost translationProvider,
            TranslationDispatcher translationDispatcher,
            LookupProviderHost lookupProvider,
            AltKeyMonitor altMonitor,
            Dispatcher dispatcher)
        {
            _lookupSyncRoot = new object();
            _translationSyncRoot = new object();
            _settings = settings;
            _settingsStore = settingsStore;
            _captureService = captureService;
            _stabilizer = stabilizer;
            _translationProvider = translationProvider;
            _translationDispatcher = translationDispatcher;
            _lookupProvider = lookupProvider;
            _altMonitor = altMonitor;
            _dispatcher = dispatcher;
            _currentDisplayText = string.Empty;
            _lastRenderedTranslationSourceText = string.Empty;
            _lastRenderedTranslationWasCommitted = false;

            Overlay = new OverlayViewModel();
            Overlay.OriginalFontSize = _settings.Overlay.OriginalFontSize;
            Overlay.TranslationFontSize = _settings.Overlay.TranslationFontSize;
            Overlay.BackgroundOpacity = _settings.Overlay.BackgroundOpacity;
            Overlay.ShowTranslationText = _settings.Overlay.ShowTranslationText;
            Panel = new ControlPanelViewModel();
            Panel.CaptureState = CaptureState.Paused;
            Panel.StatusText = "Press Resume Capture to start listening for Windows Live Captions.";
            Panel.ProviderText = string.Format("Translation: {0}", _translationProvider.DisplayName);
            Panel.TranslationStatusText = _translationProvider.StatusText;
            Panel.TranslationProviderName = _settings.Translation.ProviderName;
            Panel.TranslationSourceLanguage = _settings.Translation.SourceLanguage;
            Panel.TranslationTargetLanguage = _settings.Translation.TargetLanguage;
            Panel.TranslationExecutablePath = _translationProvider.ExecutablePath;
            Panel.TranslationArgumentsTemplate = _translationProvider.ArgumentsTemplate;
            Panel.TranslationApiUrl = _translationProvider.ApiUrl;
            Panel.TranslationApiKey = _translationProvider.ApiKey;
            Panel.TranslationApiRegion = _translationProvider.ApiRegion;
            Panel.LookupProviderText = string.Format("Lookup: {0}", _lookupProvider.DisplayName);
            Panel.LookupStatusText = _lookupProvider.StatusText;
            Panel.DictionaryProviderName = _settings.Dictionary.ProviderName;
            Panel.DictionaryFilePath = _lookupProvider.DictionaryFilePath;
            Panel.MdictExecutablePath = _lookupProvider.MdictExecutablePath;
            Panel.OverlayVisible = true;
            Panel.OriginalOnTop = _settings.Overlay.OriginalOnTop;
            Panel.HideOriginalLiveCaptions = _settings.LiveCaptions.HideOriginalWindow;
            Panel.TranslationEnabled = _settings.Translation.Enabled;
            Panel.ShowTranslationText = _settings.Overlay.ShowTranslationText;
            Panel.FontSize = _settings.Overlay.OriginalFontSize;
            Panel.BackgroundOpacity = _settings.Overlay.BackgroundOpacity;

            _captureService.SnapshotCaptured += OnSnapshotCaptured;
            _captureService.StateChanged += OnCaptureStateChanged;
            _translationDispatcher.TranslationCompleted += OnTranslationCompleted;
            _translationProvider.ProviderStatusChanged += OnTranslationProviderStatusChanged;
            _lookupProvider.ProviderStatusChanged += OnLookupProviderStatusChanged;
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
            CancelLookup();
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
            Overlay.ShowTranslationText = _settings.Overlay.ShowTranslationText;
            Panel.FontSize = _settings.Overlay.OriginalFontSize;
            Panel.BackgroundOpacity = _settings.Overlay.BackgroundOpacity;
            Panel.ShowTranslationText = _settings.Overlay.ShowTranslationText;
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

        public void UpdateTranslationEnabled(bool enabled)
        {
            _settings.Translation.Enabled = enabled;
            Panel.TranslationEnabled = enabled;
            if (!enabled)
            {
                lock (_translationSyncRoot)
                {
                    _lastRenderedTranslationSourceText = string.Empty;
                    _lastRenderedTranslationWasCommitted = false;
                }

                Overlay.UpdateTranslationText(string.Empty);
                Panel.PreviewTranslation = string.Empty;
            }

            SaveSettings();
        }

        public void UpdateTranslationProviderName(string providerName)
        {
            _translationProvider.UpdateProviderName(providerName);
            _settings.Translation.ProviderName = string.IsNullOrWhiteSpace(providerName) ? "Stub" : providerName.Trim();
            Panel.TranslationProviderName = _settings.Translation.ProviderName;
            Panel.TranslationExecutablePath = _translationProvider.ExecutablePath;
            Panel.TranslationArgumentsTemplate = _translationProvider.ArgumentsTemplate;
            Panel.TranslationApiUrl = _translationProvider.ApiUrl;
            Panel.TranslationApiKey = _translationProvider.ApiKey;
            Panel.TranslationApiRegion = _translationProvider.ApiRegion;
            SaveSettings();
        }

        public void UpdateTranslationSourceLanguage(string sourceLanguage)
        {
            string normalized = string.IsNullOrWhiteSpace(sourceLanguage) ? "auto" : sourceLanguage.Trim();
            _settings.Translation.SourceLanguage = normalized;
            Panel.TranslationSourceLanguage = normalized;
            SaveSettings();
        }

        public void UpdateTranslationTargetLanguage(string targetLanguage)
        {
            string normalized = string.IsNullOrWhiteSpace(targetLanguage) ? "zh-CN" : targetLanguage.Trim();
            _settings.Translation.TargetLanguage = normalized;
            Panel.TranslationTargetLanguage = normalized;
            SaveSettings();
        }

        public void UpdateTranslationExecutablePath(string executablePath)
        {
            _translationProvider.UpdateExecutablePath(executablePath);
            _settings.Translation.ExecutablePath = _translationProvider.ExecutablePath;
            Panel.TranslationExecutablePath = _translationProvider.ExecutablePath;
            SaveSettings();
        }

        public void UpdateTranslationArgumentsTemplate(string argumentsTemplate)
        {
            _translationProvider.UpdateArgumentsTemplate(argumentsTemplate);
            _settings.Translation.ArgumentsTemplate = _translationProvider.ArgumentsTemplate;
            Panel.TranslationArgumentsTemplate = _translationProvider.ArgumentsTemplate;
            SaveSettings();
        }

        public void UpdateTranslationApiUrl(string apiUrl)
        {
            _translationProvider.UpdateApiUrl(apiUrl);
            _settings.Translation.ApiUrl = _translationProvider.ApiUrl;
            Panel.TranslationApiUrl = _translationProvider.ApiUrl;
            SaveSettings();
        }

        public void UpdateTranslationApiKey(string apiKey)
        {
            _translationProvider.UpdateApiKey(apiKey);
            _settings.Translation.ApiKey = _translationProvider.ApiKey;
            Panel.TranslationApiKey = _translationProvider.ApiKey;
            SaveSettings();
        }

        public void UpdateTranslationApiRegion(string apiRegion)
        {
            _translationProvider.UpdateApiRegion(apiRegion);
            _settings.Translation.ApiRegion = _translationProvider.ApiRegion;
            Panel.TranslationApiRegion = _translationProvider.ApiRegion;
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

        public void UpdateShowTranslationText(bool showTranslationText)
        {
            _settings.Overlay.ShowTranslationText = showTranslationText;
            Overlay.ShowTranslationText = showTranslationText;
            Panel.ShowTranslationText = showTranslationText;
            SaveSettings();
            RaiseOverlaySettingsChanged();
        }

        public void UpdateDictionaryFilePath(string dictionaryFilePath)
        {
            _lookupProvider.UpdateDictionaryFilePath(dictionaryFilePath);
            _settings.Dictionary.ProviderName = _settings.Dictionary.ProviderName ?? "JsonFile";
            _settings.Dictionary.DictionaryFilePath = _lookupProvider.DictionaryFilePath;
            Panel.DictionaryFilePath = _lookupProvider.DictionaryFilePath;
            SaveSettings();
        }

        public void UpdateDictionaryProviderName(string providerName)
        {
            _lookupProvider.UpdateProviderName(providerName);
            _settings.Dictionary.ProviderName = string.IsNullOrWhiteSpace(providerName) ? "JsonFile" : providerName.Trim();
            _settings.Dictionary.DictionaryFilePath = _lookupProvider.DictionaryFilePath;
            _settings.Dictionary.MdictExecutablePath = _lookupProvider.MdictExecutablePath;
            Panel.DictionaryProviderName = _settings.Dictionary.ProviderName;
            Panel.DictionaryFilePath = _lookupProvider.DictionaryFilePath;
            Panel.MdictExecutablePath = _lookupProvider.MdictExecutablePath;
            SaveSettings();
        }

        public void UpdateMdictExecutablePath(string mdictExecutablePath)
        {
            _lookupProvider.UpdateMdictExecutablePath(mdictExecutablePath);
            _settings.Dictionary.MdictExecutablePath = _lookupProvider.MdictExecutablePath;
            Panel.MdictExecutablePath = _lookupProvider.MdictExecutablePath;
            SaveSettings();
        }

        public async void LookupAsync(WordTokenViewModel token)
        {
            if (token == null || !token.CanClick)
            {
                return;
            }

            CancellationToken cancellationToken;
            int requestVersion;
            lock (_lookupSyncRoot)
            {
                if (_lookupCancellationTokenSource != null)
                {
                    _lookupCancellationTokenSource.Cancel();
                    _lookupCancellationTokenSource.Dispose();
                }

                _lookupCancellationTokenSource = new CancellationTokenSource();
                cancellationToken = _lookupCancellationTokenSource.Token;
                _lookupRequestVersion++;
                requestVersion = _lookupRequestVersion;
            }

            try
            {
                LookupResult result = await _lookupProvider.LookupAsync(token.LookupKey, cancellationToken);
                if (!IsLookupRequestCurrent(requestVersion, cancellationToken))
                {
                    return;
                }

                var ignoredDispatch = _dispatcher.BeginInvoke(new Action(delegate
                {
                    if (IsLookupRequestCurrent(requestVersion, cancellationToken))
                    {
                        Overlay.UpdateLookup(result);
                    }
                }));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!IsLookupRequestCurrent(requestVersion, cancellationToken))
                {
                    return;
                }

                var ignoredDispatch = _dispatcher.BeginInvoke(new Action(delegate
                {
                    if (IsLookupRequestCurrent(requestVersion, cancellationToken))
                    {
                        Overlay.UpdateLookup(new LookupResult(
                            token.Text,
                            string.Empty,
                            null,
                            string.Empty,
                            "Dictionary lookup failed: " + ex.Message,
                            false));
                    }
                }));
            }
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

            lock (_translationSyncRoot)
            {
                _currentDisplayText = update.DisplayText ?? string.Empty;
            }

            _dispatcher.BeginInvoke(new Action(delegate
            {
                Overlay.UpdateOriginalText(update.DisplayText, tokens);
                Panel.PreviewOriginal = update.DisplayText;
            }));

            if (!string.IsNullOrWhiteSpace(update.TranslationRequestText))
            {
                if (!_settings.Translation.Enabled)
                {
                    _dispatcher.BeginInvoke(new Action(delegate
                    {
                        Overlay.UpdateTranslationText(string.Empty);
                        Panel.PreviewTranslation = string.Empty;
                    }));
                    return;
                }

                _translationDispatcher.Request(new TranslationRequest(
                    update.TranslationRequestText,
                    _settings.Translation.SourceLanguage,
                    _settings.Translation.TargetLanguage,
                    update.IsCommitted));
            }
        }

        private void OnTranslationCompleted(object sender, TranslationCompletedEventArgs e)
        {
            if (e == null || e.Result == null)
            {
                return;
            }

            lock (_translationSyncRoot)
            {
                if (!IsTranslationStillRelevant(_currentDisplayText, e.Result.SourceText))
                {
                    return;
                }

                if (!ShouldApplyTranslationResult(e.Result))
                {
                    return;
                }

                _lastRenderedTranslationSourceText = e.Result.SourceText ?? string.Empty;
                _lastRenderedTranslationWasCommitted = e.Result.IsCommitted;
            }

            _dispatcher.BeginInvoke(new Action(delegate
            {
                Overlay.UpdateTranslationText(e.Result.TranslatedText);
                Panel.PreviewTranslation = e.Result.TranslatedText;
            }));
        }

        private static bool IsTranslationStillRelevant(string displayText, string sourceText)
        {
            if (string.IsNullOrWhiteSpace(displayText) || string.IsNullOrWhiteSpace(sourceText))
            {
                return false;
            }

            if (string.Equals(displayText, sourceText, StringComparison.Ordinal))
            {
                return true;
            }

            return displayText.StartsWith(sourceText, StringComparison.Ordinal) ||
                sourceText.StartsWith(displayText, StringComparison.Ordinal);
        }

        private bool ShouldApplyTranslationResult(TranslationResult result)
        {
            if (result == null)
            {
                return false;
            }

            if (result.IsCommitted)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(_lastRenderedTranslationSourceText))
            {
                return true;
            }

            if (!_lastRenderedTranslationWasCommitted)
            {
                return true;
            }

            string previousSource = _lastRenderedTranslationSourceText;
            string currentSource = result.SourceText ?? string.Empty;

            if (currentSource.StartsWith(previousSource, StringComparison.Ordinal))
            {
                return GetUtf8Length(currentSource) - GetUtf8Length(previousSource) >= 8;
            }

            if (previousSource.StartsWith(currentSource, StringComparison.Ordinal))
            {
                return false;
            }

            return GetUtf8Length(currentSource) >= 12;
        }

        private static int GetUtf8Length(string value)
        {
            return string.IsNullOrEmpty(value) ? 0 : Encoding.UTF8.GetByteCount(value);
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
                CancelLookup();
                Overlay.UpdateLookup(null);
            }

            EventHandler handler = InteractiveModeChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnTranslationProviderStatusChanged(object sender, EventArgs e)
        {
            _dispatcher.BeginInvoke(new Action(delegate
            {
                Panel.ProviderText = string.Format("Translation: {0}", _translationProvider.DisplayName);
                Panel.TranslationStatusText = _translationProvider.StatusText;
                Panel.TranslationProviderName = _settings.Translation.ProviderName;
                Panel.TranslationExecutablePath = _translationProvider.ExecutablePath;
                Panel.TranslationArgumentsTemplate = _translationProvider.ArgumentsTemplate;
                Panel.TranslationApiUrl = _translationProvider.ApiUrl;
                Panel.TranslationApiKey = _translationProvider.ApiKey;
                Panel.TranslationApiRegion = _translationProvider.ApiRegion;
            }));
        }

        private void OnLookupProviderStatusChanged(object sender, EventArgs e)
        {
            _dispatcher.BeginInvoke(new Action(delegate
            {
                Panel.LookupProviderText = string.Format("Lookup: {0}", _lookupProvider.DisplayName);
                Panel.LookupStatusText = _lookupProvider.StatusText;
                Panel.DictionaryProviderName = _settings.Dictionary.ProviderName;
                Panel.DictionaryFilePath = _lookupProvider.DictionaryFilePath;
                Panel.MdictExecutablePath = _lookupProvider.MdictExecutablePath;
            }));
        }

        private void RaiseOverlaySettingsChanged()
        {
            EventHandler handler = OverlaySettingsChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private bool IsLookupRequestCurrent(int requestVersion, CancellationToken cancellationToken)
        {
            lock (_lookupSyncRoot)
            {
                return !cancellationToken.IsCancellationRequested &&
                    requestVersion == _lookupRequestVersion &&
                    _lookupCancellationTokenSource != null &&
                    cancellationToken == _lookupCancellationTokenSource.Token;
            }
        }

        private void CancelLookup()
        {
            lock (_lookupSyncRoot)
            {
                _lookupRequestVersion++;

                if (_lookupCancellationTokenSource != null)
                {
                    _lookupCancellationTokenSource.Cancel();
                    _lookupCancellationTokenSource.Dispose();
                    _lookupCancellationTokenSource = null;
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _captureService.SnapshotCaptured -= OnSnapshotCaptured;
            _captureService.StateChanged -= OnCaptureStateChanged;
            _translationDispatcher.TranslationCompleted -= OnTranslationCompleted;
            _translationProvider.ProviderStatusChanged -= OnTranslationProviderStatusChanged;
            _lookupProvider.ProviderStatusChanged -= OnLookupProviderStatusChanged;
            _altMonitor.AltStateChanged -= OnAltStateChanged;
            _translationDispatcher.Dispose();
            _translationProvider.Dispose();
            _altMonitor.Dispose();
            SaveSettings();
        }
    }
}
