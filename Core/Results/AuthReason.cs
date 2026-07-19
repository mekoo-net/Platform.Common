namespace Platform.Common.Results;

/// <summary>
/// Keystone 鉴权域稳定 reason 码, 供前端 i18n 映射 (snake_case).
/// 写入 ProblemDetails.extensions.reason, 与 HTTP 类别码 extensions.code (GEN_xx) 解耦.
/// </summary>
public static class AuthReason
{
    public const string EmailInvalid = "email_invalid";
    public const string PasswordRequired = "password_required";
    public const string EmailNotRegistered = "email_not_registered";
    public const string LoginRateLimited = "login_rate_limited";
    public const string InvalidPassword = "invalid_password";
    public const string InvalidCredentials = "invalid_credentials";
    public const string IamLoginFieldsRequired = "iam_login_fields_required";
}
