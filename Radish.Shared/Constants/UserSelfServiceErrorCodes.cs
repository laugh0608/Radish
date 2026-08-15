namespace Radish.Shared.Constants;

/// <summary>登录用户自服务设置与资料写入的稳定错误契约。</summary>
public static class UserSelfServiceErrorCodes
{
    public const string TimeZoneRequired = "UserSelfService.TimeZoneRequired";
    public const string TimeZoneInvalid = "UserSelfService.TimeZoneInvalid";
    public const string ProfileInvalid = "UserSelfService.ProfileInvalid";
    public const string ProfileEmailConflict = "UserSelfService.ProfileEmailConflict";
    public const string ProfileChangeLimited = "UserSelfService.ProfileChangeLimited";
    public const string ProfileConcurrentChange = "UserSelfService.ProfileConcurrentChange";
    public const string ProfilePolicyUnavailable = "UserSelfService.ProfilePolicyUnavailable";
    public const string UserUnavailable = "UserSelfService.UserUnavailable";
    public const string PasswordInvalid = "UserSelfService.PasswordInvalid";
    public const string CurrentPasswordIncorrect = "UserSelfService.CurrentPasswordIncorrect";
    public const string PasswordUpdateConflict = "UserSelfService.PasswordUpdateConflict";

    public static string ResolveMessageKey(string errorCode) => errorCode switch
    {
        TimeZoneRequired => "error.user_self_service.time_zone_required",
        TimeZoneInvalid => "error.user_self_service.time_zone_invalid",
        ProfileInvalid => "error.user_self_service.profile_invalid",
        ProfileEmailConflict => "error.user_self_service.profile_email_conflict",
        ProfileChangeLimited => "error.user_self_service.profile_change_limited",
        ProfileConcurrentChange => "error.user_self_service.profile_concurrent_change",
        ProfilePolicyUnavailable => "error.user_self_service.profile_policy_unavailable",
        UserUnavailable => "error.user_self_service.user_unavailable",
        PasswordInvalid => "error.user_self_service.password_invalid",
        CurrentPasswordIncorrect => "error.user_self_service.current_password_incorrect",
        PasswordUpdateConflict => "error.user_self_service.password_update_conflict",
        _ => throw new ArgumentOutOfRangeException(
            nameof(errorCode),
            errorCode,
            "Unknown user self-service error code.")
    };
}
