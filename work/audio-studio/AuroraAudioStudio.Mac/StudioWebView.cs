using Avalonia.Controls;

namespace AuroraAudioStudio.Mac;

public sealed class StudioWebView : NativeWebView
{
    private bool refreshing;
    public StudioWebView()
    {
        NavigationCompleted += async (_, e) =>
        {
            if (!e.IsSuccess || refreshing || !OperatingSystem.IsMacOSVersionAtLeast(27) || TopLevel.GetTopLevel(this) is not Window window) return;
            refreshing = true;
            // macOS 27 can leave an embedded WKWebView unpainted until its
            // window receives a resize. Reapply the window size after navigation has laid out the page.
            var width = window.Width;
            try
            {
                await Task.Delay(350);
                window.Width = window.Bounds.Width + 8;
                await Task.Delay(180);
                window.Width = width;
            }
            finally { refreshing = false; }
        };
    }
}
