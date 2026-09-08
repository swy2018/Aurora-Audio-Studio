using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AuroraAudioStudio.Mac;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        Name = "Aurora Audio Studio";
        AvaloniaXamlLoader.Load(this);
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            if (Program.Instance is { } instance) instance.ActivationRequested += () => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                window.WindowState = Avalonia.Controls.WindowState.Normal;
                window.Show(); window.Activate();
            });
        }
        base.OnFrameworkInitializationCompleted();
    }
}
