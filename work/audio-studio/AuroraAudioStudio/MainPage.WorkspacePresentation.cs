using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;

namespace AuroraAudioStudio;

// Workspace presentation only; execution, validation and persistence remain in services.
public sealed partial class MainPage
{
    private bool applyingPreset;
    private string? watcherWarning;
    private CancellationTokenSource? inputInspection;
    private readonly List<(DateTime At, string Key, object[] Arguments)> activityHistory = [];
    private bool StorageAvailable => taskQueue.StorageWarning is null && settings.StorageWarning is null && watcherWarning is null;

    private void InitializeResultWatchers()
    {
        try
        {
            FileSystemWatcher Watch(string folder)
            {
                var root = Path.Combine(settings.AppDataRoot, folder);
                Directory.CreateDirectory(root);
                var watcher = new FileSystemWatcher(root, "*.json") { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite };
                FileSystemEventHandler changed = (_, _) => DispatcherQueue.TryEnqueue(async () => await ImportWorkbenchResultsAsync());
                watcher.Created += changed;
                watcher.Changed += changed;
                watcher.Renamed += (_, _) => DispatcherQueue.TryEnqueue(async () => await ImportWorkbenchResultsAsync());
                watcher.Error += (_, _) => DispatcherQueue.TryEnqueue(() => { watcherWarning = "resultWatchUnavailable"; RefreshStorageWarning(); });
                watcher.EnableRaisingEvents = true;
                return watcher;
            }
            activityWatcher ??= Watch("WorkbenchTasks");
            resultWatcher ??= Watch("WorkbenchReceipts");
            watcherWarning = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { watcherWarning = ex.Message; }
    }

    private void RefreshStorageWarning()
    {
        if (StorageInfo is null || taskQueue is null) return;
        StorageInfo.IsOpen = !StorageAvailable;
        StorageInfo.Title = localization.Get("storageUnavailable");
        StorageInfo.Message = StorageAvailable ? "" : localization.Get("storageUnavailableBody") + "\n" +
            localization.Translate(taskQueue.StorageWarning ?? settings.StorageWarning ?? watcherWarning ?? "");
        if (!StorageAvailable) OpenWorkbenchButton.IsEnabled = false;
        else if (!WorkbenchProgress.IsActive) OpenWorkbenchButton.IsEnabled = true;
        UpdateUtilityRunState();
    }

    private async void RestoreStorageButton_Click(object sender, RoutedEventArgs e)
    {
        settings.Load();
        taskQueue.TryRestoreStorage();
        activityWatcher?.Dispose(); activityWatcher = null;
        resultWatcher?.Dispose(); resultWatcher = null;
        InitializeResultWatchers();
        RefreshStorageWarning();
        if (StorageAvailable) { await ImportWorkbenchResultsAsync(); RefreshWorkspace(); }
    }

    private void RefreshPresetHint()
    {
        if (UtilityPresetHint is null || localization is null || UtilityModelPicker.SelectedItem is not ComboBoxItem { Tag: string id } item) return;
        var purpose = id switch
        {
            "transkun" or "piano" => "pianoSuitability",
            "yourmt3" => "multiInstrumentSuitability",
            "basic-pitch" => "basicPitchSuitability",
            _ => "presetModelOnly"
        };
        UtilityPresetHint.Text = localization.Format("presetModel", item.Content ?? id) + "\n" + localization.Get(purpose);
    }

    private async Task InspectSelectedInputAsync(string path)
    {
        inputInspection?.Cancel();
        using var inspection = new CancellationTokenSource();
        inputInspection = inspection;
        InputMetadataText.Text = localization.Get("inputInspecting");
        try
        {
            var metadata = await MediaInputPolicy.InspectAsync(path, settings.Current.LocalAiRoot, inspection.Token);
            if (ReferenceEquals(inputInspection, inspection)) InputMetadataText.Text = FormatMediaInfo(metadata);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (ReferenceEquals(inputInspection, inspection)) InputMetadataText.Text = localization.Format("inputCheckFailed", localization.Translate(ex.Message));
        }
        finally { if (ReferenceEquals(inputInspection, inspection)) inputInspection = null; }
    }

    private string FormatMediaInfo(MediaInfo value) => localization.Format("audioSummary", Duration(value.DurationSeconds),
        value.SampleRate, value.Channels) + $" · {value.Codec}";

    private static string Duration(double? seconds) => seconds is null ? "—" : TimeSpan.FromSeconds(seconds.Value).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");

    private string FormatArtifactInfo(ArtifactInfo? info) => ArtifactPresentation.Summary(info, localization);

    private void ClearActivityHistory() { activityHistory.Clear(); utilityLogs.Clear(); }
    private void AppendUtilityLog(string key, params object[] arguments)
    {
        activityHistory.Add((DateTime.Now, key, arguments));
        if (activityHistory.Count > 200) activityHistory.RemoveAt(0);
        RenderActivityHistory();
    }
    private void RenderActivityHistory()
    {
        utilityLogs.Clear();
        foreach (var entry in activityHistory)
            utilityLogs.Add($"{entry.At:HH:mm:ss}  " + (entry.Arguments.Length == 0 ? localization.Translate(entry.Key) : localization.Format(entry.Key, entry.Arguments)));
    }

    private async void SendArtifactToMidi_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ArtifactDisplay { CanTranscribe: true } artifact) await SendToMidiAsync([artifact.Path]);
    }
    private async void SendStemsToMidi_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ArtifactDisplay { IsStem: true } artifact) return;
        await SendToMidiAsync(projects.TranscriptionSources(artifact.ProjectId));
    }
    private async Task SendToMidiAsync(IEnumerable<string> paths)
    {
        try
        {
            var files = new List<StorageFile>();
            foreach (var path in paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase)) files.Add(await StorageFile.GetFileFromPathAsync(path));
            if (files.Count == 0) { SetStatus("成品文件已移动或删除，请检查原保存位置。"); return; }
            Shell.SelectedItem = TranscriptionItem;
            await AddSourcesAsync(files);
            // Stage only: never submit a batch or change the user's selected engine implicitly.
            UtilityInfo.IsOpen = true;
            UtilityInfo.Severity = InfoBarSeverity.Informational;
            UtilityInfo.Message = localization.Get("midiHandoffHint");
        }
        catch (Exception ex) { SetStatus(ex.Message); }
    }
}
