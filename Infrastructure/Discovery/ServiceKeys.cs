namespace Platform.Common.Discovery;

/// <summary>
/// 各服务在 Consul 里登记的 Key——每个容器固定一个，注册方和调用方都只认它
/// （<c>consul:///billing</c>、网关 YARP cluster id、Jobs 的定时任务归属）。
/// </summary>
/// <remarks>
/// 写死在代码里，不进 yaml：这是跨仓库的契约，不是部署参数。
/// 一侧改了另一侧不会报错，只会表现为"No healthy Consul service instances found"。
/// 网关、ToApi.Gateway、Observability 只解析不注册，所以没有 Key。
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

    // ── 产品线
    /// <summary>Meeko.Demux 仓库。改名前是 <c>demux</c>。</summary>
    public const string Yomi = "yomi";
    public const string Tavern = "tavern";
    public const string ToApi = "toapi";
}
