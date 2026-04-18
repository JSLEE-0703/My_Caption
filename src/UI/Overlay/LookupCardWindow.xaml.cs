using System;
using System.Windows;

namespace MyCaption.UI.Overlay
{
    public partial class LookupCardWindow : Window
    {
        public LookupCardWindow()
        {
            InitializeComponent();
        }

        public void PositionNear(Point screenPoint)
        {
            UpdateLayout();

            double targetLeft = screenPoint.X + 12.0;
            double targetTop = screenPoint.Y + 12.0;
            double maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - ActualWidth - 12.0;
            double maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - ActualHeight - 12.0;

            if (targetLeft > maxLeft)
            {
                targetLeft = Math.Max(SystemParameters.VirtualScreenLeft + 12.0, screenPoint.X - ActualWidth - 12.0);
            }

            if (targetTop > maxTop)
            {
                targetTop = Math.Max(SystemParameters.VirtualScreenTop + 12.0, maxTop);
            }

            Left = Math.Max(SystemParameters.VirtualScreenLeft + 12.0, targetLeft);
            Top = Math.Max(SystemParameters.VirtualScreenTop + 12.0, targetTop);
        }
    }
}
