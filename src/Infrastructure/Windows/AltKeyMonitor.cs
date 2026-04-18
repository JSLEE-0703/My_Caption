using System;
using System.Windows.Threading;
using MyCaption.Core.Models;

namespace MyCaption.Infrastructure.Windows
{
    public sealed class AltKeyMonitor : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private bool _isAltPressed;

        public AltKeyMonitor()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background);
            _timer.Interval = TimeSpan.FromMilliseconds(16);
            _timer.Tick += OnTick;
        }

        public event EventHandler<AltStateChangedEventArgs> AltStateChanged;

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void OnTick(object sender, EventArgs e)
        {
            bool isPressed = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0;
            if (isPressed == _isAltPressed)
            {
                return;
            }

            _isAltPressed = isPressed;

            EventHandler<AltStateChangedEventArgs> handler = AltStateChanged;
            if (handler != null)
            {
                handler(this, new AltStateChangedEventArgs(_isAltPressed));
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
        }
    }
}
