using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
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
            ShowTranslationTextCheckBox.IsChecked = _runtime.Panel.ShowTranslationText;
            FontSizeSlider.Value = _runtime.Panel.FontSize;
            OpacitySlider.Value = _runtime.Panel.BackgroundOpacity;
            SelectLookupProvider(_runtime.Panel.DictionaryProviderName);
            DictionaryPathTextBox.Text = _runtime.Panel.DictionaryFilePath;
            MdictExecutablePathTextBox.Text = _runtime.Panel.MdictExecutablePath;
            UpdateLookupProviderControls();
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

        private void ShowTranslationTextCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            _runtime.UpdateShowTranslationText(ShowTranslationTextCheckBox.IsChecked == true);
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

        private void DictionaryPathTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitDictionaryPath();
        }

        private void DictionaryPathTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CommitDictionaryPath();
            e.Handled = true;
        }

        private void BrowseDictionaryButton_Click(object sender, RoutedEventArgs e)
        {
            string currentPath = DictionaryPathTextBox.Text;
            OpenFileDialog dialog = new OpenFileDialog();
            bool isMdictProvider = string.Equals(GetSelectedLookupProviderName(), "MdictCli", StringComparison.OrdinalIgnoreCase);
            dialog.Filter = isMdictProvider
                ? "MDict files (*.mdx)|*.mdx|All files (*.*)|*.*"
                : "JSON files (*.json)|*.json|All files (*.*)|*.*";
            dialog.CheckFileExists = isMdictProvider;
            dialog.FileName = string.IsNullOrWhiteSpace(currentPath)
                ? (isMdictProvider ? "dictionary.mdx" : "dictionary.json")
                : Path.GetFileName(currentPath);

            string initialDirectory = string.Empty;
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                try
                {
                    initialDirectory = Path.GetDirectoryName(currentPath);
                }
                catch
                {
                    initialDirectory = string.Empty;
                }
            }

            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }

            bool? result = dialog.ShowDialog(this);
            if (result == true)
            {
                DictionaryPathTextBox.Text = dialog.FileName;
                CommitDictionaryPath();
            }
        }

        private void CommitDictionaryPath()
        {
            if (_isInitializing)
            {
                return;
            }

            string currentText = DictionaryPathTextBox.Text ?? string.Empty;
            string savedPath = _runtime.Panel.DictionaryFilePath ?? string.Empty;
            if (string.Equals(currentText.Trim(), savedPath.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _runtime.UpdateDictionaryFilePath(currentText);
            DictionaryPathTextBox.Text = _runtime.Panel.DictionaryFilePath;
        }

        private void LookupProviderComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isInitializing)
            {
                UpdateLookupProviderControls();
                return;
            }

            string providerName = GetSelectedLookupProviderName();
            _runtime.UpdateDictionaryProviderName(providerName);
            DictionaryPathTextBox.Text = _runtime.Panel.DictionaryFilePath;
            MdictExecutablePathTextBox.Text = _runtime.Panel.MdictExecutablePath;
            UpdateLookupProviderControls();
        }

        private void MdictExecutablePathTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitMdictExecutablePath();
        }

        private void MdictExecutablePathTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CommitMdictExecutablePath();
            e.Handled = true;
        }

        private void BrowseMdictExecutableButton_Click(object sender, RoutedEventArgs e)
        {
            string currentPath = MdictExecutablePathTextBox.Text;
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
            dialog.CheckFileExists = true;
            dialog.FileName = string.IsNullOrWhiteSpace(currentPath) ? "mdict.exe" : Path.GetFileName(currentPath);

            string initialDirectory = string.Empty;
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                try
                {
                    initialDirectory = Path.GetDirectoryName(currentPath);
                }
                catch
                {
                    initialDirectory = string.Empty;
                }
            }

            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }

            bool? result = dialog.ShowDialog(this);
            if (result == true)
            {
                MdictExecutablePathTextBox.Text = dialog.FileName;
                CommitMdictExecutablePath();
            }
        }

        private void CommitMdictExecutablePath()
        {
            if (_isInitializing)
            {
                return;
            }

            string currentText = MdictExecutablePathTextBox.Text ?? string.Empty;
            string savedPath = _runtime.Panel.MdictExecutablePath ?? string.Empty;
            if (string.Equals(currentText.Trim(), savedPath.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _runtime.UpdateMdictExecutablePath(currentText);
            MdictExecutablePathTextBox.Text = _runtime.Panel.MdictExecutablePath;
        }

        private string GetSelectedLookupProviderName()
        {
            System.Windows.Controls.ComboBoxItem item = LookupProviderComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (item == null)
            {
                return "JsonFile";
            }

            string providerName = item.Tag as string;
            return string.IsNullOrWhiteSpace(providerName) ? "JsonFile" : providerName;
        }

        private void SelectLookupProvider(string providerName)
        {
            string targetProvider = string.IsNullOrWhiteSpace(providerName) ? "JsonFile" : providerName;

            for (int i = 0; i < LookupProviderComboBox.Items.Count; i++)
            {
                System.Windows.Controls.ComboBoxItem item = LookupProviderComboBox.Items[i] as System.Windows.Controls.ComboBoxItem;
                if (item == null)
                {
                    continue;
                }

                if (string.Equals(item.Tag as string, targetProvider, StringComparison.OrdinalIgnoreCase))
                {
                    LookupProviderComboBox.SelectedIndex = i;
                    return;
                }
            }

            LookupProviderComboBox.SelectedIndex = 0;
        }

        private void UpdateLookupProviderControls()
        {
            bool isMdictProvider = string.Equals(GetSelectedLookupProviderName(), "MdictCli", StringComparison.OrdinalIgnoreCase);
            MdictExecutablePanel.Visibility = isMdictProvider ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
