using System.Text.Json;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class TaskQueueService
{
    private readonly SettingsService settings;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, CancellationTokenSource> cancellations = [];
    private TaskCompletionSource pauseSignal = CompletedSignal();
    private readonly JsonSerializerOptions json = new() { WriteIndented = true };
    public List<AuroraTaskRecord> Items { get; private set; } = [];
    public event EventHandler? Changed;
    public bool IsPaused { get; private set; }
    private string StatePath => Path.Combine(settings.AppDataRoot, "tasks.json");

    public TaskQueueService(SettingsService settings)
    {
        this.settings = settings;
        Load();
    }

    public AuroraTaskRecord Create(string projectId, string title, string feature, string inputPath, string modelId, string preset = "recommended")
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
            QueueOrder = Items.Count(x => x.Status == AuroraTaskStates.Waiting),
            LogPath = Path.Combine(logFolder, $"task-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log")
        };
        Items.Insert(0, task);
        SaveChanged();
        return task;
    }

    public async Task<OperationResult> RunAsync(AuroraTaskRecord task, Func<IProgress<TaskExecutionProgress>, CancellationToken, Task<OperationResult>> work)
    {
        var cancellation = new CancellationTokenSource();
        var acquired = false;
        cancellations[task.Id] = cancellation;
        try
        {
            task.Status = AuroraTaskStates.Waiting; task.Stage = IsPaused ? "队列已暂停" : "等待本地引擎"; SaveChanged();
            await WaitUntilResumedAsync(cancellation.Token);
            await gate.WaitAsync(cancellation.Token);
            acquired = true;
            await WaitUntilResumedAsync(cancellation.Token);
            task.Status = AuroraTaskStates.Preparing; task.Stage = "正在准备模型与工作区"; task.StartedAt = DateTimeOffset.Now; task.Progress = .02; SaveChanged();
            cancellation.Token.ThrowIfCancellationRequested();
            task.Status = AuroraTaskStates.Running; task.Stage = "正在本机处理"; task.Progress = .03; SaveChanged();
            var progress = new Progress<TaskExecutionProgress>(value => Report(task, value));
            var result = await work(progress, cancellation.Token);
            task.Status = result.Success ? AuroraTaskStates.Completed : AuroraTaskStates.Failed;
            task.Progress = result.Success ? 1 : task.Progress;
            task.Stage = result.Success ? "处理完成" : "需要处理";
            task.Message = result.Message;
            task.OutputPath = result.Path ?? "";
            task.CompletedAt = DateTimeOffset.Now;
            SaveChanged();
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
            cancellations.Remove(task.Id);
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
        if (value.Percentage is { } percentage) task.Progress = Math.Clamp(percentage, task.Progress, .99);
        if (!string.IsNullOrWhiteSpace(value.Stage)) task.Stage = value.Stage;
        if (!string.IsNullOrWhiteSpace(value.LogLine))
        {
            task.Message = value.LogLine.Length > 240 ? value.LogLine[..240] : value.LogLine;
            if (!string.IsNullOrWhiteSpace(task.LogPath))
            {
                try { File.AppendAllText(task.LogPath, $"{DateTime.Now:HH:mm:ss}  {value.LogLine}{Environment.NewLine}"); } catch { }
            }
        }
        SaveChanged();
    }

    public void Cancel(string id)
    {
        if (cancellations.TryGetValue(id, out var cancellation)) cancellation.Cancel();
        var task = Items.FirstOrDefault(x => x.Id == id);
        if (task is not null && task.Status == AuroraTaskStates.Waiting)
        {
            task.Status = AuroraTaskStates.Canceled; task.Stage = "Canceled"; task.CompletedAt = DateTimeOffset.Now; SaveChanged();
        }
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
        catch { Items = []; }
        foreach (var task in Items.Where(x => x.Status is AuroraTaskStates.Preparing or AuroraTaskStates.Running))
        {
            task.Status = AuroraTaskStates.Interrupted;
            task.Stage = "Recovered after restart";
            task.Message = "Aurora closed before this task finished. You can retry it.";
        }
        TrimAndSave();
    }

    private void SaveChanged() { TrimAndSave(); Changed?.Invoke(this, EventArgs.Empty); }
    private void TrimAndSave()
    {
        Items = Items.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(settings.Current.TaskHistoryLimit, 20, 500)).ToList();
        Directory.CreateDirectory(settings.AppDataRoot);
        var temp = StatePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Items, json));
        File.Move(temp, StatePath, true);
    }
}
