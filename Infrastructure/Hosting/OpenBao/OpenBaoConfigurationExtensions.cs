using Microsoft.Extensions.Configuration;
using Serilog;

namespace Platform.Common.Hosting;

public static class OpenBaoConfigurationExtensions
{
    /// <summary>
    /// 从 OpenBao 读密钥叠加到配置上。<b>放在加载 yaml 之后</b>：连接参数就写在 yaml 的顶层 <c>OpenBao</c> 节里，
    /// 而且后加的源覆盖先加的，OpenBao 里的值会盖掉 yaml 里的开发值 / <c>null</c> 占位。
    /// <paramref name="secrets"/> 是本服务读的路径，写死在代码里：<c>AddOpenBao("Platform/Keystone", "Platform/Shared")</c>。
    /// <para>
    /// 没配 <c>OpenBao</c> 节什么都不做，只读配置文件。配了但没有凭据（RoleId / SecretId 为空、凭据文件不存在）
    /// 记一条错误、同样只读配置文件——那不是等一等就会好的事，不值得为它在启动时干等 StartupTimeout。
    /// 凭据齐了就在这一行同步拉取；等到 StartupTimeout 还拉不到，退回配置文件里的值，之后刷新时再试。
    /// </para>
    /// </summary>
    public static IConfigurationManager AddOpenBao(this IConfigurationManager configuration, params string[] secrets)
    {
        var settings = OpenBaoConnectionResolver.Resolve(configuration);
        if (settings is null)
            return configuration;

        if (!settings.HasInlineCredentials && !File.Exists(settings.AppRoleFile))
        {
            Log.Error(
                "{Section} is set but has no credentials (RoleId / SecretId empty, {AppRoleFile} not found); " +
                "OpenBao is not used, falling back to the configuration files",
                OpenBaoOptions.SectionName, settings.AppRoleFile);
            return configuration;
        }

        configuration.Add(new OpenBaoConfigurationSource(settings, secrets));
        return configuration;
    }
}
