namespace Platform.Common.Web;

/// <summary>Unix 毫秒 ↔ <see cref="DateTime"/>（UTC）转换；用于 query string 等不走 STJ 转换器的边界。</summary>
public static class EpochMillis
{
    public static DateTime? ToUtcDateTime(long? millis) =>
        millis is { } m ? DateTimeOffset.FromUnixTimeMilliseconds(m).UtcDateTime : null;

    public static DateTime ToUtcDateTime(long millis) =>
        DateTimeOffset.FromUnixTimeMilliseconds(millis).UtcDateTime;
}
