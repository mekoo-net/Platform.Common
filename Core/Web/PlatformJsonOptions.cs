using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Common.Web;

/// <summary>
/// 全平台 REST 端点统一 JSON 配置。
///
/// 契约根据 <c>web/meeko-console/docs/api/00-conventions.md</c> §10.4：
/// <list type="bullet">
///   <item><description>字段命名：camelCase（<c>createdAtUtc</c>、<c>iamUserUid</c>）</description></item>
///   <item><description>字符串枚举：snake_case 小写（<c>per_token</c>、<c>active</c>）</description></item>
///   <item><description>long 主键：按需通过 <see cref="LongToStringConverter"/> 序列化为 string，避免 JS Number 精度丢失</description></item>
///   <item><description>日期时间：Unix 毫秒（number），见 <see cref="EpochMillisDateTimeConverter"/></description></item>
/// </list>
///
/// 使用方：Demux / Bff / Keystone 三个 host 的 <c>Program.cs</c> 都调用
/// <see cref="AddPlatformJsonOptions"/> 一次即可，保证前端 console 接到的 wire 格式一致。
/// </summary>
public static class PlatformJsonOptions
{
    /// <summary>把 console 契约要求的 JSON 选项应用到 <see cref="JsonSerializerOptions"/>。</summary>
    public static void Apply(JsonSerializerOptions o)
    {
        o.PropertyNamingPolicy        = JsonNamingPolicy.CamelCase;
        o.DictionaryKeyPolicy         = JsonNamingPolicy.CamelCase;
        o.PropertyNameCaseInsensitive = true;

        // 字符串枚举走 snake_case：billingType "per_token" / providerStatus "auto_disabled" 等
        // 需要 snake_case 才能与 console docs 的 union 判别字段一致。单词枚举（"active" /
        // "disabled"）在 snake_case 下行为与 camelCase 等价，因此用 snake_case 不会丢信息。
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        o.Converters.Add(new EpochMillisDateTimeConverter());
        o.Converters.Add(new EpochMillisNullableDateTimeConverter());
    }

    /// <summary>同时注册 MVC <see cref="JsonOptions"/> 与 Minimal API <c>HttpJsonOptions</c>。</summary>
    public static IServiceCollection AddPlatformJsonOptions(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o => Apply(o.SerializerOptions));
        services.Configure<JsonOptions>(o => Apply(o.JsonSerializerOptions));
        return services;
    }
}

/// <summary>
/// 把 <see cref="long"/> 序列化为 JSON string，反序列化兼容 string 与 number。
///
/// 用法：在需要"避免 JS Number 精度"的 long id 字段上加
/// <c>[JsonConverter(typeof(LongToStringConverter))]</c>；token 计数 / 调用次数等
/// 普通 long 数值字段不必加（仍然走 JSON number）。
/// </summary>
public sealed class LongToStringConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (long.TryParse(s, out var v)) return v;
            throw new JsonException($"Cannot convert \"{s}\" to long.");
        }

        return reader.GetInt64();
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}

/// <summary>可空版本，用于 <c>long?</c> 字段。</summary>
public sealed class NullableLongToStringConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrEmpty(s)) return null;
            if (long.TryParse(s, out var v)) return v;
            throw new JsonException($"Cannot convert \"{s}\" to long.");
        }

        return reader.GetInt64();
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value.ToString());
    }
}

/// <summary>REST JSON 边界：<see cref="DateTime"/> ↔ Unix 毫秒。</summary>
public sealed class EpochMillisDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            throw new JsonException("Cannot convert null to DateTime.");

        var millis = ReadEpochMillis(ref reader);
        return DateTimeOffset.FromUnixTimeMilliseconds(millis).UtcDateTime;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        writer.WriteNumberValue(new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds());
    }

    internal static long ReadEpochMillis(ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => long.TryParse(reader.GetString(), out var v)
                ? v
                : throw new JsonException($"Cannot convert \"{reader.GetString()}\" to Unix milliseconds."),
            _ => throw new JsonException($"Unexpected token {reader.TokenType} when parsing Unix milliseconds."),
        };
    }
}

/// <summary>可空版本，用于 <c>DateTime?</c> 字段。</summary>
public sealed class EpochMillisNullableDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var millis = EpochMillisDateTimeConverter.ReadEpochMillis(ref reader);
        return DateTimeOffset.FromUnixTimeMilliseconds(millis).UtcDateTime;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var utc = value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        };
        writer.WriteNumberValue(new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds());
    }
}
