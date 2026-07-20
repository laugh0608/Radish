using Radish.Shared.Constants;

namespace Radish.Service;

internal sealed record BootstrapPasswordValidationFailure(
    string Message,
    string Code,
    object[] MessageArguments);

internal static class BootstrapPasswordPolicy
{
    private const int MinimumPasswordLength = 12;

    private static readonly string[] ForbiddenPasswords =
    [
        "admin",
        "admin123",
        "admin123456",
        "system",
        "system123456",
        "test",
        "test123456",
        "password",
        "password123",
        "password123456",
        "radish",
        "radish123456"
    ];

    public static BootstrapPasswordValidationFailure? Validate(
        string displayName,
        string email,
        string password,
        string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return Failure(
                "密码不能为空",
                BootstrapErrorCodes.PasswordRequired);
        }

        if (password != confirmPassword)
        {
            return Failure(
                "两次输入的密码不一致",
                BootstrapErrorCodes.PasswordConfirmationMismatch);
        }

        if (ForbiddenPasswords.Contains(password.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return Failure(
                "不能使用默认密码或常见弱密码",
                BootstrapErrorCodes.PasswordWeak);
        }

        if (password.Length < MinimumPasswordLength)
        {
            return Failure(
                $"密码长度至少 {MinimumPasswordLength} 位",
                BootstrapErrorCodes.PasswordLengthInvalid,
                MinimumPasswordLength);
        }

        if (!password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(IsSpecialCharacter))
        {
            return Failure(
                "密码必须同时包含大写字母、小写字母、数字和特殊字符",
                BootstrapErrorCodes.PasswordComplexityInvalid);
        }

        if (!string.IsNullOrWhiteSpace(displayName) &&
            password.Contains(displayName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                "密码不能包含展示名",
                BootstrapErrorCodes.PasswordContainsDisplayName);
        }

        var emailLocalPart = email.Split('@', 2)[0];
        if (!string.IsNullOrWhiteSpace(emailLocalPart) &&
            password.Contains(emailLocalPart.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                "密码不能包含邮箱前缀",
                BootstrapErrorCodes.PasswordContainsEmailLocalPart);
        }

        return null;
    }

    private static BootstrapPasswordValidationFailure Failure(
        string message,
        string code,
        params object[] messageArguments)
    {
        return new BootstrapPasswordValidationFailure(message, code, messageArguments);
    }

    private static bool IsSpecialCharacter(char value)
    {
        return !char.IsLetterOrDigit(value) && !char.IsWhiteSpace(value);
    }
}
