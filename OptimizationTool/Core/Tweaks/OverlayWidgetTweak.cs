using System.Windows.Forms;
using OptimizationTool.Core.Interfaces;
using OptimizationTool.UI.Controls;

namespace OptimizationTool.Core.Tweaks
{
    public class OverlayWidgetTweak : ITweak
    {
        public string Id => "DesktopOverlayWidget";
        public string Title => "Live Performance Overlay";
        public string Description => "Toggles a watermark graph on your desktop tracking CPU, Memory, and Network.";
        public bool IsAction => false;

        private static DesktopOverlayWidget? activeWidget;

        public bool IsApplied() => activeWidget != null && !activeWidget.IsDisposed && activeWidget.Visible;

        public void Apply()
        {
            if (activeWidget == null || activeWidget.IsDisposed)
            {
                activeWidget = new DesktopOverlayWidget();
            }
            activeWidget.Show();
        }

        public void Revert()
        {
            if (activeWidget != null && !activeWidget.IsDisposed)
            {
                activeWidget.Close();
                activeWidget.Dispose();
                activeWidget = null;
            }
        }
    }
}