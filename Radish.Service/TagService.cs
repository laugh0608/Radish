using AutoMapper;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using SqlSugar;

namespace Radish.Service;

/// <summary>标签服务实现</summary>
public class TagService : BaseService<Tag, TagVo>, ITagService
{
    private readonly IBaseRepository<Tag> _tagRepository;

    public TagService(IMapper mapper, IBaseRepository<Tag> baseRepository)
        : base(mapper, baseRepository)
    {
        _tagRepository = baseRepository;
    }

    /// <summary>
    /// 根据标签名称获取或创建标签
    /// </summary>
    public async Task<Tag> GetOrCreateTagAsync(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException("标签名称不能为空", nameof(tagName));
        }

        // 尝试获取现有标签
        var tags = await _tagRepository.QueryAsync(t => t.Name == tagName.Trim() && !t.IsDeleted && t.IsEnabled);
        var existingTag = tags.FirstOrDefault();

        if (existingTag != null)
        {
            return existingTag;
        }

        // 创建新标签
        var newTag = new Tag(tagName.Trim())
        {
            SortOrder = 999,
            IsEnabled = true,
            IsFixed = false,
            IsDeleted = false
        };

        var tagId = await _tagRepository.AddAsync(newTag);
        newTag.Id = tagId;

        return newTag;
    }

    /// <summary>
    /// 获取固定标签列表（按排序值升序）
    /// </summary>
    public async Task<List<TagVo>> GetFixedTagsAsync(bool includeDisabled = false)
    {
        var query = await QueryWithOrderAsync(
            t => t.IsFixed && !t.IsDeleted && (includeDisabled || t.IsEnabled),
            t => t.SortOrder,
            OrderByType.Asc);

        return query;
    }

    /// <summary>
    /// 分页查询标签（后台管理）
    /// </summary>
    public async Task<PageModel<TagVo>> GetTagPageAsync(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        bool? isEnabled = null,
        bool? isFixed = null,
        bool includeDeleted = false)
    {
        if (pageIndex < 1)
        {
            pageIndex = 1;
        }

        if (pageSize < 1 || pageSize > 100)
        {
            pageSize = 20;
        }

        keyword = keyword?.Trim();

        var (data, totalCount) = await QueryPageAsync(
            whereExpression: t =>
                (includeDeleted || !t.IsDeleted) &&
                (string.IsNullOrWhiteSpace(keyword) || t.Name.Contains(keyword) || t.Description.Contains(keyword)) &&
                (!isEnabled.HasValue || t.IsEnabled == isEnabled.Value) &&
                (!isFixed.HasValue || t.IsFixed == isFixed.Value),
            pageIndex: pageIndex,
            pageSize: pageSize,
            orderByExpression: t => t.SortOrder,
            orderByType: OrderByType.Asc,
            thenByExpression: t => t.Id,
            thenByType: OrderByType.Asc);

        return new PageModel<TagVo>
        {
            Page = pageIndex,
            PageSize = pageSize,
            DataCount = totalCount,
            PageCount = (int)Math.Ceiling(totalCount / (double)pageSize),
            Data = data
        };
    }

    /// <summary>
    /// 创建标签
    /// </summary>
    public async Task<long> CreateTagAsync(CreateTagDto createDto, long operatorId, string operatorName)
    {
        if (createDto == null)
        {
            throw new ArgumentNullException(nameof(createDto));
        }

        var normalizedName = createDto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("标签名称不能为空", nameof(createDto));
        }

        var exists = await _tagRepository.QueryExistsAsync(t => t.Name == normalizedName && !t.IsDeleted);
        if (exists)
        {
            throw new InvalidOperationException("标签名称已存在");
        }

        var tag = new Tag(new TagInitializationOptions(normalizedName)
        {
            Slug = createDto.Slug,
            Description = createDto.Description,
            Color = createDto.Color,
            SortOrder = createDto.SortOrder,
            IsEnabled = createDto.IsEnabled,
            IsFixed = createDto.IsFixed,
            CreateId = operatorId,
            CreateBy = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName
        });

        return await AddAsync(tag);
    }

    /// <summary>
    /// 更新标签
    /// </summary>
    public async Task<bool> UpdateTagAsync(long id, CreateTagDto updateDto, long operatorId, string operatorName)
    {
        if (id <= 0)
        {
            throw new ArgumentException("标签ID无效", nameof(id));
        }

        if (updateDto == null)
        {
            throw new ArgumentNullException(nameof(updateDto));
        }

        var existingTag = await _tagRepository.QueryByIdAsync(id);
        if (existingTag == null || existingTag.IsDeleted)
        {
            return false;
        }

        var normalizedName = updateDto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("标签名称不能为空", nameof(updateDto));
        }

        var duplicated = await _tagRepository.QueryExistsAsync(t => t.Id != id && t.Name == normalizedName && !t.IsDeleted);
        if (duplicated)
        {
            throw new InvalidOperationException("标签名称已存在");
        }

        existingTag.Name = normalizedName;
        existingTag.Slug = string.IsNullOrWhiteSpace(updateDto.Slug)
            ? normalizedName.ToLowerInvariant().Replace(" ", "-")
            : updateDto.Slug.Trim().ToLowerInvariant();
        existingTag.Description = updateDto.Description?.Trim() ?? string.Empty;
        existingTag.Color = updateDto.Color?.Trim() ?? string.Empty;
        existingTag.SortOrder = updateDto.SortOrder;
        existingTag.IsEnabled = updateDto.IsEnabled;
        existingTag.IsFixed = updateDto.IsFixed;
        existingTag.ModifyId = operatorId;
        existingTag.ModifyBy = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName;
        existingTag.ModifyTime = DateTime.Now;

        return await _tagRepository.UpdateAsync(existingTag);
    }

    /// <summary>
    /// 启用/禁用标签
    /// </summary>
    public async Task<bool> ToggleTagStatusAsync(long id, bool enabled, long operatorId, string operatorName)
    {
        if (id <= 0)
        {
            throw new ArgumentException("标签ID无效", nameof(id));
        }

        var existingTag = await _tagRepository.QueryByIdAsync(id);
        if (existingTag == null || existingTag.IsDeleted)
        {
            return false;
        }

        existingTag.IsEnabled = enabled;
        existingTag.ModifyId = operatorId;
        existingTag.ModifyBy = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName;
        existingTag.ModifyTime = DateTime.Now;

        return await _tagRepository.UpdateAsync(existingTag);
    }

    /// <summary>
    /// 更新标签排序
    /// </summary>
    public async Task<bool> UpdateTagSortOrderAsync(long id, int sortOrder, long operatorId, string operatorName)
    {
        if (id <= 0)
        {
            throw new ArgumentException("标签ID无效", nameof(id));
        }

        if (sortOrder < 0)
        {
            throw new ArgumentException("排序值不能为负数", nameof(sortOrder));
        }

        var existingTag = await _tagRepository.QueryByIdAsync(id);
        if (existingTag == null || existingTag.IsDeleted)
        {
            return false;
        }

        existingTag.SortOrder = sortOrder;
        existingTag.ModifyId = operatorId;
        existingTag.ModifyBy = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName;
        existingTag.ModifyTime = DateTime.Now;

        return await _tagRepository.UpdateAsync(existingTag);
    }

    /// <summary>
    /// 软删除标签
    /// </summary>
    public async Task<bool> SoftDeleteTagAsync(long id, long operatorId, string operatorName)
    {
        if (id <= 0)
        {
            throw new ArgumentException("标签ID无效", nameof(id));
        }

        var exists = await _tagRepository.QueryExistsAsync(t => t.Id == id && !t.IsDeleted);
        if (!exists)
        {
            return false;
        }

        var deletedBy = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName;
        var result = await SoftDeleteByIdAsync(id, deletedBy);
        if (!result)
        {
            return false;
        }

        await _tagRepository.UpdateColumnsAsync(
            t => new Tag
            {
                ModifyId = operatorId,
                ModifyBy = deletedBy,
                ModifyTime = DateTime.Now
            },
            t => t.Id == id);

        return true;
    }

    /// <summary>
    /// 恢复软删除标签
    /// </summary>
    public async Task<bool> RestoreTagAsync(long id, long operatorId, string operatorName)
    {
        if (id <= 0)
        {
            throw new ArgumentException("标签ID无效", nameof(id));
        }

        var existingTags = await _tagRepository.QueryAsync(t => t.Id == id);
        var existingTag = existingTags.FirstOrDefault();
        if (existingTag == null || !existingTag.IsDeleted)
        {
            return false;
        }

        var restored = await RestoreByIdAsync(id);
        if (!restored)
        {
            return false;
        }

        await _tagRepository.UpdateColumnsAsync(
            t => new Tag
            {
                ModifyId = operatorId,
                ModifyBy = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName,
                ModifyTime = DateTime.Now
            },
            t => t.Id == id);

        return true;
    }
}
