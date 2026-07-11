namespace Radish.Auth.OpenIddict;

public static class OpenIddictTransportSecurityPolicy
{
    private const string ConfigurationKey = "OpenIddict:Server:AllowInsecureHttp";

    public static bool ShouldDisableTransportSecurityRequirement(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowInsecureHttp = configuration.GetValue<bool?>(ConfigurationKey) ?? false;
        if (!allowInsecureHttp)
        {
            return false;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey}=true 仅允许用于 Development 环境；当前环境为 {environment.EnvironmentName}。");
        }

        return true;
    }
}
