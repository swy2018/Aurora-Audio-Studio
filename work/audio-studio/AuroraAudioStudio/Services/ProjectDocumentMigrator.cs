using System.Text.Json;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public static class ProjectDocumentMigrator
{
    public const int CurrentSchemaVersion = 1;

    public static AuroraProject Read(string content)
    {
        using var document = JsonDocument.Parse(content);
        var schema = document.RootElement.TryGetProperty(nameof(AuroraProject.SchemaVersion), out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : 0;
        if (schema > CurrentSchemaVersion)
            throw new InvalidDataException($"This processing record requires schema version {schema}; this Aurora version supports {CurrentSchemaVersion}.");

        var project = JsonSerializer.Deserialize<AuroraProject>(content) ?? throw new InvalidDataException("The processing record is empty.");
        Validate(project);
        project.SchemaVersion = CurrentSchemaVersion;
        return project;
    }

    public static void Validate(AuroraProject project)
    {
        if (string.IsNullOrWhiteSpace(project.Id) || project.Name is null || project.ModelId is null || project.SourcePath is null
            || project.Feature is not ("music" or "voice" or "singing" or "separation" or "transcription" or "subtitles")
            || project.Parameters is null || project.Parameters.Any(p => p.Value is null)
            || project.TaskIds is null || project.TaskIds.Any(string.IsNullOrWhiteSpace)
            || project.Artifacts is null || project.Artifacts.Any(a => a is null || a.Path is null || a.Kind is null))
            throw new InvalidDataException("处理记录字段无效，原文件已保留，请检查后重新导入。");
        if (project.Parameters.TryGetValue("sources", out var sources))
        {
            try
            {
                var paths = JsonSerializer.Deserialize<List<string>>(sources);
                if (paths is null || paths.Any(string.IsNullOrWhiteSpace)) throw new JsonException("Invalid source list.");
            }
            catch (JsonException ex) { throw new InvalidDataException("处理记录中的素材列表无效，原文件已保留。", ex); }
        }
    }
}
