using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MyCaption.Core.Models;
using MyCaption.Infrastructure.Windows;
using MyCaption.Runtime;

namespace MyCaption.UI.Overlay
{
    public partial class OverlayWindow : Window
    {
        private readonly AppRuntime _runtime;
        private LookupCardWindow _lookupWindow;
        private bool _isClosingLookupWindow;

        public OverlayWindow(AppRuntime runtime)
        {
            _runtime = runtime;
            InitializeComponent();
            DataContext = _runtime.Overlay;

            Loaded += OverlayWindow_Loaded;
            SourceInitialized += OverlayWindow_SourceInitialized;
            LocationChanged += OverlayWindow_BoundsChanged;
            SizeChanged += OverlayWindow_BoundsChanged;

            _runtime.InteractiveModeChanged += Runtime_InteractiveModeChanged;
            _runtime.OverlaySettingsChanged += Runtime_OverlaySettingsChanged;
        }

        public void ApplyOverlaySettings()
        {
            Left = _runtime.Settings.Overlay.Left;
            Top = _runtime.Settings.Overlay.Top;
            Width = _runtime.Settings.Overlay.Width;
            Height = _runtime.Settings.Overlay.Height;
            Topmost = _runtime.Settings.Overlay.Topmost;
            ApplyOrder();
        }

        private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyOverlaySettings();
        }

        private void OverlayWindow_SourceInitialized(object sender, EventArgs e)
        {
            ApplyClickThrough(!_runtime.Overlay.IsInteractive && _runtime.Settings.Interaction.StartClickThrough);
        }

        private void Runtime_InteractiveModeChanged(object sender, EventArgs e)
        {
            ApplyClickThrough(_runtime.Settings.Interaction.StartClickThrough && !_runtime.Overlay.IsInteractive);
            if (!_runtime.Overlay.IsInteractive)
            {
                HideLookupWindow();
            }
        }

        private void Runtime_OverlaySettingsChanged(object sender, EventArgs e)
        {
            ApplyOverlaySettings();
        }

        private void ApplyOrder()
        {
            bool originalOnTop = _runtime.Settings.Overlay.OriginalOnTop;
            Grid.SetRow(OriginalPanel, originalOnTop ? 1 : 2);
            Grid.SetRow(TranslationPanel, originalOnTop ? 2 : 1);
        }

        private void ApplyClickThrough(bool enabled)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            int style = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);

            if (enabled)
            {
                style |= NativeMethods.WS_EX_TRANSPARENT;
            }
            else
            {
                style &= ~NativeMethods.WS_EX_TRANSPARENT;
            }

            NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE, style);
            NativeMethods.SetWindowPos(
                handle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
        }

        private void OverlayWindow_BoundsChanged(object sender, EventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            _runtime.SaveOverlayBounds(new Rect(Left, Top, Width, Height));
        }

        private void Chrome_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_runtime.Overlay.IsInteractive)
            {
                return;
            }

            DependencyObject current = e.OriginalSource as DependencyObject;
            while (current != null)
            {
                if (current is ButtonBase || current is Thumb)
                {
                    return;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            _runtime.Overlay.UpdateLookup(null);
            HideLookupWindow();
            DragMove();
            e.Handled = true;
        }

        private void WordTokenButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_runtime.Overlay.IsInteractive)
            {
                return;
            }

            ButtonBase button = sender as ButtonBase;
            if (button == null)
            {
                return;
            }

            WordTokenViewModel token = button.Tag as WordTokenViewModel;
            if (token == null || !token.CanClick)
            {
                return;
            }

            _runtime.Overlay.BeginLookup(token.Text);
            ShowLookupWindowFor(button);
            _runtime.LookupAsync(token);
            e.Handled = true;
        }

        private void ShowLookupWindowFor(FrameworkElement anchor)
        {
            EnsureLookupWindow();
            if (_lookupWindow == null || _isClosingLookupWindow)
            {
                return;
            }

            if (!_lookupWindow.IsVisible)
            {
                _lookupWindow.Show();
            }

            Point point = GetLookupAnchorPoint(anchor);
            _lookupWindow.PositionNear(point);
        }

        private void EnsureLookupWindow()
        {
            if (_lookupWindow != null)
            {
                return;
            }

            _lookupWindow = new LookupCardWindow();
            _lookupWindow.DataContext = _runtime.Overlay;
            _lookupWindow.Closed += LookupWindow_Closed;
        }

        private void LookupWindow_Closed(object sender, EventArgs e)
        {
            _lookupWindow = null;
            _isClosingLookupWindow = false;
        }

        private Point GetLookupAnchorPoint(FrameworkElement anchor)
        {
            try
            {
                if (anchor != null)
                {
                    PresentationSource anchorSource = PresentationSource.FromVisual(anchor);
                    if (anchorSource != null)
                    {
                        Point point = anchor.PointToScreen(new Point(anchor.ActualWidth, anchor.ActualHeight));
                        if (anchorSource.CompositionTarget != null)
                        {
                            point = anchorSource.CompositionTarget.TransformFromDevice.Transform(point);
                        }

                        return point;
                    }
                }
            }
            catch
            {
            }

            return new Point(Left + Width - 28.0, Top + 48.0);
        }

        private void HideLookupWindow()
        {
            if (_lookupWindow != null && _lookupWindow.IsVisible && !_isClosingLookupWindow)
            {
                _lookupWindow.Hide();
            }
        }

        private void CloseLookupWindow()
        {
            if (_lookupWindow != null && !_isClosingLookupWindow)
            {
                try
                {
                    _isClosingLookupWindow = true;
                    _lookupWindow.Close();
                }
                finally
                {
                    _lookupWindow = null;
                    _isClosingLookupWindow = false;
                }
            }
        }

        private void TopThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (!_runtime.Overlay.IsInteractive)
            {
                return;
            }

            double newHeight = Height - e.VerticalChange;
            if (newHeight < MinHeight)
            {
                return;
            }

            Top += e.VerticalChange;
            Height = newHeight;
        }

        private void BottomThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (!_runtime.Overlay.IsInteractive)
            {
                return;
            }

            double newHeight = Height + e.VerticalChange;
            if (newHeight < MinHeight)
            {
                return;
            }

            Height = newHeight;
        }

        private void LeftThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (!_runtime.Overlay.IsInteractive)
            {
                return;
            }

            double newWidth = Width - e.HorizontalChange;
            if (newWidth < MinWidth)
            {
                return;
            }

            Left += e.HorizontalChange;
            Width = newWidth;
        }

        private void RightThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (!_runtime.Overlay.IsInteractive)
            {
                return;
            }

            double newWidth = Width + e.HorizontalChange;
            if (newWidth < MinWidth)
            {
                return;
            }

            Width = newWidth;
        }

        protected override void OnClosed(EventArgs e)
        {
            CloseLookupWindow();
            _runtime.InteractiveModeChanged -= Runtime_InteractiveModeChanged;
            _runtime.OverlaySettingsChanged -= Runtime_OverlaySettingsChanged;
            base.OnClosed(e);
        }
    }
}
