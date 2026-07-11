using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.Exceptions;
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
[ApiErrorContract]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Client)]
public class SystemConfigController : ControllerBase
{
    private readonly ISystemConfigService _systemConfigService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public SystemConfigController(
        ISystemConfigService systemConfigService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _systemConfigService = systemConfigService;
        _currentUserAccessor = currentUserAccessor;
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
            throw BuildUnexpectedError("获取系统配置失败，请稍后重试", ex);
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
            throw BuildUnexpectedError("获取配置分类失败，请稍后重试", ex);
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
            throw BuildUnexpectedError("获取配置详情失败，请稍后重试", ex);
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

            var config = await _systemConfigService.UpdateConfigAsync(id, request, BuildChangeContext());
            if (config == null)
            {
                return MessageModel<SystemConfigVo>.Failed("配置不存在");
            }

            return MessageModel<SystemConfigVo>.Success("更新成功", config);
        }
        catch (Exception ex)
        {
            throw BuildUnexpectedError("更新配置失败，请稍后重试", ex);
        }
    }

    /// <summary>恢复系统设置默认值</summary>
    [HttpPut]
    [RequireConsolePermission(ConsolePermissions.SystemConfigEdit)]
    [ProducesResponseType(typeof(MessageModel<SystemConfigVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel<SystemConfigVo>> RestoreConfigDefault(long id, [FromBody] RestoreSystemConfigDefaultDto? request = null)
    {
        try
        {
            var config = await _systemConfigService.RestoreConfigDefaultAsync(id, request, BuildChangeContext());
            if (config == null)
            {
                return MessageModel<SystemConfigVo>.Failed("配置不存在");
            }

            return MessageModel<SystemConfigVo>.Success("已恢复默认", config);
        }
        catch (Exception ex)
        {
            throw BuildUnexpectedError("恢复默认失败，请稍后重试", ex);
        }
    }

    /// <summary>获取系统设置变更历史</summary>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.SystemConfigView)]
    [ProducesResponseType(typeof(MessageModel<List<SystemConfigChangeLogVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel<List<SystemConfigChangeLogVo>>> GetConfigChangeLogs(long id, int take = 20)
    {
        try
        {
            var logs = await _systemConfigService.GetConfigChangeLogsAsync(id, take);
            if (logs == null)
            {
                return MessageModel<List<SystemConfigChangeLogVo>>.Failed("配置不存在");
            }

            return MessageModel<List<SystemConfigChangeLogVo>>.Success("获取成功", logs);
        }
        catch (Exception ex)
        {
            throw BuildUnexpectedError("获取变更历史失败，请稍后重试", ex);
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
            throw BuildUnexpectedError("创建配置失败，请稍后重试", ex);
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
            var deleted = await _systemConfigService.DeleteConfigAsync(id, BuildChangeContext());
            return deleted ? MessageModel.Success("已恢复默认") : MessageModel.Failed("配置不存在");
        }
        catch (Exception ex)
        {
            throw BuildUnexpectedError("删除配置失败，请稍后重试", ex);
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
            throw BuildUnexpectedError("获取公开站点设置失败，请稍后重试", ex);
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

    private SystemConfigChangeContext BuildChangeContext()
    {
        var current = _currentUserAccessor.Current;
        return new SystemConfigChangeContext
        {
            OperatorUserId = current.UserId > 0 ? current.UserId : null,
            OperatorUserName = string.IsNullOrWhiteSpace(current.UserName) ? null : current.UserName.Trim(),
            RequestIp = GetClientIpAddress(),
            UserAgent = Request.Headers["User-Agent"].ToString()
        };
    }

    private string? GetClientIpAddress()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(realIp)
            ? HttpContext.Connection.RemoteIpAddress?.ToString()
            : realIp;
    }
}
