using System.Text.Json;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class TaskQueueService
{
    private readonly SettingsService settings;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, CancellationTokenSource> cancellations = [];
    private readonly JsonSerializerOptions json = new() { WriteIndented = true };
    public List<AuroraTaskRecord> Items { get; private set; } = [];
    public event EventHandler? Changed;
    private string StatePath => Path.Combine(settings.AppDataRoot, "tasks.json");

    public TaskQueueService(SettingsService settings)
    {
        this.settings = settings;
        Load();
    }

    public AuroraTaskRecord Create(string projectId, string title, string feature, string inputPath, string modelId)
    {
        var task = new AuroraTaskRecord { ProjectId = projectId, Title = title, Feature = feature, InputPath = inputPath, ModelId = modelId };
        Items.Insert(0, task);
        SaveChanged();
        return task;
    }

    public async Task<OperationResult> RunAsync(AuroraTaskRecord task, Func<CancellationToken, Task<OperationResult>> work)
    {
        var cancellation = new CancellationTokenSource();
        var acquired = false;
        cancellations[task.Id] = cancellation;
        try
        {
            task.Status = AuroraTaskStates.Waiting; task.Stage = "Waiting for local engine"; SaveChanged();
            await gate.WaitAsync(cancellation.Token);
            acquired = true;
            task.Status = AuroraTaskStates.Preparing; task.Stage = "Preparing model and workspace"; task.StartedAt = DateTimeOffset.Now; task.Progress = .08; SaveChanged();
            cancellation.Token.ThrowIfCancellationRequested();
            task.Status = AuroraTaskStates.Running; task.Stage = "Processing locally"; task.Progress = .2; SaveChanged();
            var result = await work(cancellation.Token);
            task.Status = result.Success ? AuroraTaskStates.Completed : AuroraTaskStates.Failed;
            task.Progress = result.Success ? 1 : task.Progress;
            task.Stage = result.Success ? "Completed" : "Needs attention";
            task.Message = result.Message;
            task.OutputPath = result.Path ?? "";
            task.CompletedAt = DateTimeOffset.Now;
            SaveChanged();
            return result;
        }
        catch (OperationCanceledException)
        {
            task.Status = AuroraTaskStates.Canceled; task.Stage = "Canceled safely"; task.Message = "Task canceled."; task.CompletedAt = DateTimeOffset.Now; SaveChanged();
            return new OperationResult(false, "任务已安全取消。", task.LogPath);
        }
        catch (Exception ex)
        {
            task.Status = AuroraTaskStates.Failed; task.Stage = "Needs attention"; task.Message = ex.Message; task.CompletedAt = DateTimeOffset.Now; SaveChanged();
            return new OperationResult(false, ex.Message, task.LogPath);
        }
        finally
        {
            cancellations.Remove(task.Id);
            if (acquired) gate.Release();
        }
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
