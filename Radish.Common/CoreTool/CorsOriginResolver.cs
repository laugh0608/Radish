using Microsoft.Extensions.Configuration;

namespace Radish.Common.CoreTool;

/// <summary>
/// 统一解析各宿主的 CORS 允许来源。
/// 部署态优先使用 RADISH_PUBLIC_URL 收口到单一公开入口；
/// 未提供时再回退到各宿主自己的开发默认配置。
/// </summary>
public static class CorsOriginResolver
{
    public static string[] ResolveAllowedOrigins(IConfiguration configuration)
    {
        var publicUrlOrigin = NormalizeOrigin(configuration["RADISH_PUBLIC_URL"]);
        if (!string.IsNullOrWhiteSpace(publicUrlOrigin))
        {
            return [publicUrlOrigin];
        }

        var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        return configuredOrigins
            .Select(NormalizeOrigin)
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmedValue = value.Trim().Trim('"').Trim();
        if (!Uri.TryCreate(trimmedValue, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }
}
