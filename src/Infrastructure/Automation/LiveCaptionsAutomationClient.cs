using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Automation;
using MyCaption.Infrastructure.Windows;

namespace MyCaption.Infrastructure.Automation
{
    public sealed class LiveCaptionsAutomationClient
    {
        private const string ProcessName = "LiveCaptions";
        private const string WindowClassName = "LiveCaptionsDesktopWindow";
        private const string CaptionsTextBlockId = "CaptionsTextBlock";
        private const int CaptionElementWarmupAttempts = 25;
        private const int CaptionElementWarmupDelayMs = 80;
        private const int HiddenWindowOffset = 32000;

        private AutomationElement _window;
        private AutomationElement _captionsTextBlock;
        private bool _windowHidden;
        private bool _hasStoredPlacement;
        private NativeMethods.RECT _storedWindowRect;
        private int _storedExStyle;

        public bool TryAttachToRunningCaptions()
        {
            if (IsWindowAlive())
            {
                return true;
            }

            _window = FindExistingWindow();
            _captionsTextBlock = null;
            if (_window != null)
            {
                _windowHidden = false;
                _hasStoredPlacement = false;
            }

            return _window != null;
        }

        public bool EnsureConnected(bool autoLaunch)
        {
            if (TryAttachToRunningCaptions())
            {
                return TryEnsureCaptionElementReady(CaptionElementWarmupAttempts, CaptionElementWarmupDelayMs);
            }

            if (!autoLaunch)
            {
                return false;
            }

            _window = LaunchAndFindWindow();
            _captionsTextBlock = null;
            _windowHidden = false;
            _hasStoredPlacement = false;
            return _window != null &&
                TryEnsureCaptionElementReady(CaptionElementWarmupAttempts, CaptionElementWarmupDelayMs);
        }

        public bool TryReadCaptions(out string text)
        {
            text = string.Empty;

            if (!IsWindowAlive())
            {
                return false;
            }

            try
            {
                if (!TryEnsureCaptionElementReady(1, 0))
                {
                    return false;
                }

                text = _captionsTextBlock.Current.Name ?? string.Empty;
                return true;
            }
            catch (ElementNotAvailableException)
            {
                _captionsTextBlock = null;
                return TryReadAfterRefreshingElement(out text);
            }
        }

        public bool TryEnsureCaptionElementReady(int attempts, int delayMs)
        {
            if (!IsWindowAlive())
            {
                return false;
            }

            if (IsCaptionElementAlive())
            {
                return true;
            }

            if (attempts <= 0)
            {
                attempts = 1;
            }

            for (int i = 0; i < attempts; i++)
            {
                _captionsTextBlock = FindElementByAutomationId(_window, CaptionsTextBlockId);
                if (_captionsTextBlock != null)
                {
                    return true;
                }

                if (delayMs > 0 && i < attempts - 1)
                {
                    Thread.Sleep(delayMs);
                }
            }

            return false;
        }

        public void HideWindowWithoutMinimizing()
        {
            if (_window == null || _windowHidden)
            {
                return;
            }

            IntPtr handle = new IntPtr(_window.Current.NativeWindowHandle);
            if (handle == IntPtr.Zero)
            {
                return;
            }

            int currentStyle = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
            if (!_hasStoredPlacement)
            {
                _storedExStyle = currentStyle;
                if (NativeMethods.GetWindowRect(handle, out _storedWindowRect))
                {
                    _hasStoredPlacement = true;
                }
            }

            NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
            NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE, _storedExStyle | NativeMethods.WS_EX_TOOLWINDOW);
            NativeMethods.MoveWindow(
                handle,
                HiddenWindowOffset,
                HiddenWindowOffset,
                Math.Max(320, _storedWindowRect.Width),
                Math.Max(120, _storedWindowRect.Height),
                true);
            _windowHidden = true;
        }

        public void RestoreWindowPlacement()
        {
            if (_window == null || !_windowHidden)
            {
                return;
            }

            IntPtr handle = new IntPtr(_window.Current.NativeWindowHandle);
            if (handle == IntPtr.Zero)
            {
                return;
            }

            int style = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
            int restoredStyle = _hasStoredPlacement ? _storedExStyle : (style & ~NativeMethods.WS_EX_TOOLWINDOW);
            NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE, restoredStyle);
            NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
            if (_hasStoredPlacement)
            {
                NativeMethods.MoveWindow(
                    handle,
                    _storedWindowRect.Left,
                    _storedWindowRect.Top,
                    Math.Max(320, _storedWindowRect.Width),
                    Math.Max(120, _storedWindowRect.Height),
                    true);
            }

            _windowHidden = false;
            _hasStoredPlacement = false;
        }

        public void ResetHiddenState()
        {
            _windowHidden = false;
            _hasStoredPlacement = false;
        }

        public bool IsWindowAlive()
        {
            if (_window == null)
            {
                return false;
            }

            try
            {
                AutomationElement.AutomationElementInformation info = _window.Current;
                return !string.IsNullOrWhiteSpace(info.ClassName);
            }
            catch (ElementNotAvailableException)
            {
                _window = null;
                _captionsTextBlock = null;
                return false;
            }
        }

        private bool IsCaptionElementAlive()
        {
            if (_captionsTextBlock == null)
            {
                return false;
            }

            try
            {
                AutomationElement.AutomationElementInformation info = _captionsTextBlock.Current;
                return !string.IsNullOrWhiteSpace(info.AutomationId);
            }
            catch (ElementNotAvailableException)
            {
                _captionsTextBlock = null;
                return false;
            }
        }

        private bool TryReadAfterRefreshingElement(out string text)
        {
            text = string.Empty;
            if (!TryEnsureCaptionElementReady(3, 30))
            {
                if (!IsWindowAlive())
                {
                    _windowHidden = false;
                    _hasStoredPlacement = false;
                }

                return false;
            }

            try
            {
                text = _captionsTextBlock.Current.Name ?? string.Empty;
                return true;
            }
            catch (ElementNotAvailableException)
            {
                _captionsTextBlock = null;
                return false;
            }
        }

        private AutomationElement FindExistingWindow()
        {
            foreach (Process process in Process.GetProcessesByName(ProcessName))
            {
                AutomationElement window = FindWindowByProcessId(process.Id);
                if (window != null && string.Equals(window.Current.ClassName, WindowClassName, StringComparison.Ordinal))
                {
                    return window;
                }
            }

            return null;
        }

        private AutomationElement LaunchAndFindWindow()
        {
            Process process = null;

            try
            {
                process = Process.Start(ProcessName);
            }
            catch
            {
                try
                {
                    process = Process.Start(ProcessName + ".exe");
                }
                catch
                {
                    return null;
                }
            }

            if (process == null)
            {
                return null;
            }

            for (int i = 0; i < 120; i++)
            {
                AutomationElement window = FindWindowByProcessId(process.Id);
                if (window != null && string.Equals(window.Current.ClassName, WindowClassName, StringComparison.Ordinal))
                {
                    return window;
                }

                Thread.Sleep(100);
            }

            return null;
        }

        private static AutomationElement FindWindowByProcessId(int processId)
        {
            PropertyCondition condition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
            return AutomationElement.RootElement.FindFirst(TreeScope.Children, condition);
        }

        private static AutomationElement FindElementByAutomationId(AutomationElement parent, string automationId)
        {
            if (parent == null)
            {
                return null;
            }

            PropertyCondition condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
            return parent.FindFirst(TreeScope.Descendants, condition);
        }
    }
}
