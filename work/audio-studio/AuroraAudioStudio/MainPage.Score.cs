using AuroraAudioStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using Windows.Media.Core;
using Windows.Storage;

namespace AuroraAudioStudio;

public sealed partial class MainPage
{
    private async void PrepareScoreButton_Click(object sender, RoutedEventArgs e) => await RunUiActionAsync(async () =>
    {
        var picker = new FileOpenPicker(App.MainWindow.AppWindow.Id);
        picker.FileTypeFilter.Add(".mid"); picker.FileTypeFilter.Add(".midi");
        var files = await picker.PickMultipleFilesAsync();
        if (files.Count > 0) await PrepareScoreAsync(files.Select(file => file.Path));
    });

    private async Task PrepareScoreAsync(IEnumerable<string> paths)
    {
        var document = await Task.Run(() => MidiScore.Read(paths));
        var form = new StackPanel { Spacing = 14, MaxWidth = 460 };
        var fieldIds = new Dictionary<string, int>();
        TextBlock Text(string key) => new() { Text = localization.Get(key), TextWrapping = TextWrapping.Wrap };
        FrameworkElement Field(string label, FrameworkElement control)
        {
            AutomationProperties.SetName(control, localization.Get(label));
            fieldIds.TryGetValue(label, out var occurrence);
            fieldIds[label] = occurrence + 1;
            AutomationProperties.SetAutomationId(control, label + "-" + occurrence);
            return new StackPanel { Spacing = 5, Children = { Text(label), control } };
        }
        ComboBox Choices(IEnumerable<(string Label, int Value)> items, int selected)
        {
            var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var item in items) box.Items.Add(new ComboBoxItem { Content = item.Label, Tag = item.Value });
            box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(item => (int)item.Tag == selected) ?? box.Items[0];
            return box;
        }
        int Choice(ComboBox box) => (int)((ComboBoxItem)box.SelectedItem).Tag;
        form.Children.Add(Text("scoreIntro"));
        form.Children.Add(new TextBlock { Text = string.Join("\n", document.Sources.Select(Path.GetFileName)), TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var grid = Choices(new[] { ("1/4",4), ("1/8",8), ("1/16",16), ("1/32",32), (localization.Get("scoreTriplet8"),12), (localization.Get("scoreTriplet16"),24) }, 16);
        AutomationProperties.SetAutomationId(grid, "score-grid");
        var beats = new NumberBox { Minimum = 1, Maximum = 12, Value = document.Beats, SmallChange = 1 };
        var unit = Choices(new[] { ("2",2), ("4",4), ("8",8), ("16",16) }, document.BeatType);
        var meter = new Grid { ColumnSpacing = 12 };
        meter.ColumnDefinitions.Add(new ColumnDefinition()); meter.ColumnDefinitions.Add(new ColumnDefinition());
        var beatField = Field("scoreBeats", beats); var unitField = Field("scoreBeatUnit", unit); Grid.SetColumn(unitField, 1);
        meter.Children.Add(beatField); meter.Children.Add(unitField);
        form.Children.Add(Field("scoreGrid", grid)); form.Children.Add(meter);
        var originalTempo = new CheckBox { Content = localization.Get("scoreKeepTempo"), IsChecked = true };
        AutomationProperties.SetAutomationId(originalTempo, "scoreKeepTempo");
        var tempo = new NumberBox { Minimum = 20, Maximum = 400, Value = 60000000d / document.Tempos[0].Microseconds, IsEnabled = false };
        originalTempo.Checked += (_, _) => tempo.IsEnabled = false;
        originalTempo.Unchecked += (_, _) => tempo.IsEnabled = true;
        form.Children.Add(originalTempo); form.Children.Add(Field("scoreTempo", tempo));
        var keyNames = new[] { "C♭", "G♭", "D♭", "A♭", "E♭", "B♭", "F", "C", "G", "D", "A", "E", "B", "F♯", "C♯" };
        var key = Choices(keyNames.Select((name, i) => (name, i - 7)), document.Fifths);
        var offset = new NumberBox { Minimum = 0, Maximum = 60, Value = 0, SmallChange = .05 };
        form.Children.Add(Field("scoreKey", key)); form.Children.Add(Field("scoreOffset", offset));
        if (document.MeterChanges) form.Children.Add(Text("scoreMeterWarning"));
        form.Children.Add(Text("scoreParts"));
        foreach (var part in document.Parts)
        {
            var name = new TextBox { Text = part.Name, MaxLength = 120 };
            name.TextChanged += (_, _) => part.Name = name.Text;
            var instruments = MidiScore.Instruments.ToList();
            if (!instruments.Contains(part.Instrument)) instruments.Add(part.Instrument);
            var instrument = Choices(instruments.Select((item, i) => (localization.Get(item.Key), i)), instruments.IndexOf(part.Instrument));
            instrument.SelectionChanged += (_, _) => part.Instrument = instruments[Choice(instrument)];
            var mode = Choices(new[] { (localization.Get("scorePreserve"),0), (localization.Get("scoreMelody"),1), (localization.Get("scoreChord"),4) }, 0);
            mode.SelectionChanged += (_, _) => part.Simplify = Choice(mode);
            form.Children.Add(Field("scorePartName", name));
            form.Children.Add(Field("scoreInstrument", instrument)); form.Children.Add(Field("scoreSimplify", mode));
        }
        var summary = Text("scoreLimits"); form.Children.Add(summary);
        var feedback = new InfoBar { IsOpen = false, IsClosable = false }; form.Children.Add(feedback);
        AutomationProperties.SetAutomationId(feedback, "scoreFeedback");
        var player = new MediaPlayerElement { AreTransportControlsEnabled = true, AutoPlay = false, Height = 90, Visibility = Visibility.Collapsed };
        form.Children.Add(Text("scoreAudioHelp")); form.Children.Add(player);
        var open = new Button { Content = localization.Get("打开成品目录"), IsEnabled = false };
        AutomationProperties.SetAutomationId(open, "scoreOpenFolder");
        form.Children.Add(open);
        ScoreExport? result = null;
        CancellationTokenSource? processing = null;
        var scroller = new ScrollViewer { Content = form, MaxHeight = 540, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = localization.Get("scorePrepare"),
            Content = scroller,
            PrimaryButtonText = localization.Get("scoreExport"), SecondaryButtonText = localization.Get("scoreOpenXml"),
            IsSecondaryButtonEnabled = false, CloseButtonText = localization.Get("close") };
        ScoreOptions Options() => new(Choice(grid), checked((int)beats.Value), Choice(unit), originalTempo.IsChecked == true ? null : tempo.Value, Choice(key), offset.Value);
        var check = new Button { Content = localization.Get("scoreCheck") };
        AutomationProperties.SetAutomationId(check, "scoreCheck");
        form.Children.Insert(form.Children.IndexOf(summary), check);
        check.Click += (_, _) =>
        {
            try
            {
                var prepared = MidiScore.Prepare(document, Options());
                summary.Text = localization.Format("scoreSummary", prepared.Parts.Count, prepared.Parts.Sum(part => part.Removed), prepared.Parts.Sum(part => part.OutOfRange));
            }
            catch (Exception ex) { feedback.Message = localization.Translate(ex.Message); feedback.Severity = InfoBarSeverity.Error; feedback.IsOpen = true; }
        };
        open.Click += async (_, _) => await RunUiActionAsync(() => { if (result is not null) backend.OpenFolder(result.Folder); return Task.CompletedTask; });
        dialog.SecondaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            try
            {
                if (result is not null && !await Windows.System.Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(result.MusicXml)))
                { feedback.Message = localization.Get("scoreOpenHint"); feedback.Severity = InfoBarSeverity.Warning; feedback.IsOpen = true; }
            }
            catch (Exception ex) { feedback.Message = localization.Translate(ex.Message); feedback.Severity = InfoBarSeverity.Error; feedback.IsOpen = true; }
        };
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true; var deferral = args.GetDeferral();
            try
            {
                var options = Options(); MidiScore.Prepare(document, options);
                var folder = await new FolderPicker(App.MainWindow.AppWindow.Id).PickSingleFolderAsync();
                if (folder is null) return;
                scroller.IsEnabled = false;
                feedback.Message = localization.Get("scoreExporting"); feedback.Severity = InfoBarSeverity.Informational; feedback.IsOpen = true;
                using var cancellation = new CancellationTokenSource(); processing = cancellation;
                result = await Task.Run(() => MidiScore.Export(document, options, folder.Path, cancellation.Token));
                var previous = player.Source as MediaSource; player.Source = null; previous?.Dispose();
                player.Source = MediaSource.CreateFromStorageFile(await StorageFile.GetFileFromPathAsync(result.Audio));
                player.Visibility = Visibility.Visible; open.IsEnabled = true; dialog.IsSecondaryButtonEnabled = true;
                feedback.Message = localization.Format("scoreSummary", document.Parts.Count, result.RemovedNotes, result.RangeWarnings) + "\n" + result.Folder;
                feedback.Severity = result.RangeWarnings > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
                SetStatus(localization.Format("artifactExported", result.Folder));
            }
            catch (OperationCanceledException) { feedback.Message = localization.Get("scoreCanceled"); feedback.Severity = InfoBarSeverity.Informational; }
            catch (Exception ex) { feedback.Message = localization.Translate(ex.Message); feedback.Severity = InfoBarSeverity.Error; feedback.IsOpen = true; }
            finally { processing = null; scroller.IsEnabled = true; deferral.Complete(); }
        };
        dialog.Closing += (_, args) => { if (processing is not null) { args.Cancel = true; processing.Cancel(); } };
        try { await ShowDialogAsync(dialog); }
        finally
        {
            var source = player.Source as MediaSource;
            if (player.MediaPlayer?.PlaybackSession.CanPause == true) player.MediaPlayer.Pause();
            player.Source = null; player.SetMediaPlayer(null); source?.Dispose();
        }
    }
}
