using System.Diagnostics;

namespace Platform.Common.Observability;

/// <summary>
/// 平台共享 ActivitySource。业务自己的可以单独建（命名 Meeko.&lt;Service&gt;）。
/// </summary>
public static class PlatformActivitySource
{
    public const string Name = "Meeko";

    public static readonly ActivitySource Instance = new(Name);
}
