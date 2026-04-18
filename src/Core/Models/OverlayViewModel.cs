using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MyCaption.Core.Models
{
    public sealed class OverlayViewModel : BindableBase
    {
        private string _originalText;
        private string _translatedText;
        private bool _isInteractive;
        private double _originalFontSize;
        private double _translationFontSize;
        private double _backgroundOpacity;
        private string _lookupWord;
        private string _lookupPhonetic;
        private string _lookupSummary;
        private string _lookupHint;

        public OverlayViewModel()
        {
            OriginalTokens = new ObservableCollection<WordTokenViewModel>();
            _originalText = string.Empty;
            _translatedText = string.Empty;
            _originalFontSize = 26;
            _translationFontSize = 23;
            _backgroundOpacity = 0.88;
            _lookupWord = string.Empty;
            _lookupPhonetic = string.Empty;
            _lookupSummary = string.Empty;
            _lookupHint = string.Empty;
        }

        public ObservableCollection<WordTokenViewModel> OriginalTokens { get; private set; }

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
            set { SetProperty(ref _lookupPhonetic, value, "LookupPhonetic"); }
        }

        public string LookupSummary
        {
            get { return _lookupSummary; }
            set { SetProperty(ref _lookupSummary, value, "LookupSummary"); }
        }

        public string LookupHint
        {
            get { return _lookupHint; }
            set { SetProperty(ref _lookupHint, value, "LookupHint"); }
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

        public void UpdateLookup(LookupResult result)
        {
            if (result == null)
            {
                LookupWord = string.Empty;
                LookupPhonetic = string.Empty;
                LookupSummary = string.Empty;
                LookupHint = string.Empty;
                return;
            }

            LookupWord = result.Word;
            LookupPhonetic = result.Phonetic;
            LookupSummary = result.Summary;
            LookupHint = result.Hint;
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
