using System;

namespace MyCaption.Core.Models
{
    public sealed class LiveCaptionsSnapshot
    {
        public LiveCaptionsSnapshot(string rawText, DateTime capturedAt)
        {
            RawText = rawText;
            CapturedAt = capturedAt;
        }

        public string RawText { get; private set; }

        public DateTime CapturedAt { get; private set; }
    }

    public sealed class StabilizedCaptionUpdate
    {
        public StabilizedCaptionUpdate(string rawText, string normalizedText, string displayText, string translationRequestText, bool isCommitted)
        {
            RawText = rawText;
            NormalizedText = normalizedText;
            DisplayText = displayText;
            TranslationRequestText = translationRequestText;
            IsCommitted = isCommitted;
        }

        public string RawText { get; private set; }

        public string NormalizedText { get; private set; }

        public string DisplayText { get; private set; }

        public string TranslationRequestText { get; private set; }

        public bool IsCommitted { get; private set; }
    }

    public sealed class TranslationRequest
    {
        public TranslationRequest(string sourceText, string sourceLanguage, string targetLanguage)
        {
            SourceText = sourceText;
            SourceLanguage = sourceLanguage;
            TargetLanguage = targetLanguage;
        }

        public string SourceText { get; private set; }

        public string SourceLanguage { get; private set; }

        public string TargetLanguage { get; private set; }
    }

    public sealed class TranslationResult
    {
        public TranslationResult(string sourceText, string translatedText)
        {
            SourceText = sourceText;
            TranslatedText = translatedText;
        }

        public string SourceText { get; private set; }

        public string TranslatedText { get; private set; }
    }

    public sealed class LookupResult
    {
        public LookupResult(string word, string phonetic, string summary, string hint)
        {
            Word = word;
            Phonetic = phonetic;
            Summary = summary;
            Hint = hint;
        }

        public string Word { get; private set; }

        public string Phonetic { get; private set; }

        public string Summary { get; private set; }

        public string Hint { get; private set; }
    }

    public sealed class LiveCaptionsSnapshotEventArgs : EventArgs
    {
        public LiveCaptionsSnapshotEventArgs(LiveCaptionsSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public LiveCaptionsSnapshot Snapshot { get; private set; }
    }

    public sealed class CaptureStatusChangedEventArgs : EventArgs
    {
        public CaptureStatusChangedEventArgs(string statusText)
        {
            StatusText = statusText;
        }

        public string StatusText { get; private set; }
    }

    public sealed class CaptureStateChangedEventArgs : EventArgs
    {
        public CaptureStateChangedEventArgs(CaptureState state, string statusText, bool isCaptureActive)
        {
            State = state;
            StatusText = statusText;
            IsCaptureActive = isCaptureActive;
        }

        public CaptureState State { get; private set; }

        public string StatusText { get; private set; }

        public bool IsCaptureActive { get; private set; }
    }

    public sealed class TranslationCompletedEventArgs : EventArgs
    {
        public TranslationCompletedEventArgs(TranslationResult result)
        {
            Result = result;
        }

        public TranslationResult Result { get; private set; }
    }

    public sealed class AltStateChangedEventArgs : EventArgs
    {
        public AltStateChangedEventArgs(bool isAltPressed)
        {
            IsAltPressed = isAltPressed;
        }

        public bool IsAltPressed { get; private set; }
    }

    public enum CaptureState
    {
        Starting,
        ConnectedIdle,
        Running,
        WaitingForSource,
        Paused
    }
}
