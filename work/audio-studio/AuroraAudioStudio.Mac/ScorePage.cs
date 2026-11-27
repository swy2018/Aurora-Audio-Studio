using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using AuroraAudioStudio.Services;

namespace AuroraAudioStudio.Mac;

public sealed partial class MainWindow
{
    private async Task PrepareScoreAsync(IEnumerable<string>? sources = null)
    {
        if (sources is null)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = L("scorePrepare"), AllowMultiple = true,
                FileTypeFilter = [new FilePickerFileType("MIDI") { Patterns = ["*.mid", "*.midi"], AppleUniformTypeIdentifiers = ["public.midi-audio"] }] });
            sources = files.Select(file => file.TryGetLocalPath()).OfType<string>().ToArray();
        }
        var paths = sources.ToArray(); if (paths.Length == 0) return;
        var document = await Task.Run(() => MidiScore.Read(paths));
        var dialog = new Window { Title = L("scorePrepare"), Width = 780, Height = 780, MinWidth = 540, MinHeight = 480,
            Background = Surface, Foreground = Ink, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        ComboBox Choices(IEnumerable<(string Label, int Value)> items, int selected)
        {
            var entries = items.Select(item => new ComboBoxItem { Content = item.Label, Tag = item.Value }).ToArray();
            return new ComboBox { ItemsSource = entries, SelectedItem = entries.FirstOrDefault(item => (int)item.Tag! == selected) ?? entries[0], HorizontalAlignment = HorizontalAlignment.Stretch };
        }
        int Choice(ComboBox box) => (int)((ComboBoxItem)box.SelectedItem!).Tag!;
        var form = VStack(Txt(L("scoreIntro")), Txt(string.Join("\n", paths.Select(Path.GetFileName)), 12));
        var grid = Choices(new[] { ("1/4",4), ("1/8",8), ("1/16",16), ("1/32",32), (L("scoreTriplet8"),12), (L("scoreTriplet16"),24) }, 16);
        AutomationProperties.SetAutomationId(grid, "score-grid");
        var beats = new NumericUpDown { Minimum = 1, Maximum = 12, Value = document.Beats, Increment = 1 };
        var unit = Choices(new[] { ("2",2), ("4",4), ("8",8), ("16",16) }, document.BeatType);
        var originalTempo = new CheckBox { Content = L("scoreKeepTempo"), IsChecked = true };
        var tempo = new NumericUpDown { Minimum = 20, Maximum = 400, Value = (decimal)(60000000d / document.Tempos[0].Microseconds), IsEnabled = false };
        originalTempo.IsCheckedChanged += (_, _) => tempo.IsEnabled = originalTempo.IsChecked != true;
        var keys = new[] { "C♭", "G♭", "D♭", "A♭", "E♭", "B♭", "F", "C", "G", "D", "A", "E", "B", "F♯", "C♯" };
        var key = Choices(keys.Select((name, i) => (name, i - 7)), document.Fifths);
        var offset = new NumericUpDown { Minimum = 0, Maximum = 60, Value = 0, Increment = .05m };
        form.Children.Add(Field(L("scoreGrid"), grid)); form.Children.Add(Field(L("scoreBeats"), beats)); form.Children.Add(Field(L("scoreBeatUnit"), unit));
        form.Children.Add(originalTempo); form.Children.Add(Field(L("scoreTempo"), tempo)); form.Children.Add(Field(L("scoreKey"), key)); form.Children.Add(Field(L("scoreOffset"), offset));
        if (document.MeterChanges) form.Children.Add(Txt(L("scoreMeterWarning"), 12));
        form.Children.Add(Txt(L("scoreParts"), 20, true));
        foreach (var part in document.Parts)
        {
            var name = new TextBox { Text = part.Name, MaxLength = 120 };
            name.TextChanged += (_, _) => part.Name = name.Text ?? "";
            var instruments = MidiScore.Instruments.ToList(); if (!instruments.Contains(part.Instrument)) instruments.Add(part.Instrument);
            var instrument = Choices(instruments.Select((item, i) => (L(item.Key), i)), instruments.IndexOf(part.Instrument));
            instrument.SelectionChanged += (_, _) => part.Instrument = instruments[Choice(instrument)];
            var mode = Choices(new[] { (L("scorePreserve"),0), (L("scoreMelody"),1), (L("scoreChord"),4) }, 0);
            mode.SelectionChanged += (_, _) => part.Simplify = Choice(mode);
            form.Children.Add(Field(L("scorePartName"), name)); form.Children.Add(Field(L("scoreInstrument"), instrument)); form.Children.Add(Field(L("scoreSimplify"), mode));
        }
        var message = Txt(L("scoreLimits"), 12);
        var progress = new ProgressBar { IsIndeterminate = true, IsVisible = false };
        var busy = false; CancellationTokenSource? processing = null;
        ScoreExport? result = null;
        ScoreOptions Options() => new(Choice(grid), checked((int)(beats.Value ?? 4)), Choice(unit), originalTempo.IsChecked == true ? null : (double)(tempo.Value ?? 120), Choice(key), (double)(offset.Value ?? 0));
        var check = ActionButton(L("scoreCheck"), () =>
        {
            var arrangement = MidiScore.Prepare(document, Options());
            message.Text = workspace.Localization.Format("scoreSummary", arrangement.Parts.Count, arrangement.Parts.Sum(part => part.Removed), arrangement.Parts.Sum(part => part.OutOfRange));
            return Task.CompletedTask;
        });
        form.Children.Add(check); form.Children.Add(Txt(L("scoreAudioHelp"), 12));
        var listen = ActionButton(L("scoreListen"), () => result is null ? Task.CompletedTask : PreviewAsync(result.Audio)); listen.IsEnabled = false;
        var openXml = ActionButton(L("scoreOpenXml"), () => result is null ? Task.CompletedTask : OpenPathAsync(result.MusicXml)); openXml.IsEnabled = false;
        var open = ActionButton(L("打开成品目录"), () => result is null ? Task.CompletedTask : OpenPathAsync(result.Folder)); open.IsEnabled = false;
        var cancel = new Button { Content = L("cancel"), IsEnabled = false };
        cancel.Click += (_, _) => processing?.Cancel();
        var export = ActionButton(L("scoreExport"), async () =>
        {
            try
            {
                var options = Options(); MidiScore.Prepare(document, options);
                var folders = await dialog.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = L("scoreExport"), AllowMultiple = false });
                if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } folder) return;
                busy = true; form.IsEnabled = false; cancel.IsEnabled = true; progress.IsVisible = true;
                listen.IsEnabled = openXml.IsEnabled = open.IsEnabled = false;
                using var cancellation = new CancellationTokenSource(); processing = cancellation;
                message.Text = L("scoreExporting");
                result = await Task.Run(() => MidiScore.Export(document, options, folder, cancellation.Token));
                message.Text = workspace.Localization.Format("scoreSummary", document.Parts.Count, result.RemovedNotes, result.RangeWarnings) + "\n" + result.Folder;
            }
            catch (OperationCanceledException) { message.Text = L("scoreCanceled"); }
            catch (Exception ex) { message.Text = L(ex.Message); }
            finally
            {
                busy = false; processing = null; form.IsEnabled = true; cancel.IsEnabled = false; progress.IsVisible = false;
                listen.IsEnabled = openXml.IsEnabled = open.IsEnabled = result is not null;
            }
        });
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto"), Margin = new Thickness(24), RowSpacing = 12 };
        layout.Children.Add(new ScrollViewer { Content = form });
        Grid.SetRow(message, 1); layout.Children.Add(message); Grid.SetRow(progress, 2); layout.Children.Add(progress);
        var actions = Row(export, cancel, listen, openXml, open); Grid.SetRow(actions, 3); layout.Children.Add(actions);
        dialog.Content = layout;
        dialog.Closing += (_, e) => { if (busy) { e.Cancel = true; processing?.Cancel(); } };
        await dialog.ShowDialog(this);
    }
}
