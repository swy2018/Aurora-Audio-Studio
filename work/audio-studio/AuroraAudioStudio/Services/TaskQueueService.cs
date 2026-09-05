using System.Text.Json;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class TaskQueueService
{
    private readonly SettingsService settings;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, CancellationTokenSource> cancellations = [];
    private readonly object stateGate = new();
    private TaskCompletionSource pauseSignal = CompletedSignal();
    private readonly JsonSerializerOptions json = new() { WriteIndented = true };
    private DateTimeOffset lastProgressSave = DateTimeOffset.MinValue;
    public List<AuroraTaskRecord> Items { get; private set; } = [];
    public event EventHandler? Changed;
    public event EventHandler<AuroraTaskRecord>? ProgressChanged;
    private DateTimeOffset lastProgressNotification = DateTimeOffset.MinValue;
    public bool IsPaused { get; private set; }
    private string StatePath => Path.Combine(settings.AppDataRoot, "tasks.json");

    public TaskQueueService(SettingsService settings)
    {
        this.settings = settings;
        Load();
    }

    public AuroraTaskRecord Create(string projectId, string title, string feature, string inputPath, string modelId, string preset = "recommended", string sourceLanguage = "auto", string trackMode = "two-stem")
    {
        var logFolder = Path.Combine(settings.AppDataRoot, "TaskLogs");
        Directory.CreateDirectory(logFolder);
        var task = new AuroraTaskRecord
        {
            ProjectId = projectId,
            Title = title,
            Feature = feature,
            InputPath = inputPath,
            ModelId = modelId,
            Preset = preset,
            SourceLanguage = sourceLanguage,
            TrackMode = trackMode,
            QueueOrder = Items.Count(x => x.Status == AuroraTaskStates.Waiting),
            LogPath = Path.Combine(logFolder, $"task-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log")
        };
        lock (stateGate) { Items.Insert(0, task); SaveChanged(); }
        return task;
    }

    public async Task<OperationResult> RunAsync(AuroraTaskRecord task, Func<IProgress<TaskExecutionProgress>, CancellationToken, Task<OperationResult>> work)
    {
        using var cancellation = new CancellationTokenSource();
        var acquired = false;
        lock (stateGate)
        {
            if (cancellations.ContainsKey(task.Id)) return new(false, "此任务已经在队列中。");
            cancellations[task.Id] = cancellation;
        }
        try
        {
            if (task.Status == AuroraTaskStates.Canceled)
                return new OperationResult(false, "任务已取消。", task.LogPath);
            if (settings.Current.SafeMode) throw new InvalidOperationException("安全模式已启用，无法执行或重试任务。");
            task.Status = AuroraTaskStates.Waiting; task.Stage = IsPaused ? "队列已暂停" : "等待本地引擎"; SaveChanged();
            await WaitUntilResumedAsync(cancellation.Token);
            await gate.WaitAsync(cancellation.Token);
            acquired = true;
            await WaitUntilResumedAsync(cancellation.Token);
            if (settings.Current.SafeMode) throw new InvalidOperationException("安全模式已启用，无法执行或重试任务。");
            task.Status = AuroraTaskStates.Preparing; task.Stage = "正在准备模型与工作区"; task.StartedAt = DateTimeOffset.Now; task.Progress = .02; SaveChanged();
            cancellation.Token.ThrowIfCancellationRequested();
            task.Status = AuroraTaskStates.Running; task.Stage = "正在本机处理"; task.Progress = .03; SaveChanged();
            var progress = new Progress<TaskExecutionProgress>(value => Report(task, value));
            var result = await work(progress, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            lock (stateGate)
            {
                task.Status = result.Success ? AuroraTaskStates.Completed : AuroraTaskStates.Failed;
                task.Progress = result.Success ? 1 : task.Progress;
                task.Stage = result.Success ? "处理完成" : "需要处理";
                task.Message = result.Message;
                task.OutputPath = result.Success ? result.Path ?? "" : "";
                task.OutputFiles = result.Success ? result.Outputs?.ToList() ?? [] : [];
                task.Device = result.Device ?? "";
                task.CompletedAt = DateTimeOffset.Now;
                SaveChanged();
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            task.Status = AuroraTaskStates.Canceled; task.Stage = "已安全取消"; task.Message = "任务已取消。"; task.CompletedAt = DateTimeOffset.Now; SaveChanged();
            return new OperationResult(false, "任务已安全取消。", task.LogPath);
        }
        catch (Exception ex)
        {
            task.Status = AuroraTaskStates.Failed; task.Stage = "需要处理"; task.Message = ex.Message; task.CompletedAt = DateTimeOffset.Now; SaveChanged();
            return new OperationResult(false, ex.Message, task.LogPath);
        }
        finally
        {
            lock (stateGate) cancellations.Remove(task.Id);
            if (acquired) gate.Release();
        }
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        pauseSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        foreach (var task in Items.Where(x => x.Status == AuroraTaskStates.Waiting)) task.Stage = "队列已暂停";
        SaveChanged();
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        pauseSignal.TrySetResult();
        foreach (var task in Items.Where(x => x.Status == AuroraTaskStates.Waiting)) task.Stage = "等待本地引擎";
        SaveChanged();
    }

    private async Task WaitUntilResumedAsync(CancellationToken token)
    {
        while (IsPaused) await pauseSignal.Task.WaitAsync(token);
    }

    private void Report(AuroraTaskRecord task, TaskExecutionProgress value)
    {
        lock (stateGate)
        {
        if (task.Status is not (AuroraTaskStates.Running or AuroraTaskStates.Preparing)) return;
        if (value.Percentage is { } percentage) task.Progress = Math.Clamp(percentage, Math.Min(task.Progress, .99), .99);
        if (!string.IsNullOrWhiteSpace(value.Stage)) task.Stage = value.Stage;
        if (!string.IsNullOrWhiteSpace(value.LogLine))
        {
            task.Message = value.LogLine.Length > 240 ? value.LogLine[..240] : value.LogLine;
            if (!string.IsNullOrWhiteSpace(task.LogPath))
            {
                try { File.AppendAllText(task.LogPath, $"{DateTime.Now:HH:mm:ss}  {value.LogLine}{Environment.NewLine}"); } catch { }
            }
        }
        var now = DateTimeOffset.UtcNow;
        if (now - lastProgressSave >= TimeSpan.FromSeconds(1)) { lastProgressSave = now; TrimAndSave(); }
        if (now - lastProgressNotification >= TimeSpan.FromMilliseconds(250))
        {
            lastProgressNotification = now;
            ProgressChanged?.Invoke(this, task);
        }
        }
    }

    public void Cancel(string id)
    {
        lock (stateGate)
        {
        if (cancellations.TryGetValue(id, out var cancellation)) cancellation.Cancel();
        var task = Items.FirstOrDefault(x => x.Id == id);
        if (task is not null && task.Status is AuroraTaskStates.Waiting or AuroraTaskStates.Interrupted)
        {
            task.Status = AuroraTaskStates.Canceled; task.Stage = "已安全取消"; task.Message = "任务已取消。"; task.CompletedAt = DateTimeOffset.Now; SaveChanged();
        }
        }
    }

    public void CancelAll()
    {
        foreach (var task in Items.Where(x => x.CanCancel).ToArray()) Cancel(task.Id);
    }

    public void RegisterCompleted(AuroraTaskRecord task, IReadOnlyList<string> outputs)
    {
        task.Status = AuroraTaskStates.Completed; task.Progress = 1; task.Stage = "处理完成";
        task.OutputFiles = outputs.ToList(); task.OutputPath = outputs.FirstOrDefault() ?? "";
        task.StartedAt = task.CreatedAt; task.CompletedAt = DateTimeOffset.Now;
        SaveChanged();
    }

    private static TaskCompletionSource CompletedSignal()
    {
        var value = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        value.SetResult();
        return value;
    }

    private void Load()
    {
        try { if (File.Exists(StatePath)) Items = JsonSerializer.Deserialize<List<AuroraTaskRecord>>(File.ReadAllText(StatePath), json) ?? []; }
        catch
        {
            if (File.Exists(StatePath)) File.Copy(StatePath, StatePath + ".recovery-" + DateTime.UtcNow.Ticks, false);
            Items = [];
        }
        foreach (var task in Items.Where(x => x.Status is AuroraTaskStates.Waiting or AuroraTaskStates.Preparing or AuroraTaskStates.Running))
        {
            task.Status = AuroraTaskStates.Interrupted;
            task.Stage = "待恢复";
            task.Message = "上次未完成的任务已保留，可继续处理或取消。";
        }
        TrimAndSave();
    }

    private void SaveChanged() { lock (stateGate) TrimAndSave(); Changed?.Invoke(this, EventArgs.Empty); }
    private void TrimAndSave()
    {
        var retained = Items.Where(x => !x.CanCancel).OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(settings.Current.TaskHistoryLimit, 20, 500));
        Items = Items.Where(x => x.CanCancel).Concat(retained).OrderByDescending(x => x.CreatedAt).ToList();
        Directory.CreateDirectory(settings.AppDataRoot);
        var temp = StatePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Items, json));
        File.Move(temp, StatePath, true);
    }
}
