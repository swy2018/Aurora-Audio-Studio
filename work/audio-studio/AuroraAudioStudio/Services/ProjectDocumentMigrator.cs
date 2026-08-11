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
        project.SchemaVersion = CurrentSchemaVersion;
        return project;
    }
}
