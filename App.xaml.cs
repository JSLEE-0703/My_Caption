using System.Windows;
using MyCaption.Core.Capture;
using MyCaption.Core.Lookup;
using MyCaption.Core.Stabilization;
using MyCaption.Core.Translation;
using MyCaption.Infrastructure.Automation;
using MyCaption.Infrastructure.Persistence;
using MyCaption.Infrastructure.Windows;
using MyCaption.Runtime;
using MyCaption.UI.MainWindow;

namespace MyCaption
{
    public partial class App : Application
    {
        private AppRuntime _runtime;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            SettingsStore settingsStore = new SettingsStore();
            var settings = settingsStore.Load();
            LiveCaptionsAutomationClient automationClient = new LiveCaptionsAutomationClient();
            LiveCaptionsCaptureService captureService = new LiveCaptionsCaptureService(automationClient, settings.LiveCaptions);
            CaptionStabilizer stabilizer = new CaptionStabilizer(
                settings.LiveCaptions.SyncCommitThreshold,
                settings.LiveCaptions.IdleCommitThreshold);
            ITranslationProviderFactory translationProviderFactory = new TranslationProviderFactory();
            ITranslationProvider translationProvider = translationProviderFactory.Create(settings.Translation);
            TranslationDispatcher translationDispatcher = new TranslationDispatcher(translationProvider);
            ILookupProviderFactory lookupProviderFactory = new LookupProviderFactory();
            LookupProviderHost lookupProvider = new LookupProviderHost(lookupProviderFactory, settings.Dictionary);
            AltKeyMonitor altMonitor = new AltKeyMonitor();

            _runtime = new AppRuntime(
                settings,
                settingsStore,
                captureService,
                stabilizer,
                translationDispatcher,
                lookupProvider,
                altMonitor,
                Dispatcher);

            MainWindow mainWindow = new MainWindow(_runtime);
            MainWindow = mainWindow;
            _runtime.Start();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_runtime != null)
            {
                _runtime.Dispose();
            }

            base.OnExit(e);
        }
    }
}
