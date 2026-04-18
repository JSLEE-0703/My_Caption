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
                LookupPopup.IsOpen = false;
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

            LookupPopup.IsOpen = false;
            DragMove();
            e.Handled = true;
        }

        private void WordTokenButton_Click(object sender, RoutedEventArgs e)
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

            LookupPopup.PlacementTarget = button;
            LookupPopup.IsOpen = true;
            _runtime.LookupAsync(token);
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
            _runtime.InteractiveModeChanged -= Runtime_InteractiveModeChanged;
            _runtime.OverlaySettingsChanged -= Runtime_OverlaySettingsChanged;
            base.OnClosed(e);
        }
    }
}
