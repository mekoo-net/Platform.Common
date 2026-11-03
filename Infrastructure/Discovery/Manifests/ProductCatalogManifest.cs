using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.Common.Discovery;

/// <summary>计费产品清单在 Consul Service Meta 中的编码。服务注册时写，Billing 读出来建产品目录。</summary>
public static class ProductCatalogManifest
{
    public const string MetaKey = ConsulServiceMeta.Products;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = false,
    };

    public static string Encode(IEnumerable<ProductDeclaration> products)
    {
        var snapshot = products
            .Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .Select(p => new ProductDeclarationDto(
                p.Code.Trim(),
                string.IsNullOrWhiteSpace(p.DisplayName) ? p.Code.Trim() : p.DisplayName.Trim()))
            .ToArray();

        return JsonSerializer.Serialize(snapshot, SerializerOptions);
    }

    public static IReadOnlyList<ProductDeclaration> Decode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<ProductDeclaration>();

        try
        {
            var dtos = JsonSerializer.Deserialize<ProductDeclarationDto[]>(json, SerializerOptions);
            if (dtos is null || dtos.Length == 0)
                return Array.Empty<ProductDeclaration>();

            return dtos
                .Where(d => !string.IsNullOrWhiteSpace(d.Code))
                .Select(d => new ProductDeclaration
                {
                    Code = d.Code,
                    DisplayName = string.IsNullOrWhiteSpace(d.DisplayName) ? d.Code : d.DisplayName,
                })
                .ToArray();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Consul service meta contains invalid product manifest JSON.", ex);
        }
    }

    private sealed record ProductDeclarationDto(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("displayName")] string DisplayName);
}
