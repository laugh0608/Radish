using Microsoft.Extensions.Localization;
using Radish.Auth.Resources;

namespace Radish.Auth.Localization;

public static class AuthClientText
{
    public static string Resolve(
        IStringLocalizer<Errors> localizer,
        string? clientId,
        string field,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return fallback;
        }

        var localized = localizer[$"auth.client.{clientId}.{field}"];
        return localized.ResourceNotFound ? fallback : localized.Value;
    }
}
