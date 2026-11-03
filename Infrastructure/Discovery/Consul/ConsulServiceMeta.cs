namespace Platform.Common.Discovery;

/// <summary>
/// 注册方写进 Consul Service Meta、平台网关 / Jobs / Billing / 各 resolver 读出来的键。
/// 两边共用这一份，改名不会只改到一边。业务层级树的键另见 <see cref="WorldServiceMeta"/>。
/// </summary>
public static class ConsulServiceMeta
{
    public const string Routes = "meeko-routes";
    public const string BaseUrl = "meeko-base-url";
    public const string PublicUrl = "meeko-public-url";
    public const string GrpcUrl = "meeko-grpc-url";
    public const string Products = "meeko-products";
    public const string Schedules = "meeko-schedules";
}
