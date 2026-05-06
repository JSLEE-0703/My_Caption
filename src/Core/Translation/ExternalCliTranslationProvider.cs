using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyCaption.Core.Models;

namespace MyCaption.Core.Translation
{
    public sealed class ExternalCliTranslationProvider : ITranslationProvider, ITranslationProviderStatus, IDisposable
    {
        private readonly object _persistentProcessSyncRoot;
        private readonly SemaphoreSlim _persistentRequestGate;
        private readonly string _executablePath;
        private readonly string _argumentsTemplate;
        private readonly bool _persistentModeEnabled;
        private readonly string _statusSummary;
        private readonly string _initializationError;
        private Process _persistentProcess;
        private StreamWriter _persistentInput;
        private StreamReader _persistentOutput;
        private StringBuilder _persistentErrorBuffer;
        private string _persistentArguments;

        public ExternalCliTranslationProvider(TranslationSettings settings)
        {
            _persistentProcessSyncRoot = new object();
            _persistentRequestGate = new SemaphoreSlim(1, 1);
            settings = settings ?? new TranslationSettings();
            settings.ApplyDefaults();

            _executablePath = TranslationProviderFactory.NormalizeOptionalPath(settings.ExecutablePath);
            _argumentsTemplate = settings.ArgumentsTemplate == null ? string.Empty : settings.ArgumentsTemplate.Trim();
            _persistentModeEnabled = SupportsPersistentMode(_argumentsTemplate);
            _statusSummary = InitializeStatus(out _initializationError);
        }

        public string DisplayName
        {
            get { return "External CLI"; }
        }

        public string Description
        {
            get { return "Runs a local executable or script and passes source text through stdin."; }
        }

        public string StatusSummary
        {
            get { return _statusSummary; }
        }

        public string ExecutablePath
        {
            get { return _executablePath; }
        }

        public string ArgumentsTemplate
        {
            get { return _argumentsTemplate; }
        }

        public string ApiUrl
        {
            get { return string.Empty; }
        }

        public string ApiKey
        {
            get { return string.Empty; }
        }

        public string ApiRegion
        {
            get { return string.Empty; }
        }

        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(_initializationError))
            {
                return Task.FromResult(new TranslationResult(request.SourceText, "[translation error] " + _initializationError));
            }

            return Task.Run(delegate
            {
                string translatedText = _persistentModeEnabled
                    ? ExecutePersistentTranslation(request, cancellationToken)
                    : ExecuteTranslation(request, cancellationToken);
                return new TranslationResult(request.SourceText, translatedText);
            }, cancellationToken);
        }

        private string InitializeStatus(out string initializationError)
        {
            initializationError = string.Empty;

            if (string.IsNullOrWhiteSpace(_executablePath))
            {
                initializationError = "Translation CLI unavailable: executable path is empty.";
                return initializationError;
            }

            if (!File.Exists(_executablePath))
            {
                initializationError = "Translation CLI unavailable: executable not found.";
                return initializationError;
            }

            if (string.IsNullOrWhiteSpace(_argumentsTemplate))
            {
                return "External CLI ready. No arguments template configured.";
            }

            return _persistentModeEnabled
                ? "External CLI ready. Persistent Argos mode enabled."
                : "External CLI ready.";
        }

        private string ExecutePersistentTranslation(TranslationRequest request, CancellationToken cancellationToken)
        {
            _persistentRequestGate.Wait(cancellationToken);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                PersistentProcessSession session = EnsurePersistentSession(request);
                try
                {
                    ClearPersistentErrorBuffer();
                    WritePersistentRequest(session, request.SourceText ?? string.Empty);

                    string responseLine = session.Output.ReadLine();

                    if (responseLine == null)
                    {
                        throw new InvalidOperationException(GetPersistentErrorText("translation process exited unexpectedly."));
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    PersistentResponsePayload response = DeserializeJson<PersistentResponsePayload>(responseLine);
                    if (response == null)
                    {
                        throw new InvalidOperationException("translation command returned an empty response.");
                    }

                    if (!string.IsNullOrWhiteSpace(response.Error))
                    {
                        throw new InvalidOperationException(response.Error.Trim());
                    }

                    return CleanupCommandOutput(response.TranslatedText);
                }
                catch (IOException ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    ResetPersistentSession();
                    throw new InvalidOperationException("translation command failed: " + ex.Message, ex);
                }
            }
            finally
            {
                _persistentRequestGate.Release();
            }
        }

        private string ExecuteTranslation(TranslationRequest request, CancellationToken cancellationToken)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                Arguments = BuildArguments(request),
                WorkingDirectory = Path.GetDirectoryName(_executablePath) ?? AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
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

                CancellationTokenRegistration registration = cancellationToken.Register(delegate
                {
                    TryTerminateProcess(process);
                });

                try
                {
                    WriteInput(process, request.SourceText ?? string.Empty);

                    string standardOutput = process.StandardOutput.ReadToEnd();
                    string standardError = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    cancellationToken.ThrowIfCancellationRequested();

                    if (process.ExitCode != 0)
                    {
                        string errorText = CleanupCommandOutput(standardError);
                        if (string.IsNullOrWhiteSpace(errorText))
                        {
                            errorText = "translation command failed with exit code " + process.ExitCode.ToString(CultureInfo.InvariantCulture) + ".";
                        }

                        throw new InvalidOperationException(errorText);
                    }

                    return CleanupCommandOutput(standardOutput);
                }
                finally
                {
                    registration.Dispose();
                }
            }
        }

        private string BuildArguments(TranslationRequest request)
        {
            string arguments = _argumentsTemplate;
            arguments = arguments.Replace("{from}", request.SourceLanguage ?? string.Empty);
            arguments = arguments.Replace("{to}", request.TargetLanguage ?? string.Empty);
            return arguments;
        }

        private string BuildPersistentArguments(TranslationRequest request)
        {
            string arguments = BuildArguments(request);
            if (arguments.IndexOf("--persistent", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return arguments;
            }

            return string.IsNullOrWhiteSpace(arguments)
                ? "--persistent"
                : arguments + " --persistent";
        }

        private PersistentProcessSession EnsurePersistentSession(TranslationRequest request)
        {
            string arguments = BuildPersistentArguments(request);

            lock (_persistentProcessSyncRoot)
            {
                if (_persistentProcess != null &&
                    !_persistentProcess.HasExited &&
                    string.Equals(_persistentArguments, arguments, StringComparison.Ordinal))
                {
                    return new PersistentProcessSession(_persistentInput, _persistentOutput);
                }

                ResetPersistentSessionCore();

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = _executablePath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(_executablePath) ?? AppDomain.CurrentDomain.BaseDirectory,
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
                _persistentArguments = arguments;

                return new PersistentProcessSession(_persistentInput, _persistentOutput);
            }
        }

        private void WritePersistentRequest(PersistentProcessSession session, string text)
        {
            string payload = SerializeJson(new PersistentRequestPayload
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
            Process process = _persistentProcess;

            _persistentInput = null;
            _persistentOutput = null;
            _persistentErrorBuffer = null;
            _persistentArguments = null;
            _persistentProcess = null;

            if (input != null)
            {
                try
                {
                    input.Dispose();
                }
                catch
                {
                }
            }

            if (process != null)
            {
                TryTerminateProcess(process);
                try
                {
                    process.Dispose();
                }
                catch
                {
                }
            }
        }

        private static void WriteInput(Process process, string text)
        {
            StreamWriter writer = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false));
            try
            {
                writer.Write(text ?? string.Empty);
                writer.Flush();
            }
            finally
            {
                writer.Dispose();
            }
        }

        private static string CleanupCommandOutput(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Replace("\0", string.Empty).Trim();
        }

        private static bool SupportsPersistentMode(string argumentsTemplate)
        {
            return !string.IsNullOrWhiteSpace(argumentsTemplate) &&
                argumentsTemplate.IndexOf("argos_translate_stdin.py", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string SerializeJson<T>(T value)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static T DeserializeJson<T>(string json)
        {
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? string.Empty)))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                object value = serializer.ReadObject(stream);
                return value is T ? (T)value : default(T);
            }
        }

        private static void TryTerminateProcess(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            ResetPersistentSession();
            _persistentRequestGate.Dispose();
        }

        [DataContract]
        private sealed class PersistentRequestPayload
        {
            [DataMember(Name = "text")]
            public string Text { get; set; }
        }

        [DataContract]
        private sealed class PersistentResponsePayload
        {
            [DataMember(Name = "translatedText")]
            public string TranslatedText { get; set; }

            [DataMember(Name = "error")]
            public string Error { get; set; }
        }

        private sealed class PersistentProcessSession
        {
            public PersistentProcessSession(StreamWriter input, StreamReader output)
            {
                Input = input;
                Output = output;
            }

            public StreamWriter Input { get; private set; }

            public StreamReader Output { get; private set; }
        }
    }
}
