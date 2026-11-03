using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.Common.Discovery;

/// <summary>
/// Encodes/decodes scheduled task declarations into Consul service Meta.
/// </summary>
public static class ScheduleManifest
{
    public const string MetaKey = ConsulServiceMeta.Schedules;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = false,
    };

    public static string Encode(IEnumerable<ScheduledTaskDeclaration> schedules)
    {
        var snapshot = schedules
            .Where(s => !string.IsNullOrWhiteSpace(s.TaskKey) && !string.IsNullOrWhiteSpace(s.Cron))
            .Select(s => new ScheduledTaskDeclarationDto(
                s.TaskKey.Trim(),
                s.Cron.Trim()))
            .ToArray();

        return JsonSerializer.Serialize(snapshot, SerializerOptions);
    }

    public static IReadOnlyList<ScheduledTaskDeclaration> Decode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<ScheduledTaskDeclaration>();

        try
        {
            var dtos = JsonSerializer.Deserialize<ScheduledTaskDeclarationDto[]>(json, SerializerOptions);
            if (dtos is null || dtos.Length == 0)
                return Array.Empty<ScheduledTaskDeclaration>();

            return dtos
                .Where(d => !string.IsNullOrWhiteSpace(d.TaskKey) && !string.IsNullOrWhiteSpace(d.Cron))
                .Select(d => new ScheduledTaskDeclaration
                {
                    TaskKey = d.TaskKey,
                    Cron = d.Cron,
                })
                .ToArray();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Consul service meta contains invalid schedule manifest JSON.", ex);
        }
    }

    private sealed record ScheduledTaskDeclarationDto(
        [property: JsonPropertyName("taskKey")] string TaskKey,
        [property: JsonPropertyName("cron")] string Cron);
}
