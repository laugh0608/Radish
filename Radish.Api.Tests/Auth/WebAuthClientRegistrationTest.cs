using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using Radish.Auth.OpenIddict;
using Xunit;

namespace Radish.Api.Tests.Auth;

public sealed class WebAuthClientRegistrationTest
{
    [Fact]
    public async Task Seed_ShouldRegisterOnlyConfiguredWebClientAndPreserveExistingClients()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var services = new ServiceCollection().AddLogging();
        services.AddDbContext<AuthOpenIddictDbContext>(options => options.UseSqlite(connection));
        services.AddOpenIddict().AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AuthOpenIddictDbContext>());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthOpenIddictDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
        var apps = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        const string issuer = "https://radish.test:35443/";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenIddict:Server:Issuer"] = "https://localhost:5000"
        }).Build();
        var seed = new OpenIddictSeedHostedService(apps, scopes, configuration, NullLogger<OpenIddictSeedHostedService>.Instance);
        await seed.StartAsync(cancellationToken);
        Assert.Null(await apps.FindByClientIdAsync(WebAuthClient.ClientId, cancellationToken));
        var originalScalar = await apps.FindByClientIdAsync("radish-scalar", cancellationToken);
        Assert.NotNull(originalScalar);
        Assert.Equal("https://localhost:5000/scalar/oauth2-callback", Assert.Single(await apps.GetRedirectUrisAsync(originalScalar, cancellationToken)));

        const string secret = "test-only-confidential-client-secret";
        configuration["OpenIddict:Server:Issuer"] = issuer;
        configuration["OpenIddict:WebSession:Enabled"] = "true";
        configuration["OpenIddict:WebSession:ClientSecret"] = secret;
        await seed.StartAsync(cancellationToken);
        var current = await apps.FindByClientIdAsync(WebAuthClient.ClientId, cancellationToken);
        Assert.NotNull(current);
        var web = new OpenIddictApplicationDescriptor();
        await apps.PopulateAsync(web, current, cancellationToken);
        Assert.Equal(OpenIddictConstants.ClientTypes.Confidential, web.ClientType);
        Assert.Equal(new Uri(issuer + "web/session/callback"), Assert.Single(web.RedirectUris));
        var registeredLogout = Assert.Single(await apps.GetPostLogoutRedirectUrisAsync(current, cancellationToken));
        Assert.Equal(WebAuthClient.GetOrigin(new Uri(issuer)).OriginalString, registeredLogout);
        Assert.True(await apps.ValidatePostLogoutRedirectUriAsync(current, registeredLogout, cancellationToken));
        Assert.Contains(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange, web.Requirements);
        Assert.Contains(OpenIddictConstants.Permissions.GrantTypes.RefreshToken, web.Permissions);
        Assert.DoesNotContain(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials, web.Permissions);
        Assert.NotEqual(secret, web.ClientSecret);
        Assert.True(await apps.ValidateClientSecretAsync(current, secret, cancellationToken));
        await seed.StartAsync(cancellationToken);
        var repeated = await apps.FindByClientIdAsync(WebAuthClient.ClientId, cancellationToken);
        Assert.NotNull(repeated);
        var sameWeb = new OpenIddictApplicationDescriptor();
        await apps.PopulateAsync(sameWeb, repeated, cancellationToken);
        Assert.Equal(web.ClientSecret, sameWeb.ClientSecret);

        foreach (var (clientId, callback) in new[]
                 {
                     ("radish-client", "radish://oidc/callback"),
                     ("radish-console", issuer + "console/callback"),
                     ("radish-scalar", issuer + "scalar/oauth2-callback")
                 })
        {
            var application = await apps.FindByClientIdAsync(clientId, cancellationToken);
            Assert.NotNull(application);
            var descriptor = new OpenIddictApplicationDescriptor();
            await apps.PopulateAsync(descriptor, application, cancellationToken);
            Assert.Equal(OpenIddictConstants.ClientTypes.Public, descriptor.ClientType);
            Assert.Contains(new Uri(callback), descriptor.RedirectUris);
            if (clientId == "radish-scalar")
            {
                Assert.Single(descriptor.RedirectUris);
                Assert.DoesNotContain(OpenIddictConstants.Permissions.GrantTypes.RefreshToken, descriptor.Permissions);
            }
            Assert.DoesNotContain(new Uri(issuer + "web/session/callback"), descriptor.RedirectUris);
            Assert.Empty(descriptor.Settings);
        }
    }

    [Fact]
    public void EnabledWebClient_ShouldRequireASecretAndHttpsIssuer()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenIddict:WebSession:Enabled"] = "true",
            ["OpenIddict:Server:Issuer"] = "http://localhost:5000"
        }).Build();

        Assert.Contains("ClientSecret", Assert.Throws<InvalidOperationException>(() => WebAuthClient.CreateRegistration(config)).Message,
            StringComparison.Ordinal);
        config["OpenIddict:WebSession:ClientSecret"] = "test-only-confidential-client-secret";
        Assert.Contains("HTTPS", Assert.Throws<InvalidOperationException>(() => WebAuthClient.CreateRegistration(config)).Message,
            StringComparison.Ordinal);
    }
}
