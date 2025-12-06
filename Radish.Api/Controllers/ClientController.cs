using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Radish.Model;
using Radish.Model.ViewModels.Client;
using System.Security.Cryptography;
using System.Text.Json;

namespace Radish.Api.Controllers;

/// <summary>
/// 客户端管理 API
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Authorize(Roles = "System,Admin")]
public class ClientController : ControllerBase
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ILogger<ClientController> _logger;

    public ClientController(
        IOpenIddictApplicationManager applicationManager,
        ILogger<ClientController> logger)
    {
        _applicationManager = applicationManager;
        _logger = logger;
    }

    /// <summary>
    /// 获取客户端列表
    /// </summary>
    [HttpGet]
    public async Task<MessageModel<PageModel<ClientVo>>> GetClients(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null)
    {
        try
        {
            var allClients = new List<object>();
            await foreach (var app in _applicationManager.ListAsync())
            {
                // 过滤已删除的客户端
                if (!await IsDeletedAsync(app))
                {
                    allClients.Add(app);
                }
            }

            // 关键词筛选
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var filteredClients = new List<object>();
                foreach (var app in allClients)
                {
                    var clientId = await _applicationManager.GetClientIdAsync(app);
                    var displayName = await _applicationManager.GetDisplayNameAsync(app);

                    if ((clientId != null && clientId.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                        (displayName != null && displayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    {
                        filteredClients.Add(app);
                    }
                }
                allClients = filteredClients;
            }

            // 分页
            var total = allClients.Count;
            var items = allClients.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // 转换为 VO
            var clientVos = new List<ClientVo>();
            foreach (var app in items)
            {
                var vo = await MapToClientVo(app);
                clientVos.Add(vo);
            }

            var pageModel = new PageModel<ClientVo>
            {
                Page = page,
                PageSize = pageSize,
                DataCount = total,
                PageCount = (int)Math.Ceiling(total / (double)pageSize),
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
    public async Task<MessageModel<ClientSecretVo>> CreateClient([FromBody] CreateClientDto dto)
    {
        try
        {
            // 检查 ClientId 是否已存在（排除已删除的）
            var existing = await _applicationManager.FindByClientIdAsync(dto.ClientId);
            if (existing != null && !await IsDeletedAsync(existing))
            {
                return MessageModel<ClientSecretVo>.Failed($"客户端 ID '{dto.ClientId}' 已存在");
            }

            // 创建描述符
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = dto.ClientId,
                DisplayName = dto.DisplayName,
                ConsentType = dto.ConsentType ?? OpenIddictConstants.ConsentTypes.Explicit
            };

            // 生成 ClientSecret（如果需要）
            string? clientSecret = null;
            if (dto.RequireClientSecret)
            {
                clientSecret = GenerateClientSecret();
                descriptor.ClientSecret = clientSecret;
            }

            // 添加回调地址
            if (dto.RedirectUris != null)
            {
                foreach (var uri in dto.RedirectUris)
                {
                    descriptor.RedirectUris.Add(new Uri(uri));
                }
            }

            if (dto.PostLogoutRedirectUris != null)
            {
                foreach (var uri in dto.PostLogoutRedirectUris)
                {
                    descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
                }
            }

            // 添加权限
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);

            // 添加授权类型
            foreach (var grantType in dto.GrantTypes)
            {
                var permission = grantType.ToLower() switch
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

            // 添加 Scopes
            foreach (var scope in dto.Scopes)
            {
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
            }

            // 响应类型
            if (dto.GrantTypes.Contains("authorization_code"))
            {
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            }

            // PKCE
            if (dto.RequirePkce)
            {
                descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
            }

            // 设置创建信息
            SetCreatedInfo(descriptor);

            // 创建客户端
            await _applicationManager.CreateAsync(descriptor);

            _logger.LogInformation("创建客户端成功: {ClientId}", dto.ClientId);

            return MessageModel<ClientSecretVo>.Success("创建成功", new ClientSecretVo
            {
                ClientId = dto.ClientId,
                ClientSecret = clientSecret ?? "(无需密钥)",
                Message = clientSecret != null ? "请妥善保管此密钥，关闭后将无法再次查看" : "公开客户端无需密钥"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建客户端失败: {ClientId}", dto.ClientId);
            return MessageModel<ClientSecretVo>.Failed("创建失败: " + ex.Message);
        }
    }

    /// <summary>
    /// 更新客户端
    /// </summary>
    [HttpPut("{id}")]
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

            if (dto.ConsentType != null)
            {
                descriptor.ConsentType = dto.ConsentType;
            }

            if (dto.RedirectUris != null)
            {
                descriptor.RedirectUris.Clear();
                foreach (var uri in dto.RedirectUris)
                {
                    descriptor.RedirectUris.Add(new Uri(uri));
                }
            }

            if (dto.PostLogoutRedirectUris != null)
            {
                descriptor.PostLogoutRedirectUris.Clear();
                foreach (var uri in dto.PostLogoutRedirectUris)
                {
                    descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
                }
            }

            if (dto.GrantTypes != null)
            {
                // 清除旧的授权类型权限
                descriptor.Permissions.RemoveWhere(p =>
                    p == OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode ||
                    p == OpenIddictConstants.Permissions.GrantTypes.ClientCredentials ||
                    p == OpenIddictConstants.Permissions.GrantTypes.RefreshToken ||
                    p == OpenIddictConstants.Permissions.GrantTypes.Password);

                foreach (var grantType in dto.GrantTypes)
                {
                    var permission = grantType.ToLower() switch
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
            }

            if (dto.Scopes != null)
            {
                // 清除旧的 scope 权限
                descriptor.Permissions.RemoveWhere(p => p.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope));
                foreach (var scope in dto.Scopes)
                {
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
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

            // 设置更新信息
            SetUpdatedInfo(descriptor);

            // 更新客户端
            await _applicationManager.UpdateAsync(app, descriptor);

            _logger.LogInformation("更新客户端成功: {Id}", id);

            return MessageModel<string>.Success("更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新客户端失败: {Id}", id);
            return MessageModel<string>.Failed("更新失败: " + ex.Message);
        }
    }

    /// <summary>
    /// 删除客户端（软删除）
    /// </summary>
    [HttpDelete("{id}")]
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
            _logger.LogError(ex, "删除客户端失败: {Id}", id);
            return MessageModel<string>.Failed("删除失败: " + ex.Message);
        }
    }

    /// <summary>
    /// 重置客户端密钥
    /// </summary>
    [HttpPost("{id}/reset-secret")]
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

            // 生成新密钥
            var newSecret = GenerateClientSecret();
            descriptor.ClientSecret = newSecret;

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
            _logger.LogError(ex, "重置客户端密钥失败: {Id}", id);
            return MessageModel<ClientSecretVo>.Failed("重置失败: " + ex.Message);
        }
    }

    #region 私有方法

    /// <summary>
    /// 将 OpenIddict Application 映射为 ClientVo
    /// </summary>
    private async Task<ClientVo> MapToClientVo(object app)
    {
        var id = await _applicationManager.GetIdAsync(app);
        var clientId = await _applicationManager.GetClientIdAsync(app);
        var displayName = await _applicationManager.GetDisplayNameAsync(app);
        var clientType = await _applicationManager.GetClientTypeAsync(app);
        var consentType = await _applicationManager.GetConsentTypeAsync(app);

        // 获取权限
        var permissions = await _applicationManager.GetPermissionsAsync(app);

        // 解析授权类型
        var grantTypes = new List<string>();
        if (permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode))
            grantTypes.Add("authorization_code");
        if (permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials))
            grantTypes.Add("client_credentials");
        if (permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.RefreshToken))
            grantTypes.Add("refresh_token");
        if (permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.Password))
            grantTypes.Add("password");

        // 解析 Scopes
        var scopes = permissions
            .Where(p => p.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope))
            .Select(p => p.Substring(OpenIddictConstants.Permissions.Prefixes.Scope.Length))
            .ToList();

        // 获取回调地址
        var redirectUris = await _applicationManager.GetRedirectUrisAsync(app);
        var postLogoutRedirectUris = await _applicationManager.GetPostLogoutRedirectUrisAsync(app);

        // 获取要求
        var requirements = await _applicationManager.GetRequirementsAsync(app);
        var requirePkce = requirements.Contains(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

        return new ClientVo
        {
            Id = id ?? "",
            ClientId = clientId ?? "",
            DisplayName = displayName,
            Type = clientType,
            GrantTypes = string.Join(", ", grantTypes),
            RedirectUris = string.Join(", ", redirectUris.Select(u => u.ToString())),
            PostLogoutRedirectUris = string.Join(", ", postLogoutRedirectUris.Select(u => u.ToString())),
            Scopes = string.Join(", ", scopes),
            ConsentType = consentType,
            RequirePkce = requirePkce
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
    private async Task<bool> IsDeletedAsync(object app)
    {
        var properties = await _applicationManager.GetPropertiesAsync(app);
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

        // 获取当前用户 ID（从 HttpContext）
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("jti")?.Value ?? "0";

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
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("jti")?.Value ?? "0";
        descriptor.Properties["CreatedAt"] = JsonSerializer.SerializeToElement(DateTime.UtcNow.ToString("O"));
        descriptor.Properties["CreatedBy"] = JsonSerializer.SerializeToElement(userId);
    }

    /// <summary>
    /// 设置更新信息
    /// </summary>
    private void SetUpdatedInfo(OpenIddictApplicationDescriptor descriptor)
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("jti")?.Value ?? "0";
        descriptor.Properties["UpdatedAt"] = JsonSerializer.SerializeToElement(DateTime.UtcNow.ToString("O"));
        descriptor.Properties["UpdatedBy"] = JsonSerializer.SerializeToElement(userId);
    }

    #endregion
}
