using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.OpenIddict;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class OidcMapper
{
    // RadishApplication → OidcAppVo
    [MapProperty(nameof(RadishApplication.Id), nameof(OidcAppVo.VoId))]
    [MapProperty(nameof(RadishApplication.ClientId), nameof(OidcAppVo.VoClientId))]
    [MapperIgnoreTarget(nameof(OidcAppVo.VoClientSecret))]
    [MapProperty(nameof(RadishApplication.ConsentType), nameof(OidcAppVo.VoConsentType))]
    [MapProperty(nameof(RadishApplication.DisplayName), nameof(OidcAppVo.VoDisplayName))]
    [MapProperty(nameof(RadishApplication.Type), nameof(OidcAppVo.VoType))]
    [MapProperty(nameof(RadishApplication.RedirectUris), nameof(OidcAppVo.VoRedirectUris))]
    [MapProperty(nameof(RadishApplication.PostLogoutRedirectUris), nameof(OidcAppVo.VoPostLogoutRedirectUris))]
    [MapProperty(nameof(RadishApplication.Permissions), nameof(OidcAppVo.VoPermissions))]
    [MapProperty(nameof(RadishApplication.Requirements), nameof(OidcAppVo.VoRequirements))]
    [MapProperty(nameof(RadishApplication.Properties), nameof(OidcAppVo.VoProperties))]
    [MapProperty(nameof(RadishApplication.Logo), nameof(OidcAppVo.VoLogo))]
    [MapProperty(nameof(RadishApplication.Description), nameof(OidcAppVo.VoDescription))]
    [MapProperty(nameof(RadishApplication.DeveloperName), nameof(OidcAppVo.VoDeveloperName))]
    [MapProperty(nameof(RadishApplication.DeveloperEmail), nameof(OidcAppVo.VoDeveloperEmail))]
    [MapProperty(nameof(RadishApplication.Status), nameof(OidcAppVo.VoStatus))]
    [MapProperty(nameof(RadishApplication.AppType), nameof(OidcAppVo.VoAppType))]
    [MapProperty(nameof(RadishApplication.CreateTime), nameof(OidcAppVo.VoCreateTime))]
    [MapProperty(nameof(RadishApplication.CreateBy), nameof(OidcAppVo.VoCreateBy))]
    [MapProperty(nameof(RadishApplication.ModifyTime), nameof(OidcAppVo.VoModifyTime))]
    [MapProperty(nameof(RadishApplication.ModifyBy), nameof(OidcAppVo.VoModifyBy))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial OidcAppVo? ToOidcAppVo(RadishApplication? source);

    public static partial List<OidcAppVo> ToOidcAppVoList(IEnumerable<RadishApplication>? source);

    // RadishAuthorization → OidcAuthVo
    [MapProperty(nameof(RadishAuthorization.Id), nameof(OidcAuthVo.VoId))]
    [MapProperty(nameof(RadishAuthorization.ApplicationId), nameof(OidcAuthVo.VoApplicationId))]
    [MapProperty(nameof(RadishAuthorization.Subject), nameof(OidcAuthVo.VoSubject))]
    [MapProperty(nameof(RadishAuthorization.Type), nameof(OidcAuthVo.VoType))]
    [MapProperty(nameof(RadishAuthorization.Status), nameof(OidcAuthVo.VoStatus))]
    [MapProperty(nameof(RadishAuthorization.Scopes), nameof(OidcAuthVo.VoScopes))]
    [MapProperty(nameof(RadishAuthorization.Properties), nameof(OidcAuthVo.VoProperties))]
    [MapProperty(nameof(RadishAuthorization.CreateTime), nameof(OidcAuthVo.VoCreateTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial OidcAuthVo? ToOidcAuthVo(RadishAuthorization? source);

    public static partial List<OidcAuthVo> ToOidcAuthVoList(IEnumerable<RadishAuthorization>? source);

    // RadishScope → OidcScopeVo
    [MapProperty(nameof(RadishScope.Id), nameof(OidcScopeVo.VoId))]
    [MapProperty(nameof(RadishScope.Name), nameof(OidcScopeVo.VoName))]
    [MapProperty(nameof(RadishScope.DisplayName), nameof(OidcScopeVo.VoDisplayName))]
    [MapProperty(nameof(RadishScope.Description), nameof(OidcScopeVo.VoDescription))]
    [MapProperty(nameof(RadishScope.Resources), nameof(OidcScopeVo.VoResources))]
    [MapProperty(nameof(RadishScope.Properties), nameof(OidcScopeVo.VoProperties))]
    [MapProperty(nameof(RadishScope.CreateTime), nameof(OidcScopeVo.VoCreateTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial OidcScopeVo? ToOidcScopeVo(RadishScope? source);

    public static partial List<OidcScopeVo> ToOidcScopeVoList(IEnumerable<RadishScope>? source);

    // RadishToken → OidcTokenVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public static OidcTokenVo? ToOidcTokenVo(RadishToken? source)
    {
        if (source is null) return null;
        var target = MapRadishTokenToOidcTokenVo(source);
        target.VoPayloadPreview = source.Payload is null ? null! : source.Payload.Length > 100 ? source.Payload.Substring(0, 100) + "..." : source.Payload;
        return target;
    }

    [MapProperty(nameof(RadishToken.Id), nameof(OidcTokenVo.VoId))]
    [MapProperty(nameof(RadishToken.ApplicationId), nameof(OidcTokenVo.VoApplicationId))]
    [MapProperty(nameof(RadishToken.AuthorizationId), nameof(OidcTokenVo.VoAuthorizationId))]
    [MapProperty(nameof(RadishToken.Subject), nameof(OidcTokenVo.VoSubject))]
    [MapProperty(nameof(RadishToken.Type), nameof(OidcTokenVo.VoType))]
    [MapProperty(nameof(RadishToken.Status), nameof(OidcTokenVo.VoStatus))]
    [MapProperty(nameof(RadishToken.ReferenceId), nameof(OidcTokenVo.VoReferenceId))]
    [MapperIgnoreTarget(nameof(OidcTokenVo.VoPayloadPreview))]
    [MapProperty(nameof(RadishToken.Properties), nameof(OidcTokenVo.VoProperties))]
    [MapProperty(nameof(RadishToken.CreateTime), nameof(OidcTokenVo.VoCreateTime))]
    [MapProperty(nameof(RadishToken.ExpirationTime), nameof(OidcTokenVo.VoExpirationTime))]
    [MapProperty(nameof(RadishToken.RedemptionTime), nameof(OidcTokenVo.VoRedemptionTime))]
    [UserMapping(Default = false)]
    private static partial OidcTokenVo MapRadishTokenToOidcTokenVo(RadishToken source);

    public static partial List<OidcTokenVo> ToOidcTokenVoList(IEnumerable<RadishToken>? source);

    // UserClaim → UserClaimVo
    [MapProperty(nameof(UserClaim.Id), nameof(UserClaimVo.VoId))]
    [MapProperty(nameof(UserClaim.UserId), nameof(UserClaimVo.VoUserId))]
    [MapProperty(nameof(UserClaim.ClaimType), nameof(UserClaimVo.VoClaimType))]
    [MapProperty(nameof(UserClaim.ClaimValue), nameof(UserClaimVo.VoClaimValue))]
    [MapProperty(nameof(UserClaim.IsDeleted), nameof(UserClaimVo.VoIsDeleted))]
    [MapProperty(nameof(UserClaim.CreateBy), nameof(UserClaimVo.VoCreateBy))]
    [MapProperty(nameof(UserClaim.CreateTime), nameof(UserClaimVo.VoCreateTime))]
    [MapProperty(nameof(UserClaim.ModifyBy), nameof(UserClaimVo.VoModifyBy))]
    [MapProperty(nameof(UserClaim.ModifyTime), nameof(UserClaimVo.VoModifyTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserClaimVo? ToUserClaimVo(UserClaim? source);

    public static partial List<UserClaimVo> ToUserClaimVoList(IEnumerable<UserClaim>? source);

    // OidcAppVo → RadishApplication
    [MapProperty(nameof(OidcAppVo.VoClientId), nameof(RadishApplication.ClientId))]
    [MapProperty(nameof(OidcAppVo.VoClientSecret), nameof(RadishApplication.ClientSecret))]
    [MapProperty(nameof(OidcAppVo.VoConsentType), nameof(RadishApplication.ConsentType))]
    [MapProperty(nameof(OidcAppVo.VoDisplayName), nameof(RadishApplication.DisplayName))]
    [MapProperty(nameof(OidcAppVo.VoType), nameof(RadishApplication.Type))]
    [MapProperty(nameof(OidcAppVo.VoRedirectUris), nameof(RadishApplication.RedirectUris))]
    [MapProperty(nameof(OidcAppVo.VoPostLogoutRedirectUris), nameof(RadishApplication.PostLogoutRedirectUris))]
    [MapProperty(nameof(OidcAppVo.VoPermissions), nameof(RadishApplication.Permissions))]
    [MapProperty(nameof(OidcAppVo.VoRequirements), nameof(RadishApplication.Requirements))]
    [MapProperty(nameof(OidcAppVo.VoProperties), nameof(RadishApplication.Properties))]
    [MapProperty(nameof(OidcAppVo.VoLogo), nameof(RadishApplication.Logo))]
    [MapProperty(nameof(OidcAppVo.VoDescription), nameof(RadishApplication.Description))]
    [MapProperty(nameof(OidcAppVo.VoDeveloperName), nameof(RadishApplication.DeveloperName))]
    [MapProperty(nameof(OidcAppVo.VoDeveloperEmail), nameof(RadishApplication.DeveloperEmail))]
    [MapProperty(nameof(OidcAppVo.VoStatus), nameof(RadishApplication.Status))]
    [MapProperty(nameof(OidcAppVo.VoAppType), nameof(RadishApplication.AppType))]
    [MapProperty(nameof(OidcAppVo.VoCreateTime), nameof(RadishApplication.CreateTime))]
    [MapProperty(nameof(OidcAppVo.VoCreateBy), nameof(RadishApplication.CreateBy))]
    [MapProperty(nameof(OidcAppVo.VoModifyTime), nameof(RadishApplication.ModifyTime))]
    [MapProperty(nameof(OidcAppVo.VoModifyBy), nameof(RadishApplication.ModifyBy))]
    [MapProperty(nameof(OidcAppVo.VoId), nameof(RadishApplication.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial RadishApplication? ToRadishApplication(OidcAppVo? source);

    [ObjectFactory]
    private static RadishApplication CreateRadishApplication(OidcAppVo source) => new(source.VoClientId, source.VoDisplayName);

    public static partial List<RadishApplication> ToRadishApplicationList(IEnumerable<OidcAppVo>? source);

    // OidcAuthVo → RadishAuthorization
    [MapProperty(nameof(OidcAuthVo.VoApplicationId), nameof(RadishAuthorization.ApplicationId))]
    [MapProperty(nameof(OidcAuthVo.VoSubject), nameof(RadishAuthorization.Subject))]
    [MapProperty(nameof(OidcAuthVo.VoType), nameof(RadishAuthorization.Type))]
    [MapProperty(nameof(OidcAuthVo.VoStatus), nameof(RadishAuthorization.Status))]
    [MapProperty(nameof(OidcAuthVo.VoScopes), nameof(RadishAuthorization.Scopes))]
    [MapProperty(nameof(OidcAuthVo.VoProperties), nameof(RadishAuthorization.Properties))]
    [MapProperty(nameof(OidcAuthVo.VoCreateTime), nameof(RadishAuthorization.CreateTime))]
    [MapProperty(nameof(OidcAuthVo.VoId), nameof(RadishAuthorization.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial RadishAuthorization? ToRadishAuthorization(OidcAuthVo? source);

    public static partial List<RadishAuthorization> ToRadishAuthorizationList(IEnumerable<OidcAuthVo>? source);

    // OidcScopeVo → RadishScope
    [MapProperty(nameof(OidcScopeVo.VoName), nameof(RadishScope.Name))]
    [MapProperty(nameof(OidcScopeVo.VoDisplayName), nameof(RadishScope.DisplayName))]
    [MapProperty(nameof(OidcScopeVo.VoDescription), nameof(RadishScope.Description))]
    [MapProperty(nameof(OidcScopeVo.VoResources), nameof(RadishScope.Resources))]
    [MapProperty(nameof(OidcScopeVo.VoProperties), nameof(RadishScope.Properties))]
    [MapProperty(nameof(OidcScopeVo.VoCreateTime), nameof(RadishScope.CreateTime))]
    [MapProperty(nameof(OidcScopeVo.VoId), nameof(RadishScope.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial RadishScope? ToRadishScope(OidcScopeVo? source);

    [ObjectFactory]
    private static RadishScope CreateRadishScope(OidcScopeVo source) => new(source.VoName);

    public static partial List<RadishScope> ToRadishScopeList(IEnumerable<OidcScopeVo>? source);

    // OidcTokenVo → RadishToken
    [MapProperty(nameof(OidcTokenVo.VoApplicationId), nameof(RadishToken.ApplicationId))]
    [MapProperty(nameof(OidcTokenVo.VoAuthorizationId), nameof(RadishToken.AuthorizationId))]
    [MapProperty(nameof(OidcTokenVo.VoSubject), nameof(RadishToken.Subject))]
    [MapProperty(nameof(OidcTokenVo.VoType), nameof(RadishToken.Type))]
    [MapProperty(nameof(OidcTokenVo.VoStatus), nameof(RadishToken.Status))]
    [MapProperty(nameof(OidcTokenVo.VoReferenceId), nameof(RadishToken.ReferenceId))]
    [MapperIgnoreTarget(nameof(RadishToken.Payload))]
    [MapProperty(nameof(OidcTokenVo.VoProperties), nameof(RadishToken.Properties))]
    [MapProperty(nameof(OidcTokenVo.VoCreateTime), nameof(RadishToken.CreateTime))]
    [MapProperty(nameof(OidcTokenVo.VoExpirationTime), nameof(RadishToken.ExpirationTime))]
    [MapProperty(nameof(OidcTokenVo.VoRedemptionTime), nameof(RadishToken.RedemptionTime))]
    [MapProperty(nameof(OidcTokenVo.VoId), nameof(RadishToken.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial RadishToken? ToRadishToken(OidcTokenVo? source);

    public static partial List<RadishToken> ToRadishTokenList(IEnumerable<OidcTokenVo>? source);

    // UserClaimVo → UserClaim
    [MapProperty(nameof(UserClaimVo.VoUserId), nameof(UserClaim.UserId))]
    [MapProperty(nameof(UserClaimVo.VoClaimType), nameof(UserClaim.ClaimType))]
    [MapProperty(nameof(UserClaimVo.VoClaimValue), nameof(UserClaim.ClaimValue))]
    [MapProperty(nameof(UserClaimVo.VoIsDeleted), nameof(UserClaim.IsDeleted))]
    [MapProperty(nameof(UserClaimVo.VoCreateBy), nameof(UserClaim.CreateBy))]
    [MapProperty(nameof(UserClaimVo.VoCreateTime), nameof(UserClaim.CreateTime))]
    [MapProperty(nameof(UserClaimVo.VoModifyBy), nameof(UserClaim.ModifyBy))]
    [MapProperty(nameof(UserClaimVo.VoModifyTime), nameof(UserClaim.ModifyTime))]
    [MapProperty(nameof(UserClaimVo.VoId), nameof(UserClaim.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserClaim? ToUserClaim(UserClaimVo? source);

    [ObjectFactory]
    private static UserClaim CreateUserClaim(UserClaimVo source) => new(source.VoUserId, source.VoClaimType, source.VoClaimValue);

    public static partial List<UserClaim> ToUserClaimList(IEnumerable<UserClaimVo>? source);

}
