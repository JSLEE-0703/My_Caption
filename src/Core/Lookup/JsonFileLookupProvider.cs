using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using MyCaption.Core.Models;

namespace MyCaption.Core.Lookup
{
    public sealed class JsonFileLookupProvider : ILookupProvider, ILookupProviderStatus
    {
        private readonly Dictionary<string, JsonDictionaryEntry> _entries;
        private readonly string _dictionaryFilePath;
        private readonly string _statusSummary;
        private readonly string _loadFailureMessage;

        public JsonFileLookupProvider(string dictionaryFilePath)
        {
            _dictionaryFilePath = dictionaryFilePath ?? string.Empty;
            _entries = new Dictionary<string, JsonDictionaryEntry>(StringComparer.OrdinalIgnoreCase);
            _statusSummary = LoadEntries(_entries, _dictionaryFilePath, out _loadFailureMessage);
        }

        public string DisplayName
        {
            get { return "JSON file dictionary"; }
        }

        public string StatusSummary
        {
            get { return _statusSummary; }
        }

        public string DictionaryFilePath
        {
            get { return _dictionaryFilePath; }
        }

        public Task<LookupResult> LookupAsync(string word, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string normalized = NormalizeWord(word);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return Task.FromResult(new LookupResult(string.Empty, string.Empty, new List<LookupMeaning>(), string.Empty, string.Empty, "Select an English word to look it up.", false));
            }

            if (!string.IsNullOrWhiteSpace(_loadFailureMessage))
            {
                return Task.FromResult(new LookupResult(normalized, string.Empty, new List<LookupMeaning>(), string.Empty, string.Empty, _loadFailureMessage, false));
            }

            JsonDictionaryEntry entry;
            if (!TryFindEntry(normalized, out entry))
            {
                return Task.FromResult(new LookupResult(normalized, string.Empty, new List<LookupMeaning>(), string.Empty, string.Empty, "No dictionary entry found for this word.", false));
            }

            List<LookupMeaning> meanings = new List<LookupMeaning>();
            if (entry.Meanings != null)
            {
                for (int i = 0; i < entry.Meanings.Count; i++)
                {
                    JsonDictionaryMeaning meaning = entry.Meanings[i];
                    if (meaning == null || string.IsNullOrWhiteSpace(meaning.Definition))
                    {
                        continue;
                    }

                    meanings.Add(new LookupMeaning(meaning.PartOfSpeech, meaning.Definition));
                }
            }

            return Task.FromResult(new LookupResult(
                entry.Word ?? normalized,
                entry.Phonetic ?? string.Empty,
                meanings,
                entry.Example ?? string.Empty,
                string.Empty,
                meanings.Count == 0 ? "This entry has no definitions yet." : string.Empty,
                true));
        }

        private bool TryFindEntry(string word, out JsonDictionaryEntry entry)
        {
            foreach (string candidate in GetLookupCandidates(word))
            {
                if (_entries.TryGetValue(candidate, out entry))
                {
                    return true;
                }
            }

            entry = null;
            return false;
        }

        private static string LoadEntries(Dictionary<string, JsonDictionaryEntry> entries, string dictionaryFilePath, out string loadFailureMessage)
        {
            loadFailureMessage = string.Empty;

            try
            {
                EnsureSeedDictionaryFile(dictionaryFilePath);
                List<JsonDictionaryEntry> records = ReadEntries(dictionaryFilePath);
                if (records != null)
                {
                    for (int i = 0; i < records.Count; i++)
                    {
                        JsonDictionaryEntry record = records[i];
                        if (record == null || string.IsNullOrWhiteSpace(record.Word))
                        {
                            continue;
                        }

                        string key = NormalizeWord(record.Word);
                        if (string.IsNullOrWhiteSpace(key) || entries.ContainsKey(key))
                        {
                            continue;
                        }

                        entries.Add(key, record);
                    }
                }

                return string.Format(CultureInfo.InvariantCulture, "Dictionary loaded: {0} entr{1}.", entries.Count, entries.Count == 1 ? "y" : "ies");
            }
            catch (Exception ex)
            {
                loadFailureMessage = "Dictionary file unavailable: " + ex.Message;
                return loadFailureMessage;
            }
        }

        private static void EnsureSeedDictionaryFile(string dictionaryFilePath)
        {
            if (string.IsNullOrWhiteSpace(dictionaryFilePath))
            {
                throw new InvalidOperationException("Dictionary file path is empty.");
            }

            if (File.Exists(dictionaryFilePath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(dictionaryFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            List<JsonDictionaryEntry> seedEntries = CreateSeedEntries();
            using (Stream stream = File.Create(dictionaryFilePath))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<JsonDictionaryEntry>));
                serializer.WriteObject(stream, seedEntries);
            }
        }

        private static List<JsonDictionaryEntry> ReadEntries(string dictionaryFilePath)
        {
            using (Stream stream = File.OpenRead(dictionaryFilePath))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<JsonDictionaryEntry>));
                return serializer.ReadObject(stream) as List<JsonDictionaryEntry>;
            }
        }

        private static List<JsonDictionaryEntry> CreateSeedEntries()
        {
            return new List<JsonDictionaryEntry>
            {
                new JsonDictionaryEntry
                {
                    Word = "caption",
                    Phonetic = "/kap-shun/",
                    Example = "Live captions help you follow fast spoken English.",
                    Meanings = new List<JsonDictionaryMeaning>
                    {
                        new JsonDictionaryMeaning
                        {
                            PartOfSpeech = "noun",
                            Definition = "Text shown on screen that represents spoken words."
                        }
                    }
                },
                new JsonDictionaryEntry
                {
                    Word = "translate",
                    Phonetic = "/trans-late/",
                    Example = "The overlay can translate complete caption segments later.",
                    Meanings = new List<JsonDictionaryMeaning>
                    {
                        new JsonDictionaryMeaning
                        {
                            PartOfSpeech = "verb",
                            Definition = "To change words from one language into another."
                        }
                    }
                },
                new JsonDictionaryEntry
                {
                    Word = "overlay",
                    Phonetic = "/oh-ver-lay/",
                    Example = "Drag the overlay while holding Alt.",
                    Meanings = new List<JsonDictionaryMeaning>
                    {
                        new JsonDictionaryMeaning
                        {
                            PartOfSpeech = "noun",
                            Definition = "A layer of content displayed on top of another window or image."
                        }
                    }
                }
            };
        }

        private static IEnumerable<string> GetLookupCandidates(string word)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (AddCandidate(seen, word))
            {
                yield return word;
            }

            if (word.EndsWith("'s", StringComparison.Ordinal) && AddCandidate(seen, word.Substring(0, word.Length - 2)))
            {
                yield return word.Substring(0, word.Length - 2);
            }

            if (word.EndsWith("es", StringComparison.Ordinal) && word.Length > 2 && AddCandidate(seen, word.Substring(0, word.Length - 2)))
            {
                yield return word.Substring(0, word.Length - 2);
            }

            if (word.EndsWith("s", StringComparison.Ordinal) && word.Length > 1 && AddCandidate(seen, word.Substring(0, word.Length - 1)))
            {
                yield return word.Substring(0, word.Length - 1);
            }

            if (word.EndsWith("ed", StringComparison.Ordinal) && word.Length > 2)
            {
                foreach (string candidate in ExpandVerbCandidate(seen, word.Substring(0, word.Length - 2)))
                {
                    yield return candidate;
                }
            }

            if (word.EndsWith("ing", StringComparison.Ordinal) && word.Length > 3)
            {
                foreach (string candidate in ExpandVerbCandidate(seen, word.Substring(0, word.Length - 3)))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<string> ExpandVerbCandidate(HashSet<string> seen, string stem)
        {
            if (AddCandidate(seen, stem))
            {
                yield return stem;
            }

            if (stem.Length > 0 && AddCandidate(seen, stem + "e"))
            {
                yield return stem + "e";
            }

            if (stem.Length > 1 && stem[stem.Length - 1] == stem[stem.Length - 2])
            {
                string shortened = stem.Substring(0, stem.Length - 1);
                if (AddCandidate(seen, shortened))
                {
                    yield return shortened;
                }
            }
        }

        private static bool AddCandidate(HashSet<string> seen, string value)
        {
            return !string.IsNullOrWhiteSpace(value) && seen.Add(value);
        }

        private static string NormalizeWord(string word)
        {
            return string.IsNullOrWhiteSpace(word)
                ? string.Empty
                : word.Trim().ToLowerInvariant();
        }

        [DataContract]
        private sealed class JsonDictionaryEntry
        {
            [DataMember(Name = "word")]
            public string Word { get; set; }

            [DataMember(Name = "phonetic")]
            public string Phonetic { get; set; }

            [DataMember(Name = "meanings")]
            public List<JsonDictionaryMeaning> Meanings { get; set; }

            [DataMember(Name = "example")]
            public string Example { get; set; }

            [DataMember(Name = "rawHtml")]
            public string RawHtml { get; set; }

            [DataMember(Name = "sourceDictionary")]
            public string SourceDictionary { get; set; }

            [DataMember(Name = "sourceFormat")]
            public string SourceFormat { get; set; }
        }

        [DataContract]
        private sealed class JsonDictionaryMeaning
        {
            [DataMember(Name = "partOfSpeech")]
            public string PartOfSpeech { get; set; }

            [DataMember(Name = "definition")]
            public string Definition { get; set; }
        }
    }
}
