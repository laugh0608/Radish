using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Api.Controllers;

/// <summary>
/// 论坛分类控制器
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("论坛分类管理")]
public class CategoryController : ControllerBase
{
    private readonly IBaseService<Category, CategoryVo> _categoryService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;

    public CategoryController(
        IBaseService<Category, CategoryVo> categoryService,
        ICurrentUserAccessor currentUserAccessor,
        IAttachmentUrlResolver attachmentUrlResolver)
    {
        _categoryService = categoryService;
        _currentUserAccessor = currentUserAccessor;
        _attachmentUrlResolver = attachmentUrlResolver;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>
    /// 获取所有顶级分类
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetTopCategories()
    {
        var categories = (await _categoryService.QueryWithCacheAsync(
                c => c.ParentId == null && c.IsEnabled && !c.IsDeleted,
                300))
            .OrderByDescending(c => c.VoOrderSort)
            .ThenBy(c => c.VoId)
            .ToList();
        FillCategoryUrls(categories);
        return BuildSuccess(categories);
    }

    /// <summary>
    /// 获取指定分类的子分类
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetChildCategories(long parentId)
    {
        var categories = await _categoryService.QueryAsync(c => c.ParentId == parentId && c.IsEnabled && !c.IsDeleted);
        FillCategoryUrls(categories);
        return BuildSuccess(categories);
    }

    /// <summary>
    /// 根据 ID 获取分类详情
    /// </summary>
    [HttpGet("{id:long}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> GetById(long id)
    {
        var category = await _categoryService.QueryFirstAsync(c => c.Id == id && !c.IsDeleted);
        if (category == null)
        {
            return BuildError(HttpStatusCodeEnum.NotFound, "分类不存在");
        }

        FillCategoryUrl(category);
        return BuildSuccess(category);
    }

    /// <summary>
    /// 获取分类分页（管理端）
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.CategoriesView)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetPage(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        bool? isEnabled = null,
        bool includeDeleted = false)
    {
        var safePageIndex = pageIndex < 1 ? 1 : pageIndex;
        var safePageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
        var normalizedKeyword = keyword?.Trim() ?? string.Empty;
        var hasKeyword = !string.IsNullOrWhiteSpace(normalizedKeyword);
        var hasEnabledFilter = isEnabled.HasValue;
        var enabledValue = isEnabled ?? false;

        var (data, totalCount) = await _categoryService.QueryPageAsync(
            c => (includeDeleted || !c.IsDeleted) &&
                 (!hasEnabledFilter || c.IsEnabled == enabledValue) &&
                 (!hasKeyword ||
                  c.Name.Contains(normalizedKeyword) ||
                  c.Slug.Contains(normalizedKeyword) ||
                  (c.Description != null && c.Description.Contains(normalizedKeyword))),
            safePageIndex,
            safePageSize,
            c => c.OrderSort,
            OrderByType.Desc,
            c => c.CreateTime,
            OrderByType.Desc);

        FillCategoryUrls(data);
        return BuildSuccess(new PageModel<CategoryVo>
        {
            Page = safePageIndex,
            PageSize = safePageSize,
            DataCount = totalCount,
            PageCount = (int)Math.Ceiling(totalCount / (double)safePageSize),
            Data = data
        });
    }

    /// <summary>
    /// 创建分类（管理端）
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.CategoriesCreate)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> Create([FromBody] CreateCategoryDto createDto)
    {
        if (!ModelState.IsValid)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, "请求参数验证失败");
        }

        try
        {
            var level = await ResolveCategoryLevelAsync(createDto.ParentId);
            var category = new Category(createDto.Name)
            {
                Slug = NormalizeSlug(createDto.Name, createDto.Slug),
                Description = createDto.Description ?? string.Empty,
                IconAttachmentId = createDto.IconAttachmentId,
                CoverAttachmentId = createDto.CoverAttachmentId,
                ParentId = createDto.ParentId,
                Level = level,
                OrderSort = createDto.OrderSort,
                IsEnabled = createDto.IsEnabled,
                CreateBy = Current.UserName,
                CreateId = Current.UserId,
                ModifyBy = Current.UserName,
                ModifyId = Current.UserId,
                ModifyTime = DateTime.Now
            };

            var categoryId = await _categoryService.AddAsync(category);
            return BuildSuccess(categoryId, "创建成功");
        }
        catch (InvalidOperationException ex)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, ex.Message);
        }
    }

    /// <summary>
    /// 更新分类（管理端）
    /// </summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.CategoriesEdit)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Update(long id, [FromBody] CreateCategoryDto updateDto)
    {
        if (id <= 0)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, "分类 ID 无效");
        }

        if (!ModelState.IsValid)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, "请求参数验证失败");
        }

        if (updateDto.ParentId == id)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, "分类不能设置自己为父级");
        }

        var existing = await _categoryService.QueryFirstAsync(c => c.Id == id && !c.IsDeleted);
        if (existing == null)
        {
            return BuildError(HttpStatusCodeEnum.NotFound, "分类不存在");
        }

        try
        {
            var level = await ResolveCategoryLevelAsync(updateDto.ParentId);
            await _categoryService.UpdateColumnsAsync(
                c => new Category
                {
                    Name = updateDto.Name.Trim(),
                    Slug = NormalizeSlug(updateDto.Name, updateDto.Slug),
                    Description = updateDto.Description ?? string.Empty,
                    IconAttachmentId = updateDto.IconAttachmentId,
                    CoverAttachmentId = updateDto.CoverAttachmentId,
                    ParentId = updateDto.ParentId,
                    Level = level,
                    OrderSort = updateDto.OrderSort,
                    IsEnabled = updateDto.IsEnabled,
                    ModifyBy = Current.UserName,
                    ModifyId = Current.UserId,
                    ModifyTime = DateTime.Now
                },
                c => c.Id == id && !c.IsDeleted);

            return BuildSuccess(true, "更新成功");
        }
        catch (InvalidOperationException ex)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, ex.Message);
        }
    }

    /// <summary>
    /// 启用/禁用分类（管理端）
    /// </summary>
    [HttpPut("{id:long}/status")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.CategoriesToggle)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> ToggleStatus(long id, [FromQuery] bool enabled)
    {
        if (id <= 0)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, "分类 ID 无效");
        }

        var updated = await _categoryService.UpdateColumnsAsync(
            c => new Category
            {
                IsEnabled = enabled,
                ModifyBy = Current.UserName,
                ModifyId = Current.UserId,
                ModifyTime = DateTime.Now
            },
            c => c.Id == id && !c.IsDeleted);

        return updated > 0
            ? BuildSuccess(true, enabled ? "启用成功" : "禁用成功")
            : BuildError(HttpStatusCodeEnum.NotFound, "分类不存在");
    }

    /// <summary>
    /// 更新分类排序（管理端）
    /// </summary>
    [HttpPut("{id:long}/sort")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.CategoriesSort)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> UpdateSort(long id, [FromQuery] int sortOrder)
    {
        if (id <= 0)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, "分类 ID 无效");
        }

        if (sortOrder < 0)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, "排序值不能为负数");
        }

        var updated = await _categoryService.UpdateColumnsAsync(
            c => new Category
            {
                OrderSort = sortOrder,
                ModifyBy = Current.UserName,
                ModifyId = Current.UserId,
                ModifyTime = DateTime.Now
            },
            c => c.Id == id && !c.IsDeleted);

        return updated > 0
            ? BuildSuccess(true, "排序更新成功")
            : BuildError(HttpStatusCodeEnum.NotFound, "分类不存在");
    }

    /// <summary>
    /// 删除分类（管理端）
    /// </summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.CategoriesDelete)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Delete(long id)
    {
        var deleted = await _categoryService.SoftDeleteByIdAsync(id, Current.UserName);
        return deleted
            ? BuildSuccess(true, "删除成功")
            : BuildError(HttpStatusCodeEnum.NotFound, "分类不存在");
    }

    /// <summary>
    /// 恢复分类（管理端）
    /// </summary>
    [HttpPut("{id:long}/restore")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.CategoriesRestore)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Restore(long id)
    {
        var restored = await _categoryService.RestoreByIdAsync(id);
        return restored
            ? BuildSuccess(true, "恢复成功")
            : BuildError(HttpStatusCodeEnum.NotFound, "分类不存在");
    }

    private async Task<int> ResolveCategoryLevelAsync(long? parentId)
    {
        if (!parentId.HasValue)
        {
            return 0;
        }

        var parent = await _categoryService.QueryFirstAsync(c => c.Id == parentId.Value && !c.IsDeleted);
        if (parent == null)
        {
            throw new InvalidOperationException("父分类不存在");
        }

        return Math.Max(0, parent.VoLevel + 1);
    }

    private static string NormalizeSlug(string name, string? slug)
    {
        var rawValue = string.IsNullOrWhiteSpace(slug) ? name : slug;
        return rawValue.Trim().ToLowerInvariant().Replace(" ", "-", StringComparison.Ordinal);
    }

    private void FillCategoryUrls(List<CategoryVo> categories)
    {
        foreach (var category in categories)
        {
            FillCategoryUrl(category);
        }
    }

    private void FillCategoryUrl(CategoryVo category)
    {
        category.VoIcon = ResolveAttachmentUrl(category.VoIconAttachmentId);
        category.VoCoverImage = ResolveAttachmentUrl(category.VoCoverAttachmentId);
    }

    private string? ResolveAttachmentUrl(long? attachmentId)
    {
        if (!attachmentId.HasValue || attachmentId.Value <= 0)
        {
            return null;
        }

        return _attachmentUrlResolver.ResolveAttachmentUrl(attachmentId.Value);
    }

    private static MessageModel BuildSuccess(object? data, string message = "获取成功")
    {
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = message,
            ResponseData = data
        };
    }

    private static MessageModel BuildError(HttpStatusCodeEnum statusCode, string message)
    {
        return new MessageModel
        {
            IsSuccess = false,
            StatusCode = (int)statusCode,
            MessageInfo = message
        };
    }
}
