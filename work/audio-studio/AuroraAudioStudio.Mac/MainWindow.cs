using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Styling;
using AuroraAudioStudio.Core;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Mac;

public sealed partial class MainWindow : Window
{
    private readonly MacWorkspace workspace = new();
    private readonly StudioText text;
    private readonly ContentControl page = new();
    private readonly StackPanel navigation = new() { Spacing = 3 };
    private readonly TextBlock title = new();
    private readonly TextBlock subtitle = new();
    private readonly TextBlock status = new();
    private string current = "home";
    private readonly AppSettings settingsDraft;
    private readonly Grid body = new();
    private Border? workbenchHost;
    private NativeWebView? workbench;
    private bool workbenchLoaded;
    private bool workbenchReady;
    private LocalWorkbenchServer? webServer;
    private string modelSearch = "";
    private readonly DispatcherTimer saveTimer;
    private readonly FileSystemWatcher receiptWatcher;
    private static readonly SolidColorBrush Muted = new(Color.Parse("#5C6D67"));
    private static readonly SolidColorBrush Accent = new(Color.Parse("#267D63"));
    private static readonly SolidColorBrush Line = new(Color.Parse("#D9E6DF"));
    private static readonly SolidColorBrush Surface = new(Color.Parse("#FFFFFF"));
    private static readonly SolidColorBrush Pane = new(Color.Parse("#F0F6F3"));
    private static readonly SolidColorBrush Canvas = new(Color.Parse("#F7FAF8"));
    private static readonly SolidColorBrush Ink = new(Color.Parse("#15231F"));
    private readonly List<Action> languageUpdates = [];

    public MainWindow()
    {
        text = new(workspace);
        settingsDraft = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(workspace.Settings.Current))!;
        Title = "Aurora Audio Studio";
        Background = Canvas; Foreground = Ink;
        PropertyChanged += (_, e) => { if (e.Property == ActualThemeVariantProperty) UpdatePalette(); };
        ApplyTheme();
        Width = 1260; Height = 840; MinWidth = 900; MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("234,*") };
        var sidebar = new DockPanel { Margin = new Thickness(12, 16, 12, 12) };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 11, Margin = new Thickness(14, 8, 0, 22) };
        brand.Children.Add(new Image { Source = new Bitmap(AssetLoader.Open(new Uri("avares://Aurora/Assets/AuroraIcon.png"))), Width = 38, Height = 38, VerticalAlignment = VerticalAlignment.Center });
        var brandText = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        brandText.Children.Add(Txt("Aurora", 20, true));
        brandText.Children.Add(Txt("Audio Studio", 11));
        brand.Children.Add(brandText);
        DockPanel.SetDock(brand, Dock.Top); sidebar.Children.Add(brand);
        sidebar.Children.Add(new ScrollViewer { Content = navigation, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        root.Children.Add(new Border { Background = Pane, BorderBrush = Line, BorderThickness = new Thickness(0, 0, 1, 0), Child = sidebar });
        var main = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetColumn(main, 1); root.Children.Add(main);
        title.FontSize = 28; title.FontWeight = FontWeight.SemiBold;
        subtitle.Foreground = Muted; subtitle.Margin = new Thickness(0, 6, 0, 0);
        var heading = new StackPanel { Spacing = 0, Children = { title, subtitle } };
        var header = new Border { Background = Surface, BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(28, 24), Child = heading };
        main.Children.Add(header);
        Grid.SetRow(body, 1); main.Children.Add(body); body.Children.Add(page);
        var footer = new Border { Background = Surface, BorderBrush = Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(24, 11), Child = status };
        Grid.SetRow(footer, 2); main.Children.Add(footer);
        status.FontSize = 12; status.Foreground = Muted;
        status.TextWrapping = TextWrapping.NoWrap; status.TextTrimming = TextTrimming.CharacterEllipsis;
        Content = root;
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        saveTimer.Tick += (_, _) => { try { workspace.SaveDrafts(); } catch (Exception ex) { status.Text = text["error"] + ": " + ex.Message; } };
        saveTimer.Start();
        Closing += (_, e) =>
        {
            try { workspace.SaveDrafts(); }
            catch (Exception ex) { e.Cancel = true; _ = MessageAsync(text["error"], ex.Message); }
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Meta) && e.Key == Avalonia.Input.Key.S && MacWorkspace.Features.Contains(current) && !IsModelStudio(current))
            { e.Handled = true; _ = SafeAsync(async () => { await workspace.SaveProjectAsync(workspace.Drafts[current]); status.Text = text["saved"]; }); }
        };
        var receipts = Path.Combine(workspace.Settings.AppDataRoot, "WorkbenchReceipts");
        Directory.CreateDirectory(receipts);
        receiptWatcher = new FileSystemWatcher(receipts, "*.json");
        receiptWatcher.Created += (_, _) => Dispatcher.UIThread.Post(() => _ = SafeAsync(ImportWorkbenchResultsAsync));
        receiptWatcher.Renamed += (_, _) => Dispatcher.UIThread.Post(() => _ = SafeAsync(ImportWorkbenchResultsAsync));
        receiptWatcher.EnableRaisingEvents = true;
        Closed += (_, _) => { saveTimer.Stop(); receiptWatcher.Dispose(); workspace.Queue.CancelAll(); modelOperation?.Cancel(); workspace.Runtime.Dispose(); webServer?.Dispose(); utilityPreviewServer?.Dispose(); foreach (var studio in modelStudios.Values) studio.Dispose(); modelFontServer?.Dispose(); };
        workspace.Queue.Changed += (_, _) => Dispatcher.UIThread.Post(() => { if (current is "tasks" or "home") RenderPage(); });
        workspace.Queue.ProgressChanged += (_, task) => Dispatcher.UIThread.Post(() => { status.Text = task.Title + " · " + task.Stage + " · " + task.ProgressDisplay; if (current == "tasks") RenderPage(); });
        ApplyLanguage();
        Opened += async (_, _) => { await SafeAsync(ImportWorkbenchResultsAsync); await SafeAsync(ShowPreviousAppUpdateAsync); await SafeAsync(DailyAppUpdateAsync); if (workspace.Settings.Current.AutoCheckModelUpdates) await SafeAsync(() => ModelBatchAsync("updates")); };
    }

    private void ApplyTheme()
    {
        RequestedThemeVariant = workspace.Settings.Current.Theme switch { "dark" => ThemeVariant.Dark, "system" => ThemeVariant.Default, _ => ThemeVariant.Light };
        UpdatePalette();
    }

    private void UpdatePalette()
    {
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        Canvas.Color = Color.Parse(dark ? "#111A18" : "#F7FAF8");
        Pane.Color = Color.Parse(dark ? "#16221F" : "#F0F6F3");
        Surface.Color = Color.Parse(dark ? "#1B2824" : "#FFFFFF");
        Accent.Color = Color.Parse(dark ? "#57C7A4" : "#267D63");
        Ink.Color = Color.Parse(dark ? "#EFF8F4" : "#15231F");
        Muted.Color = Color.Parse(dark ? "#B0C1BA" : "#5C6D67");
        Line.Color = Color.Parse(dark ? "#32443E" : "#D9E6DF");
    }

    private void ApplyLanguage()
    {
        FontFamily = workspace.Settings.EffectiveLanguage() switch
        {
            // WKWebView supports the bundled variable Noto font; native text uses
            // the system fallback to avoid rendering its lightest variable face.
            "ja-JP" => FontFamily.Default,
            "zh-TW" => new FontFamily("PingFang TC"), "zh-CN" => new FontFamily("PingFang SC"), _ => FontFamily.Default
        };
        navigation.Children.Clear();
        foreach (var key in new[] { "home", "tasks", "results", "music", "voice", "singing", "separation", "transcription", "subtitles", "models", "maintenance", "settings", "about" })
        {
            if (key is "music" or "separation" or "models" or "settings") navigation.Children.Add(new Border { Height = 1, Background = Line, Margin = new Thickness(10, 9) });
            var button = ActionButton(text[key], () => { Navigate(key); return Task.CompletedTask; }, "nav-" + key);
            button.Classes.Add("nav");
            if (key == current) button.Classes.Add("selected");
            navigation.Children.Add(button);
        }
        RenderPage();
        foreach (var update in languageUpdates) update();
    }

    private void Navigate(string key)
    {
        if (current != key && modelStudios.TryGetValue(current, out var studio)) studio.Startup?.Cancel();
        current = key;
        ApplyLanguage();
    }

    private void RenderPage()
    {
        title.Text = current == "workbench" ? text["workbenchCheck"] : text[current];
        subtitle.Text = IsModelStudio(current) ? L("从灵感到成品，一站完成生成、编辑与导出。") : MacWorkspace.Features.Contains(current) ? text[current + "Desc"]
            : current == "workbench" ? text["checkHint"] : current == "home" ? text["homeSubtitle"] : text[current + "Subtitle"];
        status.Text = text["preview"] + "  ·  Apple Silicon  ·  " + CurrentMacVersion;
        utilityPreviewServer?.Dispose(); utilityPreviewServer = null;
        foreach (var pair in modelStudios) pair.Value.Root.IsVisible = current == pair.Key;
        if (workbenchHost is not null) workbenchHost.IsVisible = current == "workbench";
        page.IsVisible = current != "workbench" && !IsModelStudio(current);
        if (current == "workbench") { ShowWorkbench(); return; }
        if (IsModelStudio(current)) { ShowModelStudio(current); return; }
        page.Content = MacWorkspace.Features.Contains(current) ? UtilityPage(current) : current switch
        {
            "home" => HomePage(), "settings" => SettingsPage(), "models" => ModelsPage(),
            "tasks" => TasksPage(), "results" => ResultsPage(), "maintenance" => MaintenancePage(), _ => AboutPage()
        };
    }

    private Control HomePage()
    {
        var content = VStack();
        var welcomeTitle = Txt(text["welcome"], 30, true); welcomeTitle.Foreground = Brushes.White;
        var welcomeBody = Txt(L("六个功能互相独立，不需要先新建项目。选择功能后，再按提示添加素材、模型和输出位置。"), 15); welcomeBody.Foreground = Brush.Parse("#E7FFF6");
        content.Children.Add(new Border { Background = Brush.Parse("#165F4D"), CornerRadius = new CornerRadius(14), Padding = new Thickness(26), Child = VStack(welcomeTitle, welcomeBody) });
        var features = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 12, RowSpacing = 12 };
        string[] titles = ["创作音乐", "制作配音", "克隆歌声", "拆分混音", "音频转 MIDI", "生成字幕"];
        string[] hints = ["文字到完整歌曲", "设计与克隆音色", "转换与克隆演唱音色", "人声与乐器分轨", "识别演奏与旋律", "视频语音到时间轴"];
        for (var i = 0; i < MacWorkspace.Features.Length; i++)
        {
            var key = MacWorkspace.Features[i];
            var label = VStack(Txt(L(titles[i]), 14, true), Txt(L(hints[i]), 12)); label.Spacing = 2;
            var button = ActionButton("", () => { Navigate(key); return Task.CompletedTask; }, "feature-" + key);
            AutomationProperties.SetName(button, text[key]);
            var buttonContent = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
            buttonContent.Children.Add(FeatureIcon(key)); Grid.SetColumn(label, 1); buttonContent.Children.Add(label);
            button.Content = buttonContent; button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Padding = new Thickness(18, 14); button.Background = Surface;
            Grid.SetRow(button, i / 3); Grid.SetColumn(button, i % 3); features.Children.Add(button);
        }
        features.SizeChanged += (_, e) =>
        {
            var columns = e.NewSize.Width >= 780 ? 3 : e.NewSize.Width >= 520 ? 2 : 1;
            if (features.ColumnDefinitions.Count == columns) return;
            features.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", columns)));
            features.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", 6 / columns)));
            for (var i = 0; i < features.Children.Count; i++) { Grid.SetRow(features.Children[i], i / columns); Grid.SetColumn(features.Children[i], i % columns); }
        };
        content.Children.Add(features);
        var guide = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 18, RowSpacing = 12 };
        var guideText = VStack(Txt(L("第一次使用？直接选择功能"), 17, true), Txt(L("不需要新建项目。进入任一功能后，Aurora 会根据当前任务提示所需的素材、模型和输出位置。")));
        guideText.Spacing = 4; guideText.VerticalAlignment = VerticalAlignment.Center; guide.Children.Add(guideText);
        var subtitles = ActionButton(L("生成字幕"), () => { Navigate("subtitles"); return Task.CompletedTask; }); subtitles.Classes.Add("primary");
        var guideActions = Row(ActionButton(L("创作音乐"), () => { Navigate("music"); return Task.CompletedTask; }), ActionButton(L("制作配音"), () => { Navigate("voice"); return Task.CompletedTask; }), subtitles);
        guideActions.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(guideActions, 1); guide.Children.Add(guideActions);
        guide.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 780;
            guide.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : GridLength.Auto;
            Grid.SetColumnSpan(guideText, narrow ? 2 : 1); Grid.SetColumn(guideActions, narrow ? 0 : 1); Grid.SetRow(guideActions, narrow ? 1 : 0); Grid.SetColumnSpan(guideActions, narrow ? 2 : 1);
        };
        var guidePanel = Panel(guide); guidePanel.Background = Pane; guidePanel.Padding = new Thickness(20); content.Children.Add(guidePanel);
        var recentContent = VStack();
        var recentHeading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, Margin = new Thickness(0, 8, 0, 0) };
        var recentTitle = Txt(L("最近处理记录"), 20, true); recentTitle.VerticalAlignment = VerticalAlignment.Center;
        var openProject = ActionButton(text["openProject"], OpenProjectAsync, "open-project"); openProject.VerticalAlignment = VerticalAlignment.Center;
        recentHeading.Children.Add(recentTitle); Grid.SetColumn(openProject, 1); recentHeading.Children.Add(openProject);
        recentContent.Children.Add(recentHeading);
        var recent = workspace.Projects.Recent(8);
        if (recent.Count == 0) recentContent.Children.Add(Txt(L("完成分轨、扒谱或字幕任务后，处理记录会出现在这里。")));
        foreach (var project in recent)
        {
            var button = ActionButton(L("再次处理"),
                () => { workspace.LoadProject(project); Navigate(project.Feature); return Task.CompletedTask; }, "project-" + project.Id);
            var source = Txt(project.SourcePath, 12); source.TextWrapping = TextWrapping.NoWrap; source.TextTrimming = TextTrimming.CharacterEllipsis; ToolTip.SetTip(source, project.SourcePath);
            var details = VStack(Txt(project.Name, 14, true), source); details.Spacing = 4;
            var metadata = VStack(Txt(text[project.Feature], 14), Txt(project.UpdatedDisplay, 12)); metadata.Spacing = 4;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,150,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 14, RowSpacing = 6 };
            row.Children.Add(details); Grid.SetColumn(metadata, 1); row.Children.Add(metadata);
            button.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(button, 2); row.Children.Add(button);
            row.SizeChanged += (_, e) =>
            {
                var narrow = e.NewSize.Width < 500;
                row.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : new GridLength(150);
                Grid.SetColumnSpan(details, narrow ? 2 : 1); Grid.SetColumn(metadata, narrow ? 0 : 1); Grid.SetRow(metadata, narrow ? 1 : 0); Grid.SetColumnSpan(metadata, narrow ? 2 : 1);
            };
            recentContent.Children.Add(new Border { Padding = new Thickness(14, 12), Child = row });
        }
        var active = workspace.Queue.Items.Where(t => t.Status is "waiting" or "preparing" or "running").ToArray();
        var activity = VStack(Txt(L("正在进行"), 20, true), Txt(active.Length == 0 ? L("当前没有排队任务") : workspace.Localization.Format("activeTaskCount", active.Length)));
        foreach (var task in active.Take(4)) activity.Children.Add(VStack(Txt(task.Title, 14, true), Txt(L(task.Stage), 12)));
        activity.Children.Add(ActionButton(L("查看全部任务"), () => { Navigate("tasks"); return Task.CompletedTask; }, "home-all-tasks"));
        var activityPanel = Panel(activity); activityPanel.Background = Pane; activityPanel.Padding = new Thickness(22); activityPanel.VerticalAlignment = VerticalAlignment.Top;
        var records = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 28, RowSpacing = 18 };
        records.Children.Add(recentContent); Grid.SetColumn(activityPanel, 1); records.Children.Add(activityPanel);
        records.SizeChanged += (_, e) =>
        {
            var stacked = e.NewSize.Width < 780;
            records.ColumnDefinitions[1].Width = stacked ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
            Grid.SetColumn(activityPanel, stacked ? 0 : 1); Grid.SetRow(activityPanel, stacked ? 1 : 0);
        };
        content.Children.Add(records);
        return Scroll(content);
    }

    private Control SettingsPage()
    {
        var content = VStack(Txt(text["language"], 20, true));
        string[] languages = ["auto", "zh-CN", "zh-TW", "en-US", "ja-JP"];
        var select = new ComboBox { ItemsSource = new[] { "System / 跟随系统", "简体中文", "繁體中文", "English", "日本語" }, SelectedIndex = Array.IndexOf(languages, workspace.Settings.Current.Language), Width = 280 };
        AutomationProperties.SetAutomationId(select, "language-select");
        select.SelectionChanged += (_, _) =>
        {
            if (select.SelectedIndex < 0) return;
            if (!workspace.Settings.TrySetLanguage(languages[select.SelectedIndex], out var error)) { _ = MessageAsync(text["error"], error); return; }
            ApplyLanguage();
        };
        content.Children.Add(select); content.Children.Add(Txt(text["languageImmediateHint"], 13)); content.Children.Add(Rule());
        var themeKeys = new[] { "light", "dark", "system" };
        var theme = new ComboBox { Width = 280, HorizontalAlignment = HorizontalAlignment.Left, ItemsSource = new[] { L("浅色"), L("深色"), L("跟随系统") }, SelectedIndex = Math.Max(0, Array.IndexOf(themeKeys, settingsDraft.Theme)) };
        AutomationProperties.SetAutomationId(theme, "theme-select");
        theme.SelectionChanged += (_, _) => { if (theme.SelectedIndex >= 0) settingsDraft.Theme = themeKeys[theme.SelectedIndex]; };
        content.Children.Add(Field(L("主题"), theme));
        var confirmLarge = new CheckBox { Content = L("下载大型模型前始终确认"), IsChecked = settingsDraft.ConfirmLargeModelDownloads };
        var autoRelease = new CheckBox { Content = L("任务完成后释放显存"), IsChecked = settingsDraft.AutoReleaseVram };
        confirmLarge.IsCheckedChanged += (_, _) => settingsDraft.ConfirmLargeModelDownloads = confirmLarge.IsChecked == true;
        autoRelease.IsCheckedChanged += (_, _) => settingsDraft.AutoReleaseVram = autoRelease.IsChecked == true;
        content.Children.Add(confirmLarge); content.Children.Add(autoRelease);
        var autoModels = new CheckBox { Content = L("启动时检查模型更新"), IsChecked = settingsDraft.AutoCheckModelUpdates };
        autoModels.IsCheckedChanged += (_, _) => settingsDraft.AutoCheckModelUpdates = autoModels.IsChecked == true;
        content.Children.Add(autoModels);
        var autoApp = new CheckBox { Content = L("启动时检查程序更新"), IsChecked = settingsDraft.AutoCheckAppUpdates };
        autoApp.IsCheckedChanged += (_, _) => settingsDraft.AutoCheckAppUpdates = autoApp.IsChecked == true;
        content.Children.Add(autoApp);
        var channel = new ComboBox { ItemsSource = new[] { L("updateStable"), L("updateBeta") }, SelectedIndex = settingsDraft.AppUpdateChannel == "beta" ? 1 : 0, MinWidth = 240, HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetAutomationId(channel, "app-update-channel");
        channel.SelectionChanged += (_, _) => { if (channel.SelectedIndex >= 0) settingsDraft.AppUpdateChannel = channel.SelectedIndex == 1 ? "beta" : "stable"; };
        content.Children.Add(Field(L("updateChannel"), channel));
        content.Children.Add(Txt(L("updateChannelHint"), 13));
        content.Children.Add(Row(ActionButton(L("检查程序更新"), () => CheckAppUpdateAsync(true)), ActionButton(L("取消更新"), () => { appUpdateCancellation?.Cancel(); return Task.CompletedTask; })));
        var safe = new CheckBox { Content = L("安全模式"), IsChecked = settingsDraft.SafeMode };
        var history = new NumericUpDown { Minimum = 20, Maximum = 500, Increment = 20, Value = settingsDraft.TaskHistoryLimit, Width = 180, HorizontalAlignment = HorizontalAlignment.Left };
        safe.IsCheckedChanged += (_, _) => settingsDraft.SafeMode = safe.IsChecked == true;
        history.ValueChanged += (_, _) => settingsDraft.TaskHistoryLimit = (int)(history.Value ?? 100);
        content.Children.Add(safe);
        content.Children.Add(Txt(L("安全模式下可查看项目、导出成品和校验模型，不能启动推理或修改模型。"), 12));
        content.Children.Add(Field(L("保留的任务历史数量"), history));
        content.Children.Add(Txt(L("音乐与配音工作台可独立保留；歌声克隆会释放二者。退出应用时释放全部模型。"), 12));
        content.Children.Add(Rule());
        content.Children.Add(Txt(text["storage"], 20, true));
        var labels = new[] { "modelFolder", "outputFolder", "projectsFolder" };
        for (var i = 0; i < 3; i++)
        {
            var index = i;
            var value = new[] { settingsDraft.LocalAiRoot, settingsDraft.OutputRoot, settingsDraft.ProjectsRoot }[i];
            var input = Input(value, value => { if (index == 0) settingsDraft.LocalAiRoot = value; else if (index == 1) settingsDraft.OutputRoot = value; else settingsDraft.ProjectsRoot = value; }, "settings-path-" + i); input.VerticalAlignment = VerticalAlignment.Center;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
            row.Children.Add(input);
            var choose = ActionButton(text["chooseFolder"], async () =>
            {
                var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = text["chooseFolder"], AllowMultiple = false });
                if (picked.FirstOrDefault()?.TryGetLocalPath() is { } path) input.Text = path;
            }); choose.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(choose, 1); row.Children.Add(choose);
            content.Children.Add(Field(text[labels[i]], row));
        }
        content.Children.Add(Txt(text["storageHint"], 13));
        var save = ActionButton(L("保存设置"), () =>
        {
            if (modelOperation is not null || workspace.Queue.Items.Any(t => t.Status is "running" or "preparing" or "waiting"))
                throw new InvalidOperationException("请先完成或取消当前任务，再修改运行设置。");
            ReleaseOtherStudios();
            var candidate = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settingsDraft))!;
            candidate.Language = workspace.Settings.Current.Language;
            candidate.LastAppUpdateCheckDate = candidate.AppUpdateChannel == workspace.Settings.Current.AppUpdateChannel ? workspace.Settings.Current.LastAppUpdateCheckDate : null;
            if (!workspace.Settings.TrySave(candidate, out var error)) throw new IOException(error);
            settingsDraft.LocalAiRoot = candidate.LocalAiRoot; settingsDraft.OutputRoot = candidate.OutputRoot; settingsDraft.ProjectsRoot = candidate.ProjectsRoot;
            ApplyTheme(); status.Text = text["settingsSaved"]; return Task.CompletedTask;
        }, "save-settings"); save.Classes.Add("primary"); content.Children.Add(save);
        return Scroll(Panel(content));
    }

    private Control SourcePicker(string label, string path, Action<string> set, string id)
    {
        var stack = VStack();
        stack.Children.Add(Txt(label, 14, true));
        stack.Children.Add(Txt(string.IsNullOrEmpty(path) ? text["emptySource"] : path, 12));
        if (!string.IsNullOrEmpty(path) && !File.Exists(path)) stack.Children.Add(Txt(text["missingSource"], 12));
        stack.Children.Add(ActionButton(text["choose"], async () =>
        {
            var file = await PickAsync([new FilePickerFileType("Audio / Video") { Patterns = ["*.wav", "*.mp3", "*.m4a", "*.flac", "*.aac", "*.ogg", "*.mp4", "*.mov", "*.mkv"], AppleUniformTypeIdentifiers = ["public.audio", "public.movie"] }, FilePickerFileTypes.All]);
            if (file is not null) set(file);
        }, id));
        return stack;
    }

    private async Task OpenProjectAsync()
    {
        var path = await PickAsync([new FilePickerFileType("Aurora project") { Patterns = ["*.arr", "*.aurora"], AppleUniformTypeIdentifiers = ["public.data"] }]);
        if (path is null) return;
        var draft = await workspace.ImportProjectAsync(path);
        Navigate(draft.Feature);
    }

    private async Task<string?> PickAsync(IReadOnlyList<FilePickerFileType> types)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = text["choose"], AllowMultiple = false, FileTypeFilter = types });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private static Task OpenPathAsync(string path)
    {
        if (!Path.IsPathFullyQualified(path) || (!File.Exists(path) && !Directory.Exists(path))) throw new FileNotFoundException("File or folder is unavailable.", path);
        var info = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
        info.ArgumentList.Add(path); Process.Start(info)?.Dispose();
        return Task.CompletedTask;
    }

    private async Task SafeAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { await MessageAsync(text["error"], workspace.Localization.Translate(ex.Message)); }
    }

    private Button ActionButton(string label, Func<Task> action, string? id = null)
    {
        var button = new Button { Content = label };
        if (id is not null) AutomationProperties.SetAutomationId(button, id);
        button.Click += async (_, _) => { button.IsEnabled = false; try { await SafeAsync(action); } finally { button.IsEnabled = true; } };
        return button;
    }

    private async Task MessageAsync(string heading, string message)
    {
        var dialog = new Window { Title = heading, Width = 520, Height = 280, MinWidth = 380, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var close = new Button { Content = text["ok"], HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new ScrollViewer { Content = new StackPanel { Margin = new Thickness(24), Spacing = 20, Children = { Txt(heading, 20, true), Txt(message), close } } };
        await dialog.ShowDialog(this);
    }

    private static string CssColor(SolidColorBrush brush) => $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";

    private static TextBlock Txt(string value, double size = 14, bool strong = false) => new() { Text = value, FontSize = size, FontWeight = strong ? FontWeight.SemiBold : FontWeight.Normal, Foreground = strong ? Ink : Muted, TextWrapping = TextWrapping.Wrap };
    private static StackPanel VStack(params Control[] items) { var panel = new StackPanel { Spacing = 12 }; foreach (var item in items) panel.Children.Add(item); return panel; }
    private static WrapPanel Row(params Control[] items) { var panel = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 10, LineSpacing = 8 }; foreach (var item in items) { item.VerticalAlignment = VerticalAlignment.Center; panel.Children.Add(item); } return panel; }
    private static Border Panel(Control child, string color = "#FFFFFF") => new() { Child = child, Padding = new Thickness(24), CornerRadius = new CornerRadius(12), Background = color == "#EFF7F3" ? Pane : Surface, BorderBrush = Line, BorderThickness = new Thickness(1) };
    private static Border Rule() => new() { Height = 1, Background = Line, Margin = new Thickness(0, 7) };
    private static Control Field(string label, Control child) { var field = VStack(Txt(label, 14, true), child); field.Spacing = 6; return field; }
    private static ScrollViewer Scroll(Control content) => new() { Content = content, Padding = new Thickness(28, 24), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    private static TextBox Input(string value, Action<string> set, string id)
    {
        var input = new TextBox { Text = value, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetAutomationId(input, id);
        input.TextChanged += (_, _) => set(input.Text ?? "");
        return input;
    }
}
