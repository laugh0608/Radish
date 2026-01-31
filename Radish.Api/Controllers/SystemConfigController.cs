using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared;

namespace Radish.Api.Controllers;

/// <summary>系统配置控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = "SystemOrAdmin")]
public class SystemConfigController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public SystemConfigController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>获取系统配置列表</summary>
    /// <returns>系统配置列表</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<List<SystemConfigVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel<List<SystemConfigVo>>> GetSystemConfigs()
    {
        try
        {
            var configs = new List<SystemConfigVo>
            {
                new()
                {
                    VoId = 1,
                    VoCategory = "商城配置",
                    VoKey = "Shop.OrderTimeoutMinutes",
                    VoName = "订单超时时间",
                    VoValue = "30",
                    VoDescription = "订单超时时间（分钟）",
                    VoType = "number",
                    VoIsEnabled = true,
                    VoCreateTime = DateTime.Now.AddDays(-30),
                    VoModifyTime = DateTime.Now.AddDays(-1)
                },
                new()
                {
                    VoId = 2,
                    VoCategory = "商城配置",
                    VoKey = "Shop.StockWarningThreshold",
                    VoName = "库存预警阈值",
                    VoValue = "10",
                    VoDescription = "商品库存低于此值时发出预警",
                    VoType = "number",
                    VoIsEnabled = true,
                    VoCreateTime = DateTime.Now.AddDays(-30),
                    VoModifyTime = DateTime.Now.AddDays(-2)
                },
                new()
                {
                    VoId = 3,
                    VoCategory = "萝卜币配置",
                    VoKey = "Coin.DailyRewardLimit",
                    VoName = "每日奖励上限",
                    VoValue = "100",
                    VoDescription = "用户每日可获得的萝卜币奖励上限",
                    VoType = "number",
                    VoIsEnabled = true,
                    VoCreateTime = DateTime.Now.AddDays(-25),
                    VoModifyTime = DateTime.Now.AddDays(-3)
                },
                new()
                {
                    VoId = 4,
                    VoCategory = "萝卜币配置",
                    VoKey = "Coin.PostReward",
                    VoName = "发帖奖励",
                    VoValue = "5",
                    VoDescription = "用户发帖可获得的萝卜币奖励",
                    VoType = "number",
                    VoIsEnabled = true,
                    VoCreateTime = DateTime.Now.AddDays(-25),
                    VoModifyTime = DateTime.Now.AddDays(-4)
                },
                new()
                {
                    VoId = 5,
                    VoCategory = "经验值配置",
                    VoKey = "Experience.PostReward",
                    VoName = "发帖经验奖励",
                    VoValue = "10",
                    VoDescription = "用户发帖可获得的经验值",
                    VoType = "number",
                    VoIsEnabled = true,
                    VoCreateTime = DateTime.Now.AddDays(-20),
                    VoModifyTime = DateTime.Now.AddDays(-5)
                },
                new()
                {
                    VoId = 6,
                    VoCategory = "经验值配置",
                    VoKey = "Experience.CommentReward",
                    VoName = "评论经验奖励",
                    VoValue = "5",
                    VoDescription = "用户评论可获得的经验值",
                    VoType = "number",
                    VoIsEnabled = true,
                    VoCreateTime = DateTime.Now.AddDays(-20),
                    VoModifyTime = DateTime.Now.AddDays(-6)
                },
                new()
                {
                    VoId = 7,
                    VoCategory = "通知配置",
                    VoKey = "Notification.Enable",
                    VoName = "启用通知推送",
                    VoValue = "true",
                    VoDescription = "是否启用实时通知推送",
                    VoType = "boolean",
                    VoIsEnabled = true,
                    VoCreateTime = DateTime.Now.AddDays(-15),
                    VoModifyTime = DateTime.Now.AddDays(-7)
                },
                new()
                {
                    VoId = 8,
                    VoCategory = "通知配置",
                    VoKey = "Notification.DedupWindowMinutes",
                    VoName = "通知去重时间窗口",
                    VoValue = "5",
                    VoDescription = "相同类型通知的去重时间窗口（分钟）",
                    VoType = "number",
                    VoIsEnabled = true,
                    VoCreateTime = DateTime.Now.AddDays(-15),
                    VoModifyTime = DateTime.Now.AddDays(-8)
                },
                new()
                {
                    VoId = 9,
                    VoCategory = "文件上传",
                    VoKey = "FileUpload.MaxImageSize",
                    VoName = "图片最大大小",
                    VoValue = "5242880",
                    VoDescription = "图片文件最大大小（字节），默认5MB",
                    VoType = "number",
                    VoIsEnabled = true,
                    VoCreateTime = DateTime.Now.AddDays(-10),
                    VoModifyTime = DateTime.Now.AddDays(-9)
                },
                new()
                {
                    VoId = 10,
                    VoCategory = "文件上传",
                    VoKey = "FileUpload.AllowedImageTypes",
                    VoName = "允许的图片类型",
                    VoValue = "jpg,jpeg,png,gif,webp",
                    VoDescription = "允许上传的图片文件类型",
                    VoType = "string",
                    VoIsEnabled = true,
                    VoCreateTime = DateTime.Now.AddDays(-10),
                    VoModifyTime = DateTime.Now.AddDays(-10)
                }
            };

            return MessageModel<List<SystemConfigVo>>.Success("获取成功", configs);
        }
        catch (Exception ex)
        {
            return MessageModel<List<SystemConfigVo>>.Failed($"获取系统配置失败：{ex.Message}");
        }
    }

    /// <summary>获取配置分类列表</summary>
    /// <returns>配置分类列表</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<List<string>>), StatusCodes.Status200OK)]
    public async Task<MessageModel<List<string>>> GetConfigCategories()
    {
        try
        {
            var categories = new List<string>
            {
                "商城配置",
                "萝卜币配置",
                "经验值配置",
                "通知配置",
                "文件上传",
                "安全配置",
                "缓存配置",
                "日志配置"
            };

            return MessageModel<List<string>>.Success("获取成功", categories);
        }
        catch (Exception ex)
        {
            return MessageModel<List<string>>.Failed($"获取配置分类失败：{ex.Message}");
        }
    }

    /// <summary>根据ID获取配置详情</summary>
    /// <param name="id">配置ID</param>
    /// <returns>配置详情</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<SystemConfigVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel<SystemConfigVo>> GetConfigById(int id)
    {
        try
        {
            // TODO: 从数据库或配置文件中获取具体配置
            var config = new SystemConfigVo
            {
                VoId = id,
                VoCategory = "商城配置",
                VoKey = "Shop.OrderTimeoutMinutes",
                VoName = "订单超时时间",
                VoValue = "30",
                VoDescription = "订单超时时间（分钟）",
                VoType = "number",
                VoIsEnabled = true,
                VoCreateTime = DateTime.Now.AddDays(-30),
                VoModifyTime = DateTime.Now.AddDays(-1)
            };

            return MessageModel<SystemConfigVo>.Success("获取成功", config);
        }
        catch (Exception ex)
        {
            return MessageModel<SystemConfigVo>.Failed($"获取配置详情失败：{ex.Message}");
        }
    }

    /// <summary>更新系统配置</summary>
    /// <param name="id">配置ID</param>
    /// <param name="request">更新请求</param>
    /// <returns>更新结果</returns>
    [HttpPut]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> UpdateConfig(int id, [FromBody] UpdateConfigRequest request)
    {
        try
        {
            if (request == null)
            {
                return MessageModel.Failed("请求参数不能为空");
            }

            // TODO: 实现配置更新逻辑
            // 这里需要根据实际需求实现配置的持久化存储

            return MessageModel.Success("更新成功");
        }
        catch (Exception ex)
        {
            return MessageModel.Failed($"更新配置失败：{ex.Message}");
        }
    }

    /// <summary>创建系统配置</summary>
    /// <param name="request">创建请求</param>
    /// <returns>创建结果</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> CreateConfig([FromBody] CreateConfigRequest request)
    {
        try
        {
            if (request == null)
            {
                return MessageModel.Failed("请求参数不能为空");
            }

            // TODO: 实现配置创建逻辑

            return MessageModel.Success("创建成功");
        }
        catch (Exception ex)
        {
            return MessageModel.Failed($"创建配置失败：{ex.Message}");
        }
    }

    /// <summary>删除系统配置</summary>
    /// <param name="id">配置ID</param>
    /// <returns>删除结果</returns>
    [HttpDelete]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> DeleteConfig(int id)
    {
        try
        {
            // TODO: 实现配置删除逻辑

            return MessageModel.Success("删除成功");
        }
        catch (Exception ex)
        {
            return MessageModel.Failed($"删除配置失败：{ex.Message}");
        }
    }
}

/// <summary>更新配置请求</summary>
public class UpdateConfigRequest
{
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
}

/// <summary>创建配置请求</summary>
public class CreateConfigRequest
{
    public string Category { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "string";
    public bool IsEnabled { get; set; } = true;
}