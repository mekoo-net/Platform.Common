using Microsoft.Extensions.Configuration;

namespace Platform.Common.Hosting;

/// <summary>
/// <c>ConnectionStrings:*</c> 的统一读取规则。全平台只此一处解释这些节，服务里不要再自己 <c>Get&lt;T&gt;()</c>。
/// </summary>
/// <remarks>
/// 一个节点有三种合法写法，按这个顺序判定：
/// <list type="number">
///   <item>标量字符串 —— <c>Database: "Host=…;Port=…"</c>，整串塞进 <c>Url</c>，兼容历史配置；</item>
///   <item>映射 —— <c>Database: { Host: …, Port: … }</c>，绑定成结构化模型，**这是规范写法**；</item>
///   <item>缺失或 <c>null</c> —— 返回 <c>null</c>，由调用方决定是报错还是降级。</item>
/// </list>
/// 映射写法里同样可以放 <c>Url</c> 当底座，再用兄弟字段覆盖其中几项——托管数据库给整串、
/// 但连接池要自己调的场景走这条路。
/// </remarks>
public static class ConnectionSectionReader
{
    public static PostgresConnectionOptions? TryReadPostgres(
        IConfiguration configuration,
        string sectionName = PostgresConnectionOptions.SectionName) =>
        Read<PostgresConnectionOptions>(configuration, sectionName, static o => new PostgresConnectionOptions { Url = o },
            static o => !string.IsNullOrWhiteSpace(o.Url) || !string.IsNullOrWhiteSpace(o.Host));

    public static RedisConnectionOptions? TryReadRedis(
        IConfiguration configuration,
        string sectionName = RedisConnectionOptions.SectionName) =>
        Read<RedisConnectionOptions>(configuration, sectionName, static o => new RedisConnectionOptions { Url = o },
            static o => !string.IsNullOrWhiteSpace(o.Url) || !string.IsNullOrWhiteSpace(o.Host) || o.Endpoints.Count > 0);

    /// <summary>
    /// 按上述规则读任意连接节。平台专属的连接类型（如 Meeko.Common 里的 RabbitMQ）走这个入口，
    /// 而不是各自再写一遍标量/映射/缺失的判定——判定写歪一次，配置形态就多一种。
    /// </summary>
    /// <param name="fromScalar">标量写法（整串）如何变成模型。</param>
    /// <param name="hasEndpoint">模型里有没有真配上端点；没有则视为未配置，返回 <c>null</c>。</param>
    public static T? TryRead<T>(
        IConfiguration configuration,
        string sectionName,
        Func<string, T> fromScalar,
        Func<T, bool> hasEndpoint)
        where T : class, new() =>
        Read(configuration, sectionName, fromScalar, hasEndpoint);

    private static T? Read<T>(
        IConfiguration configuration,
        string sectionName,
        Func<string, T> fromScalar,
        Func<T, bool> hasEndpoint)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(sectionName);

        if (!string.IsNullOrWhiteSpace(section.Value))
            return fromScalar(section.Value.Trim());

        if (!section.GetChildren().Any())
            return null;

        T options;
        try
        {
            options = section.Get<T>() ?? new T();
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"配置节 {sectionName} 绑定失败：{ex.Message}", ex);
        }

        // 模板里键都列着、值全是 null 的情况等同未配置，交给调用方报"required"。
        return hasEndpoint(options) ? options : null;
    }
}
