using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Reflection;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;

namespace AuroraAudioStudio;

public sealed partial class MainPage : Page
{
    private readonly SettingsService settings = new();
    private readonly LocalizationService localization;
    private readonly ModelCatalogService catalog;
    private readonly BackendService backend;
    private readonly UpdateService updater;
    private readonly ModelUpdateService modelUpdater;
    private readonly ProjectService projects;
    private readonly TaskQueueService taskQueue;
    private readonly MaintenanceService maintenance;
    private readonly ObservableCollection<string> utilityLogs = [];
    private readonly UpdateFlowGuard updateFlow = new();
    private readonly SemaphoreSlim dialogGate = new(1, 1);
    private string feature = "music";

    public MainPage()
    {
        InitializeComponent();
        localization = new(settings);
        catalog = new(settings);
        backend = new(settings);
        updater = new(settings, localization);
        modelUpdater = new(catalog, settings);
        projects = new(settings);
        taskQueue = new(settings);
        maintenance = new(settings, catalog);
        ModelPicker.SelectionChanged += ModelPicker_SelectionChanged;
        UtilityLogList.ItemsSource = utilityLogs;
        backend.StatusChanged += (_, value) => DispatcherQueue.TryEnqueue(() => SetStatus(FormatBackendStatus(value)));
        taskQueue.Changed += (_, _) => DispatcherQueue.TryEnqueue(RefreshWorkspace);
        Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplySettingsToControls();
        ApplyLocalization();
        Shell.SelectedItem = HomeItem;
        RefreshModels();
        RefreshWorkspace();
        RunHealthScan();
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (settings.Current.AutoCheckAppUpdates && UpdateFlowGuard.ShouldRunDailyCheck(settings.Current.LastAppUpdateCheckDate, today))
        {
            settings.Current.LastAppUpdateCheckDate = UpdateFlowGuard.DateKey(today);
            settings.Save(settings.Current);
            await RunAppUpdateFlowAsync(false);
        }
        if (settings.Current.AutoCheckModelUpdates) _ = CheckModelsSilentlyAsync();
    }

    private void Shell_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag) return;
        feature = tag;
        if (tag == "home") { ShowOnly(HomeView); PageTitle.Text = localization.Get("home"); PageSubtitle.Text = localization.Get("homeSubtitle"); RefreshWorkspace(); return; }
        if (tag == "tasks") { ShowOnly(TasksView); PageTitle.Text = localization.Get("tasks"); PageSubtitle.Text = localization.Get("tasksSubtitle"); RefreshWorkspace(); return; }
        if (tag == "settings") { ShowOnly(SettingsView); PageTitle.Text = localization.Get("settings"); PageSubtitle.Text = localization.Get("settingsSubtitle"); return; }
        if (tag == "about") { ShowOnly(AboutView); PageTitle.Text = localization.Get("about"); PageSubtitle.Text = localization.Get("aboutSubtitle"); return; }
        if (tag == "models") { ShowOnly(ModelsView); PageTitle.Text = localization.Get("models"); PageSubtitle.Text = localization.Get("modelsSubtitle"); RefreshModels(); return; }
        if (tag == "maintenance") { ShowOnly(MaintenanceView); PageTitle.Text = localization.Get("maintenance"); PageSubtitle.Text = localization.Get("maintenanceSubtitle"); RunHealthScan(); return; }
        if (tag is "separation" or "transcription" or "subtitles") { ConfigureUtility(tag); ShowOnly(UtilityView); return; }
        ConfigureStudio(tag); ShowOnly(StudioView);
    }

    private void ConfigureStudio(string tag)
    {
        PageTitle.Text = localization.Get(tag);
        PageSubtitle.Text = "从灵感到成品，一站完成生成、编辑与导出。";
        ModelPicker.Items.Clear();
        var options = catalog.Definitions.Where(x => x.Feature == tag).ToList();
        foreach (var model in options) ModelPicker.Items.Add(new ComboBoxItem { Content = model.Name, Tag = model.Id });
        if (ModelPicker.Items.Count > 0) ModelPicker.SelectedIndex = 0;
        CurrentModelName.Text = options.FirstOrDefault()?.Name ?? "—";
        CurrentModelState.Text = options.FirstOrDefault() is { } value && catalog.IsInstalled(value) ? "已就绪" : "等待安装";
        UpdateSelectedModelState();
        Workbench.Visibility = Visibility.Collapsed; StudioEmpty.Visibility = Visibility.Visible;
    }

    private void ConfigureUtility(string tag)
    {
        PageTitle.Text = localization.Get(tag);
        var copy = tag switch
        {
            "separation" => ("从混音中分离人声、鼓、贝斯等独立音轨。", "新建分轨任务", "选择音频或视频素材，Aurora 会在本机生成六轨分离结果。", "支持 WAV、FLAC、MP3、M4A、MP4 等常见格式", "输出为可继续混音的独立 WAV 音轨。", "\uE9E9"),
            "transcription" => ("将演奏音频转换为可继续编辑的 MIDI 乐谱。", "新建扒谱任务", "选择演奏录音并匹配识别引擎，生成可继续编曲的 MIDI 文件。", "建议使用人声较少、乐器清晰的音频素材", "处理结果会保存为标准 MIDI 文件。", "\uE70F"),
            _ => ("从视频中识别语音，生成带时间轴的字幕文件。", "新建字幕任务", "选择视频素材，Aurora 会在本机完成语音识别与字幕生成。", "支持常见视频格式，清晰人声能获得更好的识别效果", "处理结果会保存到视频字幕成品目录。", "\uE8BA")
        };
        PageSubtitle.Text = copy.Item1;
        UtilityTitle.Text = copy.Item2;
        UtilityDescription.Text = copy.Item3;
        UtilityInputHint.Text = copy.Item4;
        UtilityOutputHint.Text = copy.Item5;
        UtilityFeatureIcon.Glyph = copy.Item6;
        InputPathBox.Text = string.Empty;
        UtilityInfo.IsOpen = false;
        UtilityStatusText.Text = "等待添加素材";
        UtilityOutputPathText.Text = settings.Current.OutputRoot;
        utilityLogs.Clear();
        AppendUtilityLog("工作区已准备，可以添加素材。");
        UtilityModelPicker.Items.Clear();
        foreach (var model in catalog.Definitions.Where(x => x.Feature == tag)) UtilityModelPicker.Items.Add(new ComboBoxItem { Content = model.Name, Tag = model.Id });
        if (UtilityModelPicker.Items.Count > 0) UtilityModelPicker.SelectedIndex = 0;
    }

    private async void OpenWorkbenchButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModelPicker.SelectedItem is not ComboBoxItem item || item.Tag is not string modelId || catalog.Find(modelId) is not { } model) return;
        if (!catalog.IsInstalled(model) && !await InstallSelectedModelAsync(model)) return;
        if (settings.Current.SafeMode) { SetStatus("安全模式已启用。关闭安全模式后才能启动创作引擎。"); return; }
        WorkbenchProgress.Visibility = Visibility.Visible; WorkbenchProgress.IsActive = true; StudioEmpty.Visibility = Visibility.Collapsed;
        SetStatus("正在启动 " + item.Content + "…");
        var result = await backend.StartWorkbenchAsync(feature, modelId, settings.EffectiveLanguage());
        WorkbenchProgress.IsActive = false; WorkbenchProgress.Visibility = Visibility.Collapsed;
        if (result.Success && result.Url is not null) { Workbench.Source = new Uri(result.Url); Workbench.Visibility = Visibility.Visible; SetStatus("已连接 " + item.Content); }
        else { StudioEmpty.Visibility = Visibility.Visible; EmptyTitle.Text = "暂时无法进入工作台"; EmptyBody.Text = result.Message; SetStatus(result.Message); }
    }

    private async Task<bool> InstallSelectedModelAsync(ModelDefinition model)
    {
        var modelRoot = settings.Current.LocalAiRoot;
        while (true)
        {
            var plan = ModelInstallPlanner.Create(model, modelRoot);
            var details = new StackPanel { Spacing = 10, Width = 560 };
            details.Children.Add(new TextBlock { Text = model.Name, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            details.Children.Add(new TextBlock { Text = $"{localization.Get("modelInstallLocation")}\n{plan.TargetPath}", TextWrapping = TextWrapping.Wrap });
            details.Children.Add(new TextBlock { Text = $"{localization.Get("modelDownloadSize")}：{plan.EstimatedDownload}\n{localization.Get("modelFreeSpace")}：{plan.RecommendedFreeSpace}" });
            details.Children.Add(new TextBlock { Text = localization.Get("modelRootNotice"), TextWrapping = TextWrapping.Wrap, Opacity = 0.72 });
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = localization.Get("modelInstallTitle"),
                Content = details,
                PrimaryButtonText = localization.Get("installHere"),
                SecondaryButtonText = localization.Get("changeModelFolder"),
                CloseButtonText = localization.Get("later"),
                DefaultButton = ContentDialogButton.Primary
            };
            var choice = await ShowDialogAsync(dialog);
            if (choice == ContentDialogResult.None) return false;
            if (choice == ContentDialogResult.Secondary)
            {
                var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
                picker.FileTypeFilter.Add("*");
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
                var folder = await picker.PickSingleFolderAsync();
                if (folder is not null) modelRoot = folder.Path;
                continue;
            }

            settings.Current.LocalAiRoot = modelRoot;
            settings.Save(settings.Current);
            ModelRootBox.Text = modelRoot;
            OpenWorkbenchButton.IsEnabled = false;
            WorkbenchProgress.Visibility = Visibility.Visible;
            WorkbenchProgress.IsActive = true;
            SetStatus(localization.Format("modelInstalling", model.Name));
            var result = await modelUpdater.UpdateAsync(model);
            WorkbenchProgress.IsActive = false;
            WorkbenchProgress.Visibility = Visibility.Collapsed;
            UpdateSelectedModelState();
            RefreshModels();
            if (!result.Success)
            {
                SetStatus(result.Message);
                return false;
            }
            if (!catalog.IsInstalled(model))
            {
                SetStatus(localization.Get("modelInstallIncomplete"));
                return false;
            }
            SetStatus(result.Message);
            return true;
        }
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            InputPathBox.Text = file.Path;
            UtilityStatusText.Text = "素材已添加，等待开始处理";
            AppendUtilityLog("已添加素材：" + file.Name);
        }
    }

    private async void RunUtilityButton_Click(object sender, RoutedEventArgs e)
    {
        if (settings.Current.SafeMode) { ShowUtility(false, "安全模式已启用。关闭安全模式后才能运行任务。"); return; }
        if (!File.Exists(InputPathBox.Text)) { ShowUtility(false, "没有找到可处理的素材，请重新选择文件。"); return; }
        var model = (UtilityModelPicker.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        var project = await projects.CreateAsync(feature, InputPathBox.Text, model);
        var task = taskQueue.Create(project.Id, UtilityTitle.Text, feature, InputPathBox.Text, model);
        await projects.AddTaskAsync(project, task);
        RunUtilityButton.IsEnabled = false;
        UtilityStatusText.Text = "正在处理";
        AppendUtilityLog("任务已提交，正在启动处理引擎。");
        ShowUtility(true, "正在本机处理素材，请保持 Aurora 打开。");
        var result = await taskQueue.RunAsync(task, token => backend.RunUtilityAsync(feature, InputPathBox.Text, model, settings.EffectiveLanguage(), token));
        await projects.CompleteTaskAsync(project.Id, task);
        if (settings.Current.AutoReleaseVram) backend.StopAll();
        RunUtilityButton.IsEnabled = true;
        UtilityStatusText.Text = result.Success ? "处理完成" : "处理未完成";
        AppendUtilityLog((result.Success ? "任务完成：" : "任务失败：") + result.Message);
        if (result.Path is not null) AppendUtilityLog("成品位置：" + result.Path);
        ShowUtility(result.Success, result.Message + (result.Path is null ? "" : "\n" + result.Path));
        RefreshWorkspace();
    }

    private void RefreshModels()
    {
        var states = catalog.GetStates();
        var filter = (ModelFilterPicker.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
        ModelsList.ItemsSource = filter switch
        {
            "installed" => states.Where(x => x.Installed).ToList(),
            "default" => states.Where(x => x.EditionDisplay == catalog.DefaultEditionDisplay).ToList(),
            "optional" => states.Where(x => x.EditionDisplay != catalog.DefaultEditionDisplay).ToList(),
            _ => states
        };
        ModelSummaryText.Text = catalog.FormatSummary(states);
        OutputPathText.Text = settings.Current.OutputRoot;
    }

    private void ModelFilterPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && ModelsList is not null) RefreshModels();
    }

    private void RefreshWorkspace()
    {
        var recent = projects.Recent();
        RecentProjectsList.ItemsSource = recent;
        ProjectsEmpty.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var active = taskQueue.Items.Where(x => x.Status is AuroraTaskStates.Waiting or AuroraTaskStates.Preparing or AuroraTaskStates.Running).ToList();
        HomeTasksList.ItemsSource = active.Take(4).ToList();
        TasksList.ItemsSource = taskQueue.Items.ToList();
        TasksEmpty.Visibility = taskQueue.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        QueueSummaryText.Text = active.Count == 0 ? "当前没有排队任务" : $"{active.Count} 个任务正在等待或处理";
    }

    private async void CheckAllModelsButton_Click(object sender, RoutedEventArgs e)
    {
        SetStatus("正在检查模型更新…");
        var results = await modelUpdater.CheckAllAsync();
        var available = results.Count(x => x.Value.Path == "available");
        SetStatus(available == 0 ? "所有可检查的模型均为最新。" : $"发现 {available} 个模型更新。请逐项确认更新。");
    }

    private async void ModelUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string id || catalog.Find(id) is not { } model) return;
        var check = await modelUpdater.CheckAsync(model);
        if (!catalog.IsInstalled(model) || check.Path == "available")
        {
            var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = model.Name, Content = catalog.IsInstalled(model) ? "发现新版本。Aurora 会保留可恢复的版本信息，是否现在更新？" : "此模型尚未安装。Aurora 将从官方来源下载并校验文件，是否继续？", PrimaryButtonText = catalog.IsInstalled(model) ? "更新" : "安装", CloseButtonText = "稍后" };
            if (await ShowDialogAsync(dialog) == ContentDialogResult.Primary) check = await modelUpdater.UpdateAsync(model);
        }
        SetStatus(model.Name + "：" + check.Message); RefreshModels();
    }

    private async void ModelRollbackButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string id || catalog.Find(id) is not { } model) return;
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "恢复模型版本", Content = $"将 {model.Name} 恢复到上一个已记录版本。项目文件和成品不会受影响。", PrimaryButtonText = "恢复", CloseButtonText = "取消" };
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary) return;
        var result = await modelUpdater.RollbackAsync(model); SetStatus(result.Message); RefreshModels();
    }

    private async void ModelUninstallButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string id || catalog.Find(id) is not { } model) return;
        var path = Path.Combine(settings.Current.LocalAiRoot, model.RelativeRoot);
        if (!Directory.Exists(path)) { SetStatus("此模型当前未安装。 "); return; }
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "卸载模型", Content = $"将 {model.Name} 移到 Windows 回收站。Aurora、项目和成品会保留。", PrimaryButtonText = "移到回收站", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary) return;
        try { Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin); SetStatus(model.Name + " 已移到回收站。 "); }
        catch (Exception ex) { SetStatus("卸载未完成：" + ex.Message); }
        RefreshModels();
    }

    private async void CheckAppUpdateButton_Click(object sender, RoutedEventArgs e) => await RunAppUpdateFlowAsync(true);

    private async void ReleaseNotesButton_Click(object sender, RoutedEventArgs e)
    {
        var current = CurrentDisplayVersion();
        var notes = ReleaseNotesCatalog.CurrentAndRecent(current, settings.EffectiveLanguage());
        var content = new StackPanel { Spacing = 18, Width = 620 };
        foreach (var note in notes)
        {
            var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            heading.Children.Add(new TextBlock { Text = $"Aurora {note.Version}", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            heading.Children.Add(new TextBlock { Text = note.Date, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 104, 121, 115)) });
            if (note.IsCurrent)
            {
                heading.Children.Add(new Border
                {
                    Padding = new Thickness(8, 3, 8, 3),
                    CornerRadius = new CornerRadius(7),
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 225, 244, 237)),
                    Child = new TextBlock { Text = localization.Get("currentVersion"), FontSize = 11, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 95, 77)) }
                });
            }
            content.Children.Add(new StackPanel
            {
                Spacing = 9,
                Children = { heading, new TextBlock { Text = note.Body, TextWrapping = TextWrapping.Wrap, LineHeight = 24 } }
            });
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = localization.Get("releaseNotesTitle"),
            CloseButtonText = localization.Get("close"),
            Content = new ScrollViewer { MaxHeight = 560, Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };
        await ShowDialogAsync(dialog);
    }

    private async Task RunAppUpdateFlowAsync(bool showCurrent)
    {
        if (!updateFlow.TryBegin())
        {
            if (showCurrent) ShowUpdateInfo(InfoBarSeverity.Informational, localization.Get("updateAlreadyRunning"));
            return;
        }

        var handingOff = false;
        AppUpdateButton.IsEnabled = false;
        try
        {
            SetStatus(localization.Get("updateChecking"));
            ShowGlobalUpdate(0, localization.Get("updateChecking"), true);
            var update = await updater.CheckAsync();
            if (!update.UpdateAvailable)
            {
                HideGlobalUpdate();
                if (showCurrent) ShowUpdateInfo(update.CheckSucceeded ? InfoBarSeverity.Success : InfoBarSeverity.Error, update.Message);
                return;
            }

            UpdateInfo.Severity = update.InstallerUrl is null || update.ChecksumUrl is null ? InfoBarSeverity.Warning : InfoBarSeverity.Informational;
            UpdateInfo.Message = localization.Format("updateFound", update.LatestVersion, update.Message);
            UpdateInfo.IsOpen = true;
            HideGlobalUpdate();
            if (update.InstallerUrl is null || update.ChecksumUrl is null) return;

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = localization.Get("updateDialogTitle"),
                Content = localization.Format("updateDialogBody", update.CurrentVersion, update.LatestVersion),
                PrimaryButtonText = localization.Get("updateInstall"),
                CloseButtonText = localization.Get("later")
            };
            if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary) return;

            var progress = new Progress<AppUpdateProgress>(value => ShowGlobalUpdate(value.Percentage, value.Message, value.IsIndeterminate));
            var result = await updater.DownloadAndInstallAsync(update, progress);
            ShowUpdateInfo(result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, result.Message);
            if (!result.Success)
            {
                HideGlobalUpdate();
                return;
            }

            handingOff = true;
            ShowGlobalUpdate(100, localization.Get("updateInstallerHandoff"));
            await Task.Delay(700);
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            HideGlobalUpdate();
            ShowUpdateInfo(InfoBarSeverity.Error, localization.Format("updateInstallFailed", ex.Message));
        }
        finally
        {
            updateFlow.End();
            if (!handingOff) AppUpdateButton.IsEnabled = true;
        }
    }

    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        await dialogGate.WaitAsync();
        try { return await dialog.ShowAsync(); }
        finally { dialogGate.Release(); }
    }

    private void ShowGlobalUpdate(double percentage, string message, bool isIndeterminate = false)
    {
        GlobalUpdatePanel.Visibility = Visibility.Visible;
        UpdateStageText.Text = message;
        UpdateProgress.IsIndeterminate = isIndeterminate;
        UpdateProgress.Value = Math.Clamp(percentage, 0, 100);
        UpdatePercentText.Text = isIndeterminate ? "…" : $"{Math.Round(UpdateProgress.Value):0}%";
    }

    private void HideGlobalUpdate()
    {
        UpdateProgress.IsIndeterminate = false;
        GlobalUpdatePanel.Visibility = Visibility.Collapsed;
    }

    private void ShowUpdateInfo(InfoBarSeverity severity, string message)
    {
        UpdateInfo.Severity = severity;
        UpdateInfo.Message = message;
        UpdateInfo.IsOpen = true;
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        settings.Current.Language = (LanguagePicker.SelectedItem as ComboBoxItem)?.Tag as string ?? "auto";
        settings.Current.Theme = (ThemePicker.SelectedItem as ComboBoxItem)?.Tag as string ?? "light";
        if (!string.IsNullOrWhiteSpace(ModelRootBox.Text)) settings.Current.LocalAiRoot = ModelRootBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(OutputRootBox.Text)) settings.Current.OutputRoot = OutputRootBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(ProjectsRootBox.Text)) settings.Current.ProjectsRoot = ProjectsRootBox.Text.Trim();
        settings.Current.AutoCheckAppUpdates = AppAutoUpdateToggle.IsOn; settings.Current.AutoCheckModelUpdates = ModelAutoUpdateToggle.IsOn; settings.Current.ConfirmLargeModelDownloads = ConfirmLargeToggle.IsOn;
        settings.Current.AutoReleaseVram = AutoReleaseSettingsToggle.IsOn;
        settings.Save(settings.Current); ApplyTheme(); ApplyLocalization(); RefreshModels(); SetStatus("设置已保存。");
    }

    private void ApplySettingsToControls()
    {
        SelectTag(LanguagePicker, settings.Current.Language); SelectTag(ThemePicker, settings.Current.Theme);
        ModelRootBox.Text = settings.Current.LocalAiRoot; OutputRootBox.Text = settings.Current.OutputRoot; ProjectsRootBox.Text = settings.Current.ProjectsRoot;
        AppAutoUpdateToggle.IsOn = settings.Current.AutoCheckAppUpdates; ModelAutoUpdateToggle.IsOn = settings.Current.AutoCheckModelUpdates; ConfirmLargeToggle.IsOn = settings.Current.ConfirmLargeModelDownloads;
        AutoReleaseSettingsToggle.IsOn = settings.Current.AutoReleaseVram; AutoReleaseToggle.IsOn = settings.Current.AutoReleaseVram; SafeModeToggle.IsOn = settings.Current.SafeMode; ApplyTheme();
    }

    private void ApplyLocalization()
    {
        HomeItem.Content = localization.Get("home"); TasksItem.Content = localization.Get("tasks");
        MusicItem.Content = localization.Get("music"); VoiceItem.Content = localization.Get("voice"); SingingItem.Content = localization.Get("singing");
        SeparationItem.Content = localization.Get("separation"); TranscriptionItem.Content = localization.Get("transcription"); SubtitlesItem.Content = localization.Get("subtitles");
        ModelsItem.Content = localization.Get("models"); MaintenanceItem.Content = localization.Get("maintenance"); SettingsItem.Content = localization.Get("settings"); AboutItem.Content = localization.Get("about");
        ModeLabel.Text = localization.Get("ready");
        var current = CurrentDisplayVersion();
        AboutVersionText.Text = $"{current} · {localization.Get("workbenchLabel")}";
        AboutReleaseSummary.Text = ReleaseNotesCatalog.CurrentAndRecent(current, settings.EffectiveLanguage(), 1).FirstOrDefault()?.Body ?? string.Empty;
        LocalizeTree(this);
    }

    private static string CurrentDisplayVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version is null) return "1.0.1";
        return version.Revision > 0 ? version.ToString(4) : version.ToString(3);
    }

    private void LocalizeTree(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock text && !string.IsNullOrWhiteSpace(text.Text)) text.Text = localization.Translate(text.Text);
            if (child is Button button && button.Content is string content) button.Content = localization.Translate(content);
            if (child is ComboBoxItem item && item.Content is string itemContent) item.Content = localization.Translate(itemContent);
            if (child is TextBox box) { if (box.Header is string header) box.Header = localization.Translate(header); if (!string.IsNullOrWhiteSpace(box.PlaceholderText)) box.PlaceholderText = localization.Translate(box.PlaceholderText); }
            if (child is ComboBox combo && combo.Header is string comboHeader) combo.Header = localization.Translate(comboHeader);
            if (child is ToggleSwitch toggle) { if (toggle.Header is string header) toggle.Header = localization.Translate(header); if (toggle.OnContent is string on) toggle.OnContent = localization.Translate(on); if (toggle.OffContent is string off) toggle.OffContent = localization.Translate(off); }
            LocalizeTree(child);
        }
    }

    private void ApplyTheme() => RequestedTheme = settings.Current.Theme switch { "dark" => ElementTheme.Dark, "system" => ElementTheme.Default, _ => ElementTheme.Light };
    private static void SelectTag(ComboBox box, string tag) { foreach (var value in box.Items.OfType<ComboBoxItem>()) if ((value.Tag as string) == tag) { box.SelectedItem = value; break; } }
    private void ModelPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateSelectedModelState();

    private void UpdateSelectedModelState()
    {
        if (ModelPicker.SelectedItem is not ComboBoxItem item || item.Tag is not string id || catalog.Find(id) is not { } model) return;
        var installed = catalog.IsInstalled(model);
        CurrentModelName.Text = model.Name;
        CurrentModelState.Text = localization.Get(installed ? "modelReady" : "modelMissing");
        OpenWorkbenchButton.Content = localization.Get(installed ? "openWorkbench" : "installModel");
        OpenWorkbenchButton.IsEnabled = true;
    }
    private void OpenOutputButton_Click(object sender, RoutedEventArgs e) => backend.OpenFolder(settings.Current.OutputRoot);
    private void OpenModelsButton_Click(object sender, RoutedEventArgs e) => backend.OpenFolder(settings.Current.LocalAiRoot);
    private void ReleaseButton_Click(object sender, RoutedEventArgs e) { backend.StopAll(); Workbench.Visibility = Visibility.Collapsed; StudioEmpty.Visibility = Visibility.Visible; SetStatus("当前创作引擎已安全结束。"); }
    private void DiagnosticsButton_Click(object sender, RoutedEventArgs e) { var result = backend.CreateDiagnostics(); SetStatus(result.Message + (result.Path is null ? "" : " " + result.Path)); }
    private void OpenLogsButton_Click(object sender, RoutedEventArgs e) => backend.OpenFolder(settings.LogsRoot);
    private void RunHealthScanButton_Click(object sender, RoutedEventArgs e) => RunHealthScan();
    private void RunHealthScan()
    {
        HealthList.ItemsSource = maintenance.Scan();
        GpuSummaryText.Text = maintenance.GpuSummary();
        SafeModeToggle.IsOn = settings.Current.SafeMode;
        AutoReleaseToggle.IsOn = settings.Current.AutoReleaseVram;
    }
    private void SafeModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        settings.Current.SafeMode = SafeModeToggle.IsOn;
        settings.Current.AutoReleaseVram = AutoReleaseToggle.IsOn;
        AutoReleaseSettingsToggle.IsOn = settings.Current.AutoReleaseVram;
        settings.Save(settings.Current);
        if (settings.Current.SafeMode) backend.StopAll();
        SetStatus(settings.Current.SafeMode ? "安全模式已启用。" : "安全模式已关闭，创作引擎可以启动。 ");
    }
    private void AutoReleaseToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        settings.Current.AutoReleaseVram = AutoReleaseToggle.IsOn;
        AutoReleaseSettingsToggle.IsOn = settings.Current.AutoReleaseVram;
        settings.Save(settings.Current);
    }

    private void TemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string tag) return;
        SelectNavigation(tag);
    }

    private void OpenProjectButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string id || projects.Find(id) is not { } project) return;
        SelectNavigation(project.Feature);
        if (project.Feature is "separation" or "transcription" or "subtitles")
        {
            InputPathBox.Text = project.SourcePath;
            SelectTag(UtilityModelPicker, project.ModelId);
            UtilityStatusText.Text = "项目已恢复，可以重新处理或查看原素材。";
        }
    }

    private void OpenTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string id || taskQueue.Items.FirstOrDefault(x => x.Id == id) is not { } task) return;
        var path = !string.IsNullOrWhiteSpace(task.OutputPath) ? task.OutputPath : task.InputPath;
        if (File.Exists(path)) path = Path.GetDirectoryName(path)!;
        if (!string.IsNullOrWhiteSpace(path)) backend.OpenFolder(path);
    }

    private async void RetryTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string id || taskQueue.Items.FirstOrDefault(x => x.Id == id) is not { } old || !old.CanRetry) return;
        var project = await projects.CreateAsync(old.Feature, old.InputPath, old.ModelId);
        var task = taskQueue.Create(project.Id, old.Title, old.Feature, old.InputPath, old.ModelId);
        await projects.AddTaskAsync(project, task);
        var result = await taskQueue.RunAsync(task, token => backend.RunUtilityAsync(old.Feature, old.InputPath, old.ModelId, settings.EffectiveLanguage(), token));
        await projects.CompleteTaskAsync(project.Id, task);
        SetStatus(result.Message);
    }

    private void CancelTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string id) return;
        taskQueue.Cancel(id);
        backend.StopAll();
        SetStatus("已请求安全取消任务。 ");
    }

    private void SelectNavigation(string tag)
    {
        var target = Shell.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => (x.Tag as string) == tag);
        if (target is not null) Shell.SelectedItem = target;
    }
    private void ClearUtilityLogButton_Click(object sender, RoutedEventArgs e) { utilityLogs.Clear(); AppendUtilityLog("活动记录已清空。"); }
    private void AppendUtilityLog(string message) => utilityLogs.Add($"{DateTime.Now:HH:mm:ss}  {message}");
    private async Task CheckModelsSilentlyAsync() { try { await modelUpdater.CheckAllAsync(); } catch { } }
    private void ShowUtility(bool success, string message) { UtilityInfo.Severity = success ? InfoBarSeverity.Success : InfoBarSeverity.Error; UtilityInfo.Message = message; UtilityInfo.IsOpen = true; SetStatus(message); }
    private void SetStatus(string value) { FooterStatus.Text = value; TaskStatusText.Text = value; }
    private static string FormatBackendStatus(string value)
    {
        if (value.Equals("released", StringComparison.OrdinalIgnoreCase)) return "创作引擎已结束，显存已释放。";
        if (value.StartsWith("loading:", StringComparison.OrdinalIgnoreCase)) return value[8..] + "…";
        if (value.StartsWith("running:", StringComparison.OrdinalIgnoreCase)) return "正在处理：" + value[8..];
        if (value.StartsWith("completed:", StringComparison.OrdinalIgnoreCase)) return "任务已完成。";
        if (value.StartsWith("failed:", StringComparison.OrdinalIgnoreCase)) return "任务失败：" + value[7..];
        return value;
    }
    private void ShowOnly(UIElement target) { foreach (var value in new UIElement[] { HomeView, StudioView, UtilityView, ModelsView, TasksView, MaintenanceView, SettingsView, AboutView }) value.Visibility = value == target ? Visibility.Visible : Visibility.Collapsed; }
    public void Shutdown() => backend.StopAll();
}
