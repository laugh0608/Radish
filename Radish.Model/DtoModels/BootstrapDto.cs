using Radish.Shared.Constants;

namespace Radish.Model.DtoModels;

public sealed class BootstrapCreateAdminDto
{
    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}

public enum BootstrapAdminCreationStatus
{
    Created,
    AlreadyInitialized,
    EmailTaken,
    InvalidInput,
    ConcurrentInitialization,
    Failed
}

public sealed class BootstrapAdminCreationResult
{
    public BootstrapAdminCreationStatus Status { get; set; }

    public long UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string MessageKey { get; set; } = string.Empty;

    public object[] MessageArguments { get; set; } = Array.Empty<object>();

    public static BootstrapAdminCreationResult Created(long userId, string displayName, string email)
    {
        return new BootstrapAdminCreationResult
        {
            Status = BootstrapAdminCreationStatus.Created,
            UserId = userId,
            DisplayName = displayName,
            Email = email,
            Message = "首个管理员初始化完成"
        };
    }

    public static BootstrapAdminCreationResult Failed(
        BootstrapAdminCreationStatus status,
        string message,
        string code,
        params object[] messageArguments)
    {
        return new BootstrapAdminCreationResult
        {
            Status = status,
            Message = message,
            Code = code,
            MessageKey = BootstrapErrorCodes.ResolveMessageKey(code),
            MessageArguments = messageArguments
        };
    }
}
