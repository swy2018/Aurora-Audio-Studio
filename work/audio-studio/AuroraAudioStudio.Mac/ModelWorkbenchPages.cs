using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using AuroraAudioStudio.Core;
using AuroraAudioStudio.Services;

namespace AuroraAudioStudio.Mac;

public sealed partial class MainWindow
{
    private sealed class ModelStudio : IDisposable
    {
        public required Grid Root { get; init; }
        public Action Update { get; set; } = () => { };
        public Action Reset { get; set; } = () => { };
        public NativeWebView? View;
        public ModelWorkbenchConnection? Connection;
        public CancellationTokenSource? Startup;
        public string? ConnectedModel;
        public void Dispose() { Startup?.Cancel(); Connection?.Dispose(); Connection = null; }
    }

    private readonly Dictionary<string, ModelStudio> modelStudios = [];
    private LocalWorkbenchServer? modelFontServer;
    private Uri? modelFontUri;
    private static bool IsModelStudio(string feature) => feature is "music" or "voice" or "singing";
    private string L(string value) => StudioText.Extra.ContainsKey(value) ? text[value] : workspace.Localization.Translate(value);

    private void ShowModelStudio(string feature)
    {
        if (modelStudios.TryGetValue(feature, out var existing)) { existing.Root.IsVisible = true; existing.Update(); return; }
        var models = workspace.Catalog.Definitions.Where(m => m.Feature == feature && m.IsRunnable && workspace.Runtime.Models.ContainsKey(m.Id)).ToArray();
        var root = new Grid { Margin = new Thickness(24, 20), ColumnDefinitions = new ColumnDefinitions("*,280") };
        root.Children.Add(new Border { Background = Surface, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14) });
        Grid.SetColumnSpan(root.Children[0], 2);
        var left = new Grid { Margin = new Thickness(20), RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        root.Children.Add(left);
        var picker = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 0 };
        AutomationProperties.SetAutomationId(picker, "studio-model-" + feature);
        var open = new Button(); open.Classes.Add("primary");
        AutomationProperties.SetAutomationId(open, "open-model-workbench");
        var install = ActionButton(L("模型管理"), () => { Navigate("models"); return Task.CompletedTask; });
        AutomationProperties.SetAutomationId(install, "install-model-deferred");
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 10, RowSpacing = 8 };
        picker.VerticalAlignment = VerticalAlignment.Center; actions.Children.Add(picker);
        var commands = Row(open, install); Grid.SetColumn(commands, 1); actions.Children.Add(commands);
        left.Children.Add(actions);
        var stage = new Grid { Margin = new Thickness(0, 14, 0, 10) }; Grid.SetRow(stage, 1); left.Children.Add(stage);
        var illustration = Txt("♫", 44, true); illustration.HorizontalAlignment = HorizontalAlignment.Center; illustration.Foreground = Accent;
        var emptyTitle = Txt("", 22, true); emptyTitle.TextAlignment = TextAlignment.Center;
        var emptyBody = Txt(""); emptyBody.TextAlignment = TextAlignment.Center;
        var emptyNote = Txt("", 12); emptyNote.TextAlignment = TextAlignment.Center;
        var empty = VStack(illustration, emptyTitle, emptyBody, emptyNote);
        empty.MaxWidth = 460; empty.VerticalAlignment = VerticalAlignment.Center;
        var emptyScroll = new ScrollViewer { Content = empty }; stage.Children.Add(emptyScroll);
        var progressText = Txt(""); progressText.TextAlignment = TextAlignment.Center;
        var cancel = new Button { HorizontalAlignment = HorizontalAlignment.Center };
        var progress = VStack(new ProgressBar { IsIndeterminate = true, Width = 200 }, progressText, cancel);
        progress.IsVisible = false; progress.VerticalAlignment = VerticalAlignment.Center; stage.Children.Add(progress);
        var info = Txt("", 13); info.IsVisible = false;
        var infoPanel = new Border { Child = info, Padding = new Thickness(14), CornerRadius = new CornerRadius(8), Background = Pane, IsVisible = false };
        Grid.SetRow(infoPanel, 2); left.Children.Add(infoPanel);
        var engineLabel = Txt("", 16, true); var modelName = Txt("", 14, true); var modelState = Txt("");
        var locationLabel = Txt("", 16, true); var outputPath = Txt("", 12);
        var results = ActionButton("", () => { Navigate("results"); return Task.CompletedTask; });
        var stateLabel = Txt("", 16, true); var runningState = Txt("");
        var release = new Button();
        var releaseTop = new Button();
        releaseTop.VerticalAlignment = VerticalAlignment.Center; commands.Children.Add(releaseTop);
        AutomationProperties.SetAutomationId(release, "release-model-workbench");
        var side = new Border { Margin = new Thickness(0, 1, 1, 1), Padding = new Thickness(22), Background = Pane, CornerRadius = new CornerRadius(0, 14, 14, 0),
            Child = new ScrollViewer { Content = VStack(engineLabel, modelName, modelState, Rule(), locationLabel, outputPath, results, Rule(), stateLabel, runningState, release) } };
        Grid.SetColumn(side, 1); root.Children.Add(side);
        var state = new ModelStudio { Root = root };
        modelStudios.Add(feature, state); body.Children.Add(root);
        bool updating = false;
        string infoKey = "";
        state.Update = Update;
        state.Reset = Reset;
        release.Click += (_, _) => { state.Dispose(); Reset(); };
        releaseTop.Click += (_, _) => { state.Dispose(); Reset(); };
        void Reset()
        {
            state.View?.SetValue(IsVisibleProperty, false);
            if (state.View is not null) { stage.Children.Remove(state.View); state.View = null; }
            state.ConnectedModel = null; emptyScroll.IsVisible = true; progress.IsVisible = false;
            Update();
        }
        void Update()
        {
            updating = true;
            picker.ItemsSource = models.Select(workspace.Catalog.DisplayName).ToArray();
            picker.SelectedIndex = Math.Max(0, Array.FindIndex(models, m => m.Id == workspace.Drafts[feature].ModelId));
            updating = false;
            open.Content = L("进入工作台"); install.Content = L("模型管理");
            cancel.Content = L("取消启动"); progressText.Text = L("正在启动本地引擎，首次加载可能需要几分钟…");
            emptyTitle.Text = L("开启你的下一次创作");
            emptyBody.Text = L("选择创作引擎，即刻在 Aurora 中开始。所有处理均在本机完成。");
            emptyNote.Text = text["originalWorkbenchHint"];
            engineLabel.Text = L("创作引擎"); locationLabel.Text = L("保存位置"); stateLabel.Text = L("运行状态");
            outputPath.Text = workspace.Settings.Current.OutputRoot; results.Content = L("查看我的成品"); release.Content = text["release"];
            var selected = models[picker.SelectedIndex];
            modelName.Text = workspace.Catalog.DisplayName(selected);
            modelState.Text = workspace.Workbenches.IsAvailable(selected.Id) ? text["workbenchAvailable"]
                : Directory.Exists(MacWorkspace.ModelPath(workspace.Settings.Current.LocalAiRoot, selected)) ? text["pendingMac"] : text["notInstalled"];
            runningState.Text = state.Connection is not null ? text["workbenchConnected"] : state.Startup is not null ? text["workbenchStarting"] : text["idle"];
            release.IsEnabled = state.Connection is not null;
            releaseTop.Content = release.Content; releaseTop.IsEnabled = release.IsEnabled;
            picker.IsEnabled = state.Startup is null; open.IsEnabled = state.Startup is null;
            if (infoKey.Length > 0) { info.Text = text[infoKey]; info.IsVisible = true; infoPanel.IsVisible = true; }
            if (state.View is not null && state.Connection is not null) _ = SafeAsync(() => LocalizeModelWorkbenchAsync(state.View));
        }
        picker.SelectionChanged += (_, _) =>
        {
            if (updating || picker.SelectedIndex < 0) return;
            var chosen = models[picker.SelectedIndex].Id;
            if (workspace.Drafts[feature].ModelId == chosen) return;
            workspace.Drafts[feature].ModelId = chosen;
            infoKey = ""; infoPanel.IsVisible = false; Update();
        };
        cancel.Click += (_, _) => state.Startup?.Cancel();
        open.Click += async (_, _) =>
        {
            if (state.Startup is not null) return;
            if (modelOperation is not null || workspace.Queue.Items.Any(t => t.Status is "running" or "preparing" or "waiting"))
            { await MessageAsync(text["error"], "请先等待当前处理或模型维护完成，再启动创作工作台。"); return; }
            var id = workspace.Drafts[feature].ModelId;
            if (!workspace.Workbenches.IsAvailable(id))
            { infoKey = "workbenchMissing"; Update(); return; }
            if (state.Connection is not null && state.ConnectedModel == id) return;
            ReleaseOtherStudios(feature);
            state.Dispose(); Reset();
            using var startup = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            state.Startup = startup; infoKey = ""; infoPanel.IsVisible = false;
            emptyScroll.IsVisible = false; progress.IsVisible = true; Update();
            ModelWorkbenchConnection? connection = null;
            try
            {
                connection = await workspace.Workbenches.StartAsync(id, workspace.Settings.EffectiveLanguage(), startup.Token);
                startup.Token.ThrowIfCancellationRequested();
                state.Connection = connection;
                var view = new StudioWebView { IsVisible = true };
                state.View = view; stage.Children.Add(view);
                var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                view.NavigationStarted += (_, e) => e.Cancel = !connection.Owns(e.Request);
                view.NewWindowRequested += (_, e) => e.Handled = true;
                view.NavigationCompleted += (_, e) => { if (e.IsSuccess) ready.TrySetResult(); else ready.TrySetException(new IOException(text["webFailure"])); };
                view.Source = connection.Uri;
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(30), startup.Token);
                await LocalizeModelWorkbenchAsync(view);
                // WKWebView can be created after its NativeControlHost was arranged.
                // Re-arrange after navigation so it receives the final content bounds.
                view.InvalidateMeasure(); view.InvalidateArrange(); stage.UpdateLayout();
                state.ConnectedModel = id;
            }
            catch (Exception ex)
            {
                connection?.Dispose(); state.Connection = null;
                infoKey = ex is OperationCanceledException ? "workbenchCanceled" : "webFailure";
                Reset();
                if (ex is not OperationCanceledException) await MessageAsync(text["error"], ex.Message);
            }
            finally { state.Startup = null; progress.IsVisible = false; Update(); }
        };
        root.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 1050;
            side.IsVisible = !narrow; root.ColumnDefinitions[1].Width = new GridLength(narrow ? 0 : 280);
            illustration.IsVisible = e.NewSize.Height >= 330;
        };
        left.SizeChanged += (_, e) =>
        {
            var stacked = e.NewSize.Width < 680;
            actions.ColumnDefinitions[1].Width = stacked ? new GridLength(0) : GridLength.Auto;
            Grid.SetColumnSpan(picker, stacked ? 2 : 1);
            Grid.SetColumn(commands, stacked ? 0 : 1); Grid.SetRow(commands, stacked ? 1 : 0); Grid.SetColumnSpan(commands, stacked ? 2 : 1);
        };
        Update();
    }

    private Task LocalizeModelWorkbenchAsync(NativeWebView view)
    {
        if (modelFontUri is null)
        {
            modelFontServer = new LocalWorkbenchServer();
            using var font = AssetLoader.Open(new Uri("avares://Aurora/Assets/Fonts/NotoSansJP.ttf"));
            using var bytes = new MemoryStream(); font.CopyTo(bytes);
            modelFontUri = modelFontServer.AddResource(bytes.ToArray(), "font/ttf");
        }
        var script = WorkbenchLocalization.Script(workspace.Localization, workspace.Settings.EffectiveLanguage())
            .Replace("https://aurora-fonts.local/NotoSansJP.ttf", modelFontUri.AbsoluteUri, StringComparison.Ordinal);
        return view.InvokeScript(script);
    }
}
