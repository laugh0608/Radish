namespace Radish.Shared.Constants;

/// <summary>
/// 首次管理员初始化业务域的稳定错误码。
/// </summary>
public static class BootstrapErrorCodes
{
    public const string DisplayNameRequired = "Bootstrap.DisplayNameRequired";
    public const string DisplayNameLengthInvalid = "Bootstrap.DisplayNameLengthInvalid";
    public const string DisplayNameCharactersInvalid = "Bootstrap.DisplayNameCharactersInvalid";
    public const string EmailInvalid = "Bootstrap.EmailInvalid";
    public const string PasswordRequired = "Bootstrap.PasswordRequired";
    public const string PasswordConfirmationMismatch = "Bootstrap.PasswordConfirmationMismatch";
    public const string PasswordLengthInvalid = "Bootstrap.PasswordLengthInvalid";
    public const string PasswordComplexityInvalid = "Bootstrap.PasswordComplexityInvalid";
    public const string PasswordContainsDisplayName = "Bootstrap.PasswordContainsDisplayName";
    public const string PasswordContainsEmailLocalPart = "Bootstrap.PasswordContainsEmailLocalPart";
    public const string PasswordWeak = "Bootstrap.PasswordWeak";
    public const string AlreadyInitialized = "Bootstrap.AlreadyInitialized";
    public const string EmailTaken = "Bootstrap.EmailTaken";
    public const string ConcurrentInitialization = "Bootstrap.ConcurrentInitialization";
    public const string InitializationFailed = "Bootstrap.InitializationFailed";

    public static string ResolveMessageKey(string errorCode)
    {
        return errorCode switch
        {
            DisplayNameRequired => "error.bootstrap.display_name_required",
            DisplayNameLengthInvalid => "error.bootstrap.display_name_length_invalid",
            DisplayNameCharactersInvalid => "error.bootstrap.display_name_characters_invalid",
            EmailInvalid => "error.bootstrap.email_invalid",
            PasswordRequired => "error.bootstrap.password_required",
            PasswordConfirmationMismatch => "error.bootstrap.password_confirmation_mismatch",
            PasswordLengthInvalid => "error.bootstrap.password_length_invalid",
            PasswordComplexityInvalid => "error.bootstrap.password_complexity_invalid",
            PasswordContainsDisplayName => "error.bootstrap.password_contains_display_name",
            PasswordContainsEmailLocalPart => "error.bootstrap.password_contains_email_local_part",
            PasswordWeak => "error.bootstrap.password_weak",
            AlreadyInitialized => "error.bootstrap.already_initialized",
            EmailTaken => "error.bootstrap.email_taken",
            ConcurrentInitialization => "error.bootstrap.concurrent_initialization",
            InitializationFailed => "error.bootstrap.initialization_failed",
            _ => throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, "Unknown bootstrap error code.")
        };
    }
}
