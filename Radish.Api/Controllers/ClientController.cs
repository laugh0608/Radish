using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Radish.Api.Filters;
using Radish.Api.Services;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.Model;
using Radish.Model.ViewModels.Client;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Radish.Api.Controllers;

/// <summary>
/// 客户端管理 API
/// </summary>
[ApiController]
[ApiErrorContract]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Authorize(Policy = AuthorizationPolicies.Client)]
public class ClientController : ControllerBase
{
    private const int MaxPageSize = 100;
    private const int MaxKeywordLength = 100;
    private static readonly HashSet<string> SupportedGrantTypes = new(StringComparer.Ordinal)
    {
        "authorization_code",
        "client_credentials",
        "refresh_token",
        "password"
    };

    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IClientApplicationQueryService _queryService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ILogger<ClientController> _logger;

    public ClientController(
        IOpenIddictApplicationManager applicationManager,
        IClientApplicationQueryService queryService,
        ICurrentUserAccessor currentUserAccessor,
        ILogger<ClientController> logger)
    {
        _applicationManager = applicationManager;
        _queryService = queryService;
        _currentUserAccessor = currentUserAccessor;
        _logger = logger;
    }

    /// <summary>
    /// 获取客户端列表
    /// </summary>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.ApplicationsView)]
    public async Task<MessageModel<PageModel<ClientVo>>> GetClients(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
        {
            return MessageModel<PageModel<ClientVo>>.Failed($"分页参数无效：page 必须大于 0，pageSize 必须在 1-{MaxPageSize} 之间");
        }

        var normalizedKeyword = keyword?.Trim();
        if (normalizedKeyword?.Length > MaxKeywordLength)
        {
            return MessageModel<PageModel<ClientVo>>.Failed($"搜索关键词长度不能超过 {MaxKeywordLength}");
        }

        try
        {
            var queryPage = await _queryService.QueryAsync(
                page,
                pageSize,
                normalizedKeyword,
                cancellationToken);
            var clientVos = new List<ClientVo>();
            foreach (var applicationId in queryPage.ApplicationIds)
            {
                var app = await _applicationManager.FindByIdAsync(applicationId, cancellationToken);
                if (app != null && !await IsDeletedAsync(app, cancellationToken))
                {
                    clientVos.Add(await MapToClientVo(app, cancellationToken));
                }
            }

            var pageModel = new PageModel<ClientVo>
            {
                Page = page,
                PageSize = pageSize,
                DataCount = queryPage.Total,
                PageCount = (int)Math.Ceiling(queryPage.Total / (double)pageSize),
                Data = clientVos
            };

            return MessageModel<PageModel<ClientVo>>.Success("获取成功", pageModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取客户端列表失败");
            return MessageModel<PageModel<ClientVo>>.Failed("获取失败");
        }
    }

    /// <summary>
    /// 获取客户端详情
    /// </summary>
    [HttpGet("{id}")]
    [RequireConsolePermission(ConsolePermissions.ApplicationsView, ConsolePermissions.ApplicationsEdit)]
    public async Task<MessageModel<ClientVo>> GetClient(string id)
    {
        try
        {
            var app = await _applicationManager.FindByIdAsync(id);
            if (app == null || await IsDeletedAsync(app))
            {
                return MessageModel<ClientVo>.Failed("客户端不存在");
            }

            var vo = await MapToClientVo(app);
            return MessageModel<ClientVo>.Success("获取成功", vo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取客户端详情失败: {Id}", id);
            return MessageModel<ClientVo>.Failed("获取失败");
        }
    }

    /// <summary>
    /// 创建客户端
    /// </summary>
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.ApplicationsCreate)]
    public async Task<MessageModel<ClientSecretVo>> CreateClient([FromBody] CreateClientDto dto)
    {
        try
        {
            var clientType = ResolveClientType(dto.ClientType, dto.RequireClientSecret);
            var grantTypes = NormalizeGrantTypes(dto.GrantTypes);
            var scopes = NormalizeValues(dto.Scopes);
            var redirectUris = NormalizeValues(dto.RedirectUris);
            var postLogoutRedirectUris = NormalizeValues(dto.PostLogoutRedirectUris);
            var validationError = ValidateClientConfiguration(
                clientType,
                grantTypes,
                scopes,
                redirectUris,
                postLogoutRedirectUris,
                dto.ConsentType);
            if (validationError != null)
            {
                return MessageModel<ClientSecretVo>.Failed(validationError);
            }

            // OpenIddict 对 ClientId 有唯一索引；软删除记录也不能直接复用。
            var existing = await _applicationManager.FindByClientIdAsync(dto.ClientId);
            if (existing != null)
            {
                var suffix = await IsDeletedAsync(existing) ? "（已软删除，当前不支持复用）" : string.Empty;
                return MessageModel<ClientSecretVo>.Failed($"客户端 ID '{dto.ClientId}' 已存在{suffix}");
            }

            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = dto.ClientId,
                DisplayName = dto.DisplayName,
                ClientType = clientType,
                ConsentType = NormalizeConsentType(dto.ConsentType)
            };

            string? clientSecret = null;
            if (clientType == OpenIddictConstants.ClientTypes.Confidential)
            {
                clientSecret = GenerateClientSecret();
                descriptor.ClientSecret = clientSecret;
            }

            foreach (var uri in redirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(uri));
            }

            foreach (var uri in postLogoutRedirectUris)
            {
                descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
            }

            ApplyManagedPermissions(descriptor, grantTypes, scopes);
            if (dto.RequirePkce)
            {
                descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
            }

            SetApplicationMetadata(
                descriptor,
                dto.Description,
                dto.DeveloperName,
                dto.DeveloperEmail,
                status: "Active",
                appType: "ThirdParty");

            // 设置创建信息
            SetCreatedInfo(descriptor);

            // 创建客户端
            await _applicationManager.CreateAsync(descriptor);

            _logger.LogInformation("创建客户端成功: {ClientId}", dto.ClientId);

            return MessageModel<ClientSecretVo>.Success("创建成功", new ClientSecretVo
            {
                ClientId = dto.ClientId,
                ClientSecret = clientSecret,
                Message = clientSecret != null ? "请妥善保管此密钥，关闭后将无法再次查看" : "公开客户端无需密钥"
            });
        }
        catch (Exception ex)
        {
            throw BuildUnexpectedError("创建客户端失败，请稍后重试", ex);
        }
    }

    /// <summary>
    /// 更新客户端
    /// </summary>
    [HttpPut("{id}")]
    [RequireConsolePermission(ConsolePermissions.ApplicationsEdit)]
    public async Task<MessageModel<string>> UpdateClient(string id, [FromBody] UpdateClientDto dto)
    {
        try
        {
            var app = await _applicationManager.FindByIdAsync(id);
            if (app == null || await IsDeletedAsync(app))
            {
                return MessageModel<string>.Failed("客户端不存在");
            }

            // 获取现有描述符
            var descriptor = new OpenIddictApplicationDescriptor();
            await _applicationManager.PopulateAsync(descriptor, app);

            // 更新字段
            if (dto.DisplayName != null)
            {
                descriptor.DisplayName = dto.DisplayName;
            }

            if (dto.Description != null)
            {
                descriptor.Properties["description"] = JsonSerializer.SerializeToElement(dto.Description);
            }

            if (dto.DeveloperName != null)
            {
                descriptor.Properties["developerName"] = JsonSerializer.SerializeToElement(dto.DeveloperName);
            }

            if (dto.DeveloperEmail != null)
            {
                descriptor.Properties["developerEmail"] = JsonSerializer.SerializeToElement(dto.DeveloperEmail);
            }

            if (dto.ConsentType != null)
            {
                descriptor.ConsentType = NormalizeConsentType(dto.ConsentType);
            }

            if (dto.RedirectUris != null)
            {
                var redirectUris = NormalizeValues(dto.RedirectUris);
                var uriValidationError = ValidateAbsoluteUris(redirectUris, "回调 URI");
                if (uriValidationError != null)
                {
                    return MessageModel<string>.Failed(uriValidationError);
                }

                descriptor.RedirectUris.Clear();
                foreach (var uri in redirectUris)
                {
                    descriptor.RedirectUris.Add(new Uri(uri));
                }
            }

            if (dto.PostLogoutRedirectUris != null)
            {
                var postLogoutRedirectUris = NormalizeValues(dto.PostLogoutRedirectUris);
                var uriValidationError = ValidateAbsoluteUris(postLogoutRedirectUris, "登出回调 URI");
                if (uriValidationError != null)
                {
                    return MessageModel<string>.Failed(uriValidationError);
                }

                descriptor.PostLogoutRedirectUris.Clear();
                foreach (var uri in postLogoutRedirectUris)
                {
                    descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
                }
            }

            if (dto.RequirePkce.HasValue)
            {
                if (dto.RequirePkce.Value)
                {
                    descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
                }
                else
                {
                    descriptor.Requirements.Remove(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
                }
            }

            var grantTypes = dto.GrantTypes != null
                ? NormalizeGrantTypes(dto.GrantTypes)
                : GetGrantTypes(descriptor.Permissions);
            var scopes = dto.Scopes != null
                ? NormalizeValues(dto.Scopes)
                : GetScopes(descriptor.Permissions);
            var clientType = descriptor.ClientType ?? OpenIddictConstants.ClientTypes.Public;
            var validationError = ValidateClientConfiguration(
                clientType,
                grantTypes,
                scopes,
                descriptor.RedirectUris.Select(uri => uri.ToString()).ToList(),
                descriptor.PostLogoutRedirectUris.Select(uri => uri.ToString()).ToList(),
                descriptor.ConsentType);
            if (validationError != null)
            {
                return MessageModel<string>.Failed(validationError);
            }

            ApplyManagedPermissions(descriptor, grantTypes, scopes);

            // 设置更新信息
            SetUpdatedInfo(descriptor);

            // 更新客户端
            await _applicationManager.UpdateAsync(app, descriptor);

            _logger.LogInformation("更新客户端成功: {Id}", id);

            return MessageModel<string>.Success("更新成功");
        }
        catch (Exception ex)
        {
            throw BuildUnexpectedError("更新客户端失败，请稍后重试", ex);
        }
    }

    /// <summary>
    /// 删除客户端（软删除）
    /// </summary>
    [HttpDelete("{id}")]
    [RequireConsolePermission(ConsolePermissions.ApplicationsDelete)]
    public async Task<MessageModel<string>> DeleteClient(string id)
    {
        try
        {
            var app = await _applicationManager.FindByIdAsync(id);
            if (app == null || await IsDeletedAsync(app))
            {
                return MessageModel<string>.Failed("客户端不存在");
            }

            // 软删除：标记为已删除
            await MarkAsDeletedAsync(app);

            var clientId = await _applicationManager.GetClientIdAsync(app);
            _logger.LogInformation("软删除客户端成功: {ClientId} (ID: {Id})", clientId, id);

            return MessageModel<string>.Success("删除成功");
        }
        catch (Exception ex)
        {
            throw BuildUnexpectedError("删除客户端失败，请稍后重试", ex);
        }
    }

    /// <summary>
    /// 重置客户端密钥
    /// </summary>
    [HttpPost("{id}")]
    [RequireConsolePermission(ConsolePermissions.ApplicationsResetSecret)]
    public async Task<MessageModel<ClientSecretVo>> ResetClientSecret(string id)
    {
        try
        {
            var app = await _applicationManager.FindByIdAsync(id);
            if (app == null || await IsDeletedAsync(app))
            {
                return MessageModel<ClientSecretVo>.Failed("客户端不存在");
            }

            var descriptor = new OpenIddictApplicationDescriptor();
            await _applicationManager.PopulateAsync(descriptor, app);

            var clientType = await _applicationManager.GetClientTypeAsync(app);
            if (!string.Equals(
                    clientType,
                    OpenIddictConstants.ClientTypes.Confidential,
                    StringComparison.Ordinal))
            {
                return MessageModel<ClientSecretVo>.Failed("公开客户端不使用 Client Secret，不能执行密钥轮换");
            }

            // 生成新密钥
            var newSecret = GenerateClientSecret();
            descriptor.ClientSecret = newSecret;
            SetUpdatedInfo(descriptor);

            // 更新客户端
            await _applicationManager.UpdateAsync(app, descriptor);

            var clientId = await _applicationManager.GetClientIdAsync(app);

            _logger.LogInformation("重置客户端密钥成功: {ClientId}", clientId);

            return MessageModel<ClientSecretVo>.Success("重置成功", new ClientSecretVo
            {
                ClientId = clientId ?? "",
                ClientSecret = newSecret,
                Message = "请妥善保管此密钥，关闭后将无法再次查看"
            });
        }
        catch (Exception ex)
        {
            throw BuildUnexpectedError("重置客户端密钥失败，请稍后重试", ex);
        }
    }

    private static BusinessException BuildUnexpectedError(string message, Exception exception)
    {
        return new BusinessException(
            message,
            exception,
            StatusCodes.Status500InternalServerError,
            "System.UnexpectedError",
            "error.system.unexpected_error");
    }

    #region 私有方法

    private static string ResolveClientType(string? clientType, bool requireClientSecret)
    {
        if (!string.IsNullOrWhiteSpace(clientType))
        {
            return clientType.Trim().ToLowerInvariant();
        }

        return requireClientSecret
            ? OpenIddictConstants.ClientTypes.Confidential
            : OpenIddictConstants.ClientTypes.Public;
    }

    private static string NormalizeConsentType(string? consentType)
    {
        return string.IsNullOrWhiteSpace(consentType)
            ? OpenIddictConstants.ConsentTypes.Explicit
            : consentType.Trim().ToLowerInvariant();
    }

    private static List<string> NormalizeValues(IEnumerable<string>? values)
    {
        return values?
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? new List<string>();
    }

    private static List<string> NormalizeGrantTypes(IEnumerable<string>? values)
    {
        return NormalizeValues(values)
            .Select(value => value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string? ValidateClientConfiguration(
        string clientType,
        IReadOnlyCollection<string> grantTypes,
        IReadOnlyCollection<string> scopes,
        IReadOnlyCollection<string> redirectUris,
        IReadOnlyCollection<string> postLogoutRedirectUris,
        string? consentType)
    {
        if (clientType != OpenIddictConstants.ClientTypes.Public &&
            clientType != OpenIddictConstants.ClientTypes.Confidential)
        {
            return "客户端类型只允许 public 或 confidential";
        }

        if (grantTypes.Count == 0)
        {
            return "至少需要一个授权类型";
        }

        var unsupportedGrantType = grantTypes.FirstOrDefault(grantType => !SupportedGrantTypes.Contains(grantType));
        if (unsupportedGrantType != null)
        {
            return $"不支持的授权类型：{unsupportedGrantType}";
        }

        if (scopes.Count == 0)
        {
            return "至少需要一个授权范围";
        }

        if (grantTypes.Contains("authorization_code") && redirectUris.Count == 0)
        {
            return "authorization_code 授权类型至少需要一个回调 URI";
        }

        if (clientType == OpenIddictConstants.ClientTypes.Public && grantTypes.Contains("client_credentials"))
        {
            return "公开客户端不能使用 client_credentials 授权类型";
        }

        var normalizedConsentType = NormalizeConsentType(consentType);
        var supportedConsentTypes = new[]
        {
            OpenIddictConstants.ConsentTypes.Explicit,
            OpenIddictConstants.ConsentTypes.Implicit,
            OpenIddictConstants.ConsentTypes.External,
            OpenIddictConstants.ConsentTypes.Systematic
        };
        if (!supportedConsentTypes.Contains(normalizedConsentType, StringComparer.Ordinal))
        {
            return "不支持的同意类型";
        }

        var redirectUriError = ValidateAbsoluteUris(redirectUris, "回调 URI");
        if (redirectUriError != null)
        {
            return redirectUriError;
        }

        var postLogoutRedirectUriError = ValidateAbsoluteUris(postLogoutRedirectUris, "登出回调 URI");
        if (postLogoutRedirectUriError != null)
        {
            return postLogoutRedirectUriError;
        }

        return null;
    }

    private static string? ValidateAbsoluteUris(IEnumerable<string> values, string fieldName)
    {
        foreach (var value in values)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.Fragment))
            {
                return $"{fieldName} 必须是无 fragment 的绝对 URI：{value}";
            }
        }

        return null;
    }

    private static void ApplyManagedPermissions(
        OpenIddictApplicationDescriptor descriptor,
        IReadOnlyCollection<string> grantTypes,
        IReadOnlyCollection<string> scopes)
    {
        descriptor.Permissions.RemoveWhere(permission =>
            permission == OpenIddictConstants.Permissions.Endpoints.Authorization ||
            permission == OpenIddictConstants.Permissions.Endpoints.Token ||
            permission == OpenIddictConstants.Permissions.Endpoints.EndSession ||
            permission == OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode ||
            permission == OpenIddictConstants.Permissions.GrantTypes.ClientCredentials ||
            permission == OpenIddictConstants.Permissions.GrantTypes.RefreshToken ||
            permission == OpenIddictConstants.Permissions.GrantTypes.Password ||
            permission == OpenIddictConstants.Permissions.ResponseTypes.Code ||
            permission.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope, StringComparison.Ordinal));

        if (grantTypes.Contains("authorization_code"))
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
        }

        if (grantTypes.Count > 0)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        }

        if (descriptor.PostLogoutRedirectUris.Count > 0)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
        }

        foreach (var grantType in grantTypes)
        {
            var permission = grantType switch
            {
                "authorization_code" => OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                "client_credentials" => OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                "refresh_token" => OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                "password" => OpenIddictConstants.Permissions.GrantTypes.Password,
                _ => null
            };
            if (permission != null)
            {
                descriptor.Permissions.Add(permission);
            }
        }

        foreach (var scope in scopes)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }
    }

    private static List<string> GetGrantTypes(IEnumerable<string> permissions)
    {
        var grantTypes = new List<string>();
        var permissionSet = permissions.ToHashSet(StringComparer.Ordinal);
        if (permissionSet.Contains(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode))
            grantTypes.Add("authorization_code");
        if (permissionSet.Contains(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials))
            grantTypes.Add("client_credentials");
        if (permissionSet.Contains(OpenIddictConstants.Permissions.GrantTypes.RefreshToken))
            grantTypes.Add("refresh_token");
        if (permissionSet.Contains(OpenIddictConstants.Permissions.GrantTypes.Password))
            grantTypes.Add("password");
        return grantTypes;
    }

    private static List<string> GetScopes(IEnumerable<string> permissions)
    {
        return permissions
            .Where(permission => permission.StartsWith(
                OpenIddictConstants.Permissions.Prefixes.Scope,
                StringComparison.Ordinal))
            .Select(permission => permission[OpenIddictConstants.Permissions.Prefixes.Scope.Length..])
            .ToList();
    }

    private static DateTime? ParseUtcDateTime(string? value)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// 将 OpenIddict Application 映射为 ClientVo
    /// </summary>
    private async Task<ClientVo> MapToClientVo(
        object app,
        CancellationToken cancellationToken = default)
    {
        var id = await _applicationManager.GetIdAsync(app, cancellationToken);
        var clientId = await _applicationManager.GetClientIdAsync(app, cancellationToken);
        var displayName = await _applicationManager.GetDisplayNameAsync(app, cancellationToken);
        var clientType = await _applicationManager.GetClientTypeAsync(app, cancellationToken);
        var consentType = await _applicationManager.GetConsentTypeAsync(app, cancellationToken);
        var properties = await _applicationManager.GetPropertiesAsync(app, cancellationToken);

        // 获取权限
        var permissions = await _applicationManager.GetPermissionsAsync(app, cancellationToken);

        // 解析授权类型
        var grantTypes = GetGrantTypes(permissions);
        var scopes = GetScopes(permissions);

        // 获取回调地址
        var redirectUris = await _applicationManager.GetRedirectUrisAsync(app, cancellationToken);
        var postLogoutRedirectUris = await _applicationManager.GetPostLogoutRedirectUrisAsync(app, cancellationToken);

        // 获取要求
        var requirements = await _applicationManager.GetRequirementsAsync(app, cancellationToken);
        var requirePkce = requirements.Contains(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

        var description = GetPropertyValue(properties, "description");
        var developerName = GetPropertyValue(properties, "developerName");
        var developerEmail = GetPropertyValue(properties, "developerEmail");
        var status = GetPropertyValue(properties, "status") ?? "Active";
        var appType = GetPropertyValue(properties, "appType")
                      ?? GetDefaultAppType(clientId);
        var createdAt = ParseUtcDateTime(GetPropertyValue(properties, "CreatedAt"));

        return new ClientVo
        {
            Id = id ?? "",
            ClientId = clientId ?? "",
            DisplayName = displayName,
            Description = description,
            DeveloperName = developerName,
            DeveloperEmail = developerEmail,
            Type = appType,
            Status = status,
            ClientType = clientType ?? OpenIddictConstants.ClientTypes.Public,
            GrantTypes = grantTypes,
            RedirectUris = redirectUris.ToList(),
            PostLogoutRedirectUris = postLogoutRedirectUris.ToList(),
            Scopes = scopes,
            ConsentType = consentType,
            RequirePkce = requirePkce,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// 生成客户端密钥
    /// </summary>
    private static string GenerateClientSecret()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// 检查客户端是否已被软删除
    /// </summary>
    private async Task<bool> IsDeletedAsync(
        object app,
        CancellationToken cancellationToken = default)
    {
        var properties = await _applicationManager.GetPropertiesAsync(app, cancellationToken);
        if (properties.TryGetValue("IsDeleted", out var value))
        {
            return value.GetString() == "true";
        }
        return false;
    }

    /// <summary>
    /// 标记客户端为已删除
    /// </summary>
    private async Task MarkAsDeletedAsync(object app)
    {
        var descriptor = new OpenIddictApplicationDescriptor();
        await _applicationManager.PopulateAsync(descriptor, app);

        var userId = _currentUserAccessor.Current.UserId.ToString();

        descriptor.Properties["IsDeleted"] = JsonSerializer.SerializeToElement("true");
        descriptor.Properties["DeletedAt"] = JsonSerializer.SerializeToElement(DateTime.UtcNow.ToString("O"));
        descriptor.Properties["DeletedBy"] = JsonSerializer.SerializeToElement(userId);

        await _applicationManager.UpdateAsync(app, descriptor);
    }

    /// <summary>
    /// 设置创建信息
    /// </summary>
    private void SetCreatedInfo(OpenIddictApplicationDescriptor descriptor)
    {
        var userId = _currentUserAccessor.Current.UserId.ToString();
        descriptor.Properties["CreatedAt"] = JsonSerializer.SerializeToElement(DateTime.UtcNow.ToString("O"));
        descriptor.Properties["CreatedBy"] = JsonSerializer.SerializeToElement(userId);
        descriptor.Properties["IsDeleted"] = JsonSerializer.SerializeToElement("false");
    }

    /// <summary>
    /// 设置更新信息
    /// </summary>
    private void SetUpdatedInfo(OpenIddictApplicationDescriptor descriptor)
    {
        var userId = _currentUserAccessor.Current.UserId.ToString();
        descriptor.Properties["UpdatedAt"] = JsonSerializer.SerializeToElement(DateTime.UtcNow.ToString("O"));
        descriptor.Properties["UpdatedBy"] = JsonSerializer.SerializeToElement(userId);
    }

    private static void SetApplicationMetadata(
        OpenIddictApplicationDescriptor descriptor,
        string? description,
        string? developerName,
        string? developerEmail,
        string status,
        string appType)
    {
        descriptor.Properties["description"] = JsonSerializer.SerializeToElement(description ?? string.Empty);
        descriptor.Properties["developerName"] = JsonSerializer.SerializeToElement(developerName ?? string.Empty);
        descriptor.Properties["developerEmail"] = JsonSerializer.SerializeToElement(developerEmail ?? string.Empty);
        descriptor.Properties["status"] = JsonSerializer.SerializeToElement(status);
        descriptor.Properties["appType"] = JsonSerializer.SerializeToElement(appType);
    }

    private static string? GetPropertyValue(System.Collections.Immutable.ImmutableDictionary<string, JsonElement> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
    }

    private static string GetDefaultAppType(string? clientId)
    {
        return !string.IsNullOrWhiteSpace(clientId) &&
               clientId.StartsWith("radish-", StringComparison.OrdinalIgnoreCase)
            ? "Internal"
            : "ThirdParty";
    }

    #endregion
}
