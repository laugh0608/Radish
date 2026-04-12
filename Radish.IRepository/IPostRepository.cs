using Radish.Model;

namespace Radish.IRepository;

/// <summary>帖子仓储接口</summary>
public interface IPostRepository
{
    /// <summary>
    /// 分页查询普通论坛帖子列表
    /// </summary>
    /// <param name="categoryId">分类 Id（可空）</param>
    /// <param name="keyword">关键词（可空，匹配标题和正文）</param>
    /// <param name="startTime">开始时间（可空，基于帖子创建时间）</param>
    /// <param name="endTime">结束时间（可空，基于帖子创建时间）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="sortBy">排序方式：newest / hottest / essence</param>
    /// <returns>分页帖子实体和总数</returns>
    Task<(List<Post> data, int totalCount)> QueryForumPostPageAsync(
        long? categoryId,
        long? tagId,
        string? keyword,
        DateTime? startTime,
        DateTime? endTime,
        int pageIndex,
        int pageSize,
        string sortBy);

    /// <summary>
    /// 分页查询问答帖子
    /// </summary>
    /// <param name="categoryId">分类 Id（可空）</param>
    /// <param name="keyword">关键词（可空，匹配标题和正文）</param>
    /// <param name="startTime">开始时间（可空，基于帖子创建时间）</param>
    /// <param name="endTime">结束时间（可空，基于帖子创建时间）</param>
    /// <param name="isSolved">是否已解决（可空）</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="sortBy">排序方式：newest / pending / answers</param>
    /// <returns>分页帖子实体和总数</returns>
    Task<(List<Post> data, int totalCount)> QueryQuestionPostPageAsync(
        long? categoryId,
        long? tagId,
        string? keyword,
        DateTime? startTime,
        DateTime? endTime,
        bool? isSolved,
        int pageIndex,
        int pageSize,
        string sortBy);
}
