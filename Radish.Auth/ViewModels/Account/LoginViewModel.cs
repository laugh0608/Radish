using System.Collections.Immutable;
using System.Text.Json;
using OpenIddict.Abstractions;
using Radish.Model.OpenIddict;

namespace Radish.Auth.ViewModels.Account;

public sealed class LoginViewModel
{
    public string? ReturnUrl { get; init; }
    public string? PrefillUserName { get; init; }
    public string? ErrorMessage { get; init; }
    public ClientSummaryViewModel Client { get; init; } = ClientSummaryViewModel.Empty;
}

public sealed class ClientSummaryViewModel
{
    public string ClientId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Logo { get; init; }
    public string? DeveloperName { get; init; }

    public static ClientSummaryViewModel Empty { get; } = new();

    public static ClientSummaryViewModel FromApplication(RadishApplication application)
    {
        return new ClientSummaryViewModel
        {
            ClientId = application.ClientId,
            DisplayName = string.IsNullOrWhiteSpace(application.DisplayName) ? application.ClientId : application.DisplayName,
            Description = application.Description,
            Logo = application.Logo,
            DeveloperName = application.DeveloperName
        };
    }

    public static ClientSummaryViewModel FromDescriptor(OpenIddictApplicationDescriptor descriptor)
    {
        var logo = GetCustomProperty(descriptor, "logo");
        var description = GetCustomProperty(descriptor, "description");
        var developerName = GetCustomProperty(descriptor, "developerName");

        return new ClientSummaryViewModel
        {
            ClientId = descriptor.ClientId ?? string.Empty,
            DisplayName = string.IsNullOrWhiteSpace(descriptor.DisplayName)
                ? (descriptor.ClientId ?? string.Empty)
                : descriptor.DisplayName,
            Description = description,
            Logo = logo,
            DeveloperName = developerName
        };
    }

    public static ClientSummaryViewModel FromStoreData(string? clientId, string? displayName, ImmutableDictionary<string, JsonElement>? properties)
    {
        var logo = GetPropertyFromDictionary(properties, "logo");
        var description = GetPropertyFromDictionary(properties, "description");
        var developerName = GetPropertyFromDictionary(properties, "developerName");

        return new ClientSummaryViewModel
        {
            ClientId = clientId ?? string.Empty,
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? (clientId ?? string.Empty)
                : displayName,
            Description = description,
            Logo = logo,
            DeveloperName = developerName
        };
    }

    private static string? GetCustomProperty(OpenIddictApplicationDescriptor descriptor, string key)
    {
        if (descriptor.Properties is null || !descriptor.Properties.TryGetValue(key, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        return element.ToString();
    }

    private static string? GetPropertyFromDictionary(ImmutableDictionary<string, JsonElement>? properties, string key)
    {
        if (properties is null || !properties.TryGetValue(key, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        return element.ToString();
    }
}
