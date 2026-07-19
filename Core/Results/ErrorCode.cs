namespace Platform.Common.Results;

/// <summary>
/// 平台统一错误码。前缀 5 字符；与 HTTP / gRPC 状态码解耦，便于跨协议复用。
/// </summary>
public static class ErrorCode
{
    public const string Unknown          = "GEN_00";
    public const string Validation       = "GEN_01";
    public const string NotFound         = "GEN_02";
    public const string Conflict         = "GEN_03";
    public const string Unauthorized     = "GEN_04";
    public const string Forbidden        = "GEN_05";
    public const string TooManyRequests  = "GEN_06";
    public const string Upstream         = "GEN_07";
    public const string DependencyDown   = "GEN_08";
    public const string Timeout          = "GEN_09";
    public const string NotImplemented   = "GEN_10";
}
