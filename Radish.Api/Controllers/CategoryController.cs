using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>
/// 论坛分类控制器
/// </summary>
/// <remarks>
/// 提供论坛分类的查询、创建、更新等接口
/// </remarks>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("论坛分类管理")]
public class CategoryController : ControllerBase
{
    private readonly IBaseService<Category, CategoryVo> _categoryService;

    public CategoryController(IBaseService<Category, CategoryVo> categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// 获取所有顶级分类
    /// </summary>
    /// <returns>顶级分类列表</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetTopCategories()
    {
        var categories = await _categoryService.QueryAsync(c => c.ParentId == null && c.IsEnabled && !c.IsDeleted);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = categories
        };
    }

    /// <summary>
    /// 获取指定分类的子分类
    /// </summary>
    /// <param name="parentId">父分类 ID</param>
    /// <returns>子分类列表</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetChildCategories(long parentId)
    {
        var categories = await _categoryService.QueryAsync(c => c.ParentId == parentId && c.IsEnabled && !c.IsDeleted);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = categories
        };
    }

    /// <summary>
    /// 根据 ID 获取分类详情
    /// </summary>
    /// <param name="id">分类 ID</param>
    /// <returns>分类详情</returns>
    [HttpGet("{id:long}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> GetById(long id)
    {
        var category = await _categoryService.QueryByIdAsync(id);

        if (category == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "分类不存在"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = category
        };
    }

    /// <summary>
    /// 创建分类
    /// </summary>
    /// <param name="category">分类信息</param>
    /// <returns>创建的分类 ID</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> Create([FromBody] Category category)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "分类名称不能为空"
            };
        }

        var categoryId = await _categoryService.AddAsync(category);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "创建成功",
            ResponseData = categoryId
        };
    }
}
