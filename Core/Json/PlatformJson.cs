using System.Text.Encodings.Web;
using System.Text.Json;

namespace Platform.Common.Json;

/// <summary>
/// 进程内共享的 <see cref="JsonSerializerOptions"/> 实例，供**手写**
/// <c>JsonSerializer.Serialize/Deserialize</c> 的场景使用：Redis 快照、Consul KV、
/// 数据库 json 列、SSE 荷载等不经过 MVC 管道、拿不到 DI 里 options 的地方。
///
/// <para><b>与 <see cref="Platform.Common.Web.PlatformJsonOptions"/> 的区别</b>：那个是
/// REST 边界契约（额外挂 snake_case 枚举转换与 Unix 毫秒日期转换），只应用在 ASP.NET
/// 的序列化器上。这里的实例**不带**那些转换器——Redis / KV / DB 里已经存着按 CLR 默认
/// 形状写下去的历史数据，套上 REST 转换器会读不回来。两者不可互换。</para>
///
/// <para><b>为什么必须共享实例</b>：System.Text.Json 没有全局默认值可改
/// （<c>JsonSerializerOptions.Default</c> 与 <c>.Web</c> 都是冻结的），每 <c>new</c> 一次
/// 就多一份独立的类型元数据缓存。实例首次使用后即冻结、可安全并发复用，因此正确做法是
/// 全进程共用少数几个静态实例，而不是各处各建一份等价副本。</para>
///
/// <para>这里的实例都已 <c>MakeReadOnly</c>：误改会立刻抛
/// <see cref="InvalidOperationException"/>，而不是在别处产生难查的行为漂移。</para>
/// </summary>
public static class PlatformJson
{
    /// <summary>
    /// Web 默认形状：属性名 camelCase、反序列化大小写不敏感、允许读取带引号的数字。
    /// 前端 console 与各服务间 REST 风格荷载用这个。
    /// </summary>
    public static readonly JsonSerializerOptions Web = Relaxed(JsonSerializerDefaults.Web);

    /// <summary>
    /// CLR 成员名原样输出（不套命名策略）、反序列化大小写敏感。
    /// 只在进程内自产自销、字段名即契约的荷载上用（Redis 快照、KV 值）。
    /// </summary>
    public static readonly JsonSerializerOptions Exact = Relaxed(JsonSerializerDefaults.General);

    private static JsonSerializerOptions Relaxed(JsonSerializerDefaults defaults)
    {
        // 默认编码器把汉字 / emoji 转义成 \uXXXX。这里的荷载都是 UTF-8 原文传输，
        // 转义只会让体积翻倍且难以肉眼排查。"Unsafe" 仅指直接嵌进 HTML <script> 的场景。
        var o = new JsonSerializerOptions(defaults)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        o.MakeReadOnly(populateMissingResolver: true);
        return o;
    }
}
