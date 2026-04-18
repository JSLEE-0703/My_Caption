using System;
using System.Windows;
using MyCaption.Runtime;
using MyCaption.UI.Overlay;

namespace MyCaption.UI.MainWindow
{
    public partial class MainWindow : Window
    {
        private readonly AppRuntime _runtime;
        private OverlayWindow _overlayWindow;
        private bool _isInitializing;

        public MainWindow(AppRuntime runtime)
        {
            _runtime = runtime;
            _isInitializing = true;

            InitializeComponent();
            DataContext = _runtime.Panel;

            Loaded += OnLoaded;
            Closed += OnClosed;
            _runtime.OverlaySettingsChanged += Runtime_OverlaySettingsChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            OriginalOnTopCheckBox.IsChecked = _runtime.Panel.OriginalOnTop;
            HideOriginalWindowCheckBox.IsChecked = _runtime.Panel.HideOriginalLiveCaptions;
            FontSizeSlider.Value = _runtime.Panel.FontSize;
            OpacitySlider.Value = _runtime.Panel.BackgroundOpacity;
            EnsureOverlayWindow();
            _isInitializing = false;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Close();
                _overlayWindow = null;
            }

            _runtime.OverlaySettingsChanged -= Runtime_OverlaySettingsChanged;
        }

        private void Runtime_OverlaySettingsChanged(object sender, EventArgs e)
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.ApplyOverlaySettings();
            }
        }

        private void EnsureOverlayWindow()
        {
            if (!_runtime.Panel.OverlayVisible)
            {
                return;
            }

            if (_overlayWindow == null)
            {
                _overlayWindow = new OverlayWindow(_runtime);
                _overlayWindow.Closed += OverlayWindow_Closed;
                _overlayWindow.Show();
            }
            else if (!_overlayWindow.IsVisible)
            {
                _overlayWindow.Show();
            }
        }

        private void OverlayWindow_Closed(object sender, EventArgs e)
        {
            _overlayWindow = null;
            _runtime.UpdateOverlayVisible(false);
            OverlayToggleButton.Content = "Show Overlay";
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_runtime.Panel.IsRunning)
            {
                _runtime.Stop();
            }
            else
            {
                _runtime.Start();
            }
        }

        private void OverlayToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_runtime.Panel.OverlayVisible)
            {
                if (_overlayWindow != null)
                {
                    _overlayWindow.Close();
                    _overlayWindow = null;
                }

                _runtime.UpdateOverlayVisible(false);
            }
            else
            {
                _runtime.UpdateOverlayVisible(true);
                EnsureOverlayWindow();
            }
            OverlayToggleButton.Content = _runtime.Panel.OverlayVisible ? "Hide Overlay" : "Show Overlay";
        }

        private void ResetOverlayButton_Click(object sender, RoutedEventArgs e)
        {
            _runtime.ResetOverlayBounds();
            EnsureOverlayWindow();
        }

        private void OriginalOnTopCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            _runtime.UpdateOriginalOrder(OriginalOnTopCheckBox.IsChecked == true);
        }

        private void HideOriginalWindowCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            _runtime.UpdateHideOriginalWindow(HideOriginalWindowCheckBox.IsChecked == true);
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing)
            {
                return;
            }

            _runtime.UpdateFontSize(e.NewValue);
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing)
            {
                return;
            }

            _runtime.UpdateBackgroundOpacity(e.NewValue);
        }
    }
}
