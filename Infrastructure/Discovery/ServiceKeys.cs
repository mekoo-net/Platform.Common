namespace Platform.Common.Discovery;

/// <summary>
/// 各服务在 Consul 里登记的 Key——每个容器固定一个，注册方和调用方都只认它
/// （<c>consul:///billing</c>、网关 YARP cluster id、Jobs 的定时任务归属）。
/// </summary>
/// <remarks>
/// 写死在代码里，不进 yaml：这是跨仓库的契约，不是部署参数。
/// 一侧改了另一侧不会报错，只会表现为"No healthy Consul service instances found"。
/// <para>网关注册不是为了被调用，而是让遥测控制台从 catalog 发现它属于哪个业务世界
/// （<see cref="WorldServiceMeta"/>）。它们不带路由清单，平台网关只给它们建 cluster、不生成路由。
/// ToApi.Gateway 和 Observability 只解析不注册，所以没有 Key。</para>
/// <para>网关的 Key 不要取 <c>gateway</c>：OneApi.Gateway 已经以 <c>Gateway</c> 注册，
/// 两者只差大小写，而 Consul DNS 查找不区分大小写。</para>
/// </remarks>
public static class ServiceKeys
{
    // ── 平台（Meeko.Platform）
    public const string Keystone = "keystone";
    public const string Billing = "billing";
    public const string Notice = "notice";
    public const string Storage = "storage";
    public const string Bff = "bff";
    public const string Jobs = "jobs";
    public const string PlatformGateway = "platform-gateway";

    // ── 产品线
    /// <summary>Meeko.Demux 仓库。改名前是 <c>demux</c>。</summary>
    public const string Yomi = "yomi";
    public const string YomiGateway = "yomi-gateway";
    public const string Tavern = "tavern";
    /// <summary>WorldLine 网关（<c>Meeko.Tavern/Product/WorldLine/Gateway</c>）。</summary>
    public const string TavernGateway = "tavern-gateway";
    public const string ToApi = "toapi";
}
