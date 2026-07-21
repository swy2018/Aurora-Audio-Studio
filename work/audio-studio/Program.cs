using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;

namespace AIAudioStudio;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var form = new MainForm();
        var previewIndex = Array.IndexOf(args, "--render-preview");
        if (previewIndex >= 0 && previewIndex + 1 < args.Length)
        {
            try
            {
                var languageIndex = Array.IndexOf(args, "--preview-language");
                if (languageIndex >= 0 && languageIndex + 1 < args.Length)
                    form.SetPreviewLanguage(args[languageIndex + 1]);
                var featureIndex = Array.IndexOf(args, "--preview-feature");
                if (featureIndex >= 0 && featureIndex + 1 < args.Length)
                    form.SelectPreviewFeature(args[featureIndex + 1]);
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-32000, -32000);
                form.Show();
                Application.DoEvents();
                form.RenderPreview(args[previewIndex + 1]);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                File.WriteAllText(args[previewIndex + 1] + ".error.txt", ex.ToString());
                Environment.Exit(2);
            }
        }
        Application.Run(form);
    }
}

internal sealed class MainForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    private const string MusicUrl = "http://127.0.0.1:7860";
    private const string TtsUrl = "http://127.0.0.1:7861";
    private const string SeedVcUrl = "http://127.0.0.1:7862";
    private const string AppVersion = "0.8.0";

    private static readonly Color AppBackground = Color.FromArgb(7, 12, 20);
    private static readonly Color SidebarBackground = Color.FromArgb(9, 15, 25);
    private static readonly Color Surface = Color.FromArgb(15, 23, 36);
    private static readonly Color SurfaceLight = Color.FromArgb(21, 31, 47);
    private static readonly Color Border = Color.FromArgb(38, 55, 78);
    private static readonly Color Muted = Color.FromArgb(143, 158, 180);
    private static readonly Color Accent = Color.FromArgb(36, 201, 255);

    private readonly WebView2 web = new() { Dock = DockStyle.Fill, BackColor = AppBackground, Visible = false };
    private readonly Label status = new()
    {
        Dock = DockStyle.Fill,
        Padding = new Padding(14, 0, 0, 0),
        Text = "就绪 · 选择左侧功能开始",
        ForeColor = Color.FromArgb(167, 181, 201),
        BackColor = Color.FromArgb(8, 14, 23),
        Font = new Font("Microsoft YaHei UI", 9f),
        TextAlign = ContentAlignment.MiddleLeft
    };
    private readonly Panel dashboard = new() { Dock = DockStyle.Fill, BackColor = AppBackground };
    private readonly Label pageTitle = new();
    private readonly Label pageSubtitle = new();
    private readonly Label heroTitle = new();
    private readonly Label heroDescription = new();
    private readonly Label modelTag = new();
    private readonly Label recentName = new();
    private readonly Label recentMeta = new();
    private readonly AccentButton primaryButton = new(true);
    private readonly AccentButton secondaryButton = new(false);
    private readonly Button modelSelect = new();
    private readonly ContextMenuStrip modelMenu = new();
    private readonly Dictionary<string, Button> navButtons = new();
    private readonly Dictionary<string, Label> sectionLabels = new();
    private Label? windowTitleLabel;
    private Label? topHeading;
    private Label? localModeLabel;
    private Label? startupLabel;
    private Label? recentTitleLabel;
    private AccentButton? releaseButton;
    private AccentButton? outputButton;
    private AccentButton? updateButton;
    private AccentButton? websiteButton;
    private AccentButton? openRecentButton;
    private Button? languageButton;
    private string localAiRoot = @"C:\LocalAI";
    private string outputRoot = DefaultOutputRoot();
    private bool englishUi = !CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    private ModelOption? selectedModelOption;
    private bool modelSelectorActive;
    private string selectedFeature = "music";
    private Process? musicProcess;
    private Process? ttsProcess;
    private Process? seedVcProcess;
    private Process? utilityProcess;
    private CancellationTokenSource? navigationCancellation;
    private string currentPrimaryText = "";
    private Func<Task>? currentPrimaryAction;
    private List<ModelCatalogEntry>? modelCatalog;
    private string MusicRoot => Path.Combine(localAiRoot, "ACE-Step-1.5");
    private string TtsRoot => Path.Combine(localAiRoot, "IndexTTS2");
    private string SeedVcRoot => Path.Combine(localAiRoot, "Seed-VC");
    private string SubtitleExe => Path.Combine(localAiRoot, "SubtitleEdit", "SubtitleEdit.exe");
    private string SeparatorExe => Path.Combine(localAiRoot, "AudioTools", "separator-env", "Scripts", "audio-separator.exe");
    private string SeparatorModels => Path.Combine(localAiRoot, "AudioTools", "models");
    private string BasicPitchExe => Path.Combine(localAiRoot, "AudioTools", "pitch-env", "Scripts", "basic-pitch.exe");
    private string FfmpegDir => Path.Combine(localAiRoot, "Faster-Whisper-XXL", "Faster-Whisper-XXL");
    private string LogsRoot => Path.Combine(localAiRoot, "Logs");
    private string NumbaCacheRoot => Path.Combine(localAiRoot, "AudioTools", "numba-cache");
    private string T(string zh, string en) => englishUi ? en : zh;
    private static string DefaultOutputRoot()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktop) || !Directory.Exists(desktop))
            desktop = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(desktop, "AI工作流");
    }

    public MainForm()
    {
        Text = $"Aurora Audio Studio {AppVersion}";
        MinimumSize = new Size(1180, 760);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft YaHei UI", 10f);
        FormBorderStyle = FormBorderStyle.None;
        Padding = new Padding(1);
        StartPosition = FormStartPosition.Manual;
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
        Width = Math.Min(1920, Math.Min(workArea.Width, Math.Max(1760, (int)(workArea.Width * 0.56))));
        Height = Math.Min(1120, Math.Min(workArea.Height, Math.Max(1040, (int)(workArea.Height * 0.78))));
        Location = new Point(
            workArea.Left + Math.Max(0, (workArea.Width - Width) / 2),
            workArea.Top + Math.Max(0, (workArea.Height - Height) / 2));
        BackColor = AppBackground;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        LoadUserSettings();

        var sidebar = BuildSidebar();
        var content = new Panel { Dock = DockStyle.Fill, BackColor = AppBackground };
        content.Controls.Add(web);
        content.Controls.Add(dashboard);
        content.Controls.Add(BuildTopBar());
        Controls.Add(content);
        Controls.Add(sidebar);
        Controls.Add(BuildWindowTitleBar());

        BuildDashboard();
        ApplyStaticLanguage();
        SelectFeature("music");
        FormClosing += (_, _) => StopBackends();
    }

    private Control BuildWindowTitleBar()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.FromArgb(7, 12, 20) };
        var iconPath = Path.Combine(AppContext.BaseDirectory, "assets", "app-icon-v2.png");
        if (File.Exists(iconPath))
            bar.Controls.Add(new PictureBox
            {
                Image = Image.FromFile(iconPath),
                Location = new Point(13, 8),
                Size = new Size(26, 26),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            });
        windowTitleLabel = new Label
        {
            Text = $"Aurora Audio Studio  ·  {AppVersion}",
            Location = new Point(47, 0),
            Width = 280,
            Height = 42,
            ForeColor = Color.FromArgb(210, 220, 234),
            Font = new Font("Microsoft YaHei UI", 9.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };
        bar.Controls.Add(windowTitleLabel);

        var close = CreateWindowButton("\u00D7", 14.5f);
        close.Click += (_, _) => Close();
        close.FlatAppearance.MouseOverBackColor = Color.FromArgb(196, 43, 58);
        var maximize = CreateWindowButton("\u25A1", 12f);
        maximize.Click += (_, _) => ToggleMaximize();
        var minimize = CreateWindowButton("\u2212", 13f);
        minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        bar.Controls.Add(minimize);
        bar.Controls.Add(maximize);
        bar.Controls.Add(close);

        void BeginDrag(object? _, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
        }
        bar.MouseDown += BeginDrag;
        foreach (Control child in bar.Controls)
            if (child is not Button) child.MouseDown += BeginDrag;
        bar.DoubleClick += (_, _) => ToggleMaximize();
        return bar;
    }

    private static Button CreateWindowButton(string text, float fontSize)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Right,
            Width = 52,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(7, 12, 20),
            ForeColor = Color.FromArgb(190, 202, 218),
            Font = new Font("Segoe UI", fontSize, FontStyle.Regular),
            TabStop = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 42, 58);
        return button;
    }

    private void ToggleMaximize() => WindowState = WindowState == FormWindowState.Maximized
        ? FormWindowState.Normal
        : FormWindowState.Maximized;

    protected override void WndProc(ref Message message)
    {
        const int wmNcHitTest = 0x84;
        if (message.Msg == wmNcHitTest && WindowState == FormWindowState.Normal)
        {
            var screenPoint = new Point((short)(message.LParam.ToInt64() & 0xffff),
                (short)((message.LParam.ToInt64() >> 16) & 0xffff));
            var point = PointToClient(screenPoint);
            const int grip = 8;
            var left = point.X <= grip;
            var right = point.X >= ClientSize.Width - grip;
            var top = point.Y <= grip;
            var bottom = point.Y >= ClientSize.Height - grip;
            if (left && top) { message.Result = (IntPtr)13; return; }
            if (right && top) { message.Result = (IntPtr)14; return; }
            if (left && bottom) { message.Result = (IntPtr)16; return; }
            if (right && bottom) { message.Result = (IntPtr)17; return; }
            if (left) { message.Result = (IntPtr)10; return; }
            if (right) { message.Result = (IntPtr)11; return; }
            if (top) { message.Result = (IntPtr)12; return; }
            if (bottom) { message.Result = (IntPtr)15; return; }
        }
        base.WndProc(ref message);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!OperatingSystem.IsWindows()) return;
        var enabled = 1;
        if (DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int)) != 0)
            DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
        var captionColor = ColorTranslator.ToWin32(Color.FromArgb(7, 12, 20));
        var textColor = ColorTranslator.ToWin32(Color.White);
        DwmSetWindowAttribute(Handle, 35, ref captionColor, sizeof(int));
        DwmSetWindowAttribute(Handle, 36, ref textColor, sizeof(int));
    }

    private Panel BuildSidebar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 304,
            BackColor = SidebarBackground,
            Padding = new Padding(18, 16, 18, 16)
        };
        var brand = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = SidebarBackground };
        var iconPath = Path.Combine(AppContext.BaseDirectory, "assets", "app-icon-v2.png");
        if (File.Exists(iconPath))
        {
            brand.Controls.Add(new PictureBox
            {
                Image = Image.FromFile(iconPath),
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(0, 13),
                Size = new Size(62, 62),
                BackColor = Color.Transparent
            });
        }
        brand.Controls.Add(new Label
        {
            Text = "AURORA AUDIO",
            Location = new Point(68, 18),
            Size = new Size(170, 30),
            AutoSize = false,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });
        brand.Controls.Add(new Label
        {
            Text = "LOCAL AI PRODUCTION STUDIO",
            Location = new Point(70, 48),
            Size = new Size(168, 20),
            AutoSize = false,
            ForeColor = Color.FromArgb(91, 201, 255),
            Font = new Font("Segoe UI", 6.6f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 220, BackColor = SidebarBackground };
        var release = new AccentButton(false)
        {
            Text = T("释放模型显存", "Unload models"),
            Location = new Point(0, 12),
            Size = new Size(266, 40),
            ForeColor = Color.FromArgb(170, 185, 205),
            Font = new Font("Microsoft YaHei UI", 10f)
        };
        releaseButton = release;
        release.Click += (_, _) =>
        {
            StopBackends();
            ShowDashboard(T("已停止全部音频模型，显存正在释放。", "All audio backends stopped. VRAM is being released."));
        };
        var output = new AccentButton(false)
        {
            Text = T("设置输出位置", "Set output folder"),
            Location = new Point(0, 58),
            Size = new Size(266, 38),
            ForeColor = Color.FromArgb(170, 185, 205),
            Font = new Font("Microsoft YaHei UI", 10f)
        };
        outputButton = output;
        output.Click += (_, _) => ChooseOutputLocation();
        var update = new AccentButton(false)
        {
            Text = T("检查更新", "Check updates"),
            Location = new Point(0, 102),
            Size = new Size(266, 38),
            ForeColor = Color.FromArgb(170, 185, 205),
            Font = new Font("Microsoft YaHei UI", 10f)
        };
        updateButton = update;
        update.Click += async (_, _) => await CheckForUpdatesAsync();
        var website = new AccentButton(false)
        {
            Text = T("访问官网", "Visit website"),
            Location = new Point(0, 146),
            Size = new Size(266, 38),
            ForeColor = Color.FromArgb(170, 185, 205),
            Font = new Font("Microsoft YaHei UI", 10f)
        };
        websiteButton = website;
        website.Click += (_, _) => OpenWebsite();
        bottom.Controls.Add(release);
        bottom.Controls.Add(output);
        bottom.Controls.Add(update);
        bottom.Controls.Add(website);
        startupLabel = new Label
        {
            Text = T("不会开机自启动  ·  仅本机运行", "No startup item · Local only"),
            Dock = DockStyle.Bottom,
            Height = 34,
            ForeColor = Color.FromArgb(91, 107, 129),
            Font = new Font("Microsoft YaHei UI", 8.5f),
            TextAlign = ContentAlignment.MiddleCenter
        };
        bottom.Controls.Add(startupLabel);

        var navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = SidebarBackground,
            Padding = new Padding(0, 8, 0, 0)
        };
        AddSectionLabel(navigation, "creation");
        AddFeatureButton(navigation, "music");
        AddFeatureButton(navigation, "tts");
        AddFeatureButton(navigation, "singing");
        AddSectionLabel(navigation, "tools");
        AddFeatureButton(navigation, "separator");
        AddFeatureButton(navigation, "pitch");
        AddFeatureButton(navigation, "subtitle");

        panel.Controls.Add(navigation);
        panel.Controls.Add(bottom);
        panel.Controls.Add(brand);
        return panel;
    }

    private void AddSectionLabel(FlowLayoutPanel parent, string key)
    {
        var label = new Label
        {
            Text = SectionText(key),
            Width = 258,
            Height = 32,
            Margin = new Padding(9, 10, 0, 2),
            ForeColor = Color.FromArgb(91, 107, 129),
            Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        sectionLabels[key] = label;
        parent.Controls.Add(label);
    }

    private void AddFeatureButton(FlowLayoutPanel parent, string key)
    {
        var button = CreateNavButton(FeatureNavText(key), key);
        button.Click += (_, _) => SelectFeature(key);
        navButtons[key] = button;
        parent.Controls.Add(button);
    }

    private static Button CreateNavButton(string text, string key)
    {
        var button = new Button
        {
            Name = key,
            Text = "     " + text,
            Width = 266,
            Height = 48,
            Margin = new Padding(0, 3, 0, 3),
            FlatStyle = FlatStyle.Flat,
            BackColor = SidebarBackground,
            ForeColor = Color.FromArgb(183, 195, 212),
            Font = new Font("Microsoft YaHei UI", 10.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(17, 29, 44);
        return button;
    }

    private string SectionText(string key) => key switch
    {
        "creation" => T("创作", "Create"),
        "tools" => T("制作工具", "Production tools"),
        _ => key
    };

    private string FeatureNavText(string key) => key switch
    {
        "music" => T("音乐创作", "Music generation"),
        "tts" => T("AI配音与声音克隆", "AI voice clone"),
        "singing" => T("歌声克隆", "Singing clone"),
        "separator" => T("去人声 / AI分轨", "Vocal removal"),
        "pitch" => T("AI扒谱（MIDI）", "MIDI transcription"),
        "subtitle" => T("视频 AI 字幕", "Video AI subtitles"),
        _ => key
    };

    private Control BuildTopBar()
    {
        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 68,
            BackColor = Color.FromArgb(8, 14, 23),
            ColumnCount = 4,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 178));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        topHeading = new Label
        {
            Text = T("工作台", "Workbench"),
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 0, 0, 0),
            ForeColor = Color.FromArgb(210, 220, 234),
            Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        localModeLabel = new Label
        {
            Text = T("●  本地离线模式", "●  Local offline"),
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(89, 224, 173),
            Font = new Font("Microsoft YaHei UI", 9.5f),
            TextAlign = ContentAlignment.MiddleCenter
        };
        languageButton = new Button
        {
            Text = englishUi ? "中文" : "English",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 16, 18, 16),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(15, 23, 36),
            ForeColor = Color.FromArgb(190, 205, 224),
            Font = new Font("Microsoft YaHei UI", 8.8f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        languageButton.FlatAppearance.BorderSize = 1;
        languageButton.FlatAppearance.BorderColor = Color.FromArgb(45, 65, 91);
        languageButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 37, 53);
        languageButton.Click += (_, _) =>
        {
            englishUi = !englishUi;
            SaveUserSettings();
            ApplyStaticLanguage();
            SelectFeature(selectedFeature);
        };
        top.Controls.Add(topHeading, 0, 0);
        top.Controls.Add(status, 1, 0);
        top.Controls.Add(localModeLabel, 2, 0);
        top.Controls.Add(languageButton, 3, 0);
        return top;
    }

    private void ApplyStaticLanguage()
    {
        if (windowTitleLabel is not null) windowTitleLabel.Text = $"Aurora Audio Studio  ·  {AppVersion}";
        if (topHeading is not null) topHeading.Text = T("工作台", "Workbench");
        if (localModeLabel is not null) localModeLabel.Text = T("●  本地离线模式", "●  Local offline");
        if (languageButton is not null) languageButton.Text = englishUi ? "中文" : "English";
        if (releaseButton is not null) releaseButton.Text = T("释放模型显存", "Unload models");
        if (outputButton is not null) outputButton.Text = T("设置输出位置", "Set output folder");
        if (updateButton is not null) updateButton.Text = T("检查更新", "Check updates");
        if (websiteButton is not null) websiteButton.Text = T("访问官网", "Visit website");
        if (startupLabel is not null) startupLabel.Text = T("不会开机自启动  ·  仅本机运行", "No startup item · Local only");
        if (recentTitleLabel is not null) recentTitleLabel.Text = T("最近成品", "Recent outputs");
        if (openRecentButton is not null) openRecentButton.Text = T("打开成品目录", "Open output folder");
        foreach (var pair in sectionLabels)
            pair.Value.Text = SectionText(pair.Key);
        foreach (var pair in navButtons)
            pair.Value.Text = "     " + FeatureNavText(pair.Key);
    }

    private void BuildDashboard()
    {
        pageTitle.SetBounds(54, 38, 850, 46);
        pageTitle.Visible = false;
        pageTitle.ForeColor = Color.White;
        pageTitle.Font = new Font("Microsoft YaHei UI", 25f, FontStyle.Bold);
        pageSubtitle.SetBounds(57, 88, 900, 28);
        pageSubtitle.Visible = false;
        pageSubtitle.ForeColor = Muted;
        pageSubtitle.Font = new Font("Microsoft YaHei UI", 10.5f);

        var hero = new AuroraPanel(Path.Combine(AppContext.BaseDirectory, "assets", "aurora-hero.png"))
        {
            Location = new Point(54, 42),
            Size = new Size(1240, 540),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        heroTitle.SetBounds(52, 62, 520, 62);
        heroTitle.ForeColor = Color.White;
        heroTitle.BackColor = Color.Transparent;
        heroTitle.Font = new Font("Microsoft YaHei UI", 27f, FontStyle.Bold);
        heroDescription.SetBounds(55, 138, 540, 86);
        heroDescription.ForeColor = Color.FromArgb(188, 203, 223);
        heroDescription.BackColor = Color.Transparent;
        heroDescription.Font = new Font("Microsoft YaHei UI", 10.5f);
        modelTag.SetBounds(55, 238, 460, 30);
        modelTag.ForeColor = Color.FromArgb(105, 214, 255);
        modelTag.BackColor = Color.Transparent;
        modelTag.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
        primaryButton.SetBounds(54, 306, 204, 52);
        secondaryButton.SetBounds(272, 306, 190, 52);
        modelSelect.SetBounds(272, 306, 278, 52);
        modelSelect.FlatStyle = FlatStyle.Flat;
        modelSelect.BackColor = Color.FromArgb(20, 31, 47);
        modelSelect.ForeColor = Color.FromArgb(219, 229, 242);
        modelSelect.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        modelSelect.TextAlign = ContentAlignment.MiddleLeft;
        modelSelect.Padding = new Padding(12, 0, 0, 0);
        modelSelect.Visible = false;
        modelSelect.FlatAppearance.BorderSize = 1;
        modelSelect.FlatAppearance.BorderColor = Color.FromArgb(53, 78, 105);
        modelSelect.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 43, 62);
        modelSelect.Click += (_, _) => modelMenu.Show(modelSelect, new Point(0, modelSelect.Height + 2));
        modelMenu.BackColor = Color.FromArgb(20, 31, 47);
        modelMenu.ForeColor = Color.FromArgb(219, 229, 242);
        modelMenu.ShowImageMargin = false;
        modelMenu.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
        hero.Controls.Add(heroTitle);
        hero.Controls.Add(heroDescription);
        hero.Controls.Add(modelTag);
        hero.Controls.Add(primaryButton);
        hero.Controls.Add(modelSelect);
        hero.Controls.Add(secondaryButton);

        var recentCard = new CardPanel
        {
            Location = new Point(54, 516),
            Size = new Size(1040, 136),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Surface
        };
        recentTitleLabel = new Label
        {
            Text = T("最近成品", "Recent outputs"),
            Location = new Point(26, 20),
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold)
        };
        recentCard.Controls.Add(recentTitleLabel);
        recentName.SetBounds(27, 53, 720, 28);
        recentName.ForeColor = Color.FromArgb(216, 225, 238);
        recentName.Font = new Font("Microsoft YaHei UI", 10f);
        recentMeta.SetBounds(27, 82, 760, 22);
        recentMeta.ForeColor = Muted;
        recentMeta.Font = new Font("Microsoft YaHei UI", 8.8f);
        var openRecent = new AccentButton(false)
        {
            Text = T("打开成品目录", "Open output folder"),
            Size = new Size(210, 42),
            Location = new Point(890, 42),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        openRecentButton = openRecent;
        openRecent.Click += (_, _) => OpenFolder(CurrentOutputFolder());
        recentCard.Controls.Add(recentName);
        recentCard.Controls.Add(recentMeta);
        recentCard.Controls.Add(openRecent);

        dashboard.Controls.Add(pageTitle);
        dashboard.Controls.Add(pageSubtitle);
        dashboard.Controls.Add(hero);
        dashboard.Controls.Add(recentCard);
        dashboard.Resize += (_, _) => LayoutDashboard(hero, recentCard, openRecent);
        hero.Resize += (_, _) => LayoutHero(hero);
        LayoutDashboard(hero, recentCard, openRecent);
    }

    private void LayoutDashboard(Control hero, Control recentCard, Control openRecent)
    {
        var contentWidth = dashboard.ClientSize.Width;
        var contentHeight = dashboard.ClientSize.Height;
        var sideMargin = contentWidth >= 1500 ? 46 : contentWidth >= 1100 ? 38 : 28;
        var topMargin = 34;
        var available = Math.Max(760, contentWidth - sideMargin * 2);
        var recentHeight = 170;
        var cardGap = 30;
        var bottomPadding = 36;
        var maxHeroByHeight = Math.Max(560, contentHeight - topMargin - cardGap - recentHeight - bottomPadding);
        var heroHeight = Math.Min(maxHeroByHeight, Math.Max(620, (int)(contentHeight * 0.66)));
        hero.SetBounds(sideMargin, topMargin, available, heroHeight);
        recentCard.SetBounds(sideMargin, hero.Bottom + cardGap, available, recentHeight);
        openRecent.Location = new Point(Math.Max(24, recentCard.Width - openRecent.Width - 34), 48);
        LayoutHero(hero);
    }

    private void LayoutHero(Control hero)
    {
        var scale = Math.Max(1.0, Math.Min(1.34, hero.Width / 1280d));
        var left = (int)(58 * scale);
        var top = (int)(60 * scale);
        var contentWidth = Math.Max(620, Math.Min(860, (int)(hero.Width * 0.48)));
        heroTitle.SetBounds(left, top, contentWidth, (int)(74 * scale));
        heroDescription.SetBounds(left + 3, top + (int)(86 * scale), contentWidth, (int)(106 * scale));
        modelTag.SetBounds(left + 3, top + (int)(214 * scale), contentWidth, (int)(38 * scale));
        var gap = (int)(16 * scale);
        var buttonTop = top + (int)(270 * scale);
        if (modelSelectorActive)
        {
            primaryButton.SetBounds(left + 2, buttonTop, (int)(224 * scale), (int)(58 * scale));
            modelSelect.SetBounds(primaryButton.Right + gap, buttonTop, Math.Min((int)(380 * scale), Math.Max((int)(300 * scale), contentWidth - primaryButton.Width - gap)), primaryButton.Height);
            secondaryButton.SetBounds(left + 2, buttonTop + primaryButton.Height + (int)(14 * scale), (int)(224 * scale), (int)(48 * scale));
        }
        else
        {
            var primaryWidth = Math.Min((int)(280 * scale), Math.Max((int)(230 * scale), (contentWidth - gap) / 2));
            var secondaryWidth = Math.Min((int)(250 * scale), Math.Max((int)(210 * scale), contentWidth - primaryWidth - gap));
            primaryButton.SetBounds(left + 2, buttonTop, primaryWidth, (int)(58 * scale));
            secondaryButton.SetBounds(primaryButton.Right + gap, buttonTop, secondaryWidth, primaryButton.Height);
        }
    }

    private void SelectFeature(string key)
    {
        selectedFeature = key;
        foreach (var pair in navButtons)
        {
            pair.Value.BackColor = pair.Key == key ? Color.FromArgb(16, 39, 58) : SidebarBackground;
            pair.Value.ForeColor = pair.Key == key ? Color.FromArgb(83, 215, 255) : Color.FromArgb(183, 195, 212);
        }

        var spec = key switch
        {
            "tts" => new FeatureSpec(T("AI配音与声音克隆", "AI Voice and Voice Cloning"), T("克隆自然说话音色，生成中文旁白、角色对白，\r\n以及可直接使用的配音成品。", "Clone natural speaking voices for narration, character lines,\r\nand production-ready voice assets."), "IndexTTS2 · FP16", T("启动配音模型", "Start voice model"), T("打开配音成品", "Open voice outputs"), OpenTtsAsync, "AI配音"),
            "singing" => new FeatureSpec(T("歌声克隆", "Singing Voice Clone"), T("保留原歌曲的旋律与唱法，\r\n将演唱音色替换成你的参考人声。", "Keep the source melody and phrasing,\r\nthen replace the singer timbre with a reference voice."), "Seed-VC · 44.1 kHz", T("启动歌声克隆", "Start singing clone"), T("打开歌声成品", "Open singing outputs"), OpenSingingVoiceAsync, "AI歌声克隆"),
            "separator" => new FeatureSpec(T("去人声 / AI分轨", "Vocal Removal / AI Stems"), T("自动分离成两个结果：纯人声 Vocal + 纯伴奏 Instrumental，\r\n为翻唱、混音和扒谱准备干净素材。", "Automatically creates two files: clean Vocal and Instrumental,\r\nready for covers, remixing, and transcription."), "BS-RoFormer · GPU", T("选择音频开始分离", "Select audio to separate"), T("打开分离成品", "Open stem outputs"), SeparateAudioAsync, "AI分轨"),
            "pitch" => new FeatureSpec(T("AI扒谱", "AI Transcription"), T("分析独唱或单乐器音高，\r\n自动生成可编辑 MIDI 与音符事件表。", "Analyze solo vocal or single-instrument pitch,\r\nthen export editable MIDI and note-event tables."), "Basic Pitch · MIDI", T("选择音频开始扒谱", "Select audio to transcribe"), T("打开扒谱成品", "Open MIDI outputs"), TranscribeMusicAsync, "AI扒谱"),
            "subtitle" => new FeatureSpec(T("视频 AI 字幕", "Video AI Subtitles"), T("本地识别视频语音并生成时间轴字幕，\r\n随后在原生字幕工作台中快速校对。", "Recognize video speech locally and generate timed subtitles,\r\nthen polish them in the native subtitle editor."), "Subtitle Edit · Faster-Whisper", T("打开字幕工作台", "Open subtitle studio"), T("打开字幕成品", "Open subtitle outputs"), () => { OpenSubtitles(); return Task.CompletedTask; }, "AI字幕"),
            _ => new FeatureSpec(T("音乐创作", "Music Generation"), T("从中文灵感开始，生成完整歌曲、纯音乐与旋律，\r\n支持多种人声风格和音乐类型。", "Start from a Chinese or English idea and generate full songs,\r\ninstrumentals, melodies, and varied vocal/music styles."), "ACE-Step 1.5 · Local", T("启动音乐模型", "Start music model"), T("打开音乐成品", "Open music outputs"), OpenMusicAsync, "AI音乐")
        };
        pageTitle.Text = spec.Title;
        pageSubtitle.Text = T("本地创作工作台  ·  文件自动整理到 AI工作流", "Local production studio · Files are organized into AI Workflow");
        heroTitle.Text = spec.Title;
        heroDescription.Text = spec.Description;
        primaryButton.Text = spec.PrimaryText;
        secondaryButton.Text = spec.SecondaryText;
        currentPrimaryText = spec.PrimaryText;
        currentPrimaryAction = spec.PrimaryAction;
        primaryButton.SetClick(async () => await spec.PrimaryAction());
        secondaryButton.SetClick(() => OpenFolder(OutputFolder(spec.OutputName)));
        ConfigureModelSelector(key, spec.Model);
        RefreshRecent(spec.OutputName);
        ShowDashboard(T("就绪 · ", "Ready · ") + spec.Title);
    }

    private void ConfigureModelSelector(string key, string fallbackModel)
    {
        modelMenu.Items.Clear();
        var options = ModelOptions(key).ToList();
        foreach (var option in options)
        {
            var item = new ToolStripMenuItem(option.Name + (option.IsInstalled ? "" : T("（未安装）", " (not installed)"))) { Tag = option };
            item.BackColor = Color.FromArgb(20, 31, 47);
            item.ForeColor = option.IsInstalled ? Color.FromArgb(219, 229, 242) : Color.FromArgb(135, 150, 170);
            item.Click += (_, _) =>
            {
                selectedModelOption = option;
                UpdateSelectedModelLabel();
            };
            modelMenu.Items.Add(item);
        }
        if (key is "music" or "tts" or "singing" or "separator" or "pitch" or "subtitle")
        {
            modelMenu.Items.Add(new ToolStripSeparator());
            var install = new ToolStripMenuItem(T("安装当前选择模型...", "Install selected model..."))
            {
                BackColor = Color.FromArgb(20, 31, 47),
                ForeColor = Color.FromArgb(105, 214, 255)
            };
            install.Click += (_, _) => ChooseInstallLocationAndRunInstaller(key, selectedModelOption);
            modelMenu.Items.Add(install);
            var installDefault = new ToolStripMenuItem(T("选择位置并安装默认模型...", "Choose location and install default model..."))
            {
                BackColor = Color.FromArgb(20, 31, 47),
                ForeColor = Color.FromArgb(105, 214, 255)
            };
            installDefault.Click += (_, _) => ChooseInstallLocationAndRunInstaller(key, ModelOptions(key).FirstOrDefault());
            modelMenu.Items.Add(installDefault);
            var open = new ToolStripMenuItem(T("打开当前 LocalAI 目录", "Open current LocalAI folder"))
            {
                BackColor = Color.FromArgb(20, 31, 47),
                ForeColor = Color.FromArgb(219, 229, 242)
            };
            open.Click += (_, _) => OpenFolder(localAiRoot);
            modelMenu.Items.Add(open);
        }
        modelSelectorActive = options.Count > 0;
        modelSelect.Visible = modelSelectorActive;
        if (modelSelectorActive)
        {
            selectedModelOption = options[0];
            UpdateSelectedModelLabel();
        }
        else
        {
            selectedModelOption = null;
            modelTag.Text = "●  " + fallbackModel;
        }
        LayoutHero((Control)modelSelect.Parent!);
    }

    private void UpdateSelectedModelLabel()
    {
        if (selectedModelOption is not { } option) return;
        modelTag.Text = "●  " + option.Tag + (option.IsInstalled ? "" : T(" · 未安装", " · Not installed"));
        modelSelect.Text = option.Name + "    ▼";
        if (option.IsInstalled)
        {
            primaryButton.Text = currentPrimaryText;
            if (currentPrimaryAction is not null)
                primaryButton.SetClick(async () => await currentPrimaryAction());
        }
        else
        {
            primaryButton.Text = T("安装此模型", "Install this model");
            primaryButton.SetClick(() => ChooseInstallLocationAndRunInstaller(selectedFeature, option));
        }
    }

    private ModelOption SelectedModel()
        => selectedModelOption ?? ModelOptions(selectedFeature).First();

    private IEnumerable<ModelOption> ModelOptions(string feature)
        => DefaultModelOptions(feature).Concat(ExternalModelOptions(feature));

    private IEnumerable<ModelOption> DefaultModelOptions(string feature) => feature switch
    {
        "music" =>
        [
            new("ACE-Step 1.5", T("ACE-Step 1.5 · 本地模型", "ACE-Step 1.5 · Local model"), IsMusicInstalled()),
            new("YuE 音乐模型", T("YuE · 预留入口", "YuE · installable entry"), Directory.Exists(Path.Combine(localAiRoot, "YuE"))),
            new("Stable Audio Open", T("Stable Audio Open · 预留入口", "Stable Audio Open · installable entry"), Directory.Exists(Path.Combine(localAiRoot, "StableAudioOpen")))
        ],
        "tts" =>
        [
            new("IndexTTS2", T("IndexTTS2 · 本地 FP16", "IndexTTS2 · Local FP16"), IsTtsInstalled()),
            new("GPT-SoVITS", T("GPT-SoVITS · 预留入口", "GPT-SoVITS · installable entry"), Directory.Exists(Path.Combine(localAiRoot, "GPT-SoVITS"))),
            new("CosyVoice", T("CosyVoice · 预留入口", "CosyVoice · installable entry"), Directory.Exists(Path.Combine(localAiRoot, "CosyVoice")))
        ],
        "singing" =>
        [
            new("Seed-VC 44.1k", "Seed-VC · 44.1 kHz", IsSingingInstalled()),
            new("RVC WebUI", T("RVC WebUI · 预留入口", "RVC WebUI · installable entry"), Directory.Exists(Path.Combine(localAiRoot, "RVC-WebUI"))),
            new("DiffSinger", T("DiffSinger · 预留入口", "DiffSinger · installable entry"), Directory.Exists(Path.Combine(localAiRoot, "DiffSinger")))
        ],
        "separator" =>
        [
            new("BS-RoFormer 分轨", T("BS-RoFormer · 本地 GPU", "BS-RoFormer · Local GPU"), IsSeparatorInstalled())
        ],
        "pitch" =>
        [
            new("Basic Pitch 扒谱", "Basic Pitch · MIDI", IsPitchInstalled())
        ],
        "subtitle" =>
        [
            new("Subtitle Edit 字幕", "Subtitle Edit · Faster-Whisper", IsSubtitleInstalled())
        ],
        _ => []
    };

    private IEnumerable<ModelOption> ExternalModelOptions(string feature)
    {
        foreach (var entry in LoadModelCatalog())
        {
            if (!entry.Feature.Equals(feature, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrWhiteSpace(entry.Name)) continue;
            var tag = englishUi
                ? FirstNonEmpty(entry.TagEn, entry.TagZh, entry.Name)
                : FirstNonEmpty(entry.TagZh, entry.TagEn, entry.Name);
            yield return new ModelOption(entry.Name, tag, IsCatalogModelInstalled(entry), entry.InstallScript);
        }
    }

    private IEnumerable<ModelCatalogEntry> LoadModelCatalog()
    {
        if (modelCatalog is not null) return modelCatalog;
        modelCatalog = [];
        var paths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "models.json"),
            UserModelCatalogPath()
        };
        foreach (var path in paths)
        {
            try
            {
                if (!File.Exists(path)) continue;
                var catalog = JsonSerializer.Deserialize<ModelCatalogFile>(File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (catalog?.Models is null) continue;
                modelCatalog.AddRange(catalog.Models);
            }
            catch
            {
                // Invalid community model catalogs should not block the app from opening.
            }
        }
        return modelCatalog;
    }

    private bool IsCatalogModelInstalled(ModelCatalogEntry entry)
    {
        if (entry.InstalledPaths is null || entry.InstalledPaths.Length == 0)
            return Directory.Exists(Path.Combine(localAiRoot, entry.Name));
        return entry.InstalledPaths.Any(path =>
        {
            var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(localAiRoot, path);
            return File.Exists(fullPath) || Directory.Exists(fullPath);
        });
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";

    private void RefreshRecent(string outputName)
    {
        var folder = OutputFolder(outputName);
        var latest = Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path)).OrderByDescending(file => file.LastWriteTime).FirstOrDefault()
            : null;
        recentName.Text = latest?.Name ?? T("这里还没有成品", "No outputs yet");
        recentMeta.Text = latest is null
            ? T("完成第一次任务后，最新文件会显示在这里。", "After the first task finishes, the newest file will appear here.")
            : $"{latest.LastWriteTime:yyyy-MM-dd  HH:mm}  ·  {FormatSize(latest.Length)}";
    }

    private string CurrentOutputFolder() => selectedFeature switch
    {
        "tts" => OutputFolder("AI配音"),
        "singing" => OutputFolder("AI歌声克隆"),
        "separator" => OutputFolder("AI分轨"),
        "pitch" => OutputFolder("AI扒谱"),
        "subtitle" => OutputFolder("AI字幕"),
        _ => OutputFolder("AI音乐")
    };

    private bool IsMusicInstalled() => File.Exists(Path.Combine(MusicRoot, "acestep", "acestep_v15_pipeline.py")) &&
        (File.Exists(Path.Combine(MusicRoot, "python_embeded", "python.exe")) ||
         File.Exists(Path.Combine(MusicRoot, ".venv", "Scripts", "python.exe")));

    private bool IsTtsInstalled() => File.Exists(Path.Combine(TtsRoot, "webui.py")) &&
        File.Exists(Path.Combine(TtsRoot, ".venv", "Scripts", "python.exe"));

    private bool IsSingingInstalled() => File.Exists(Path.Combine(SeedVcRoot, "app_svc_local.py")) &&
        File.Exists(Path.Combine(SeedVcRoot, ".venv", "Scripts", "python.exe"));

    private bool IsSeparatorInstalled() => File.Exists(SeparatorExe) && Directory.Exists(SeparatorModels);

    private bool IsPitchInstalled() => File.Exists(BasicPitchExe);

    private bool IsSubtitleInstalled() => File.Exists(SubtitleExe);

    private void ChooseOutputLocation()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = T("选择 AI 成品输出目录。软件会在这里创建 AI音乐、AI配音、AI字幕等文件夹。",
                "Choose the AI output folder. Aurora will create AI music, voice, subtitles, and tool folders here."),
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(outputRoot) ? outputRoot : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        outputRoot = dialog.SelectedPath;
        Directory.CreateDirectory(outputRoot);
        SaveUserSettings();
        RefreshRecent(CurrentOutputName());
        ShowDashboard(T("输出位置已设置：", "Output folder set: ") + outputRoot);
    }

    private void OpenWebsite()
        => OpenUrl("https://swy2018.github.io/Aurora-Audio-Studio/");

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Aurora-Audio-Studio/" + AppVersion);
            var json = await http.GetStringAsync("https://api.github.com/repos/swy2018/Aurora-Audio-Studio/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.TryGetProperty("tag_name", out var tagNode) ? tagNode.GetString() ?? "" : "";
            var page = doc.RootElement.TryGetProperty("html_url", out var urlNode) ? urlNode.GetString() ?? "" : "";
            var latest = tag.TrimStart('v', 'V');
            if (latest.Equals(AppVersion, StringComparison.OrdinalIgnoreCase))
            {
                ShowDashboard(T($"当前已经是最新版：{AppVersion}", $"You are already on the latest version: {AppVersion}"));
                return;
            }
            ShowDashboard(T($"发现新版本：{tag}。已打开下载页。", $"New version found: {tag}. The download page has been opened."));
            if (!string.IsNullOrWhiteSpace(page)) OpenUrl(page);
        }
        catch
        {
            ShowDashboard(T("检查更新失败。你可以点“访问官网”手动查看最新版。",
                "Update check failed. Use Visit website to check the latest version manually."));
        }
    }

    private string CurrentOutputName() => selectedFeature switch
    {
        "tts" => "AI配音",
        "singing" => "AI歌声克隆",
        "separator" => "AI分轨",
        "pitch" => "AI扒谱",
        "subtitle" => "AI字幕",
        _ => "AI音乐"
    };

    private void ChooseInstallLocationAndRunInstaller(string feature, ModelOption? option)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = T("选择安装位置。软件会在你选的位置下面创建 LocalAI 文件夹。",
                "Choose an install location. Aurora will create a LocalAI folder inside it."),
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(localAiRoot) ? Directory.GetParent(localAiRoot)?.FullName ?? localAiRoot : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        localAiRoot = Path.Combine(dialog.SelectedPath, "LocalAI");
        Directory.CreateDirectory(localAiRoot);
        SaveUserSettings();
        var installOption = option ?? ModelOptions(feature).FirstOrDefault();
        WriteInstallerScript(feature, installOption);
        Process.Start(new ProcessStartInfo("powershell.exe",
        $"-NoExit -ExecutionPolicy Bypass -File \"{InstallerScriptPath()}\"")
        {
            UseShellExecute = true,
            WorkingDirectory = localAiRoot
        });
        ConfigureModelSelector(selectedFeature, modelTag.Text.TrimStart('●', ' '));
        ShowDashboard(T("已打开安装窗口。安装位置：", "Installer window opened. Install folder: ") + localAiRoot);
    }

    private string SettingsPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aurora Audio Studio", "settings.json");

    private string SettingsDirectory()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aurora Audio Studio");

    private string UserModelCatalogPath()
        => Path.Combine(SettingsDirectory(), "models.json");

    private string InstallerScriptPath()
        => Path.Combine(localAiRoot, "AuroraInstaller", "install_selected_models.ps1");

    private string InstallLogPath()
        => Path.Combine(localAiRoot, "Logs", "aurora-install.log");

    private void LoadUserSettings()
    {
        try
        {
            var path = SettingsPath();
            if (!File.Exists(path)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("localAiRoot", out var local) && !string.IsNullOrWhiteSpace(local.GetString()))
                localAiRoot = local.GetString()!;
            if (doc.RootElement.TryGetProperty("outputRoot", out var output) && !string.IsNullOrWhiteSpace(output.GetString()))
                outputRoot = output.GetString()!;
            if (doc.RootElement.TryGetProperty("language", out var language) && !string.IsNullOrWhiteSpace(language.GetString()))
                englishUi = language.GetString()!.Equals("en", StringComparison.OrdinalIgnoreCase);
        }
        catch { }
    }

    private void SaveUserSettings()
    {
        var path = SettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(new { localAiRoot, outputRoot, language = englishUi ? "en" : "zh" }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static string CustomInstallBlock(string modelName, string? installScript)
    {
        if (BuiltInInstallerModels.Contains(modelName)) return "";
        if (string.IsNullOrWhiteSpace(installScript))
        {
            return """
Write-Host '这是 models.json 里的扩展模型，但没有提供 installScript。'
Write-Host '请把模型文件放到 LocalAI 下对应目录，或在 models.json 中补充 installScript。'
""";
        }

        return installScript;
    }

    private static readonly HashSet<string> BuiltInInstallerModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACE-Step 1.5",
        "YuE 音乐模型",
        "Stable Audio Open",
        "IndexTTS2",
        "GPT-SoVITS",
        "CosyVoice",
        "Seed-VC 44.1k",
        "RVC WebUI",
        "DiffSinger",
        "BS-RoFormer 分轨",
        "Basic Pitch 扒谱",
        "Subtitle Edit 字幕"
    };

    private void WriteInstallerScript(string feature, ModelOption? option)
    {
        var modelName = option?.Name ?? feature;
        var scriptPath = InstallerScriptPath();
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(InstallLogPath())!);
        var customInstallBlock = CustomInstallBlock(modelName, option?.InstallScript);
        var script = $$"""
$ErrorActionPreference = 'Stop'
$root = '{{localAiRoot}}'
$log = '{{InstallLogPath()}}'
New-Item -ItemType Directory -Force -Path $root, (Split-Path $log) | Out-Null
Start-Transcript -Path $log -Append
Write-Host ''
Write-Host 'Aurora Audio Studio 模型安装器'
Write-Host '安装位置:' $root
Write-Host '选择模型:' '{{modelName}}'
Write-Host '说明: 只安装你在软件里选择的功能；网络和模型很大，耗时可能较长。'
Write-Host ''

function Need-Git {
  if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host '缺少 Git。请先安装 Git for Windows: https://git-scm.com/download/win'
    throw 'Git not found'
  }
}

function Need-Uv {
  if (-not (Get-Command uv -ErrorAction SilentlyContinue)) {
    py -m pip install -U uv
  }
}

Need-Git
Need-Uv

if ('{{modelName}}' -eq 'ACE-Step 1.5') {
  $dir = Join-Path $root 'ACE-Step-1.5'
  if (-not (Test-Path $dir)) { git clone https://github.com/ACE-Step/ACE-Step-1.5.git $dir }
  Push-Location $dir
  uv sync
  uv run python -m acestep.model_downloader --download-source modelscope
  Pop-Location
}

if ('{{modelName}}' -eq 'YuE 音乐模型') {
  $dir = Join-Path $root 'YuE'
  if (-not (Test-Path $dir)) { git clone https://github.com/multimodal-art-projection/YuE.git $dir }
  Push-Location $dir
  py -3.10 -m venv .venv
  .\.venv\Scripts\python.exe -m pip install -U pip
  if (Test-Path requirements.txt) { .\.venv\Scripts\pip.exe install -r requirements.txt }
  .\.venv\Scripts\pip.exe install -U 'huggingface_hub[cli]'
  Write-Host 'YuE 项目和依赖已安装。模型权重较大，首次运行或按项目说明会继续下载。'
  Pop-Location
}

if ('{{modelName}}' -eq 'Stable Audio Open') {
  $dir = Join-Path $root 'StableAudioOpen'
  if (-not (Test-Path $dir)) { git clone https://github.com/Stability-AI/stable-audio-tools.git $dir }
  Push-Location $dir
  py -3.10 -m venv .venv
  .\.venv\Scripts\python.exe -m pip install -U pip
  .\.venv\Scripts\pip.exe install -e .
  .\.venv\Scripts\pip.exe install -U 'huggingface_hub[cli]'
  .\.venv\Scripts\huggingface-cli.exe download stabilityai/stable-audio-open-1.0 --local-dir checkpoints\stable-audio-open-1.0
  Pop-Location
}

if ('{{modelName}}' -eq 'IndexTTS2') {
  $dir = Join-Path $root 'IndexTTS2'
  if (-not (Test-Path $dir)) { git clone https://github.com/index-tts/index-tts.git $dir }
  Push-Location $dir
  uv sync --extra webui --default-index 'https://mirrors.aliyun.com/pypi/simple'
  uv tool install 'modelscope'
  modelscope download --model IndexTeam/IndexTTS-2 --local_dir checkpoints
  Pop-Location
}

if ('{{modelName}}' -eq 'GPT-SoVITS') {
  $dir = Join-Path $root 'GPT-SoVITS'
  if (-not (Test-Path $dir)) { git clone https://github.com/RVC-Boss/GPT-SoVITS.git $dir }
  Push-Location $dir
  py -3.10 -m venv .venv
  .\.venv\Scripts\python.exe -m pip install -U pip
  if (Test-Path requirements.txt) { .\.venv\Scripts\pip.exe install -r requirements.txt }
  Write-Host 'GPT-SoVITS 已下载并安装依赖。模型权重请按项目首次启动提示下载。'
  Pop-Location
}

if ('{{modelName}}' -eq 'CosyVoice') {
  $dir = Join-Path $root 'CosyVoice'
  if (-not (Test-Path $dir)) { git clone https://github.com/FunAudioLLM/CosyVoice.git $dir }
  Push-Location $dir
  py -3.10 -m venv .venv
  .\.venv\Scripts\python.exe -m pip install -U pip
  if (Test-Path requirements.txt) { .\.venv\Scripts\pip.exe install -r requirements.txt }
  .\.venv\Scripts\pip.exe install -U modelscope
  modelscope download --model iic/CosyVoice2-0.5B --local_dir pretrained_models\CosyVoice2-0.5B
  Pop-Location
}

if ('{{modelName}}' -eq 'Seed-VC 44.1k') {
  $dir = Join-Path $root 'Seed-VC'
  if (-not (Test-Path $dir)) { git clone https://github.com/Plachtaa/seed-vc.git $dir }
  Push-Location $dir
  py -3.10 -m venv .venv
  .\.venv\Scripts\python.exe -m pip install -U pip
  .\.venv\Scripts\pip.exe install -r requirements.txt
  New-Item -ItemType Directory -Force -Path checkpoints\manual | Out-Null
  .\.venv\Scripts\pip.exe install -U 'huggingface_hub[cli]'
  .\.venv\Scripts\huggingface-cli.exe download Plachta/Seed-VC DiT_seed_v2_uvit_whisper_base_f0_44k_bigvgan_pruned_ft_ema.pth config_dit_mel_seed_uvit_whisper_base_f0_44k.yml --local-dir checkpoints\manual
  Pop-Location
}

if ('{{modelName}}' -eq 'RVC WebUI') {
  $dir = Join-Path $root 'RVC-WebUI'
  if (-not (Test-Path $dir)) { git clone https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI.git $dir }
  Push-Location $dir
  py -3.10 -m venv .venv
  .\.venv\Scripts\python.exe -m pip install -U pip
  if (Test-Path requirements.txt) { .\.venv\Scripts\pip.exe install -r requirements.txt }
  Write-Host 'RVC WebUI 已下载并安装依赖。预训练权重按项目首次启动提示下载。'
  Pop-Location
}

if ('{{modelName}}' -eq 'DiffSinger') {
  $dir = Join-Path $root 'DiffSinger'
  if (-not (Test-Path $dir)) { git clone https://github.com/openvpi/DiffSinger.git $dir }
  Push-Location $dir
  py -3.10 -m venv .venv
  .\.venv\Scripts\python.exe -m pip install -U pip
  if (Test-Path requirements.txt) { .\.venv\Scripts\pip.exe install -r requirements.txt }
  Write-Host 'DiffSinger 已下载并安装依赖。歌声模型权重需要按项目说明选择下载。'
  Pop-Location
}

if ('{{modelName}}' -eq 'BS-RoFormer 分轨') {
  $dir = Join-Path $root 'AudioTools'
  New-Item -ItemType Directory -Force -Path $dir, (Join-Path $dir 'models') | Out-Null
  py -3.10 -m venv (Join-Path $dir 'separator-env')
  & (Join-Path $dir 'separator-env\Scripts\python.exe') -m pip install -U pip audio-separator[gpu]
  Write-Host '分轨模型文件较大，如未自动下载，请把 model_bs_roformer_ep_317_sdr_12.9755.ckpt 放到:' (Join-Path $dir 'models')
}

if ('{{modelName}}' -eq 'Basic Pitch 扒谱') {
  $dir = Join-Path $root 'AudioTools'
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
  py -3.10 -m venv (Join-Path $dir 'pitch-env')
  & (Join-Path $dir 'pitch-env\Scripts\python.exe') -m pip install -U pip basic-pitch[onnx]
}

if ('{{modelName}}' -eq 'Subtitle Edit 字幕') {
  $dir = Join-Path $root 'SubtitleEdit'
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
  $api = Invoke-RestMethod 'https://api.github.com/repos/SubtitleEdit/subtitleedit/releases/latest'
  $asset = $api.assets | Where-Object { $_.name -like '*Windows*x64*.zip' } | Select-Object -First 1
  if (-not $asset) { throw '没有找到 Subtitle Edit Windows x64 zip' }
  $zip = Join-Path $root 'SubtitleEdit-Windows-x64.zip'
  Invoke-WebRequest $asset.browser_download_url -OutFile $zip
  Expand-Archive -LiteralPath $zip -DestinationPath $dir -Force
  $settings = Join-Path $dir 'Settings.json'
  if (Test-Path $settings) {
    $text = Get-Content -Raw -LiteralPath $settings
    $text = $text.Replace('"Language": "English"', '"Language": "ChineseSimplified"')
    $text = $text.Replace('"LastLanguage": "en-us"', '"LastLanguage": "zh-Hans"')
    Set-Content -LiteralPath $settings -Value $text -Encoding UTF8
  }
}

{{customInstallBlock}}

Write-Host ''
Write-Host '安装脚本执行完成。回到 Aurora 重新点击模型即可。'
Stop-Transcript
Pause
""";
        File.WriteAllText(scriptPath, script);
    }

    private static string FormatSize(long bytes) => bytes >= 1024L * 1024L
        ? $"{bytes / 1024d / 1024d:0.0} MB"
        : $"{Math.Max(1, bytes / 1024d):0} KB";

    public void RenderPreview(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        PerformLayout();
        Invalidate(true);
        Update();
        for (var i = 0; i < 4; i++)
        {
            Application.DoEvents();
            Thread.Sleep(75);
        }
        using var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height);
        DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path);
    }

    public void SelectPreviewFeature(string key) => SelectFeature(key);

    public void SetPreviewLanguage(string language)
    {
        englishUi = language.Equals("en", StringComparison.OrdinalIgnoreCase);
        ApplyStaticLanguage();
        SelectFeature(selectedFeature);
    }

    private sealed record FeatureSpec(string Title, string Description, string Model, string PrimaryText,
        string SecondaryText, Func<Task> PrimaryAction, string OutputName);
    private sealed record ModelOption(string Name, string Tag, bool IsInstalled, string? InstallScript = null);

    private sealed class ModelCatalogFile
    {
        public int Version { get; set; } = 1;
        public List<ModelCatalogEntry> Models { get; set; } = [];
    }

    private sealed class ModelCatalogEntry
    {
        public string Feature { get; set; } = "";
        public string Name { get; set; } = "";
        public string? TagZh { get; set; }
        public string? TagEn { get; set; }
        public string[]? InstalledPaths { get; set; }
        public string? InstallScript { get; set; }
    }

    private async Task OpenMusicAsync()
    {
        var option = SelectedModel();
        if (option.Name != "ACE-Step 1.5")
        {
            ShowDashboard(T($"{option.Name} 尚未安装或暂未接入启动器。当前可用：ACE-Step 1.5。",
                $"{option.Name} is not installed or not wired to the launcher yet. Available now: ACE-Step 1.5."));
            return;
        }
        StopProcess(ref seedVcProcess);
        var python = File.Exists(Path.Combine(MusicRoot, "python_embeded", "python.exe"))
            ? Path.Combine(MusicRoot, "python_embeded", "python.exe")
            : Path.Combine(MusicRoot, ".venv", "Scripts", "python.exe");
        if (!File.Exists(python))
        {
            ShowDashboard(T("音乐环境尚未安装完成。请在模型下拉菜单里点“安装/选择模型位置...”。",
                "Music environment is not ready. Use the model menu to install or choose a model location."));
            return;
        }

        if (musicProcess is null || musicProcess.HasExited)
        {
            var script = Path.Combine(MusicRoot, "acestep", "acestep_v15_pipeline.py");
            musicProcess = StartHidden(python,
                $"\"{script}\" --port 7860 --server-name 127.0.0.1 --language zh --config_path acestep-v15-turbo --lm_model_path acestep-5Hz-lm-1.7B --download-source modelscope --init_service true",
                MusicRoot, "music.log");
        }
        await NavigateWhenReadyAsync(MusicUrl, T("正在启动音乐模型，首次加载会较慢……", "Starting the music model. First load can be slow..."));
    }

    private async Task OpenTtsAsync()
    {
        var option = SelectedModel();
        if (option.Name != "IndexTTS2")
        {
            ShowDashboard(T($"{option.Name} 尚未安装或暂未接入启动器。当前可用：IndexTTS2。",
                $"{option.Name} is not installed or not wired to the launcher yet. Available now: IndexTTS2."));
            return;
        }
        StopProcess(ref seedVcProcess);
        var python = Path.Combine(TtsRoot, ".venv", "Scripts", "python.exe");
        if (!File.Exists(python))
        {
            ShowDashboard(T("配音环境尚未安装完成。请在模型下拉菜单里点“安装/选择模型位置...”。",
                "Voice environment is not ready. Use the model menu to install or choose a model location."));
            return;
        }

        if (ttsProcess is null || ttsProcess.HasExited)
        {
            var script = Path.Combine(TtsRoot, "webui.py");
            ttsProcess = StartHidden(python,
                $"\"{script}\" --host 127.0.0.1 --port 7861 --model_dir checkpoints --fp16",
                TtsRoot, "tts.log", useModelScope: true);
        }
        await NavigateWhenReadyAsync(TtsUrl, T("正在启动配音模型，首次加载会较慢……", "Starting the voice model. First load can be slow..."));
    }

    private async Task OpenSingingVoiceAsync()
    {
        var option = SelectedModel();
        if (option.Name != "Seed-VC 44.1k")
        {
            ShowDashboard(T($"{option.Name} 尚未安装或暂未接入启动器。当前可用：Seed-VC 44.1k。",
                $"{option.Name} is not installed or not wired to the launcher yet. Available now: Seed-VC 44.1k."));
            return;
        }
        var python = Path.Combine(SeedVcRoot, ".venv", "Scripts", "python.exe");
        var checkpoint = Path.Combine(SeedVcRoot, "checkpoints", "manual",
            "DiT_seed_v2_uvit_whisper_base_f0_44k_bigvgan_pruned_ft_ema_v2.pth");
        var config = Path.Combine(SeedVcRoot, "checkpoints", "manual",
            "config_dit_mel_seed_uvit_whisper_base_f0_44k.yml");
        if (!File.Exists(python) || !File.Exists(checkpoint) || !File.Exists(config))
        {
            ShowDashboard(T("歌声克隆环境尚未安装完成。请在模型下拉菜单里点“安装/选择模型位置...”。",
                "Singing voice clone environment is not ready. Use the model menu to install or choose a model location."));
            return;
        }

        StopProcess(ref musicProcess);
        StopProcess(ref ttsProcess);
        if (seedVcProcess is null || seedVcProcess.HasExited)
        {
            var output = OutputFolder("AI歌声克隆");
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(NumbaCacheRoot);
            var script = Path.Combine(SeedVcRoot, "app_svc_local.py");
            var environment = new Dictionary<string, string>
            {
                ["PATH"] = FfmpegDir + ";" + Environment.GetEnvironmentVariable("PATH"),
                ["GRADIO_SERVER_NAME"] = "127.0.0.1",
                ["GRADIO_SERVER_PORT"] = "7862",
                ["GRADIO_TEMP_DIR"] = output,
                ["AI_OUTPUT_DIR"] = output,
                ["HF_HUB_DISABLE_XET"] = "1",
                ["HF_HUB_ENABLE_HF_TRANSFER"] = "0",
                ["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1",
                ["NUMBA_CACHE_DIR"] = NumbaCacheRoot,
                ["PYTHONUTF8"] = "1"
            };
            seedVcProcess = StartHidden(python,
                $"\"{script}\" --checkpoint \"{checkpoint}\" --config \"{config}\" --fp16 True",
                SeedVcRoot, "seed-vc.log", environment: environment);
        }
        await NavigateWhenReadyAsync(SeedVcUrl, T("正在启动 44.1kHz 歌声克隆模型，请稍候……", "Starting the 44.1 kHz singing voice clone model..."));
    }

    private void OpenSubtitles()
    {
        if (!File.Exists(SubtitleExe))
        {
            ShowDashboard(T("字幕工具尚未安装完成。请先在模型下拉菜单或安装脚本里安装字幕工具。",
                "Subtitle tools are not installed yet. Use the model menu or installer first."));
            return;
        }
        EnsureSubtitleEditChinese();
        Process.Start(new ProcessStartInfo(SubtitleExe)
        {
            WorkingDirectory = OutputFolder("AI字幕"),
            UseShellExecute = true
        });
        status.Text = T("已打开原生字幕工具。请打开视频后选择：视频 → 音频转文字。",
            "Native subtitle editor opened. Open a video, then choose Video → Audio to text.");
    }

    private void EnsureSubtitleEditChinese()
    {
        try
        {
            var settings = Path.Combine(Path.GetDirectoryName(SubtitleExe)!, "Settings.json");
            if (!File.Exists(settings)) return;
            var text = File.ReadAllText(settings);
            text = text.Replace("\"Language\": \"English\"", "\"Language\": \"ChineseSimplified\"");
            text = text.Replace("\"LastLanguage\": \"en-us\"", "\"LastLanguage\": \"zh-Hans\"");
            File.WriteAllText(settings, text);
        }
        catch
        {
            // The tool still opens; the user can switch language manually from Options.
        }
    }

    private async Task SeparateAudioAsync()
    {
        if (!File.Exists(SeparatorExe) || !Directory.Exists(SeparatorModels))
        {
            ShowDashboard(T("AI 分轨环境尚未安装完成。请安装分轨工具和 BS-RoFormer 模型。",
                "AI stem separation is not ready. Install the separation tool and BS-RoFormer model."));
            return;
        }
        if (utilityProcess is { HasExited: false })
        {
            status.Text = T("当前已有音频任务在运行，请等待完成。", "An audio task is already running. Please wait.");
            return;
        }

        using var picker = CreateAudioPicker(T("选择要去人声或分轨的音频", "Choose audio for vocal removal / stem separation"));
        if (picker.ShowDialog(this) != DialogResult.OK) return;

        var output = OutputFolder("AI分轨");
        Directory.CreateDirectory(output);
        var startInfo = new ProcessStartInfo
        {
            FileName = SeparatorExe,
            WorkingDirectory = output,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(picker.FileName);
        startInfo.ArgumentList.Add("--model_filename");
        startInfo.ArgumentList.Add("model_bs_roformer_ep_317_sdr_12.9755.ckpt");
        startInfo.ArgumentList.Add("--model_file_dir");
        startInfo.ArgumentList.Add(SeparatorModels);
        startInfo.ArgumentList.Add("--output_dir");
        startInfo.ArgumentList.Add(output);
        startInfo.ArgumentList.Add("--output_format");
        startInfo.ArgumentList.Add("WAV");
        startInfo.ArgumentList.Add("--use_autocast");
        startInfo.Environment["PATH"] = FfmpegDir + ";" + Environment.GetEnvironmentVariable("PATH");
        var numbaCache = NumbaCacheRoot;
        Directory.CreateDirectory(numbaCache);
        startInfo.Environment["NUMBA_CACHE_DIR"] = numbaCache;

        await RunUtilityAsync(startInfo, "separator",
            T("正在使用 BS-RoFormer 分离人声和伴奏，请稍候……", "Separating vocal and instrumental with BS-RoFormer..."),
            T("分轨完成：已生成人声和伴奏 WAV。可点“打开分离成品”查看。",
                "Stem separation complete: vocal and instrumental WAV files were created. Use Open stem outputs to view them."),
            output);
    }

    private async Task TranscribeMusicAsync()
    {
        if (!File.Exists(BasicPitchExe))
        {
            ShowDashboard(T("AI 扒谱环境尚未安装完成。请安装扒谱工具。",
                "AI transcription is not ready. Install the transcription tool first."));
            return;
        }
        if (utilityProcess is { HasExited: false })
        {
            status.Text = T("当前已有音频任务在运行，请等待完成。", "An audio task is already running. Please wait.");
            return;
        }

        using var picker = CreateAudioPicker(T("选择要扒谱的音频（单独人声或单乐器效果最好）",
            "Choose audio to transcribe (solo voice or one instrument works best)"));
        if (picker.ShowDialog(this) != DialogResult.OK) return;

        var output = OutputFolder("AI扒谱");
        Directory.CreateDirectory(output);
        var startInfo = new ProcessStartInfo
        {
            FileName = BasicPitchExe,
            WorkingDirectory = output,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(output);
        startInfo.ArgumentList.Add(picker.FileName);
        startInfo.ArgumentList.Add("--model-serialization");
        startInfo.ArgumentList.Add("onnx");
        startInfo.ArgumentList.Add("--save-midi");
        startInfo.ArgumentList.Add("--save-note-events");
        startInfo.Environment["PYTHONUTF8"] = "1";
        var numbaCache = NumbaCacheRoot;
        Directory.CreateDirectory(numbaCache);
        startInfo.Environment["NUMBA_CACHE_DIR"] = numbaCache;

        await RunUtilityAsync(startInfo, "basic-pitch",
            T("正在分析音高并生成 MIDI，请稍候……", "Analyzing pitch and generating MIDI..."),
            T("扒谱完成：已生成 MIDI 和音符事件 CSV。可点“打开扒谱成品”查看。",
                "Transcription complete: MIDI and note-event CSV files were created. Use Open MIDI outputs to view them."),
            output);
    }

    private OpenFileDialog CreateAudioPicker(string title) => new()
    {
        Title = title,
        Filter = T("音频文件|*.wav;*.mp3;*.flac;*.m4a;*.aac;*.ogg;*.wma;*.opus|所有文件|*.*",
            "Audio files|*.wav;*.mp3;*.flac;*.m4a;*.aac;*.ogg;*.wma;*.opus|All files|*.*"),
        CheckFileExists = true,
        Multiselect = false
    };

    private async Task RunUtilityAsync(ProcessStartInfo startInfo, string logPrefix, string runningText,
        string successText, string outputFolder)
    {
        web.Visible = false;
        dashboard.Visible = true;
        status.BringToFront();
        status.Text = runningText;
        Cursor = Cursors.WaitCursor;
        var logDir = LogsRoot;
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, $"{logPrefix}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        Process? running = null;
        try
        {
            running = new Process { StartInfo = startInfo };
            utilityProcess = running;
            running.Start();
            var stdoutTask = running.StandardOutput.ReadToEndAsync();
            var stderrTask = running.StandardError.ReadToEndAsync();
            await running.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            File.WriteAllText(logPath, stdout + Environment.NewLine + stderr);

            if (running.ExitCode == 0)
            {
                status.Text = successText;
                OpenFolder(outputFolder);
            }
            else
            {
                status.Text = T($"任务失败（代码 {running.ExitCode}），日志：{logPath}",
                    $"Task failed (code {running.ExitCode}). Log: {logPath}");
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(logPath, ex.ToString());
            status.Text = T($"任务启动失败，日志：{logPath}", $"Task failed to start. Log: {logPath}");
        }
        finally
        {
            Cursor = Cursors.Default;
            if (ReferenceEquals(utilityProcess, running)) utilityProcess = null;
            running?.Dispose();
        }
    }

    private async Task NavigateWhenReadyAsync(string url, string waitingText)
    {
        navigationCancellation?.Cancel();
        navigationCancellation = new CancellationTokenSource();
        var token = navigationCancellation.Token;
        web.Visible = false;
        dashboard.Visible = true;
        status.BringToFront();
        status.Text = waitingText;

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        for (var i = 0; i < 900 && !token.IsCancellationRequested; i++)
        {
            try
            {
                using var response = await client.GetAsync(url, token);
                if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect)
                {
                    await web.EnsureCoreWebView2Async();
                    web.CoreWebView2.Settings.AreDevToolsEnabled = false;
                    web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                    web.Source = new Uri(url);
                    dashboard.Visible = false;
                    web.Visible = true;
                    web.BringToFront();
                    status.BringToFront();
                    status.Text = T("已连接本地模型。此窗口内运行，不会打开浏览器。",
                        "Connected to the local model. It runs inside this window; no browser opens.");
                    return;
                }
            }
            catch when (!token.IsCancellationRequested)
            {
                // Backend is still loading.
            }
            await Task.Delay(1000, token).ContinueWith(_ => { }, TaskScheduler.Default);
        }
        if (!token.IsCancellationRequested)
            ShowDashboard(T("模型启动超时，请查看日志目录：", "Model startup timed out. Check logs at: ") + LogsRoot);
    }

    private Process StartHidden(string fileName, string arguments, string workingDirectory, string logName,
        bool useModelScope = false, IReadOnlyDictionary<string, string>? environment = null)
    {
        var logDir = LogsRoot;
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, logName);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };
        if (useModelScope)
            process.StartInfo.Environment["USE_MODELSCOPE"] = "true";
        if (environment is not null)
            foreach (var item in environment)
                process.StartInfo.Environment[item.Key] = item.Value;
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) File.AppendAllText(logPath, e.Data + Environment.NewLine); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) File.AppendAllText(logPath, e.Data + Environment.NewLine); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private void StopBackends()
    {
        navigationCancellation?.Cancel();
        StopProcess(ref musicProcess);
        StopProcess(ref ttsProcess);
        StopProcess(ref seedVcProcess);
        StopProcess(ref utilityProcess);
    }

    private static void StopProcess(ref Process? process)
    {
        try
        {
            if (process is { HasExited: false }) process.Kill(entireProcessTree: true);
        }
        catch { }
        finally
        {
            process?.Dispose();
            process = null;
        }
    }

    private void ShowDashboard(string message)
    {
        web.Visible = false;
        dashboard.Visible = true;
        status.BringToFront();
        status.Text = message;
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private static void OpenUrl(string url)
        => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private string OutputFolder(string name) => Path.Combine(outputRoot, name);
}

internal sealed class AccentButton : Button
{
    private readonly bool primary;
    private EventHandler? activeHandler;
    private bool hovered;
    private bool pressed;

    public AccentButton(bool primary)
    {
        this.primary = primary;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = primary ? 0 : 1;
        FlatAppearance.BorderColor = Color.FromArgb(53, 78, 105);
        BackColor = primary ? Color.FromArgb(25, 183, 235) : Color.FromArgb(20, 31, 47);
        ForeColor = primary ? Color.FromArgb(3, 17, 26) : Color.FromArgb(219, 229, 242);
        Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        TextAlign = ContentAlignment.MiddleCenter;
        Padding = Padding.Empty;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(74, 211, 252) : Color.FromArgb(29, 43, 62);
        Resize += (_, _) => UpdateRegion();
    }

    public void SetClick(EventHandler handler)
    {
        if (activeHandler is not null) Click -= activeHandler;
        activeHandler = handler;
        Click += activeHandler;
    }

    public void SetClick(Action action) => SetClick((_, _) => action());

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = RoundedRectangle(new Rectangle(0, 0, Width, Height), 11);
        Region = new Region(path);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = false;
        pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        pressed = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var fill = primary
            ? (pressed ? Color.FromArgb(18, 156, 204) : hovered ? Color.FromArgb(74, 211, 252) : Color.FromArgb(25, 183, 235))
            : (pressed ? Color.FromArgb(15, 25, 39) : hovered ? Color.FromArgb(29, 43, 62) : Color.FromArgb(20, 31, 47));
        using var path = RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 11);
        using var brush = new SolidBrush(fill);
        e.Graphics.FillPath(brush, path);
        if (!primary)
        {
            using var pen = new Pen(Color.FromArgb(53, 78, 105));
            e.Graphics.DrawPath(pen, path);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal class CardPanel : Panel
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(38, 55, 78));
        using var path = RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 15);
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        using var path = RoundedRectangle(new Rectangle(0, 0, Width, Height), 15);
        Region = new Region(path);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class AuroraPanel : CardPanel
{
    private readonly Image? background;

    public AuroraPanel(string imagePath)
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(9, 17, 28);
        if (File.Exists(imagePath)) background = Image.FromFile(imagePath);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (background is null) return;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(background, ClientRectangle);
        using var shade = new LinearGradientBrush(ClientRectangle,
            Color.FromArgb(126, 5, 11, 19), Color.FromArgb(8, 5, 11, 19), LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(shade, ClientRectangle);
    }
}
