using System;
using System.Threading;
using System.Threading.Tasks;
using MyCaption.Core.Models;

namespace MyCaption.Core.Translation
{
    public sealed class TranslationDispatcher : IDisposable
    {
        private readonly object _syncRoot;
        private readonly ITranslationProvider _provider;
        private CancellationTokenSource _currentCancellation;
        private int _requestVersion;
        private string _lastRequestedText;

        public TranslationDispatcher(ITranslationProvider provider)
        {
            _provider = provider;
            _syncRoot = new object();
            _lastRequestedText = string.Empty;
        }

        public event EventHandler<TranslationCompletedEventArgs> TranslationCompleted;

        public ITranslationProvider Provider
        {
            get { return _provider; }
        }

        public void Request(TranslationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.SourceText))
            {
                return;
            }

            int version;
            CancellationTokenSource cancellation;

            lock (_syncRoot)
            {
                if (string.Equals(_lastRequestedText, request.SourceText, StringComparison.Ordinal))
                {
                    return;
                }

                _lastRequestedText = request.SourceText;
                _requestVersion++;
                version = _requestVersion;

                if (_currentCancellation != null)
                {
                    _currentCancellation.Cancel();
                    _currentCancellation.Dispose();
                }

                _currentCancellation = new CancellationTokenSource();
                cancellation = _currentCancellation;
            }

            Task.Factory.StartNew(
                delegate { return ExecuteAsync(request, version, cancellation.Token); },
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default).Unwrap();
        }

        private async Task ExecuteAsync(TranslationRequest request, int version, CancellationToken cancellationToken)
        {
            TranslationResult result;

            try
            {
                result = await _provider.TranslateAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                result = new TranslationResult(request.SourceText, "[translation error] " + ex.Message, request.IsCommitted);
            }

            lock (_syncRoot)
            {
                if (version != _requestVersion)
                {
                    return;
                }
            }

            EventHandler<TranslationCompletedEventArgs> handler = TranslationCompleted;
            if (handler != null)
            {
                handler(this, new TranslationCompletedEventArgs(result));
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_currentCancellation != null)
                {
                    _currentCancellation.Cancel();
                    _currentCancellation.Dispose();
                    _currentCancellation = null;
                }
            }
        }
    }
}
