using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.Common.Discovery;

/// <summary>
/// 路由 manifest 在 Consul Service Meta 中的编码。平台网关按它重建 YARP 路由。
/// </summary>
public static class ServiceRouteManifest
{
    public const string MetaKey = ConsulServiceMeta.Routes;
    public const string BaseUrlMetaKey = ConsulServiceMeta.BaseUrl;
    public const string PublicUrlMetaKey = ConsulServiceMeta.PublicUrl;
    public const string GrpcUrlMetaKey = ConsulServiceMeta.GrpcUrl;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = false,
    };

    public static string Encode(IEnumerable<RouteManifest> routes)
    {
        var snapshot = routes
            .Where(r => !string.IsNullOrWhiteSpace(r.Prefix))
            .Select(r => new RouteManifestDto(
                r.Prefix.Trim(),
                string.IsNullOrWhiteSpace(r.Policy) ? "Default" : r.Policy.Trim(),
                r.StripPrefix))
            .ToArray();

        return JsonSerializer.Serialize(snapshot, SerializerOptions);
    }

    public static IReadOnlyList<RouteManifest> Decode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<RouteManifest>();

        try
        {
            var dtos = JsonSerializer.Deserialize<RouteManifestDto[]>(json, SerializerOptions);
            if (dtos is null || dtos.Length == 0)
                return Array.Empty<RouteManifest>();

            return dtos
                .Where(d => !string.IsNullOrWhiteSpace(d.Prefix))
                .Select(d => new RouteManifest
                {
                    Prefix = d.Prefix,
                    Policy = string.IsNullOrWhiteSpace(d.Policy) ? "Default" : d.Policy,
                    StripPrefix = d.StripPrefix,
                })
                .ToArray();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Consul service meta contains invalid route manifest JSON.", ex);
        }
    }

    private sealed record RouteManifestDto(
        [property: JsonPropertyName("prefix")] string Prefix,
        [property: JsonPropertyName("policy")] string Policy,
        [property: JsonPropertyName("stripPrefix")] bool StripPrefix);
}
