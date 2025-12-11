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
}
