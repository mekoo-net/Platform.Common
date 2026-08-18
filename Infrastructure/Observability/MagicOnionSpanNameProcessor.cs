using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenTelemetry;

namespace Platform.Common.Observability;

/// <summary>
/// MagicOnion 的 gRPC 服务名等于 C# 接口名，OTel 会画出
/// <c>ILlmDispatchService/DispatchAsync</c>。接口名留给编译器和线上路径，
/// 瀑布图上改成 DispatchService / BillingService。
/// </summary>
public sealed class MagicOnionSpanNameProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        var rewritten = MagicOnionSpanNames.Rewrite(activity.DisplayName);
        if (rewritten != activity.DisplayName)
            activity.DisplayName = rewritten;

        RewriteTag(activity, "rpc.service");
    }

    private static void RewriteTag(Activity activity, string key)
    {
        if (activity.GetTagItem(key) is not string value || value.Length == 0)
            return;
        var rewritten = MagicOnionSpanNames.Rewrite(value);
        if (rewritten != value)
            activity.SetTag(key, rewritten);
    }
}

/// <summary>
/// <c>ILlmDispatchService</c> → <c>DispatchService</c>，
/// <c>ILlmBillingService</c> → <c>BillingService</c>，
/// <c>ILlmCatalogService</c> → <c>CatalogService</c>。
/// ASP.NET 入站还会带 <c>POST /</c> 前缀，剥掉后跟客户端 span 对齐。
/// </summary>
public static class MagicOnionSpanNames
{
    private static readonly Regex ILlmService = new(@"ILlm(\w+Service)", RegexOptions.Compiled);

    public static string Rewrite(string name)
    {
        if (string.IsNullOrEmpty(name) || name.IndexOf("ILlm", StringComparison.Ordinal) < 0)
            return name;

        var s = ILlmService.Replace(name, "$1");
        const string httpPrefix = "POST /";
        if (s.StartsWith(httpPrefix, StringComparison.Ordinal))
            s = s[httpPrefix.Length..];
        return s;
    }
}
