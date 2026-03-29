using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared;

namespace Radish.Api.Controllers;

/// <summary>系统配置控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Client)]
public class SystemConfigController : ControllerBase
{
    private readonly ISystemConfigService _systemConfigService;

    public SystemConfigController(ISystemConfigService systemConfigService)
    {
        _systemConfigService = systemConfigService;
    }

    /// <summary>获取系统配置列表</summary>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.SystemConfigView)]
    [ProducesResponseType(typeof(MessageModel<List<SystemConfigVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel<List<SystemConfigVo>>> GetSystemConfigs()
    {
        try
        {
            var configs = await _systemConfigService.GetSystemConfigsAsync();
            return MessageModel<List<SystemConfigVo>>.Success("获取成功", configs);
        }
        catch (Exception ex)
        {
            return MessageModel<List<SystemConfigVo>>.Failed($"获取系统配置失败：{ex.Message}");
        }
    }

    /// <summary>获取配置分类列表</summary>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.SystemConfigView)]
    [ProducesResponseType(typeof(MessageModel<List<string>>), StatusCodes.Status200OK)]
    public async Task<MessageModel<List<string>>> GetConfigCategories()
    {
        try
        {
            var categories = await _systemConfigService.GetConfigCategoriesAsync();
            return MessageModel<List<string>>.Success("获取成功", categories);
        }
        catch (Exception ex)
        {
            return MessageModel<List<string>>.Failed($"获取配置分类失败：{ex.Message}");
        }
    }

    /// <summary>根据 ID 获取配置详情</summary>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.SystemConfigView, ConsolePermissions.SystemConfigEdit)]
    [ProducesResponseType(typeof(MessageModel<SystemConfigVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel<SystemConfigVo>> GetConfigById(long id)
    {
        try
        {
            var config = await _systemConfigService.GetConfigByIdAsync(id);
            if (config == null)
            {
                return MessageModel<SystemConfigVo>.Failed("配置不存在");
            }

            return MessageModel<SystemConfigVo>.Success("获取成功", config);
        }
        catch (Exception ex)
        {
            return MessageModel<SystemConfigVo>.Failed($"获取配置详情失败：{ex.Message}");
        }
    }

    /// <summary>更新系统配置</summary>
    [HttpPut]
    [RequireConsolePermission(ConsolePermissions.SystemConfigEdit)]
    [ProducesResponseType(typeof(MessageModel<SystemConfigVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel<SystemConfigVo>> UpdateConfig(long id, [FromBody] UpdateSystemConfigDto request)
    {
        try
        {
            if (request == null)
            {
                return MessageModel<SystemConfigVo>.Failed("请求参数不能为空");
            }

            var config = await _systemConfigService.UpdateConfigAsync(id, request);
            if (config == null)
            {
                return MessageModel<SystemConfigVo>.Failed("配置不存在");
            }

            return MessageModel<SystemConfigVo>.Success("更新成功", config);
        }
        catch (Exception ex)
        {
            return MessageModel<SystemConfigVo>.Failed($"更新配置失败：{ex.Message}");
        }
    }

    /// <summary>创建系统配置</summary>
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.SystemConfigCreate)]
    [ProducesResponseType(typeof(MessageModel<SystemConfigVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel<SystemConfigVo>> CreateConfig([FromBody] CreateSystemConfigDto request)
    {
        try
        {
            if (request == null)
            {
                return MessageModel<SystemConfigVo>.Failed("请求参数不能为空");
            }

            var config = await _systemConfigService.CreateConfigAsync(request);
            return MessageModel<SystemConfigVo>.Success("创建成功", config);
        }
        catch (Exception ex)
        {
            return MessageModel<SystemConfigVo>.Failed($"创建配置失败：{ex.Message}");
        }
    }

    /// <summary>删除系统配置</summary>
    [HttpDelete]
    [RequireConsolePermission(ConsolePermissions.SystemConfigDelete)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> DeleteConfig(long id)
    {
        try
        {
            var deleted = await _systemConfigService.DeleteConfigAsync(id);
            return deleted ? MessageModel.Success("删除成功") : MessageModel.Failed("配置不存在");
        }
        catch (Exception ex)
        {
            return MessageModel.Failed($"删除配置失败：{ex.Message}");
        }
    }

    /// <summary>获取公开站点设置</summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<PublicSiteSettingsVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel<PublicSiteSettingsVo>> GetPublicSiteSettings()
    {
        try
        {
            var settings = await _systemConfigService.GetPublicSiteSettingsAsync();
            return MessageModel<PublicSiteSettingsVo>.Success("获取成功", settings);
        }
        catch (Exception ex)
        {
            return MessageModel<PublicSiteSettingsVo>.Failed($"获取公开站点设置失败：{ex.Message}");
        }
    }
}
