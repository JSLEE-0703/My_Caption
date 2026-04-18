namespace MyCaption.Core.Models
{
    public sealed class WordTokenViewModel : BindableBase
    {
        private string _text;
        private string _lookupKey;
        private bool _isLookupCandidate;
        private bool _canInteract;

        public WordTokenViewModel(string text, string lookupKey, bool isLookupCandidate)
        {
            _text = text;
            _lookupKey = lookupKey;
            _isLookupCandidate = isLookupCandidate;
        }

        public string Text
        {
            get { return _text; }
            set { SetProperty(ref _text, value, "Text"); }
        }

        public string LookupKey
        {
            get { return _lookupKey; }
            set { SetProperty(ref _lookupKey, value, "LookupKey"); }
        }

        public bool IsLookupCandidate
        {
            get { return _isLookupCandidate; }
            set
            {
                if (SetProperty(ref _isLookupCandidate, value, "IsLookupCandidate"))
                {
                    OnPropertyChanged("CanClick");
                }
            }
        }

        public bool CanInteract
        {
            get { return _canInteract; }
            set
            {
                if (SetProperty(ref _canInteract, value, "CanInteract"))
                {
                    OnPropertyChanged("CanClick");
                }
            }
        }

        public bool CanClick
        {
            get { return IsLookupCandidate && CanInteract; }
        }
    }
}
