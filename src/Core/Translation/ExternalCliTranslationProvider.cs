using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyCaption.Core.Models;

namespace MyCaption.Core.Translation
{
    public sealed class ExternalCliTranslationProvider : ITranslationProvider, ITranslationProviderStatus
    {
        private readonly string _executablePath;
        private readonly string _argumentsTemplate;
        private readonly string _statusSummary;
        private readonly string _initializationError;

        public ExternalCliTranslationProvider(TranslationSettings settings)
        {
            settings = settings ?? new TranslationSettings();
            settings.ApplyDefaults();

            _executablePath = TranslationProviderFactory.NormalizeOptionalPath(settings.ExecutablePath);
            _argumentsTemplate = settings.ArgumentsTemplate == null ? string.Empty : settings.ArgumentsTemplate.Trim();
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
                string translatedText = ExecuteTranslation(request, cancellationToken);
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

            return string.IsNullOrWhiteSpace(_argumentsTemplate)
                ? "External CLI ready. No arguments template configured."
                : "External CLI ready.";
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
    }
}
