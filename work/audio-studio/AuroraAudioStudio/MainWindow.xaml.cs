using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using System.Reflection;
using Windows.Graphics;
using System.Runtime.InteropServices;
using AuroraAudioStudio.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AuroraAudioStudio;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly WindowStateService windowState = new();
    private int normalWidth = 1560;
    private int normalHeight = 960;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var displayVersion = version is null ? "1.8.0" : version.Revision > 0 ? version.ToString(4) : version.ToString(3);
        AppTitleBar.Subtitle = displayVersion;

        var versionedIcon = Path.Combine(AppContext.BaseDirectory, "Assets", $"AppIcon-{displayVersion}.ico");
        AppWindow.SetIcon(File.Exists(versionedIcon)
            ? versionedIcon
            : Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
        var saved = windowState.Load();
        normalWidth = saved.Width;
        normalHeight = saved.Height;
        AppWindow.Resize(new SizeInt32(saved.Width, saved.Height));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 960;
            presenter.PreferredMinimumHeight = 640;
            if (saved.IsMaximized) presenter.Maximize();
        }
        AppWindow.Changed += (_, args) =>
        {
            if (!args.DidSizeChange || AppWindow.Presenter is not OverlappedPresenter current || current.State != OverlappedPresenterState.Restored) return;
            normalWidth = Math.Max(960, AppWindow.Size.Width);
            normalHeight = Math.Max(640, AppWindow.Size.Height);
        };
        Closed += (_, _) =>
        {
            var maximized = AppWindow.Presenter is OverlappedPresenter current && current.State == OverlappedPresenterState.Maximized;
            windowState.Save(new WindowState(normalWidth, normalHeight, maximized));
            (RootFrame.Content as MainPage)?.Shutdown();
        };
    }

    public void BringToFront()
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ShowWindow(handle, 9);
        Activate();
        SetForegroundWindow(handle);
    }

    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hWnd, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hWnd);
}
