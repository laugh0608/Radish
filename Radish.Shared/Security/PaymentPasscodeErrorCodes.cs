namespace Radish.Shared.Security;

public static class PaymentPasscodeErrorCodes
{
    public const string Required = "PaymentPassword.Required";
    public const string FormatInvalid = "PaymentPassword.FormatInvalid";
    public const string RepeatedDigits = "PaymentPassword.RepeatedDigits";
    public const string ConfirmationMismatch = "PaymentPassword.ConfirmationMismatch";
    public const string AlreadyConfigured = "PaymentPassword.AlreadyConfigured";
    public const string NotConfigured = "PaymentPassword.NotConfigured";
    public const string Invalid = "PaymentPassword.Invalid";
    public const string Locked = "PaymentPassword.Locked";
    public const string UpgradeRequired = "PAYMENT_PASSCODE_UPGRADE_REQUIRED";

    public static string ResolveMessageKey(string? errorCode)
    {
        return errorCode switch
        {
            Required => "error.payment_password.required",
            FormatInvalid => "error.payment_password.format_invalid",
            RepeatedDigits => "error.payment_password.repeated_digits",
            ConfirmationMismatch => "error.payment_password.confirmation_mismatch",
            AlreadyConfigured => "error.payment_password.already_configured",
            NotConfigured => "error.payment_password.not_configured",
            Locked => "error.payment_password.locked",
            UpgradeRequired => "error.payment_password.upgrade_required",
            _ => "error.payment_password.invalid"
        };
    }
}
