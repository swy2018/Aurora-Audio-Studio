using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AuroraAudioStudio.Core;

namespace AuroraAudioStudio.Mac;

public sealed partial class MainWindow
{
    private string modelFilter = "all";
    private int modelCompleted, modelTotal;
    private string modelActivity = "";

    private string LocalizedModelHealth(string value)
    {
        foreach (var prefix in new[] { "模型文件已校验", "文件与运行环境已校验", "短任务已验证", "文件齐全 · 待运行验证", "未安装或需要修复" })
            if (value.StartsWith(prefix)) return L(prefix) + value[prefix.Length..];
        return L(value);
    }

    private Control ModelsPage()
    {
        var runtime = workspace.Runtime;
        var definitions = workspace.Catalog.Definitions;
        var states = definitions.ToDictionary(m => m.Id, m => runtime.ModelStatus(m.Id));
        var descriptions = workspace.Catalog.GetStates().Select(s => s with { Installed = states[s.Id].Installed }).ToList();
        var pending = runtime.Models.Keys.Where(id => states[id].HasUpdate && states[id].Installed).ToArray();
        var root = new Grid { Margin = new Thickness(28, 24), RowDefinitions = new RowDefinitions("Auto,Auto,*"), RowSpacing = 14 };
        var heading = VStack(Txt(L("创作引擎与模型"), 20, true), Txt(workspace.Catalog.FormatSummary(descriptions), 12)); heading.Spacing = 5;
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 16, RowSpacing = 12 };
        header.Children.Add(heading);
        var filters = new[] { "all", "installed", "default", "optional" };
        var filter = new ComboBox { Width = 150, ItemsSource = new[] { L("全部模型"), L("已安装"), L("默认组件"), L("可选模型") }, SelectedIndex = Array.IndexOf(filters, modelFilter) };
        AutomationProperties.SetAutomationId(filter, "model-filter");
        var checkAll = ActionButton(L("检查全部更新"), () => ModelBatchAsync("updates"), "check-all-model-updates"); checkAll.Classes.Add("primary");
        var updateAll = ActionButton(L("更新全部") + $" ({pending.Length})", UpdateAllModelsAsync, "update-all-models"); updateAll.IsVisible = pending.Length > 0;
        checkAll.IsEnabled = updateAll.IsEnabled = modelOperation is null;
        var actions = Row(filter, checkAll, updateAll, ActionButton(L("查看本地文件"), () => OpenPathAsync(runtime.Root), "open-model-files"));
        Grid.SetColumn(actions, 1); header.Children.Add(actions); root.Children.Add(header);
        header.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 1100;
            header.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : GridLength.Auto;
            Grid.SetColumnSpan(heading, narrow ? 2 : 1); Grid.SetColumn(actions, narrow ? 0 : 1); Grid.SetRow(actions, narrow ? 1 : 0); Grid.SetColumnSpan(actions, narrow ? 2 : 1);
        };
        var activity = VStack();
        if (modelOperation is not null)
        {
            activity.Children.Add(Txt(modelActivity, 13, true));
            activity.Children.Add(new ProgressBar { Minimum = 0, Maximum = Math.Max(1, modelTotal), Value = modelCompleted, IsIndeterminate = modelTotal <= 1 });
            activity.Children.Add(Row(Txt(modelLog, 12), ActionButton(L("取消"), () => { modelOperation?.Cancel(); return Task.CompletedTask; }, "cancel-model-operation")));
        }
        else if (modelLog.Length > 0) activity.Children.Add(Txt(modelLog, 12));
        Grid.SetRow(activity, 1); root.Children.Add(activity);
        var content = VStack();
        var list = VStack();
        var search = Input(modelSearch, value => { modelSearch = value; Fill(); }, "model-search"); search.PlaceholderText = text["filter"];
        content.Children.Add(search);
        content.Children.Add(list);
        var scroll = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 2); root.Children.Add(scroll);
        filter.SelectionChanged += (_, _) => { if (filter.SelectedIndex >= 0) { modelFilter = filters[filter.SelectedIndex]; Fill(); } };
        void Fill()
        {
            list.Children.Clear();
            var metadata = workspace.Catalog.GetStates().ToDictionary(s => s.Id);
            foreach (var feature in MacWorkspace.Features)
            {
                var items = definitions.Where(m => m.Feature == feature &&
                    (modelFilter == "all" || modelFilter == "installed" && states[m.Id].Installed || modelFilter == "default" && m.IsDefault || modelFilter == "optional" && !m.IsDefault) &&
                    (m.Name.Contains(modelSearch, StringComparison.OrdinalIgnoreCase) || m.Id.Contains(modelSearch, StringComparison.OrdinalIgnoreCase))).ToArray();
                if (items.Length == 0) continue;
                var group = Txt(text[feature], 18, true); group.Margin = new Thickness(2, 12, 0, 0); list.Children.Add(group);
                foreach (var model in items)
                {
                    var state = states[model.Id]; var info = metadata[model.Id];
                    var supported = runtime.Models.TryGetValue(model.Id, out var spec);
                    var busy = runtime.BusyModel == model.Id;
                    var name = VStack(Txt(info.Name, 17, true), Txt(info.Purpose, 13)); name.Spacing = 3;
                    var stateLabel = !supported ? L("尚无 Mac 适配器") : busy ? L("维护中") : state.HasUpdate ? L("有更新") : state.Health.StartsWith("需要修复") ? L("需要修复") : state.Installed ? state.Health.StartsWith("短任务已验证") ? L("短任务已验证") : L("文件齐全") : L("未安装");
                    Border Tag(string value) => new() { Background = Pane, Padding = new Thickness(8, 4), CornerRadius = new CornerRadius(6), Child = Txt(value, 12) };
                    var badges = Row(Tag(info.EditionDisplay), Tag(stateLabel));
                    if (supported && spec!.DownloadOnly) badges.Children.Add(Tag(model.Id == "subtitle-edit" ? L("外部字幕编辑器") : L("仅模型管理")));
                    var size = supported ? spec!.Gb < .01 ? $"{spec.Gb * 1000:0.##} MB" : $"{spec.Gb:0.##} GB" : "—";
                    var detail = Txt($"{L("版本")} {state.Version} · {L("原版显存参考")} {info.RecommendedVram} · {L("下载约")} {size} · {info.License} · {info.Source}", 12);
                    var excludedReason = model.Id == "minimax-music3" ? "当前上游推理实现要求 CUDA，无法在 Apple Silicon 运行。"
                        : model.Id == "faster-whisper" ? "上游 XXL 仅提供 Windows/Linux x64 程序；Mac 字幕功能使用原有 Whisper 模型。"
                        : "此组件保留原版信息，尚不能在 Mac 安装或运行。";
                    var health = Txt(!supported ? L(excludedReason) : state.UpdateMessage.Length > 0 ? L(state.UpdateMessage) : LocalizedModelHealth(state.Health), 12);
                    var path = Txt(supported ? runtime.Current(model.Id) : "—", 11); path.TextWrapping = TextWrapping.NoWrap; path.TextTrimming = TextTrimming.CharacterEllipsis; ToolTip.SetTip(path, path.Text);
                    var details = VStack(name, badges, Txt(info.FeatureDisplay + " · " + info.Languages, 13), detail, health, path); details.Spacing = 10;
                    var primaryText = !supported ? L("尚未适配") : busy ? L("维护中…") : state.HasUpdate ? L("更新") : state.Health.StartsWith("需要修复") ? L("修复") : state.Installed ? L("检查 / 修复") : L("安装");
                    var primary = ActionButton(primaryText, () => ModelPrimaryAsync(model.Id), "model-primary-" + model.Id); primary.Classes.Add("primary"); primary.HorizontalAlignment = HorizontalAlignment.Stretch;
                    primary.IsEnabled = supported && modelOperation is null;
                    var rollback = ActionButton(L("回退"), () => ModelActionAsync(model.Id, "rollback"), "model-rollback-" + model.Id);
                    var uninstall = ActionButton(L("卸载"), () => ModelActionAsync(model.Id, "uninstall"), "model-uninstall-" + model.Id);
                    rollback.IsEnabled = supported && modelOperation is null && Directory.Exists(Path.Combine(runtime.Root, "models", model.Id, "previous"));
                    uninstall.IsEnabled = supported && modelOperation is null && Directory.Exists(Path.Combine(runtime.Root, "models", model.Id));
                    var secondary = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 8 };
                    rollback.HorizontalAlignment = uninstall.HorizontalAlignment = HorizontalAlignment.Stretch;
                    secondary.Children.Add(rollback); Grid.SetColumn(uninstall, 1); secondary.Children.Add(uninstall);
                    var buttons = VStack(primary, secondary); buttons.Spacing = 8; buttons.Width = 176; buttons.VerticalAlignment = VerticalAlignment.Center;
                    var card = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 24, RowSpacing = 12 };
                    card.Children.Add(details); Grid.SetColumn(buttons, 1); card.Children.Add(buttons);
                    card.SizeChanged += (_, e) =>
                    {
                        var narrow = e.NewSize.Width < 620;
                        card.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : GridLength.Auto;
                        Grid.SetColumnSpan(details, narrow ? 2 : 1); Grid.SetRow(buttons, narrow ? 1 : 0); Grid.SetColumn(buttons, narrow ? 0 : 1);
                        buttons.HorizontalAlignment = HorizontalAlignment.Left;
                    };
                    var panel = Panel(card); panel.Padding = new Thickness(20, 18); list.Children.Add(panel);
                }
            }
            if (list.Children.Count == 0) list.Children.Add(Txt(L("没有符合筛选条件的模型。"), 14));
        }
        Fill(); return root;
    }
}
