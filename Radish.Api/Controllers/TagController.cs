using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model;
using Radish.Shared;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>
/// 论坛标签控制器
/// </summary>
/// <remarks>
/// 提供标签的查询、热门标签等接口
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("论坛标签管理")]
public class TagController : ControllerBase
{
    private readonly ITagService _tagService;

    public TagController(ITagService tagService)
    {
        _tagService = tagService;
    }

    /// <summary>
    /// 获取所有标签
    /// </summary>
    /// <returns>标签列表</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetAll()
    {
        var tags = await _tagService.QueryAsync(t => t.IsEnabled && !t.IsDeleted);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = tags
        };
    }

    /// <summary>
    /// 获取热门标签
    /// </summary>
    /// <param name="topCount">返回数量，默认 20</param>
    /// <returns>热门标签列表（按帖子数量降序）</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetHotTags(int topCount = 20)
    {
        var allTags = await _tagService.QueryAsync(t => t.IsEnabled && !t.IsDeleted);
        var hotTags = allTags.OrderByDescending(t => t.PostCount)
                            .Take(topCount)
                            .ToList();

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = hotTags
        };
    }

    /// <summary>
    /// 根据 ID 获取标签详情
    /// </summary>
    /// <param name="id">标签 ID</param>
    /// <returns>标签详情</returns>
    [HttpGet("{id:long}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> GetById(long id)
    {
        var tag = await _tagService.QueryByIdAsync(id);

        if (tag == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "标签不存在"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = tag
        };
    }
}
