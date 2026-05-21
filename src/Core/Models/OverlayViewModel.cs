using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MyCaption.Core.Models
{
    public sealed class OverlayViewModel : BindableBase
    {
        private string _originalText;
        private string _translatedText;
        private bool _isInteractive;
        private bool _isLookupVisible;
        private bool _showTranslationText;
        private double _originalFontSize;
        private double _translationFontSize;
        private double _backgroundOpacity;
        private string _lookupWord;
        private string _lookupPhonetic;
        private string _lookupExample;
        private string _lookupRawContent;
        private string _lookupStatusMessage;

        public OverlayViewModel()
        {
            OriginalTokens = new ObservableCollection<WordTokenViewModel>();
            LookupMeanings = new ObservableCollection<string>();
            _originalText = string.Empty;
            _translatedText = string.Empty;
            _isLookupVisible = false;
            _showTranslationText = true;
            _originalFontSize = 26;
            _translationFontSize = 23;
            _backgroundOpacity = 0.88;
            _lookupWord = string.Empty;
            _lookupPhonetic = string.Empty;
            _lookupExample = string.Empty;
            _lookupRawContent = string.Empty;
            _lookupStatusMessage = string.Empty;
        }

        public ObservableCollection<WordTokenViewModel> OriginalTokens { get; private set; }

        public ObservableCollection<string> LookupMeanings { get; private set; }

        public string OriginalText
        {
            get { return _originalText; }
            set { SetProperty(ref _originalText, value, "OriginalText"); }
        }

        public string TranslatedText
        {
            get { return _translatedText; }
            set { SetProperty(ref _translatedText, value, "TranslatedText"); }
        }

        public bool IsInteractive
        {
            get { return _isInteractive; }
            set
            {
                if (SetProperty(ref _isInteractive, value, "IsInteractive"))
                {
                    UpdateTokenInteractivity();
                }
            }
        }

        public bool ShowTranslationText
        {
            get { return _showTranslationText; }
            set { SetProperty(ref _showTranslationText, value, "ShowTranslationText"); }
        }

        public bool IsLookupVisible
        {
            get { return _isLookupVisible; }
            set { SetProperty(ref _isLookupVisible, value, "IsLookupVisible"); }
        }

        public double OriginalFontSize
        {
            get { return _originalFontSize; }
            set { SetProperty(ref _originalFontSize, value, "OriginalFontSize"); }
        }

        public double TranslationFontSize
        {
            get { return _translationFontSize; }
            set { SetProperty(ref _translationFontSize, value, "TranslationFontSize"); }
        }

        public double BackgroundOpacity
        {
            get { return _backgroundOpacity; }
            set { SetProperty(ref _backgroundOpacity, value, "BackgroundOpacity"); }
        }

        public string LookupWord
        {
            get { return _lookupWord; }
            set { SetProperty(ref _lookupWord, value, "LookupWord"); }
        }

        public string LookupPhonetic
        {
            get { return _lookupPhonetic; }
            set
            {
                if (SetProperty(ref _lookupPhonetic, value, "LookupPhonetic"))
                {
                    OnPropertyChanged("HasLookupPhonetic");
                }
            }
        }

        public string LookupExample
        {
            get { return _lookupExample; }
            set
            {
                if (SetProperty(ref _lookupExample, value, "LookupExample"))
                {
                    OnPropertyChanged("HasLookupExample");
                }
            }
        }

        public string LookupRawContent
        {
            get { return _lookupRawContent; }
            set
            {
                if (SetProperty(ref _lookupRawContent, value, "LookupRawContent"))
                {
                    OnPropertyChanged("HasLookupRawContent");
                }
            }
        }

        public string LookupStatusMessage
        {
            get { return _lookupStatusMessage; }
            set
            {
                if (SetProperty(ref _lookupStatusMessage, value, "LookupStatusMessage"))
                {
                    OnPropertyChanged("HasLookupStatusMessage");
                }
            }
        }

        public bool HasLookupPhonetic
        {
            get { return !string.IsNullOrWhiteSpace(LookupPhonetic); }
        }

        public bool HasLookupExample
        {
            get { return !string.IsNullOrWhiteSpace(LookupExample); }
        }

        public bool HasLookupRawContent
        {
            get { return !string.IsNullOrWhiteSpace(LookupRawContent); }
        }

        public bool HasLookupStatusMessage
        {
            get { return !string.IsNullOrWhiteSpace(LookupStatusMessage); }
        }

        public bool HasLookupMeanings
        {
            get { return LookupMeanings.Count > 0; }
        }

        public void UpdateOriginalText(string text, IEnumerable<WordTokenViewModel> tokens)
        {
            OriginalText = text ?? string.Empty;

            OriginalTokens.Clear();
            foreach (WordTokenViewModel token in tokens)
            {
                token.CanInteract = IsInteractive;
                OriginalTokens.Add(token);
            }
        }

        public void UpdateTranslationText(string text)
        {
            TranslatedText = text ?? string.Empty;
        }

        public void BeginLookup(string word)
        {
            IsLookupVisible = true;
            LookupWord = word ?? string.Empty;
            LookupPhonetic = string.Empty;
            LookupMeanings.Clear();
            LookupExample = string.Empty;
            LookupRawContent = string.Empty;
            LookupStatusMessage = "Looking up dictionary entry...";
            OnPropertyChanged("HasLookupMeanings");
        }

        public void UpdateLookup(LookupResult result)
        {
            if (result == null)
            {
                IsLookupVisible = false;
                LookupWord = string.Empty;
                LookupPhonetic = string.Empty;
                LookupMeanings.Clear();
                LookupExample = string.Empty;
                LookupRawContent = string.Empty;
                LookupStatusMessage = string.Empty;
                OnPropertyChanged("HasLookupMeanings");
                return;
            }

            IsLookupVisible = true;
            LookupWord = result.Word;
            LookupPhonetic = result.Phonetic;
            LookupMeanings.Clear();

            foreach (LookupMeaning meaning in result.Meanings)
            {
                if (meaning == null || string.IsNullOrWhiteSpace(meaning.Definition))
                {
                    continue;
                }

                string line = string.IsNullOrWhiteSpace(meaning.PartOfSpeech)
                    ? meaning.Definition
                    : meaning.PartOfSpeech + " - " + meaning.Definition;
                LookupMeanings.Add(line);
            }

            LookupExample = result.Example ?? string.Empty;
            LookupRawContent = result.RawContent ?? string.Empty;
            LookupStatusMessage = result.StatusMessage ?? string.Empty;
            OnPropertyChanged("HasLookupMeanings");
        }

        private void UpdateTokenInteractivity()
        {
            foreach (WordTokenViewModel token in OriginalTokens)
            {
                token.CanInteract = IsInteractive;
            }
        }
    }
}
