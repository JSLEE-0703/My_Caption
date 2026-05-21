using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyCaption.Core.Models;

namespace MyCaption.Core.Lookup
{
    public sealed class MdictLookupProvider : ILookupProvider, ILookupProviderStatus, IMdictLookupProviderStatus, IDisposable
    {
        private readonly object _persistentProcessSyncRoot;
        private readonly SemaphoreSlim _persistentRequestGate;
        private readonly string _dictionaryFilePath;
        private readonly string _mdictExecutablePath;
        private readonly string _pythonExecutablePath;
        private readonly string _persistentScriptPath;
        private readonly string _statusSummary;
        private readonly string _loadFailureMessage;
        private Process _persistentProcess;
        private StreamWriter _persistentInput;
        private StreamReader _persistentOutput;
        private StringBuilder _persistentErrorBuffer;

        public MdictLookupProvider(string dictionaryFilePath, string mdictExecutablePath)
        {
            _persistentProcessSyncRoot = new object();
            _persistentRequestGate = new SemaphoreSlim(1, 1);
            _dictionaryFilePath = dictionaryFilePath ?? string.Empty;
            _mdictExecutablePath = ResolveMdictExecutablePath(mdictExecutablePath);
            _pythonExecutablePath = ResolvePythonExecutablePath(mdictExecutablePath, _mdictExecutablePath);
            _persistentScriptPath = ResolvePersistentScriptPath();
            _statusSummary = InitializeStatus(out _loadFailureMessage);
        }

        public string DisplayName
        {
            get { return "MDict CLI dictionary"; }
        }

        public string StatusSummary
        {
            get { return _statusSummary; }
        }

        public string DictionaryFilePath
        {
            get { return _dictionaryFilePath; }
        }

        public string MdictExecutablePath
        {
            get { return _mdictExecutablePath; }
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

            return Task.Run(delegate
            {
                foreach (string candidate in GetLookupCandidates(normalized))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string rawHtml = QueryRawHtml(candidate, cancellationToken);
                    if (string.IsNullOrWhiteSpace(rawHtml))
                    {
                        continue;
                    }

                    return MdictLookupParser.Parse(candidate, rawHtml);
                }

                return new LookupResult(normalized, string.Empty, new List<LookupMeaning>(), string.Empty, string.Empty, "No dictionary entry found for this word.", false);
            }, cancellationToken);
        }

        public void Dispose()
        {
            ResetPersistentSession();
        }

        private string InitializeStatus(out string loadFailureMessage)
        {
            loadFailureMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(_dictionaryFilePath))
            {
                loadFailureMessage = "Dictionary file unavailable: MDX file path is empty.";
                return loadFailureMessage;
            }

            if (!File.Exists(_dictionaryFilePath))
            {
                loadFailureMessage = "Dictionary file unavailable: MDX file not found.";
                return loadFailureMessage;
            }

            bool hasPythonRuntime = !string.IsNullOrWhiteSpace(_pythonExecutablePath) && File.Exists(_pythonExecutablePath);
            bool hasExecutableRuntime = !string.IsNullOrWhiteSpace(_mdictExecutablePath) && File.Exists(_mdictExecutablePath);
            if (!hasPythonRuntime && !hasExecutableRuntime)
            {
                loadFailureMessage = "Dictionary file unavailable: mdict runtime not found.";
                return loadFailureMessage;
            }

            try
            {
                RunMdictCommand("--version");
            }
            catch (Exception ex)
            {
                loadFailureMessage = "Dictionary file unavailable: " + ex.Message;
                return loadFailureMessage;
            }

            try
            {
                string metadata = RunMdictCommand("-m", _dictionaryFilePath);
                if (!string.IsNullOrWhiteSpace(metadata))
                {
                    string title = TryReadMetadataValue(metadata, "Title");
                    string recordCount = TryReadMetadataValue(metadata, "Record");
                    string mddPath = Path.ChangeExtension(_dictionaryFilePath, ".mdd");
                    bool hasMdd = File.Exists(mddPath);
                    string modeSuffix = SupportsPersistentQueryMode() ? " Fast query mode enabled." : string.Empty;

                    if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(recordCount))
                    {
                        return string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}: {1} entries.{2}{3}",
                            title,
                            recordCount,
                            hasMdd ? " Sidecar MDD detected." : string.Empty,
                            modeSuffix);
                    }
                }
            }
            catch
            {
            }

            return "MDict ready." +
                (File.Exists(Path.ChangeExtension(_dictionaryFilePath, ".mdd")) ? " Sidecar MDD detected." : string.Empty) +
                (SupportsPersistentQueryMode() ? " Fast query mode enabled." : string.Empty);
        }

        private string QueryRawHtml(string word, CancellationToken cancellationToken)
        {
            if (SupportsPersistentQueryMode())
            {
                return QueryRawHtmlPersistent(word, cancellationToken);
            }

            string output = RunMdictCommand("-q", word, _dictionaryFilePath);
            output = CleanupCommandOutput(output);
            return string.IsNullOrWhiteSpace(output) ? string.Empty : output;
        }

        private string QueryRawHtmlPersistent(string word, CancellationToken cancellationToken)
        {
            _persistentRequestGate.Wait(cancellationToken);

            try
            {
                PersistentQuerySession session = EnsurePersistentSession();
                try
                {
                    ClearPersistentErrorBuffer();
                    WritePersistentRequest(session, word ?? string.Empty);
                    string responseLine = session.Output.ReadLine();
                    if (responseLine == null)
                    {
                        throw new InvalidOperationException(GetPersistentErrorText("mdict query process exited unexpectedly."));
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    PersistentQueryResponse response = DeserializeJson<PersistentQueryResponse>(responseLine);
                    if (response == null)
                    {
                        throw new InvalidOperationException("mdict query returned an empty response.");
                    }

                    if (!string.IsNullOrWhiteSpace(response.Error))
                    {
                        throw new InvalidOperationException(response.Error.Trim());
                    }

                    string result = CleanupCommandOutput(response.Result);
                    return string.IsNullOrWhiteSpace(result) ? string.Empty : result;
                }
                catch (IOException ex)
                {
                    ResetPersistentSession();
                    throw new InvalidOperationException("mdict query failed: " + ex.Message, ex);
                }
            }
            finally
            {
                _persistentRequestGate.Release();
            }
        }

        private string RunMdictCommand(params string[] arguments)
        {
            bool usePython = !string.IsNullOrWhiteSpace(_pythonExecutablePath) && File.Exists(_pythonExecutablePath);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = usePython ? _pythonExecutablePath : _mdictExecutablePath,
                Arguments = usePython ? BuildPythonArguments(arguments) : BuildExecutableArguments(arguments),
                WorkingDirectory = Path.GetDirectoryName(_dictionaryFilePath) ?? AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string errorText = CleanupCommandOutput(standardError);
                    if (string.IsNullOrWhiteSpace(errorText))
                    {
                        errorText = "mdict query failed with exit code " + process.ExitCode.ToString(CultureInfo.InvariantCulture) + ".";
                    }

                    throw new InvalidOperationException(errorText);
                }

                if (!string.IsNullOrWhiteSpace(standardOutput))
                {
                    return standardOutput;
                }

                return standardError ?? string.Empty;
            }
        }

        private static string CleanupCommandOutput(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string cleaned = text.Replace("\0", string.Empty);
            string[] lines = cleaned.Replace("\r\n", "\n").Replace('\r', '\n').Split(new[] { '\n' }, StringSplitOptions.None);
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.IndexOf("--- Elapsed time:", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(line);
            }

            return builder.ToString().Trim();
        }

        private static string TryReadMetadataValue(string text, string key)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!line.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf(':');
                if (separatorIndex < 0 || separatorIndex == line.Length - 1)
                {
                    return string.Empty;
                }

                return line.Substring(separatorIndex + 1).Trim().Trim('"');
            }

            return string.Empty;
        }

        private static string ResolveMdictExecutablePath(string mdictExecutablePath)
        {
            foreach (string candidate in GetMdictExecutableCandidates(mdictExecutablePath))
            {
                string normalizedCandidate = NormalizePath(candidate);
                if (!string.IsNullOrWhiteSpace(normalizedCandidate) && File.Exists(normalizedCandidate))
                {
                    return normalizedCandidate;
                }
            }

            return NormalizePath(mdictExecutablePath);
        }

        private static string ResolvePythonExecutablePath(string requestedMdictExecutablePath, string resolvedMdictExecutablePath)
        {
            foreach (string candidate in GetPythonExecutableCandidates(requestedMdictExecutablePath, resolvedMdictExecutablePath))
            {
                string normalizedCandidate = NormalizePath(candidate);
                if (!string.IsNullOrWhiteSpace(normalizedCandidate) && File.Exists(normalizedCandidate))
                {
                    return normalizedCandidate;
                }
            }

            return string.Empty;
        }

        private static string ResolvePersistentScriptPath()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = Path.Combine(baseDirectory, "tools", "mdict_query_stdin.py");
            return File.Exists(scriptPath) ? scriptPath : string.Empty;
        }

        private static IEnumerable<string> GetMdictExecutableCandidates(string mdictExecutablePath)
        {
            if (!string.IsNullOrWhiteSpace(mdictExecutablePath))
            {
                yield return mdictExecutablePath;
            }

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                yield return Path.Combine(baseDirectory, "runtime", "mdict", "mdict.exe");
                yield return Path.Combine(baseDirectory, "runtime", "python", "Scripts", "mdict.exe");
                yield return Path.Combine(baseDirectory, "tools", "mdict.exe");
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                yield return Path.Combine(userProfile, ".conda", "envs", "herobot_env", "Scripts", "mdict.exe");
            }
        }

        private static IEnumerable<string> GetPythonExecutableCandidates(string requestedMdictExecutablePath, string resolvedMdictExecutablePath)
        {
            if (!string.IsNullOrWhiteSpace(requestedMdictExecutablePath))
            {
                yield return BuildEnvironmentPythonPath(requestedMdictExecutablePath);
            }

            if (!string.IsNullOrWhiteSpace(resolvedMdictExecutablePath))
            {
                yield return BuildEnvironmentPythonPath(resolvedMdictExecutablePath);
            }

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                yield return Path.Combine(baseDirectory, "runtime", "python", "python.exe");
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                yield return Path.Combine(userProfile, ".conda", "envs", "herobot_env", "python.exe");
            }
        }

        private static string BuildEnvironmentPythonPath(string mdictExecutablePath)
        {
            if (string.IsNullOrWhiteSpace(mdictExecutablePath))
            {
                return string.Empty;
            }

            try
            {
                string scriptsDirectory = Path.GetDirectoryName(mdictExecutablePath);
                if (string.IsNullOrWhiteSpace(scriptsDirectory))
                {
                    return string.Empty;
                }

                string environmentDirectory = Path.GetDirectoryName(scriptsDirectory);
                if (string.IsNullOrWhiteSpace(environmentDirectory))
                {
                    return string.Empty;
                }

                return Path.Combine(environmentDirectory, "python.exe");
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                string trimmedPath = path.Trim();
                if (!Path.IsPathRooted(trimmedPath))
                {
                    trimmedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, trimmedPath);
                }

                return Path.GetFullPath(trimmedPath);
            }
            catch
            {
                return path.Trim();
            }
        }

        private static string BuildPythonArguments(string[] arguments)
        {
            StringBuilder builder = new StringBuilder("-X utf8 -m mdict_utils");
            for (int i = 0; i < arguments.Length; i++)
            {
                builder.Append(' ');
                builder.Append(QuoteProcessArgument(arguments[i] ?? string.Empty));
            }

            return builder.ToString();
        }

        private static string BuildExecutableArguments(string[] arguments)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(QuoteProcessArgument(arguments[i] ?? string.Empty));
            }

            return builder.ToString();
        }

        private static string NormalizeWord(string word)
        {
            return string.IsNullOrWhiteSpace(word)
                ? string.Empty
                : word.Trim().ToLowerInvariant();
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

        private bool SupportsPersistentQueryMode()
        {
            return !string.IsNullOrWhiteSpace(_pythonExecutablePath) &&
                File.Exists(_pythonExecutablePath) &&
                !string.IsNullOrWhiteSpace(_persistentScriptPath) &&
                File.Exists(_persistentScriptPath);
        }

        private PersistentQuerySession EnsurePersistentSession()
        {
            lock (_persistentProcessSyncRoot)
            {
                if (_persistentProcess != null && !_persistentProcess.HasExited)
                {
                    return new PersistentQuerySession(_persistentInput, _persistentOutput);
                }

                ResetPersistentSessionCore();

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = _pythonExecutablePath,
                    Arguments = "-X utf8 " + QuoteProcessArgument(_persistentScriptPath) + " " + QuoteProcessArgument(_dictionaryFilePath),
                    WorkingDirectory = Path.GetDirectoryName(_dictionaryFilePath) ?? AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                Process process = new Process();
                process.StartInfo = startInfo;
                process.ErrorDataReceived += PersistentProcess_ErrorDataReceived;
                process.Start();
                process.BeginErrorReadLine();

                _persistentProcess = process;
                _persistentInput = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false));
                _persistentOutput = process.StandardOutput;
                _persistentErrorBuffer = new StringBuilder();

                return new PersistentQuerySession(_persistentInput, _persistentOutput);
            }
        }

        private void WritePersistentRequest(PersistentQuerySession session, string text)
        {
            string payload = SerializeJson(new PersistentQueryRequest
            {
                Text = text ?? string.Empty
            });

            session.Input.WriteLine(payload);
            session.Input.Flush();
        }

        private void PersistentProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            lock (_persistentProcessSyncRoot)
            {
                if (_persistentErrorBuffer == null)
                {
                    return;
                }

                if (_persistentErrorBuffer.Length > 0)
                {
                    _persistentErrorBuffer.AppendLine();
                }

                _persistentErrorBuffer.Append(e.Data);
            }
        }

        private void ClearPersistentErrorBuffer()
        {
            lock (_persistentProcessSyncRoot)
            {
                if (_persistentErrorBuffer != null)
                {
                    _persistentErrorBuffer.Length = 0;
                }
            }
        }

        private string GetPersistentErrorText(string fallback)
        {
            lock (_persistentProcessSyncRoot)
            {
                if (_persistentErrorBuffer == null || _persistentErrorBuffer.Length == 0)
                {
                    return fallback;
                }

                string errorText = CleanupCommandOutput(_persistentErrorBuffer.ToString());
                return string.IsNullOrWhiteSpace(errorText) ? fallback : errorText;
            }
        }

        private void ResetPersistentSession()
        {
            lock (_persistentProcessSyncRoot)
            {
                ResetPersistentSessionCore();
            }
        }

        private void ResetPersistentSessionCore()
        {
            StreamWriter input = _persistentInput;
            StreamReader output = _persistentOutput;
            Process process = _persistentProcess;

            _persistentInput = null;
            _persistentOutput = null;
            _persistentProcess = null;
            _persistentErrorBuffer = null;

            try
            {
                if (input != null)
                {
                    input.Dispose();
                }
            }
            catch
            {
            }

            try
            {
                if (output != null)
                {
                    output.Dispose();
                }
            }
            catch
            {
            }

            try
            {
                if (process != null)
                {
                    process.ErrorDataReceived -= PersistentProcess_ErrorDataReceived;
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }

                    process.Dispose();
                }
            }
            catch
            {
            }
        }

        private static string SerializeJson<T>(T value)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static T DeserializeJson<T>(string jsonText) where T : class
        {
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                return null;
            }

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonText);
            using (MemoryStream stream = new MemoryStream(jsonBytes))
            {
                return serializer.ReadObject(stream) as T;
            }
        }

        private static string QuoteProcessArgument(string value)
        {
            string escaped = (value ?? string.Empty).Replace("\"", "\\\"");
            return "\"" + escaped + "\"";
        }

        private sealed class PersistentQuerySession
        {
            public PersistentQuerySession(StreamWriter input, StreamReader output)
            {
                Input = input;
                Output = output;
            }

            public StreamWriter Input { get; private set; }

            public StreamReader Output { get; private set; }
        }

        [DataContract]
        private sealed class PersistentQueryRequest
        {
            [DataMember(Name = "text")]
            public string Text { get; set; }
        }

        [DataContract]
        private sealed class PersistentQueryResponse
        {
            [DataMember(Name = "result")]
            public string Result { get; set; }

            [DataMember(Name = "error")]
            public string Error { get; set; }
        }
    }
}
