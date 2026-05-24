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
            TranslationEnabledCheckBox.IsChecked = _runtime.Panel.TranslationEnabled;
            ShowTranslationTextCheckBox.IsChecked = _runtime.Panel.ShowTranslationText;
            FontSizeSlider.Value = _runtime.Panel.FontSize;
            OpacitySlider.Value = _runtime.Panel.BackgroundOpacity;
            SelectTranslationProvider(_runtime.Panel.TranslationProviderName);
            TranslationSourceLanguageTextBox.Text = _runtime.Panel.TranslationSourceLanguage;
            TranslationTargetLanguageTextBox.Text = _runtime.Panel.TranslationTargetLanguage;
            TranslationExecutablePathTextBox.Text = _runtime.Panel.TranslationExecutablePath;
            TranslationArgumentsTemplateTextBox.Text = _runtime.Panel.TranslationArgumentsTemplate;
            TranslationApiUrlTextBox.Text = _runtime.Panel.TranslationApiUrl;
            TranslationApiKeyTextBox.Text = _runtime.Panel.TranslationApiKey;
            TranslationApiRegionTextBox.Text = _runtime.Panel.TranslationApiRegion;
            SelectLookupProvider(_runtime.Panel.DictionaryProviderName);
            DictionaryPathTextBox.Text = _runtime.Panel.DictionaryFilePath;
            MdictExecutablePathTextBox.Text = _runtime.Panel.MdictExecutablePath;
            UpdateAdvancedProvidersExpander();
            UpdateTranslationProviderControls();
            UpdateLookupProviderControls();
            EnsureOverlayWindow();
            _isInitializing = false;
            EnsureDictionarySetup();
            _runtime.WarmUpLookupProvider();
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

        private void TranslationEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            _runtime.UpdateTranslationEnabled(TranslationEnabledCheckBox.IsChecked == true);
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

        private void TranslationSourceLanguageTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitTranslationSourceLanguage();
        }

        private void TranslationSourceLanguageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CommitTranslationSourceLanguage();
            e.Handled = true;
        }

        private void TranslationTargetLanguageTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitTranslationTargetLanguage();
        }

        private void TranslationTargetLanguageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CommitTranslationTargetLanguage();
            e.Handled = true;
        }

        private void TranslationExecutablePathTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitTranslationExecutablePath();
        }

        private void TranslationExecutablePathTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CommitTranslationExecutablePath();
            e.Handled = true;
        }

        private void TranslationArgumentsTemplateTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitTranslationArgumentsTemplate();
        }

        private void TranslationArgumentsTemplateTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CommitTranslationArgumentsTemplate();
            e.Handled = true;
        }

        private void TranslationApiUrlTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitTranslationApiUrl();
        }

        private void TranslationApiUrlTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CommitTranslationApiUrl();
            e.Handled = true;
        }

        private void TranslationApiKeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitTranslationApiKey();
        }

        private void TranslationApiKeyTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CommitTranslationApiKey();
            e.Handled = true;
        }

        private void TranslationApiRegionTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitTranslationApiRegion();
        }

        private void TranslationApiRegionTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CommitTranslationApiRegion();
            e.Handled = true;
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
            PromptForDictionaryFileSelection();
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

        private void TranslationProviderComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isInitializing)
            {
                UpdateTranslationProviderControls();
                return;
            }

            string providerName = GetSelectedTranslationProviderName();
            _runtime.UpdateTranslationProviderName(providerName);
            TranslationExecutablePathTextBox.Text = _runtime.Panel.TranslationExecutablePath;
            TranslationArgumentsTemplateTextBox.Text = _runtime.Panel.TranslationArgumentsTemplate;
            TranslationApiUrlTextBox.Text = _runtime.Panel.TranslationApiUrl;
            TranslationApiKeyTextBox.Text = _runtime.Panel.TranslationApiKey;
            TranslationApiRegionTextBox.Text = _runtime.Panel.TranslationApiRegion;
            UpdateAdvancedProvidersExpander();
            UpdateTranslationProviderControls();
        }

        private void CommitTranslationSourceLanguage()
        {
            if (_isInitializing)
            {
                return;
            }

            string currentText = TranslationSourceLanguageTextBox.Text ?? string.Empty;
            string savedValue = _runtime.Panel.TranslationSourceLanguage ?? string.Empty;
            if (string.Equals(currentText.Trim(), savedValue.Trim(), StringComparison.Ordinal))
            {
                return;
            }

            _runtime.UpdateTranslationSourceLanguage(currentText);
            TranslationSourceLanguageTextBox.Text = _runtime.Panel.TranslationSourceLanguage;
        }

        private void CommitTranslationTargetLanguage()
        {
            if (_isInitializing)
            {
                return;
            }

            string currentText = TranslationTargetLanguageTextBox.Text ?? string.Empty;
            string savedValue = _runtime.Panel.TranslationTargetLanguage ?? string.Empty;
            if (string.Equals(currentText.Trim(), savedValue.Trim(), StringComparison.Ordinal))
            {
                return;
            }

            _runtime.UpdateTranslationTargetLanguage(currentText);
            TranslationTargetLanguageTextBox.Text = _runtime.Panel.TranslationTargetLanguage;
        }

        private void BrowseTranslationExecutableButton_Click(object sender, RoutedEventArgs e)
        {
            string currentPath = TranslationExecutablePathTextBox.Text;
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
            dialog.CheckFileExists = true;
            dialog.FileName = string.IsNullOrWhiteSpace(currentPath) ? "translator.exe" : Path.GetFileName(currentPath);

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
                TranslationExecutablePathTextBox.Text = dialog.FileName;
                CommitTranslationExecutablePath();
            }
        }

        private void CommitTranslationExecutablePath()
        {
            if (_isInitializing)
            {
                return;
            }

            string currentText = TranslationExecutablePathTextBox.Text ?? string.Empty;
            string savedPath = _runtime.Panel.TranslationExecutablePath ?? string.Empty;
            if (string.Equals(currentText.Trim(), savedPath.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _runtime.UpdateTranslationExecutablePath(currentText);
            TranslationExecutablePathTextBox.Text = _runtime.Panel.TranslationExecutablePath;
        }

        private void CommitTranslationArgumentsTemplate()
        {
            if (_isInitializing)
            {
                return;
            }

            string currentText = TranslationArgumentsTemplateTextBox.Text ?? string.Empty;
            string savedValue = _runtime.Panel.TranslationArgumentsTemplate ?? string.Empty;
            if (string.Equals(currentText.Trim(), savedValue.Trim(), StringComparison.Ordinal))
            {
                return;
            }

            _runtime.UpdateTranslationArgumentsTemplate(currentText);
            TranslationArgumentsTemplateTextBox.Text = _runtime.Panel.TranslationArgumentsTemplate;
        }

        private void CommitTranslationApiUrl()
        {
            if (_isInitializing)
            {
                return;
            }

            string currentText = TranslationApiUrlTextBox.Text ?? string.Empty;
            string savedValue = _runtime.Panel.TranslationApiUrl ?? string.Empty;
            if (string.Equals(currentText.Trim(), savedValue.Trim(), StringComparison.Ordinal))
            {
                return;
            }

            _runtime.UpdateTranslationApiUrl(currentText);
            TranslationApiUrlTextBox.Text = _runtime.Panel.TranslationApiUrl;
        }

        private void CommitTranslationApiKey()
        {
            if (_isInitializing)
            {
                return;
            }

            string currentText = TranslationApiKeyTextBox.Text ?? string.Empty;
            string savedValue = _runtime.Panel.TranslationApiKey ?? string.Empty;
            if (string.Equals(currentText.Trim(), savedValue.Trim(), StringComparison.Ordinal))
            {
                return;
            }

            _runtime.UpdateTranslationApiKey(currentText);
            TranslationApiKeyTextBox.Text = _runtime.Panel.TranslationApiKey;
        }

        private void CommitTranslationApiRegion()
        {
            if (_isInitializing)
            {
                return;
            }

            string currentText = TranslationApiRegionTextBox.Text ?? string.Empty;
            string savedValue = _runtime.Panel.TranslationApiRegion ?? string.Empty;
            if (string.Equals(currentText.Trim(), savedValue.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _runtime.UpdateTranslationApiRegion(currentText);
            TranslationApiRegionTextBox.Text = _runtime.Panel.TranslationApiRegion;
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
            UpdateAdvancedProvidersExpander();
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

        private string GetSelectedTranslationProviderName()
        {
            System.Windows.Controls.ComboBoxItem item = TranslationProviderComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (item == null)
            {
                return "Stub";
            }

            string providerName = item.Tag as string;
            return string.IsNullOrWhiteSpace(providerName) ? "Stub" : providerName;
        }

        private void SelectTranslationProvider(string providerName)
        {
            string targetProvider = string.IsNullOrWhiteSpace(providerName) ? "Stub" : providerName;

            for (int i = 0; i < TranslationProviderComboBox.Items.Count; i++)
            {
                System.Windows.Controls.ComboBoxItem item = TranslationProviderComboBox.Items[i] as System.Windows.Controls.ComboBoxItem;
                if (item == null)
                {
                    continue;
                }

                if (string.Equals(item.Tag as string, targetProvider, StringComparison.OrdinalIgnoreCase))
                {
                    TranslationProviderComboBox.SelectedIndex = i;
                    return;
                }
            }

            TranslationProviderComboBox.SelectedIndex = 0;
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

        private void UpdateAdvancedProvidersExpander()
        {
            bool usesDefaultTranslationProvider = string.Equals(GetSelectedTranslationProviderName(), "ExternalCli", StringComparison.OrdinalIgnoreCase);
            bool usesDefaultLookupProvider = string.Equals(GetSelectedLookupProviderName(), "MdictCli", StringComparison.OrdinalIgnoreCase);
            AdvancedProvidersExpander.IsExpanded = !(usesDefaultTranslationProvider && usesDefaultLookupProvider);
        }

        private void EnsureDictionarySetup()
        {
            if (!string.Equals(_runtime.Panel.DictionaryProviderName, "MdictCli", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string bundledDictionaryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dictionary", "default.mdx");
            string configuredDictionaryPath = _runtime.Panel.DictionaryFilePath ?? string.Empty;

            if (File.Exists(bundledDictionaryPath))
            {
                if (!string.Equals(configuredDictionaryPath, bundledDictionaryPath, StringComparison.OrdinalIgnoreCase) &&
                    !File.Exists(configuredDictionaryPath))
                {
                    _runtime.UpdateDictionaryFilePath(bundledDictionaryPath);
                    DictionaryPathTextBox.Text = _runtime.Panel.DictionaryFilePath;
                }

                return;
            }

            if (File.Exists(configuredDictionaryPath))
            {
                return;
            }

            _runtime.Panel.LookupStatusText = "MDict dictionary missing. Choose an .mdx file to enable word lookup.";

            MessageBoxResult result = MessageBox.Show(
                this,
                "The bundled MDict dictionary was not found.\n\nChoose an .mdx dictionary now to enable word lookup?",
                "Dictionary Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                PromptForDictionaryFileSelection();
            }
        }

        private void PromptForDictionaryFileSelection()
        {
            string selectedPath;
            if (!TrySelectDictionaryFile(out selectedPath))
            {
                return;
            }

            DictionaryPathTextBox.Text = selectedPath;
            _runtime.UpdateDictionaryFilePath(selectedPath);
            DictionaryPathTextBox.Text = _runtime.Panel.DictionaryFilePath;
            _runtime.WarmUpLookupProvider();
        }

        private bool TrySelectDictionaryFile(out string selectedPath)
        {
            selectedPath = string.Empty;

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
            if (result != true)
            {
                return false;
            }

            selectedPath = dialog.FileName;
            return true;
        }

        private void UpdateTranslationProviderControls()
        {
            bool isExternalCliProvider = string.Equals(GetSelectedTranslationProviderName(), "ExternalCli", StringComparison.OrdinalIgnoreCase);
            bool isApiProvider =
                string.Equals(GetSelectedTranslationProviderName(), "DeepL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetSelectedTranslationProviderName(), "AzureTranslator", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetSelectedTranslationProviderName(), "GoogleCloud", StringComparison.OrdinalIgnoreCase);
            bool isAzureProvider = string.Equals(GetSelectedTranslationProviderName(), "AzureTranslator", StringComparison.OrdinalIgnoreCase);
            ExternalTranslationPanel.Visibility = isExternalCliProvider ? Visibility.Visible : Visibility.Collapsed;
            ApiTranslationPanel.Visibility = isApiProvider ? Visibility.Visible : Visibility.Collapsed;
            TranslationApiRegionPanel.Visibility = isAzureProvider ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
