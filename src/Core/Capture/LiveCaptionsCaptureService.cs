using System;
using System.Threading;
using System.Threading.Tasks;
using MyCaption.Core.Models;
using MyCaption.Infrastructure.Automation;

namespace MyCaption.Core.Capture
{
    public sealed class LiveCaptionsCaptureService : IDisposable
    {
        private const int ReadFailureThreshold = 4;
        private const int WaitingRetryDelayMs = 500;
        private const string StartingStatus = "Looking for Windows Live Captions...";
        private const string ConnectedIdleStatus = "Live Captions connected. Waiting for speech...";
        private const string RunningStatus = "Capturing subtitles from Windows Live Captions.";
        private const string WaitingStatus = "Live Captions is unavailable. Waiting to reconnect automatically...";

        private readonly LiveCaptionsAutomationClient _automationClient;
        private readonly LiveCaptionsSettings _settings;
        private CancellationTokenSource _cancellation;
        private string _lastStatus;
        private CaptureState _lastState;
        private bool _lastIsCaptureActive;
        private bool _isRunning;
        private int _readFailureCount;

        public LiveCaptionsCaptureService(LiveCaptionsAutomationClient automationClient, LiveCaptionsSettings settings)
        {
            _automationClient = automationClient;
            _settings = settings;
            _lastStatus = string.Empty;
            _lastState = CaptureState.Paused;
        }

        public event EventHandler<LiveCaptionsSnapshotEventArgs> SnapshotCaptured;
        public event EventHandler<CaptureStateChangedEventArgs> StateChanged;

        public void Start()
        {
            if (_isRunning)
            {
                return;
            }

            if (_cancellation != null)
            {
                _cancellation.Dispose();
                _cancellation = null;
            }

            _isRunning = true;
            _readFailureCount = 0;
            _cancellation = new CancellationTokenSource();
            PublishState(CaptureState.Starting, StartingStatus, true);

            Task.Factory.StartNew(
                delegate { CaptureLoop(_cancellation.Token); },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void Stop()
        {
            StopInternal(CaptureState.Paused, "Capture paused.", true);
        }

        private void CaptureLoop(CancellationToken cancellationToken)
        {
            bool isAttached = false;

            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                if (!isAttached)
                {
                    if (_automationClient.EnsureWindowConnected(_settings.AutoLaunchIfMissing))
                    {
                        ApplyOriginalWindowVisibilitySetting();
                        if (_automationClient.TryEnsureCaptionElementReady(2, 30))
                        {
                            isAttached = true;
                            _readFailureCount = 0;
                            PublishState(CaptureState.ConnectedIdle, ConnectedIdleStatus, true);
                            Thread.Sleep(_settings.PollIntervalMs);
                        }
                        else
                        {
                            EnterWaitingState(WaitingStatus);
                            Thread.Sleep(WaitingRetryDelayMs);
                        }
                    }
                    else
                    {
                        EnterWaitingState(WaitingStatus);
                        Thread.Sleep(WaitingRetryDelayMs);
                    }

                    continue;
                }

                if (!_automationClient.IsWindowAlive())
                {
                    HandleTransientReadFailure(ref isAttached);
                    continue;
                }

                if (!_automationClient.TryEnsureCaptionElementReady(2, 30))
                {
                    HandleTransientReadFailure(ref isAttached);
                    continue;
                }

                string text;
                if (_automationClient.TryReadCaptions(out text))
                {
                    _readFailureCount = 0;

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        PublishState(CaptureState.ConnectedIdle, ConnectedIdleStatus, true);
                    }
                    else
                    {
                        PublishState(CaptureState.Running, RunningStatus, true);

                        EventHandler<LiveCaptionsSnapshotEventArgs> handler = SnapshotCaptured;
                        if (handler != null)
                        {
                            handler(this, new LiveCaptionsSnapshotEventArgs(new LiveCaptionsSnapshot(text, DateTime.UtcNow)));
                        }
                    }
                }
                else
                {
                    HandleTransientReadFailure(ref isAttached);
                    continue;
                }

                Thread.Sleep(_settings.PollIntervalMs);
            }
        }

        public void UpdateOriginalWindowVisibility(bool hideOriginalWindow)
        {
            _settings.HideOriginalWindow = hideOriginalWindow;

            if (!_isRunning)
            {
                if (!hideOriginalWindow)
                {
                    _automationClient.RestoreWindowPlacement();
                }

                return;
            }

            ApplyOriginalWindowVisibilitySetting();
        }

        private void StopInternal(CaptureState state, string statusText, bool restoreWindow)
        {
            _isRunning = false;
            _readFailureCount = 0;

            if (_cancellation != null)
            {
                _cancellation.Cancel();
            }

            if (restoreWindow)
            {
                _automationClient.RestoreWindowPlacement();
            }

            PublishState(state, statusText, false);
        }

        private void EnterWaitingState(string statusText)
        {
            PublishState(CaptureState.WaitingForSource, statusText, true);
        }

        private void ApplyOriginalWindowVisibilitySetting()
        {
            if (_settings.HideOriginalWindow)
            {
                _automationClient.HideWindowWithoutMinimizing();
            }
            else
            {
                _automationClient.RestoreWindowPlacement();
            }
        }

        private void HandleTransientReadFailure(ref bool isAttached)
        {
            _readFailureCount++;
            if (_readFailureCount < ReadFailureThreshold)
            {
                return;
            }

            _readFailureCount = 0;
            isAttached = false;
            if (!_automationClient.IsWindowAlive())
            {
                _automationClient.ResetHiddenState();
            }

            EnterWaitingState(WaitingStatus);
        }

        private void PublishState(CaptureState state, string statusText, bool isCaptureActive)
        {
            if (_lastState == state &&
                _lastIsCaptureActive == isCaptureActive &&
                string.Equals(_lastStatus, statusText, StringComparison.Ordinal))
            {
                return;
            }

            _lastState = state;
            _lastStatus = statusText;
            _lastIsCaptureActive = isCaptureActive;
            EventHandler<CaptureStateChangedEventArgs> handler = StateChanged;
            if (handler != null)
            {
                handler(this, new CaptureStateChangedEventArgs(state, statusText, isCaptureActive));
            }
        }

        public void Dispose()
        {
            Stop();

            if (_cancellation != null)
            {
                _cancellation.Dispose();
                _cancellation = null;
            }
        }
    }
}
