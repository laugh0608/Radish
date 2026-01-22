using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Model;
using Radish.Shared;
using Radish.Shared.CustomEnum;

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
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetSystemConfigs()
    {
        try
        {
            var configs = new List<object>
            {
                new
                {
                    Id = 1,
                    Category = "商城配置",
                    Key = "Shop.OrderTimeoutMinutes",
                    Name = "订单超时时间",
                    Value = "30",
                    Description = "订单超时时间（分钟）",
                    Type = "number",
                    IsEnabled = true,
                    CreateTime = DateTime.Now.AddDays(-30),
                    ModifyTime = DateTime.Now.AddDays(-1)
                },
                new
                {
                    Id = 2,
                    Category = "商城配置",
                    Key = "Shop.StockWarningThreshold",
                    Name = "库存预警阈值",
                    Value = "10",
                    Description = "商品库存低于此值时发出预警",
                    Type = "number",
                    IsEnabled = true,
                    CreateTime = DateTime.Now.AddDays(-30),
                    ModifyTime = DateTime.Now.AddDays(-2)
                },
                new
                {
                    Id = 3,
                    Category = "萝卜币配置",
                    Key = "Coin.DailyRewardLimit",
                    Name = "每日奖励上限",
                    Value = "100",
                    Description = "用户每日可获得的萝卜币奖励上限",
                    Type = "number",
                    IsEnabled = true,
                    CreateTime = DateTime.Now.AddDays(-25),
                    ModifyTime = DateTime.Now.AddDays(-3)
                },
                new
                {
                    Id = 4,
                    Category = "萝卜币配置",
                    Key = "Coin.PostReward",
                    Name = "发帖奖励",
                    Value = "5",
                    Description = "用户发帖可获得的萝卜币奖励",
                    Type = "number",
                    IsEnabled = true,
                    CreateTime = DateTime.Now.AddDays(-25),
                    ModifyTime = DateTime.Now.AddDays(-4)
                },
                new
                {
                    Id = 5,
                    Category = "经验值配置",
                    Key = "Experience.PostReward",
                    Name = "发帖经验奖励",
                    Value = "10",
                    Description = "用户发帖可获得的经验值",
                    Type = "number",
                    IsEnabled = true,
                    CreateTime = DateTime.Now.AddDays(-20),
                    ModifyTime = DateTime.Now.AddDays(-5)
                },
                new
                {
                    Id = 6,
                    Category = "经验值配置",
                    Key = "Experience.CommentReward",
                    Name = "评论经验奖励",
                    Value = "5",
                    Description = "用户评论可获得的经验值",
                    Type = "number",
                    IsEnabled = true,
                    CreateTime = DateTime.Now.AddDays(-20),
                    ModifyTime = DateTime.Now.AddDays(-6)
                },
                new
                {
                    Id = 7,
                    Category = "通知配置",
                    Key = "Notification.Enable",
                    Name = "启用通知推送",
                    Value = "true",
                    Description = "是否启用实时通知推送",
                    Type = "boolean",
                    IsEnabled = true,
                    CreateTime = DateTime.Now.AddDays(-15),
                    ModifyTime = DateTime.Now.AddDays(-7)
                },
                new
                {
                    Id = 8,
                    Category = "通知配置",
                    Key = "Notification.DedupWindowMinutes",
                    Name = "通知去重时间窗口",
                    Value = "5",
                    Description = "相同类型通知的去重时间窗口（分钟）",
                    Type = "number",
                    IsEnabled = true,
                    CreateTime = DateTime.Now.AddDays(-15),
                    ModifyTime = DateTime.Now.AddDays(-8)
                },
                new
                {
                    Id = 9,
                    Category = "文件上传",
                    Key = "FileUpload.MaxImageSize",
                    Name = "图片最大大小",
                    Value = "5242880",
                    Description = "图片文件最大大小（字节），默认5MB",
                    Type = "number",
                    IsEnabled = true,
                    CreateTime = DateTime.Now.AddDays(-10),
                    ModifyTime = DateTime.Now.AddDays(-9)
                },
                new
                {
                    Id = 10,
                    Category = "文件上传",
                    Key = "FileUpload.AllowedImageTypes",
                    Name = "允许的图片类型",
                    Value = "jpg,jpeg,png,gif,webp",
                    Description = "允许上传的图片文件类型",
                    Type = "string",
                    IsEnabled = true,
                    CreateTime = DateTime.Now.AddDays(-10),
                    ModifyTime = DateTime.Now.AddDays(-10)
                }
            };

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = configs
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = $"获取系统配置失败：{ex.Message}"
            };
        }
    }

    /// <summary>获取配置分类列表</summary>
    /// <returns>配置分类列表</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetConfigCategories()
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

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = categories
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = $"获取配置分类失败：{ex.Message}"
            };
        }
    }

    /// <summary>根据ID获取配置详情</summary>
    /// <param name="id">配置ID</param>
    /// <returns>配置详情</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetConfigById(int id)
    {
        try
        {
            // TODO: 从数据库或配置文件中获取具体配置
            var config = new
            {
                Id = id,
                Category = "商城配置",
                Key = "Shop.OrderTimeoutMinutes",
                Name = "订单超时时间",
                Value = "30",
                Description = "订单超时时间（分钟）",
                Type = "number",
                IsEnabled = true,
                CreateTime = DateTime.Now.AddDays(-30),
                ModifyTime = DateTime.Now.AddDays(-1)
            };

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = config
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = $"获取配置详情失败：{ex.Message}"
            };
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
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                    MessageInfo = "请求参数不能为空"
                };
            }

            // TODO: 实现配置更新逻辑
            // 这里需要根据实际需求实现配置的持久化存储

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "更新成功"
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = $"更新配置失败：{ex.Message}"
            };
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
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                    MessageInfo = "请求参数不能为空"
                };
            }

            // TODO: 实现配置创建逻辑

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "创建成功"
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = $"创建配置失败：{ex.Message}"
            };
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

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "删除成功"
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = $"删除配置失败：{ex.Message}"
            };
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