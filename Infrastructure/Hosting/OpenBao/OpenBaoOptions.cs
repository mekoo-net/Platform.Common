namespace Platform.Common.Hosting;

/// <summary>
/// 怎么连 OpenBao（yaml 顶层节 <c>OpenBao</c>）。这是引导配置：连上 OpenBao 之前就要用，只能写在本地 yaml，
/// <b>不放 <c>Connection</c></b>——<c>Connection</c> 整节都可以放进 OpenBao，把 OpenBao 自己的连接也塞进去，
/// 就成了"从 OpenBao 里读怎么连 OpenBao"。字段写法（Host / Port / Ssl / Url）和 Consul 一样。
/// 读哪些路径不在这里，写死在各服务的 <c>AddOpenBao(...)</c> 调用里——那是代码和 OpenBao 布局之间的约定，
/// 和 ServiceKeys 一样不交给部署机改。
/// </summary>
public sealed class OpenBaoOptions
{
    public const string SectionName = "OpenBao";

    /// <summary>可选的整串底座：<c>http://openbao:8200</c>。下面任何显式字段都会覆盖它。</summary>
    public string? Url { get; set; }

    public string? Host { get; set; }

    /// <summary>缺省 8200（OpenBao 开不开 TLS 都是这个端口）。</summary>
    public int? Port { get; set; }

    public bool? Ssl { get; set; }

    /// <summary>
    /// AppRole 凭据直接写在配置里。<b>只限 Debug 的仓库 yaml</b>：部署机的 <c>/opt/meeko</c> 整个挂进每个服务，
    /// 写在那里等于所有服务都拿得到所有服务的凭据——生产用 <see cref="AppRoleFile"/>。两个要么都写，要么都不写。
    /// </summary>
    public string? RoleId { get; set; }

    /// <inheritdoc cref="RoleId"/>
    public string? SecretId { get; set; }

    /// <summary>
    /// AppRole 凭据文件，内容 <c>{"role_id":"…","secret_id":"…"}</c>，没写 <see cref="RoleId"/> 时用它，
    /// 缺省 <c>/run/secrets/openbao-approle.json</c>。每个服务只挂自己那一份；每轮刷新都重读，轮换 secret_id 不用重启。
    /// </summary>
    public string? AppRoleFile { get; set; }

    public string? AppRoleMount { get; set; }

    /// <summary>多久重读一次，缺省 5 分钟。变了就触发配置重载，<c>IOptionsMonitor</c> 能看到新值。</summary>
    public TimeSpan? RefreshInterval { get; set; }

    /// <summary>
    /// 启动时连不上最多等多久，缺省 2 分钟。宿主机重启时 OpenBao 和各服务同时起来，要给它解封的时间；
    /// 等不到就退回配置文件里的值起来，之后每轮刷新再试。
    /// </summary>
    public TimeSpan? StartupTimeout { get; set; }
}
