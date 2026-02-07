using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>标签服务接口</summary>
public interface ITagService : IBaseService<Tag, TagVo>
{
    /// <summary>
    /// 根据标签名称获取或创建标签
    /// </summary>
    /// <param name="tagName">标签名称</param>
    /// <returns>标签实体</returns>
    Task<Tag> GetOrCreateTagAsync(string tagName);

    /// <summary>
    /// 获取固定标签列表（按排序值升序）
    /// </summary>
    /// <param name="includeDisabled">是否包含未启用标签</param>
    /// <returns>固定标签列表</returns>
    Task<List<TagVo>> GetFixedTagsAsync(bool includeDisabled = false);

    /// <summary>
    /// 分页查询标签（后台管理）
    /// </summary>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="keyword">关键词（匹配名称/描述）</param>
    /// <param name="isEnabled">启用状态筛选</param>
    /// <param name="isFixed">固定标签筛选</param>
    /// <returns>分页标签数据</returns>
    Task<PageModel<TagVo>> GetTagPageAsync(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        bool? isEnabled = null,
        bool? isFixed = null,
        bool includeDeleted = false);

    /// <summary>
    /// 创建标签
    /// </summary>
    /// <param name="createDto">创建参数</param>
    /// <param name="operatorId">操作人 ID</param>
    /// <param name="operatorName">操作人名称</param>
    /// <returns>新标签 ID</returns>
    Task<long> CreateTagAsync(CreateTagDto createDto, long operatorId, string operatorName);

    /// <summary>
    /// 更新标签
    /// </summary>
    /// <param name="id">标签 ID</param>
    /// <param name="updateDto">更新参数</param>
    /// <param name="operatorId">操作人 ID</param>
    /// <param name="operatorName">操作人名称</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdateTagAsync(long id, CreateTagDto updateDto, long operatorId, string operatorName);

    /// <summary>
    /// 启用/禁用标签
    /// </summary>
    /// <param name="id">标签 ID</param>
    /// <param name="enabled">是否启用</param>
    /// <param name="operatorId">操作人 ID</param>
    /// <param name="operatorName">操作人名称</param>
    /// <returns>是否成功</returns>
    Task<bool> ToggleTagStatusAsync(long id, bool enabled, long operatorId, string operatorName);

    /// <summary>
    /// 更新标签排序
    /// </summary>
    /// <param name="id">标签 ID</param>
    /// <param name="sortOrder">排序值</param>
    /// <param name="operatorId">操作人 ID</param>
    /// <param name="operatorName">操作人名称</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdateTagSortOrderAsync(long id, int sortOrder, long operatorId, string operatorName);

    /// <summary>
    /// 软删除标签
    /// </summary>
    /// <param name="id">标签 ID</param>
    /// <param name="operatorId">操作人 ID</param>
    /// <param name="operatorName">操作人名称</param>
    /// <returns>是否成功</returns>
    Task<bool> SoftDeleteTagAsync(long id, long operatorId, string operatorName);

    /// <summary>
    /// 恢复软删除标签
    /// </summary>
    /// <param name="id">标签 ID</param>
    /// <param name="operatorId">操作人 ID</param>
    /// <param name="operatorName">操作人名称</param>
    /// <returns>是否成功</returns>
    Task<bool> RestoreTagAsync(long id, long operatorId, string operatorName);
}
