using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using MyCaption.Core.Models;

namespace MyCaption.Core.Lookup
{
    internal static class MdictLookupParser
    {
        public static LookupResult Parse(string word, string rawHtml)
        {
            string lookupWord = string.IsNullOrWhiteSpace(word) ? string.Empty : word;
            string html = rawHtml ?? string.Empty;
            string markedText = MarkStructuredContent(html);
            string normalizedText = NormalizeText(markedText);
            string phonetic = ExtractPhonetic(normalizedText);
            List<LookupMeaning> meanings = ExtractMeanings(normalizedText);
            string example = ExtractExample(normalizedText);

            return new LookupResult(
                lookupWord,
                phonetic,
                meanings,
                example,
                meanings.Count == 0 ? "This entry has no definitions yet." : string.Empty,
                true);
        }

        private static string MarkStructuredContent(string rawHtml)
        {
            string text = rawHtml ?? string.Empty;
            text = Regex.Replace(text, @"<span[^>]*class=""CB""[^>]*>", "\n[[PHON]]", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<span[^>]*class=""DX""[^>]*>", "\n[[POS]]", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<span[^>]*class=""JX""[^>]*>", "\n[[DEF]]", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<span[^>]*class=""GZ""[^>]*>", "\n[[TR]]", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<span[^>]*class=""LY""[^>]*>", "\n[[EX]]", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"</(p|div|li|tr|h\d|span)>", "\n", RegexOptions.IgnoreCase);
            return text;
        }

        private static string NormalizeText(string rawHtml)
        {
            if (string.IsNullOrWhiteSpace(rawHtml))
            {
                return string.Empty;
            }

            string text = Regex.Replace(rawHtml, @"<(script|style)[^>]*>.*?</\1>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<[^>]+>", " ");
            text = text.Replace("&nbsp;", " ");
            text = WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"\r\n?", "\n");
            text = Regex.Replace(text, @"[ \t\f\v]+", " ");
            text = Regex.Replace(text, @"\n{3,}", "\n\n");
            return text.Trim();
        }

        private static string ExtractPhonetic(string normalizedText)
        {
            string[] lines = SplitLines(normalizedText);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!line.StartsWith("[[PHON]]", StringComparison.Ordinal))
                {
                    continue;
                }

                string value = CleanupInlineText(line.Substring(8));
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            Match slashMatch = Regex.Match(normalizedText ?? string.Empty, @"(?<![A-Za-z0-9])/(.{1,80}?)/(?![A-Za-z0-9])");
            if (slashMatch.Success)
            {
                return "/" + CleanupInlineText(slashMatch.Groups[1].Value) + "/";
            }

            Match bracketMatch = Regex.Match(normalizedText ?? string.Empty, @"\[(.{1,80}?)\]");
            if (bracketMatch.Success)
            {
                return "[" + CleanupInlineText(bracketMatch.Groups[1].Value) + "]";
            }

            return string.Empty;
        }

        private static List<LookupMeaning> ExtractMeanings(string normalizedText)
        {
            List<LookupMeaning> meanings = new List<LookupMeaning>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string currentPartOfSpeech = string.Empty;
            int lastMeaningIndex = -1;
            string[] lines = SplitLines(normalizedText);

            for (int i = 0; i < lines.Length && meanings.Count < 3; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("[[POS]]", StringComparison.Ordinal))
                {
                    currentPartOfSpeech = CleanupPartOfSpeech(line.Substring(7));
                    continue;
                }

                if (line.StartsWith("[[DEF]]", StringComparison.Ordinal))
                {
                    string definition = CleanupDefinition(line.Substring(7));
                    if (TryAddMeaning(meanings, seen, currentPartOfSpeech, definition))
                    {
                        lastMeaningIndex = meanings.Count - 1;
                    }

                    continue;
                }

                if (line.StartsWith("[[TR]]", StringComparison.Ordinal))
                {
                    string translation = CleanupDefinition(line.Substring(6));
                    if (string.IsNullOrWhiteSpace(translation))
                    {
                        continue;
                    }

                    if (lastMeaningIndex >= 0 && lastMeaningIndex < meanings.Count)
                    {
                        LookupMeaning lastMeaning = meanings[lastMeaningIndex];
                        if (lastMeaning.Definition.IndexOf(translation, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            meanings[lastMeaningIndex] = new LookupMeaning(lastMeaning.PartOfSpeech, lastMeaning.Definition + " | " + translation);
                        }
                    }
                    else
                    {
                        TryAddMeaning(meanings, seen, currentPartOfSpeech, translation);
                    }
                }
            }

            if (meanings.Count > 0)
            {
                return meanings;
            }

            for (int i = 0; i < lines.Length && meanings.Count < 3; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("[[", StringComparison.Ordinal))
                {
                    continue;
                }

                TryAddMeaning(meanings, seen, string.Empty, line);
            }

            return meanings;
        }

        private static string ExtractExample(string normalizedText)
        {
            string[] lines = SplitLines(normalizedText);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!line.StartsWith("[[EX]]", StringComparison.Ordinal))
                {
                    continue;
                }

                string example = CleanupInlineText(line.Substring(6));
                if (LooksLikeExample(example))
                {
                    return example;
                }
            }

            return string.Empty;
        }

        private static bool TryAddMeaning(List<LookupMeaning> meanings, HashSet<string> seen, string partOfSpeech, string candidate)
        {
            string definition = CleanupDefinition(candidate);
            if (string.IsNullOrWhiteSpace(definition))
            {
                return false;
            }

            if (definition.Length < 6 || definition.Length > 320)
            {
                return false;
            }

            if (definition.StartsWith("[[", StringComparison.Ordinal) || LooksLikeNoise(definition) || LooksLikeExample(definition) || !seen.Add(definition))
            {
                return false;
            }

            meanings.Add(new LookupMeaning(partOfSpeech, definition));
            return true;
        }

        private static string CleanupPartOfSpeech(string value)
        {
            string cleaned = CleanupInlineText(value).ToLowerInvariant();
            if (cleaned.StartsWith("for ", StringComparison.Ordinal))
            {
                return cleaned;
            }

            if (cleaned.StartsWith("(", StringComparison.Ordinal) && cleaned.EndsWith(")", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return cleaned;
        }

        private static string CleanupDefinition(string value)
        {
            string cleaned = CleanupInlineText(value);
            cleaned = Regex.Replace(cleaned, @"^\d+[.\)]\s*", string.Empty);
            cleaned = cleaned.Replace("■", string.Empty);
            cleaned = cleaned.Replace("•", string.Empty);
            cleaned = cleaned.Trim();
            return cleaned;
        }

        private static string CleanupInlineText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string cleaned = WebUtility.HtmlDecode(value);
            cleaned = cleaned.Replace("\0", string.Empty);
            cleaned = Regex.Replace(cleaned, @"<[^>]+>", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            cleaned = cleaned.Trim(' ', '-', ':', ';');
            return cleaned.Trim();
        }

        private static bool LooksLikeExample(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Length >= 18 &&
                value.Length <= 220 &&
                value.IndexOf(' ') >= 0 &&
                (value.Contains(".") || value.Contains(";") || value.Contains("!") || value.Contains("?"));
        }

        private static bool LooksLikeNoise(string value)
        {
            return Regex.IsMatch(value, @"^(see also|synonyms?|antonyms?|phrases?|derivatives?)$", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(value, @"^\W+$");
        }

        private static string[] SplitLines(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
