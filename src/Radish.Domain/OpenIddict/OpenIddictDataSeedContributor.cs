using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using OpenIddict.Abstractions;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.OpenIddict.Scopes;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;

namespace Radish.OpenIddict;

/* Creates initial data that is needed to property run the application
 * and make client-to-server communication possible.
 */
public class OpenIddictDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IConfiguration _configuration;
    private readonly IOpenIddictApplicationRepository _openIddictApplicationRepository;
    private readonly IAbpApplicationManager _applicationManager;
    private readonly IOpenIddictScopeRepository _openIddictScopeRepository;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly IPermissionDataSeeder _permissionDataSeeder;
    private readonly IStringLocalizer<OpenIddictResponse> L;

    public OpenIddictDataSeedContributor(
        IConfiguration configuration,
        IOpenIddictApplicationRepository openIddictApplicationRepository,
        IAbpApplicationManager applicationManager,
        IOpenIddictScopeRepository openIddictScopeRepository,
        IOpenIddictScopeManager scopeManager,
        IPermissionDataSeeder permissionDataSeeder,
        IStringLocalizer<OpenIddictResponse> l)
    {
        _configuration = configuration;
        _openIddictApplicationRepository = openIddictApplicationRepository;
        _applicationManager = applicationManager;
        _openIddictScopeRepository = openIddictScopeRepository;
        _scopeManager = scopeManager;
        _permissionDataSeeder = permissionDataSeeder;
        L = l;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        await CreateScopesAsync();
        await CreateApplicationsAsync();
    }

    private async Task CreateScopesAsync()
    {
        if (await _openIddictScopeRepository.FindByNameAsync("Radish") == null)
        {
            await _scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = "Radish", DisplayName = "Radish API", Resources = { "Radish" }
            });
        }
    }

    private async Task CreateApplicationsAsync()
    {
        var commonScopes = new List<string>
        {
            OpenIddictConstants.Permissions.Scopes.Address,
            OpenIddictConstants.Permissions.Scopes.Email,
            OpenIddictConstants.Permissions.Scopes.Phone,
            OpenIddictConstants.Permissions.Scopes.Profile,
            OpenIddictConstants.Permissions.Scopes.Roles,
            "Radish"
        };

        var configurationSection = _configuration.GetSection("OpenIddict:Applications");

        #region App / React Client

        var appAndReactClientId = configurationSection["Radish_React:ClientId"];
        if (!appAndReactClientId.IsNullOrWhiteSpace())
        {
            var appAndReactClientRootUrl =
                configurationSection["Radish_React:RootUrl"]?.TrimEnd('/');
            var reactRedirects = WithHttpHttpsPair(appAndReactClientRootUrl).ToList();
            var reactPostLogoutRedirects = WithHttpHttpsPair(appAndReactClientRootUrl).ToList();
            await CreateApplicationAsync(
                applicationType: OpenIddictConstants.ApplicationTypes.Web,
                name: appAndReactClientId!,
                type: OpenIddictConstants.ClientTypes.Public,
                consentType: OpenIddictConstants.ConsentTypes.Implicit,
                displayName: "App / React App",
                secret: null,
                grantTypes: new List<string>
                {
                    OpenIddictConstants.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.GrantTypes.Password,
                    OpenIddictConstants.GrantTypes.ClientCredentials,
                    OpenIddictConstants.GrantTypes.RefreshToken,
                    "LinkLogin",
                    "Impersonation"
                },
                scopes: commonScopes,
                redirectUris: reactRedirects,
                postLogoutRedirectUris: reactPostLogoutRedirects,
                clientUri: appAndReactClientRootUrl,
                logoUri: "/images/clients/blazor.svg"
            );
        }

        #endregion
        
        #region Console / Angular Client

        var consoleAndAngularClientId = configurationSection["Radish_Console:ClientId"]; // 旧：Radish_App
        if (!consoleAndAngularClientId.IsNullOrWhiteSpace())
        {
            var consoleAndAngularClientRootUrl =
                configurationSection["Radish_Console:RootUrl"]?.TrimEnd('/'); // 旧：Radish_App
            var angularRedirects = new List<string>();
            angularRedirects.AddRange(WithHttpHttpsPair(consoleAndAngularClientRootUrl));
            angularRedirects.AddRange(WithHttpHttpsPair($"{consoleAndAngularClientRootUrl}/assets/silent-refresh.html"));
            var angularPostLogoutRedirects = WithHttpHttpsPair(consoleAndAngularClientRootUrl).ToList();
            await CreateApplicationAsync(
                applicationType: OpenIddictConstants.ApplicationTypes.Web,
                name: consoleAndAngularClientId!,
                type: OpenIddictConstants.ClientTypes.Public,
                consentType: OpenIddictConstants.ConsentTypes.Implicit,
                displayName: "Console / Angular App",
                secret: null,
                grantTypes: new List<string>
                {
                    OpenIddictConstants.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.GrantTypes.Password,
                    OpenIddictConstants.GrantTypes.ClientCredentials,
                    OpenIddictConstants.GrantTypes.RefreshToken,
                    "LinkLogin",
                    "Impersonation"
                },
                scopes: commonScopes,
                // 允许 Angular 顶层回调与静默刷新回调（prompt=none / iframe），并同时包含 http/https 配对
                redirectUris: angularRedirects,
                postLogoutRedirectUris: angularPostLogoutRedirects,
                clientUri: consoleAndAngularClientRootUrl,
                logoUri: "/images/clients/angular.svg"
            );
        }

        #endregion

        #region Swagger Client

        var swaggerClientId = configurationSection["Radish_Swagger:ClientId"];
        if (!swaggerClientId.IsNullOrWhiteSpace())
        {
            var swaggerRootUrl = configurationSection["Radish_Swagger:RootUrl"]?.TrimEnd('/');

            await CreateApplicationAsync(
                applicationType: OpenIddictConstants.ApplicationTypes.Web,
                name: swaggerClientId!,
                type: OpenIddictConstants.ClientTypes.Public,
                consentType: OpenIddictConstants.ConsentTypes.Implicit,
                displayName: "Swagger App",
                secret: null,
                grantTypes: new List<string> { OpenIddictConstants.GrantTypes.AuthorizationCode, },
                scopes: commonScopes,
                redirectUris: new List<string> { $"{swaggerRootUrl}/swagger/oauth2-redirect.html" },
                // 将 ClientUri 指向具体文档页，避免首页卡片点击仅回到根路径导致的“原地刷新”
                clientUri: swaggerRootUrl.EnsureEndsWith('/') + "swagger",
                logoUri: "/images/clients/swagger.svg"
            );
        }

        #endregion

        #region Scalar Client

        // @luobo 2025.10.6 Scalar Client
        var scalarClientId = configurationSection["Radish_Scalar:ClientId"];
        if (!scalarClientId.IsNullOrWhiteSpace())
        {
            var scalarRootUrl = configurationSection["Radish_Scalar:RootUrl"]?.TrimEnd('/');
        
            await CreateApplicationAsync(
                applicationType: OpenIddictConstants.ApplicationTypes.Web,
                name: scalarClientId!,
                type: OpenIddictConstants.ClientTypes.Public,
                consentType: OpenIddictConstants.ConsentTypes.Implicit,
                displayName: "Scalar App",
                secret: null,
                grantTypes: new List<string> { OpenIddictConstants.GrantTypes.AuthorizationCode, },
                scopes: commonScopes,
                // TODO: $"{scalarRootUrl}/scalar/oauth2-redirect.html" 不知道是干嘛的
                redirectUris: new List<string> { $"{scalarRootUrl}/scalar/oauth2-redirect.html" },
                // 将 ClientUri 指向具体文档页，避免首页卡片点击仅回到根路径导致的“原地刷新”
                clientUri: scalarRootUrl.EnsureEndsWith('/') + "scalar",
                logoUri: "/images/clients/aspnetcore.svg"
            );
        }

        #endregion
    }

    private async Task CreateApplicationAsync(
        [NotNull] string applicationType,
        [NotNull] string name,
        [NotNull] string type,
        [NotNull] string consentType,
        string displayName,
        string? secret,
        List<string> grantTypes,
        List<string> scopes,
        List<string>? redirectUris = null,
        List<string>? postLogoutRedirectUris = null,
        List<string>? permissions = null,
        string? clientUri = null,
        string? logoUri = null)
    {
        if (!string.IsNullOrEmpty(secret) && string.Equals(type, OpenIddictConstants.ClientTypes.Public,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(L["NoClientSecretCanBeSetForPublicApplications"]);
        }

        if (string.IsNullOrEmpty(secret) && string.Equals(type, OpenIddictConstants.ClientTypes.Confidential,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(L["TheClientSecretIsRequiredForConfidentialApplications"]);
        }

        var client = await _openIddictApplicationRepository.FindByClientIdAsync(name);

        var application = new AbpApplicationDescriptor
        {
            ApplicationType = applicationType,
            ClientId = name,
            ClientType = type,
            ClientSecret = secret,
            ConsentType = consentType,
            DisplayName = displayName,
            ClientUri = clientUri,
            LogoUri = logoUri,
        };

        Check.NotNullOrEmpty(grantTypes, nameof(grantTypes));
        Check.NotNullOrEmpty(scopes, nameof(scopes));

        if (new[] { OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.Implicit }.All(
                grantTypes.Contains))
        {
            application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.CodeIdToken);

            if (string.Equals(type, OpenIddictConstants.ClientTypes.Public, StringComparison.OrdinalIgnoreCase))
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.CodeIdTokenToken);
                application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.CodeToken);
            }
        }

        if (!redirectUris.IsNullOrEmpty() || !postLogoutRedirectUris.IsNullOrEmpty())
        {
            application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
        }

        var buildInGrantTypes = new[]
        {
            OpenIddictConstants.GrantTypes.Implicit, OpenIddictConstants.GrantTypes.Password,
            OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.ClientCredentials,
            OpenIddictConstants.GrantTypes.DeviceCode, OpenIddictConstants.GrantTypes.RefreshToken
        };

        foreach (var grantType in grantTypes)
        {
            if (grantType == OpenIddictConstants.GrantTypes.AuthorizationCode)
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
                application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            }

            if (grantType == OpenIddictConstants.GrantTypes.AuthorizationCode ||
                grantType == OpenIddictConstants.GrantTypes.Implicit)
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            }

            if (grantType == OpenIddictConstants.GrantTypes.AuthorizationCode ||
                grantType == OpenIddictConstants.GrantTypes.ClientCredentials ||
                grantType == OpenIddictConstants.GrantTypes.Password ||
                grantType == OpenIddictConstants.GrantTypes.RefreshToken ||
                grantType == OpenIddictConstants.GrantTypes.DeviceCode)
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
                application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
                application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Introspection);
            }

            if (grantType == OpenIddictConstants.GrantTypes.ClientCredentials)
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
            }

            if (grantType == OpenIddictConstants.GrantTypes.Implicit)
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.Implicit);
            }

            if (grantType == OpenIddictConstants.GrantTypes.Password)
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.Password);
            }

            if (grantType == OpenIddictConstants.GrantTypes.RefreshToken)
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            }

            if (grantType == OpenIddictConstants.GrantTypes.DeviceCode)
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.DeviceCode);
                application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.DeviceAuthorization);
            }

            if (grantType == OpenIddictConstants.GrantTypes.Implicit)
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.IdToken);
                if (string.Equals(type, OpenIddictConstants.ClientTypes.Public, StringComparison.OrdinalIgnoreCase))
                {
                    application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.IdTokenToken);
                    application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Token);
                }
            }

            if (!buildInGrantTypes.Contains(grantType))
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.GrantType + grantType);
            }
        }

        var buildInScopes = new[]
        {
            OpenIddictConstants.Permissions.Scopes.Address, OpenIddictConstants.Permissions.Scopes.Email,
            OpenIddictConstants.Permissions.Scopes.Phone, OpenIddictConstants.Permissions.Scopes.Profile,
            OpenIddictConstants.Permissions.Scopes.Roles
        };

        foreach (var scope in scopes)
        {
            if (buildInScopes.Contains(scope))
            {
                application.Permissions.Add(scope);
            }
            else
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
            }
        }

        if (!redirectUris.IsNullOrEmpty())
        {
            foreach (var redirectUri in redirectUris!.Where(redirectUri => !redirectUri.IsNullOrWhiteSpace()))
            {
                if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) || !uri.IsWellFormedOriginalString())
                {
                    throw new BusinessException(L["InvalidRedirectUri", redirectUri]);
                }

                if (application.RedirectUris.All(x => x != uri))
                {
                    application.RedirectUris.Add(uri);
                }
            }
        }

        if (!postLogoutRedirectUris.IsNullOrEmpty())
        {
            foreach (var postLogoutRedirectUri in postLogoutRedirectUris!.Where(postLogoutRedirectUri =>
                         !postLogoutRedirectUri.IsNullOrWhiteSpace()))
            {
                if (!Uri.TryCreate(postLogoutRedirectUri, UriKind.Absolute, out var uri) ||
                    !uri.IsWellFormedOriginalString())
                {
                    throw new BusinessException(L["InvalidPostLogoutRedirectUri", postLogoutRedirectUri]);
                }

                if (application.PostLogoutRedirectUris.All(x => x != uri))
                {
                    application.PostLogoutRedirectUris.Add(uri);
                }
            }
        }

        if (permissions != null)
        {
            await _permissionDataSeeder.SeedAsync(
                ClientPermissionValueProvider.ProviderName,
                name,
                permissions,
                null
            );
        }

        if (client == null)
        {
            await _applicationManager.CreateAsync(application);
            return;
        }

        var requiresUpdate = false;

        // 同步更新 ClientUri（例如为 Swagger/Scalar 写入具体文档页路径）
        if (!string.Equals(client.ClientUri, application.ClientUri, StringComparison.OrdinalIgnoreCase))
        {
            client.ClientUri = application.ClientUri;
            requiresUpdate = true;
        }

        if (!HasSameRedirectUris(client, application))
        {
            client.RedirectUris =
                JsonSerializer.Serialize(application.RedirectUris.Select(q => q.ToString().RemovePostFix("/")));
            client.PostLogoutRedirectUris =
                JsonSerializer.Serialize(
                    application.PostLogoutRedirectUris.Select(q => q.ToString().RemovePostFix("/")));

            requiresUpdate = true;
        }

        if (!HasSameScopes(client, application))
        {
            client.Permissions = JsonSerializer.Serialize(application.Permissions.Select(q => q.ToString()));
            requiresUpdate = true;
        }

        if (requiresUpdate)
        {
            await _applicationManager.UpdateAsync(client.ToModel());
        }
    }

    private static IEnumerable<string> WithHttpHttpsPair(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) yield break;
        var s = url.Trim().TrimEnd('/');
        yield return s;

        if (Uri.TryCreate(s, UriKind.Absolute, out var uri) &&
            (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            var other = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                ? Uri.UriSchemeHttps
                : Uri.UriSchemeHttp;
            var counterpart = $"{other}://{uri.Host}{(uri.IsDefaultPort ? string.Empty : ":" + uri.Port)}{uri.AbsolutePath}";
            counterpart = counterpart.TrimEnd('/');
            if (!string.Equals(counterpart, s, StringComparison.OrdinalIgnoreCase))
                yield return counterpart;
        }
    }

    private bool HasSameRedirectUris(OpenIddictApplication existingClient, AbpApplicationDescriptor application)
    {
        return existingClient.RedirectUris ==
               JsonSerializer.Serialize(application.RedirectUris.Select(q => q.ToString().RemovePostFix("/")));
    }

    private bool HasSameScopes(OpenIddictApplication existingClient, AbpApplicationDescriptor application)
    {
        return existingClient.Permissions ==
               JsonSerializer.Serialize(application.Permissions.Select(q => q.ToString().TrimEnd('/')));
    }
}
