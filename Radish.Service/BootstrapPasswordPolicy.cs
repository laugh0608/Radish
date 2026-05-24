namespace Radish.Service;

public static class BootstrapPasswordPolicy
{
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

    public static string? Validate(string loginName, string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "密码不能为空";
        }

        if (password != confirmPassword)
        {
            return "两次输入的密码不一致";
        }

        if (password.Length < 12)
        {
            return "密码长度至少 12 位";
        }

        if (!password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(IsSpecialCharacter))
        {
            return "密码必须同时包含大写字母、小写字母、数字和特殊字符";
        }

        if (!string.IsNullOrWhiteSpace(loginName) &&
            password.Contains(loginName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return "密码不能包含登录账号";
        }

        if (ForbiddenPasswords.Contains(password.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return "不能使用默认密码或常见弱密码";
        }

        return null;
    }

    private static bool IsSpecialCharacter(char value)
    {
        return !char.IsLetterOrDigit(value) && !char.IsWhiteSpace(value);
    }
}
