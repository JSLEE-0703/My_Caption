using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MyCaption.Core.Models;

namespace MyCaption.Core.Stabilization
{
    public sealed class CaptionStabilizer
    {
        private static readonly char[] SentenceTerminators = new char[] { '.', '?', '!', '。', '？', '！' };
        private static readonly Regex AcronymRegex = new Regex(@"([A-Z])\s*\.\s*([A-Z])(?![A-Za-z]+)", RegexOptions.Compiled);
        private static readonly Regex AcronymWithWordsRegex = new Regex(@"([A-Z])\s*\.\s*([A-Z])(?=[A-Za-z]+)", RegexOptions.Compiled);
        private static readonly Regex PunctuationSpaceRegex = new Regex(@"\s*([.!?,])\s*", RegexOptions.Compiled);
        private static readonly Regex CjkPunctuationSpaceRegex = new Regex(@"\s*([。！？，、])\s*", RegexOptions.Compiled);

        private readonly int _syncCommitThreshold;
        private readonly int _idleCommitThreshold;
        private string _lastDisplayText;
        private string _lastRequestedText;
        private int _syncCount;
        private int _idleCount;

        public CaptionStabilizer(int syncCommitThreshold, int idleCommitThreshold)
        {
            _syncCommitThreshold = syncCommitThreshold;
            _idleCommitThreshold = idleCommitThreshold;
            _lastDisplayText = string.Empty;
            _lastRequestedText = string.Empty;
        }

        public StabilizedCaptionUpdate Process(LiveCaptionsSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.RawText))
            {
                return null;
            }

            string normalized = Normalize(snapshot.RawText);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            string displayText = ExtractDisplaySegment(normalized);
            if (string.IsNullOrWhiteSpace(displayText))
            {
                return null;
            }

            string translationRequest = null;
            bool isCommitted = false;

            if (string.Equals(displayText, _lastDisplayText, StringComparison.Ordinal))
            {
                _idleCount++;
            }
            else
            {
                _lastDisplayText = displayText;
                _idleCount = 0;

                if (GetUtf8Length(displayText) >= 10)
                {
                    _syncCount++;
                }
            }

            if (EndsWithSentenceTerminator(displayText))
            {
                translationRequest = TrimToSentence(displayText);
                isCommitted = true;
                _syncCount = 0;
                _idleCount = 0;
            }
            else if (_syncCount >= _syncCommitThreshold || _idleCount >= _idleCommitThreshold)
            {
                translationRequest = displayText.Trim();
                _syncCount = 0;
                _idleCount = 0;
            }

            if (!string.IsNullOrWhiteSpace(translationRequest))
            {
                if (IsSimilarToLastRequest(translationRequest))
                {
                    translationRequest = null;
                }
                else
                {
                    _lastRequestedText = translationRequest;
                }
            }

            return new StabilizedCaptionUpdate(snapshot.RawText, normalized, displayText, translationRequest, isCommitted);
        }

        public static IList<WordTokenViewModel> Tokenize(string text)
        {
            List<WordTokenViewModel> tokens = new List<WordTokenViewModel>();
            if (string.IsNullOrEmpty(text))
            {
                return tokens;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if (IsWordCharacter(current))
                {
                    builder.Append(current);
                    continue;
                }

                FlushWord(tokens, builder);
                tokens.Add(new WordTokenViewModel(current.ToString(), current.ToString(), false));
            }

            FlushWord(tokens, builder);
            return tokens;
        }

        private static void FlushWord(ICollection<WordTokenViewModel> tokens, StringBuilder builder)
        {
            if (builder.Length == 0)
            {
                return;
            }

            string tokenText = builder.ToString();
            builder.Clear();
            tokens.Add(new WordTokenViewModel(tokenText, tokenText.ToLowerInvariant(), true));
        }

        private static bool IsWordCharacter(char value)
        {
            return char.IsLetter(value) || value == '\'';
        }

        private static string Normalize(string rawText)
        {
            string text = rawText.Trim();
            text = AcronymRegex.Replace(text, "$1$2");
            text = AcronymWithWordsRegex.Replace(text, "$1 $2");
            text = PunctuationSpaceRegex.Replace(text, "$1 ");
            text = CjkPunctuationSpaceRegex.Replace(text, "$1");
            text = ReplaceNewlines(text);
            return text.Trim();
        }

        private static string ReplaceNewlines(string text)
        {
            string[] parts = text.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    char last = builder[builder.Length - 1];
                    builder.Append(IsCjkChar(last) ? " " : ". ");
                }

                builder.Append(part);
            }

            return builder.ToString();
        }

        private static string ExtractDisplaySegment(string normalizedText)
        {
            int lastEosIndex = LastSentenceBreakBeforeTail(normalizedText);
            string latest = normalizedText.Substring(lastEosIndex + 1).Trim();

            if (lastEosIndex > 0 && GetUtf8Length(latest) < 10)
            {
                int previous = normalizedText.Substring(0, lastEosIndex).LastIndexOfAny(SentenceTerminators);
                latest = normalizedText.Substring(previous + 1).Trim();
            }

            return latest;
        }

        private static int LastSentenceBreakBeforeTail(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return -1;
            }

            if (EndsWithSentenceTerminator(text))
            {
                if (text.Length == 1)
                {
                    return -1;
                }

                return text.Substring(0, text.Length - 1).LastIndexOfAny(SentenceTerminators);
            }

            return text.LastIndexOfAny(SentenceTerminators);
        }

        private static bool EndsWithSentenceTerminator(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            char last = text[text.Length - 1];
            for (int i = 0; i < SentenceTerminators.Length; i++)
            {
                if (last == SentenceTerminators[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static string TrimToSentence(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            int last = text.LastIndexOfAny(SentenceTerminators);
            if (last < 0)
            {
                return text.Trim();
            }

            return text.Substring(0, last + 1).Trim();
        }

        private bool IsSimilarToLastRequest(string candidate)
        {
            if (string.IsNullOrWhiteSpace(_lastRequestedText))
            {
                return false;
            }

            if (candidate.StartsWith(_lastRequestedText, StringComparison.Ordinal))
            {
                return GetUtf8Length(candidate) - GetUtf8Length(_lastRequestedText) < 10;
            }

            if (_lastRequestedText.StartsWith(candidate, StringComparison.Ordinal))
            {
                return true;
            }

            return Similarity(candidate, _lastRequestedText) >= 0.92;
        }

        private static int GetUtf8Length(string value)
        {
            return Encoding.UTF8.GetByteCount(value);
        }

        private static bool IsCjkChar(char ch)
        {
            return
                (ch >= '\u4E00' && ch <= '\u9FFF') ||
                (ch >= '\u3400' && ch <= '\u4DBF') ||
                (ch >= '\u3000' && ch <= '\u303F') ||
                (ch >= '\u3040' && ch <= '\u309F') ||
                (ch >= '\u30A0' && ch <= '\u30FF');
        }

        private static double Similarity(string left, string right)
        {
            int max = Math.Max(left.Length, right.Length);
            if (max == 0)
            {
                return 1.0;
            }

            int distance = LevenshteinDistance(left, right);
            return 1.0 - ((double)distance / max);
        }

        private static int LevenshteinDistance(string left, string right)
        {
            if (string.IsNullOrEmpty(left))
            {
                return string.IsNullOrEmpty(right) ? 0 : right.Length;
            }

            if (string.IsNullOrEmpty(right))
            {
                return left.Length;
            }

            int[,] matrix = new int[left.Length + 1, right.Length + 1];
            for (int i = 0; i <= left.Length; i++)
            {
                matrix[i, 0] = i;
            }

            for (int j = 0; j <= right.Length; j++)
            {
                matrix[0, j] = j;
            }

            for (int i = 1; i <= left.Length; i++)
            {
                for (int j = 1; j <= right.Length; j++)
                {
                    int cost = left[i - 1] == right[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[left.Length, right.Length];
        }
    }
}
