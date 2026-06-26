using System.Text.RegularExpressions;

namespace Radish.Shared.Security;

public static class PaymentPasscodeRules
{
    public const int Length = 6;
    public const int LegacySha256PasscodeVersion = 1;
    public const int Argon2idPasscodeVersion = 2;
    public const int CurrentPasscodeVersion = Argon2idPasscodeVersion;
    public const string NumericPattern = @"^\d{6}$";
    public const string RepeatedDigitPattern = @"^(\d)\1{5}$";
    public const string EmptyErrorMessage = "支付口令不能为空";
    public const string FormatErrorMessage = "支付口令必须为6位数字";
    public const string RepeatedDigitErrorMessage = "支付口令不能为6个相同数字";
    public const string UpgradeRequiredErrorMessage = "旧支付口令已废弃，请前往安全设置重置新的6位数字支付口令";
    public const string UpgradeRequiredStatusText = "旧口令已废弃";
    public const string UpgradeRequiredSuggestion = "检测到您仍在使用旧支付口令，旧规则已废弃，请尽快重置为新的6位数字支付口令";

    public static bool IsFormatValid(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, NumericPattern);
    }

    public static bool IsRepeatedDigits(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, RepeatedDigitPattern);
    }

    public static bool IsAccepted(string? value)
    {
        return IsFormatValid(value) && !IsRepeatedDigits(value);
    }

    public static bool IsSequential(string? value)
    {
        if (!IsFormatValid(value) || value == null)
        {
            return false;
        }

        return IsStepSequence(value, 1) || IsStepSequence(value, -1);
    }

    public static int GetStrengthLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (!IsFormatValid(value))
        {
            return 0;
        }

        if (IsRepeatedDigits(value))
        {
            return 1;
        }

        if (IsSequential(value))
        {
            return 2;
        }

        var uniqueDigitCount = value.Distinct().Count();
        if (uniqueDigitCount <= 2)
        {
            return 3;
        }

        if (uniqueDigitCount == 3)
        {
            return 4;
        }

        return 5;
    }

    public static string? GetValidationMessage(string? value, bool allowRepeatedDigits = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EmptyErrorMessage;
        }

        if (!IsFormatValid(value))
        {
            return FormatErrorMessage;
        }

        if (!allowRepeatedDigits && IsRepeatedDigits(value))
        {
            return RepeatedDigitErrorMessage;
        }

        return null;
    }

    public static bool RequiresUpgrade(int? passcodeVersion)
    {
        return !passcodeVersion.HasValue
               || passcodeVersion < LegacySha256PasscodeVersion
               || passcodeVersion > CurrentPasscodeVersion;
    }

    public static bool CanVerifyAndUpgrade(int? passcodeVersion)
    {
        return passcodeVersion == LegacySha256PasscodeVersion;
    }

    public static bool UsesCurrentHashVersion(int? passcodeVersion)
    {
        return passcodeVersion == CurrentPasscodeVersion;
    }

    private static bool IsStepSequence(string value, int step)
    {
        for (var index = 1; index < value.Length; index++)
        {
            if (value[index] - value[index - 1] != step)
            {
                return false;
            }
        }

        return true;
    }
}
