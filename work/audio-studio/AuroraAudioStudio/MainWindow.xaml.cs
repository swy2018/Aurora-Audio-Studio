using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using System.Reflection;
using Windows.Graphics;

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
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var displayVersion = version is null ? "1.0.1" : version.Revision > 0 ? version.ToString(4) : version.ToString(3);
        AppTitleBar.Subtitle = displayVersion;

        var versionedIcon = Path.Combine(AppContext.BaseDirectory, "Assets", $"AppIcon-{displayVersion}.ico");
        AppWindow.SetIcon(File.Exists(versionedIcon)
            ? versionedIcon
            : Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
        AppWindow.Resize(new SizeInt32(1560, 960));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.Maximize();
        Closed += (_, _) => (RootFrame.Content as MainPage)?.Shutdown();
    }
}
