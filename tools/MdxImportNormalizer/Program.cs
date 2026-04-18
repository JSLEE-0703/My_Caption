using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace MdxImportNormalizer
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                Arguments arguments = Arguments.Parse(args);
                List<NormalizedEntry> entries = NormalizeEntries(arguments);
                WriteOutput(entries, arguments.OutputPath);
                Console.WriteLine("Wrote {0} normalized entries to {1}", entries.Count, arguments.OutputPath);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static List<NormalizedEntry> NormalizeEntries(Arguments arguments)
        {
            Dictionary<string, NormalizedEntry> map = new Dictionary<string, NormalizedEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (IntermediateEntry entry in ReadIntermediate(arguments.InputPath))
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Word))
                {
                    continue;
                }

                string word = entry.Word.Trim();
                if (word.Length == 0 || map.ContainsKey(word))
                {
                    continue;
                }

                string rawHtml = entry.RawHtml ?? string.Empty;
                string normalizedText = NormalizeText(rawHtml);
                string phonetic = ExtractPhonetic(rawHtml, normalizedText);
                List<MeaningRecord> meanings = ExtractMeanings(word, rawHtml, normalizedText);
                string example = ExtractExample(rawHtml, normalizedText);

                map.Add(word, new NormalizedEntry
                {
                    Word = word,
                    Phonetic = phonetic,
                    Meanings = meanings,
                    Example = example,
                    RawHtml = rawHtml,
                    SourceDictionary = arguments.SourceDictionary,
                    SourceFormat = arguments.SourceFormat
                });
            }

            return new List<NormalizedEntry>(map.Values);
        }

        private static IEnumerable<IntermediateEntry> ReadIntermediate(string inputPath)
        {
            string extension = Path.GetExtension(inputPath) ?? string.Empty;
            if (extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase))
            {
                return ReadTsv(inputPath);
            }

            if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return ReadMdictText(inputPath);
            }

            if (LooksLikeMdictText(inputPath))
            {
                return ReadMdictText(inputPath);
            }

            return ReadJsonLines(inputPath);
        }

        private static bool LooksLikeMdictText(string inputPath)
        {
            using (StreamReader reader = new StreamReader(inputPath, Encoding.UTF8))
            {
                while (!reader.EndOfStream)
                {
                    int peek = reader.Peek();
                    if (peek < 0)
                    {
                        return false;
                    }

                    char current = (char)peek;
                    if (char.IsWhiteSpace(current))
                    {
                        reader.Read();
                        continue;
                    }

                    return current != '{' && current != '[';
                }
            }

            return false;
        }

        private static IEnumerable<IntermediateEntry> ReadJsonLines(string inputPath)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(IntermediateEntry));

            using (StreamReader reader = new StreamReader(inputPath, Encoding.UTF8))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    byte[] bytes = Encoding.UTF8.GetBytes(line);
                    using (MemoryStream stream = new MemoryStream(bytes))
                    {
                        IntermediateEntry entry = serializer.ReadObject(stream) as IntermediateEntry;
                        if (entry != null)
                        {
                            yield return entry;
                        }
                    }
                }
            }
        }

        private static IEnumerable<IntermediateEntry> ReadTsv(string inputPath)
        {
            using (StreamReader reader = new StreamReader(inputPath, Encoding.UTF8))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    int separatorIndex = line.IndexOf('\t');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    string word = line.Substring(0, separatorIndex);
                    string rawHtml = line.Substring(separatorIndex + 1);
                    yield return new IntermediateEntry
                    {
                        Word = word,
                        RawHtml = rawHtml
                    };
                }
            }
        }

        private static IEnumerable<IntermediateEntry> ReadMdictText(string inputPath)
        {
            using (StreamReader reader = new StreamReader(inputPath, Encoding.UTF8))
            {
                string currentWord = null;
                StringBuilder rawHtml = new StringBuilder();

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (currentWord == null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        currentWord = line.Trim();
                        continue;
                    }

                    if (string.Equals(line, "</>", StringComparison.Ordinal))
                    {
                        yield return new IntermediateEntry
                        {
                            Word = currentWord,
                            RawHtml = rawHtml.ToString().Trim()
                        };

                        currentWord = null;
                        rawHtml.Clear();
                        continue;
                    }

                    if (rawHtml.Length > 0)
                    {
                        rawHtml.AppendLine();
                    }

                    rawHtml.Append(line);
                }

                if (!string.IsNullOrWhiteSpace(currentWord))
                {
                    yield return new IntermediateEntry
                    {
                        Word = currentWord,
                        RawHtml = rawHtml.ToString().Trim()
                    };
                }
            }
        }

        private static string NormalizeText(string rawHtml)
        {
            if (string.IsNullOrWhiteSpace(rawHtml))
            {
                return string.Empty;
            }

            string text = Regex.Replace(rawHtml, @"<(script|style)[^>]*>.*?</\1>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"</(p|div|li|tr|h\d)>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<[^>]+>", " ");
            text = text.Replace("&nbsp;", " ");
            text = System.Net.WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"\r\n?", "\n");
            text = Regex.Replace(text, @"[ \t\f\v]+", " ");
            text = Regex.Replace(text, @"\n{3,}", "\n\n");
            return text.Trim();
        }

        private static string ExtractPhonetic(string rawHtml, string normalizedText)
        {
            string combined = normalizedText ?? string.Empty;
            Match slashMatch = Regex.Match(combined, @"(?<![A-Za-z0-9])/(.{1,80}?)/(?![A-Za-z0-9])");
            if (slashMatch.Success)
            {
                return "/" + CleanupInlineText(slashMatch.Groups[1].Value) + "/";
            }

            Match bracketMatch = Regex.Match(combined, @"\[(.{1,80}?)\]");
            if (bracketMatch.Success)
            {
                return "[" + CleanupInlineText(bracketMatch.Groups[1].Value) + "]";
            }

            combined = rawHtml ?? string.Empty;
            slashMatch = Regex.Match(combined, @"(?<![A-Za-z0-9])/(.{1,80}?)/(?![A-Za-z0-9])");
            if (slashMatch.Success)
            {
                return "/" + CleanupInlineText(slashMatch.Groups[1].Value) + "/";
            }

            bracketMatch = Regex.Match(combined, @"\[(.{1,80}?)\]");
            if (bracketMatch.Success)
            {
                return "[" + CleanupInlineText(bracketMatch.Groups[1].Value) + "]";
            }

            return string.Empty;
        }

        private static List<MeaningRecord> ExtractMeanings(string headword, string rawHtml, string normalizedText)
        {
            List<MeaningRecord> meanings = new List<MeaningRecord>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string item in ExtractListItems(rawHtml))
            {
                if (TryAddMeaning(headword, meanings, seen, item) && meanings.Count >= 3)
                {
                    return meanings;
                }
            }

            string[] lines = normalizedText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length && meanings.Count < 3; i++)
            {
                TryAddMeaning(headword, meanings, seen, lines[i]);
            }

            return meanings;
        }

        private static IEnumerable<string> ExtractListItems(string rawHtml)
        {
            if (string.IsNullOrWhiteSpace(rawHtml))
            {
                yield break;
            }

            MatchCollection matches = Regex.Matches(rawHtml, @"<li[^>]*>(.*?)</li>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            for (int i = 0; i < matches.Count; i++)
            {
                string text = NormalizeText(matches[i].Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return text;
                }
            }
        }

        private static bool TryAddMeaning(string headword, List<MeaningRecord> meanings, HashSet<string> seen, string candidate)
        {
            string cleaned = CleanupInlineText(candidate);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return false;
            }

            if (cleaned.Equals(headword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (Regex.IsMatch(cleaned, @"^(\/.+\/|\[.+\])$"))
            {
                return false;
            }

            if (LooksLikeExample(cleaned))
            {
                return false;
            }

            if (cleaned.Length < 6 || cleaned.Length > 280)
            {
                return false;
            }

            if (LooksLikeNoise(cleaned) || !seen.Add(cleaned))
            {
                return false;
            }

            string partOfSpeech = string.Empty;
            string definition = cleaned;
            Match match = Regex.Match(cleaned, @"^(noun|verb|adjective|adverb|pronoun|preposition|conjunction|interjection|auxiliary)\s*[:.\-]?\s+(.*)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                partOfSpeech = match.Groups[1].Value.ToLowerInvariant();
                definition = match.Groups[2].Value.Trim();
            }

            meanings.Add(new MeaningRecord
            {
                PartOfSpeech = partOfSpeech,
                Definition = definition
            });
            return true;
        }

        private static string ExtractExample(string rawHtml, string normalizedText)
        {
            foreach (string item in ExtractListItems(rawHtml))
            {
                string cleaned = CleanupInlineText(item);
                if (LooksLikeExample(cleaned))
                {
                    return cleaned;
                }
            }

            string[] lines = normalizedText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string cleaned = CleanupInlineText(lines[i]);
                if (LooksLikeExample(cleaned))
                {
                    return cleaned;
                }
            }

            return string.Empty;
        }

        private static bool LooksLikeExample(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (Regex.IsMatch(value, @"^(noun|verb|adjective|adverb|pronoun|preposition|conjunction|interjection|auxiliary)\s*[:.\-]?\s+", RegexOptions.IgnoreCase))
            {
                return false;
            }

            return value.Length >= 18 &&
                value.Length <= 220 &&
                (value.IndexOf(' ') >= 0) &&
                (value.Contains(".") || value.Contains(";") || value.Contains("!") || value.Contains("?"));
        }

        private static bool LooksLikeNoise(string value)
        {
            if (Regex.IsMatch(value, @"^(see also|synonyms?|antonyms?|phrases?|derivatives?)$", RegexOptions.IgnoreCase))
            {
                return true;
            }

            if (Regex.IsMatch(value, @"^\W+$"))
            {
                return true;
            }

            return false;
        }

        private static string CleanupInlineText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string cleaned = value.Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            cleaned = cleaned.Trim(' ', '-', ':', ';');
            return cleaned;
        }

        private static void WriteOutput(List<NormalizedEntry> entries, string outputPath)
        {
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (FileStream stream = File.Create(outputPath))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<NormalizedEntry>));
                serializer.WriteObject(stream, entries);
            }
        }

        [DataContract]
        private sealed class IntermediateEntry
        {
            [DataMember(Name = "word")]
            public string Word { get; set; }

            [DataMember(Name = "rawHtml")]
            public string RawHtml { get; set; }
        }

        [DataContract]
        private sealed class MeaningRecord
        {
            [DataMember(Name = "partOfSpeech")]
            public string PartOfSpeech { get; set; }

            [DataMember(Name = "definition")]
            public string Definition { get; set; }
        }

        [DataContract]
        private sealed class NormalizedEntry
        {
            [DataMember(Name = "word")]
            public string Word { get; set; }

            [DataMember(Name = "phonetic")]
            public string Phonetic { get; set; }

            [DataMember(Name = "meanings")]
            public List<MeaningRecord> Meanings { get; set; }

            [DataMember(Name = "example")]
            public string Example { get; set; }

            [DataMember(Name = "rawHtml")]
            public string RawHtml { get; set; }

            [DataMember(Name = "sourceDictionary")]
            public string SourceDictionary { get; set; }

            [DataMember(Name = "sourceFormat")]
            public string SourceFormat { get; set; }
        }

        private sealed class Arguments
        {
            public string InputPath { get; private set; }

            public string OutputPath { get; private set; }

            public string SourceDictionary { get; private set; }

            public string SourceFormat { get; private set; }

            public static Arguments Parse(string[] args)
            {
                Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < args.Length; i++)
                {
                    string current = args[i];
                    if (!current.StartsWith("--", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (i == args.Length - 1)
                    {
                        throw new InvalidOperationException("Missing value for argument: " + current);
                    }

                    values[current.Substring(2)] = args[i + 1];
                    i++;
                }

                string inputPath = Require(values, "input");
                string outputPath = Require(values, "output");
                string sourceDictionary = Require(values, "source-dictionary");
                string sourceFormat = Require(values, "source-format");

                if (!File.Exists(inputPath))
                {
                    throw new FileNotFoundException("Intermediate input file not found.", inputPath);
                }

                return new Arguments
                {
                    InputPath = Path.GetFullPath(inputPath),
                    OutputPath = Path.GetFullPath(outputPath),
                    SourceDictionary = sourceDictionary,
                    SourceFormat = sourceFormat
                };
            }

            private static string Require(IDictionary<string, string> values, string key)
            {
                string value;
                if (!values.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException("Missing required argument --" + key);
                }

                return value;
            }
        }
    }
}
