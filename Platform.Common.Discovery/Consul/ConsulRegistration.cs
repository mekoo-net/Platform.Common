using System.Net;
using Consul;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Platform.Common.Discovery;

/// <summary>
/// 一条 Consul 服务注册的完整生命周期：启动即注册，之后每 <see cref="Interval"/> 对照一次集群 catalog——
/// 不在了或内容变了就重新注册，带 TTL 检查的顺带续期；进程退出时注销。子类只用 <see cref="Describe"/> 说明注册成什么。
/// </summary>
/// <remarks>
/// <para>对照的是 catalog（<c>/v1/health/service</c>）而不是 agent 自己的列表：调用方 resolver、平台网关看的都是 catalog，
/// agent 的本地视图可能和它不一致（换主、数据恢复、连到了另一台 server）。只有 catalog 里确实没有才重新注册——
/// 重复注册会重建健康检查，状态先回到 critical，健康过滤的调用方会短暂看不到这个实例。</para>
/// <para>只有连不上 Consul（网络错误、超时、5xx）会在下一轮重试。Consul 拒绝这条注册（4xx，多半是声明写错）
/// 或任何意料之外的异常都直接抛出：后台服务的未处理异常默认记日志并停掉进程
/// （<see cref="BackgroundServiceExceptionBehavior.StopHost"/>），比在后台默默重试更早被发现。</para>
/// </remarks>
public abstract partial class ConsulRegistration(IConsulClient consul, ILogger logger) : BackgroundService
{
    private AgentServiceRegistration? _registered;

    /// <summary>对照 catalog 的间隔。带 TTL 检查时也是续期间隔，必须明显短于 TTL。</summary>
    protected virtual TimeSpan Interval => TimeSpan.FromSeconds(30);

    /// <summary>
    /// 这一轮要注册成什么。每轮都会调用：内容随实例状态变化的子类（如 provider 开始排空）据此重新注册，
    /// 内容固定的子类返回同一个对象即可。
    /// </summary>
    protected abstract AgentServiceRegistration Describe();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await SyncAsync(stoppingToken);
            }
            catch (Exception ex) when (IsConsulUnavailable(ex, stoppingToken))
            {
                LogConsulUnavailable(logger, ex, _registered?.ID ?? Describe().ID, Interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_registered is not { } registration)
            return;

        try
        {
            await consul.Agent.ServiceDeregister(registration.ID, cancellationToken);
            LogDeregistered(logger, registration.ID);
        }
        catch (Exception ex) when (IsConsulUnavailable(ex, CancellationToken.None))
        {
            // 退出时连不上 Consul：交给健康检查过期后的自动注销收尾。
            LogDeregisterFailed(logger, ex, registration.ID);
        }
    }

    private async Task SyncAsync(CancellationToken ct)
    {
        var registration = Describe();
        if (_registered is null)
        {
            await consul.Agent.ServiceRegister(registration, ct);
            LogRegistered(logger, registration.Name, registration.ID, registration.Address, registration.Port, registration.Meta);
        }
        else if (!SameRegistration(registration, _registered))
        {
            await consul.Agent.ServiceRegister(registration, ct);
            LogUpdated(logger, registration.Name, registration.ID, registration.Meta);
        }
        else if (!await IsInCatalogAsync(registration, ct))
        {
            await consul.Agent.ServiceRegister(registration, ct);
            LogRegisteredAgain(logger, registration.Name, registration.ID);
        }

        _registered = registration;

        if (registration.Check?.TTL is not null)
            await PassTtlAsync(registration, ct);
    }

    private async Task PassTtlAsync(AgentServiceRegistration registration, CancellationToken ct)
    {
        var checkId = $"service:{registration.ID}";
        try
        {
            await consul.Agent.PassTTL(checkId, "", ct);
        }
        catch (ConsulRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.InternalServerError)
        {
            // agent 手里已经没有这条检查（本地状态丢了，catalog 还没跟上），续不了期：补上注册再续。
            await consul.Agent.ServiceRegister(registration, ct);
            LogRegisteredAgain(logger, registration.Name, registration.ID);
            await consul.Agent.PassTTL(checkId, "", ct);
        }
    }

    private async Task<bool> IsInCatalogAsync(AgentServiceRegistration registration, CancellationToken ct)
    {
        var entries = await consul.Health.Service(registration.Name, string.Empty, false, ct);
        return entries.Response.Any(e => e.Service.ID == registration.ID);
    }

    private static bool SameRegistration(AgentServiceRegistration a, AgentServiceRegistration b) =>
        ReferenceEquals(a, b)
        || (a.ID == b.ID
            && a.Name == b.Name
            && a.Address == b.Address
            && a.Port == b.Port
            && (a.Tags ?? []).SequenceEqual(b.Tags ?? [])
            && SameMeta(a.Meta, b.Meta));

    private static bool SameMeta(IDictionary<string, string>? a, IDictionary<string, string>? b) =>
        (a?.Count ?? 0) == (b?.Count ?? 0)
        && (a is null || a.All(kv => b!.TryGetValue(kv.Key, out var value) && value == kv.Value));

    /// <summary>连不上 Consul 这一类：等下一轮。停机引起的取消不算，交给宿主正常收尾。</summary>
    private static bool IsConsulUnavailable(Exception ex, CancellationToken stoppingToken) => ex switch
    {
        HttpRequestException => true,
        TaskCanceledException => !stoppingToken.IsCancellationRequested,
        ConsulRequestException { StatusCode: >= HttpStatusCode.InternalServerError } => true,
        _ => false,
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered {Service} ({Id}) with Consul at {Address}:{Port}; meta {Meta}")]
    private static partial void LogRegistered(
        ILogger logger, string service, string id, string address, int port, IDictionary<string, string>? meta);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated the Consul registration of {Service} ({Id}); meta {Meta}")]
    private static partial void LogUpdated(ILogger logger, string service, string id, IDictionary<string, string>? meta);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Service} ({Id}) was missing from Consul; registered it again")]
    private static partial void LogRegisteredAgain(ILogger logger, string service, string id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consul unreachable while keeping {Id} registered; retrying in {Interval}")]
    private static partial void LogConsulUnavailable(ILogger logger, Exception exception, string id, TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deregistered {Id} from Consul")]
    private static partial void LogDeregistered(ILogger logger, string id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not deregister {Id} from Consul; its health check will expire it")]
    private static partial void LogDeregisterFailed(ILogger logger, Exception exception, string id);
}
