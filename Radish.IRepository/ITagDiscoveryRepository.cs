using Radish.Model;

namespace Radish.IRepository;

/// <summary>公开标签发现聚合仓储。</summary>
public interface ITagDiscoveryRepository
{
    /// <summary>统计标签下当前公开可见的帖子数量。</summary>
    Task<int> QueryPublicPostCountAsync(long tagId);

    /// <summary>按公开帖子数量查询热门标签。</summary>
    Task<List<Tag>> QueryHotPublicTagsAsync(int topCount);

    /// <summary>查询与来源标签共同出现在公开帖子中的相关标签。</summary>
    Task<List<Tag>> QueryRelatedPublicTagsAsync(long sourceTagId, int topCount);

    /// <summary>统计至少关联一篇公开帖子的标签数量。</summary>
    Task<int> QueryIndexableTagCountAsync();

    /// <summary>分页查询可被公开索引的标签。</summary>
    Task<List<Tag>> QueryIndexableTagPageAsync(int pageIndex, int pageSize);
}
