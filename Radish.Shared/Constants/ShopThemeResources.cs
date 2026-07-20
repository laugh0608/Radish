namespace Radish.Shared.Constants;

/// <summary>商城主题权益与前端主题注册表共享的稳定资源标识。</summary>
public static class ShopThemeResources
{
    public const string DarkNight = "theme-dark-night";
    public const string Sakura = "theme-sakura";

    public static IReadOnlyList<string> SupportedValues { get; } = [DarkNight, Sakura];

    public static bool IsSupported(string? value)
    {
        var normalizedValue = value?.Trim();
        return SupportedValues.Contains(normalizedValue, StringComparer.Ordinal);
    }
}
