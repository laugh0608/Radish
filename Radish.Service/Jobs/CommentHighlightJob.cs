using Radish.IRepository;
using Radish.Model;
using Serilog;
using SqlSugar;

namespace Radish.Service.Jobs;

/// <summary>
/// 神评/沙发统计定时任务
/// </summary>
/// <remarks>
/// 每天凌晨 1 点执行，统计前一天的神评和沙发
/// </remarks>
public class CommentHighlightJob
{
    private readonly IBaseRepository<Comment> _commentRepository;
    private readonly IBaseRepository<CommentHighlight> _highlightRepository;

    public CommentHighlightJob(
        IBaseRepository<Comment> commentRepository,
        IBaseRepository<CommentHighlight> highlightRepository)
    {
        _commentRepository = commentRepository;
        _highlightRepository = highlightRepository;
    }

    /// <summary>
    /// 执行神评/沙发统计
    /// </summary>
    /// <param name="statDate">统计日期（默认为昨天）</param>
    /// <returns>统计结果（神评数量, 沙发数量）</returns>
    public async Task<(int godCommentCount, int sofaCount)> ExecuteAsync(DateTime? statDate = null)
    {
        try
        {
            // 默认统计昨天的数据
            var targetDate = (statDate ?? DateTime.Today.AddDays(-1)).Date;
            Log.Information("[CommentHighlight] 开始统计神评/沙发，日期：{StatDate}", targetDate);

            // 1. 统计神评（父评论中点赞数最高的）
            var godCommentCount = await StatGodCommentsAsync(targetDate);

            // 2. 统计沙发（每个父评论下子评论中点赞数最高的）
            var sofaCount = await StatSofasAsync(targetDate);

            Log.Information("[CommentHighlight] 统计完成，神评：{GodCount} 个，沙发：{SofaCount} 个",
                godCommentCount, sofaCount);

            return (godCommentCount, sofaCount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CommentHighlight] 统计神评/沙发时发生异常");
            return (0, 0);
        }
    }

    /// <summary>
    /// 统计神评（每个帖子的父评论中点赞数最高的）
    /// </summary>
    private async Task<int> StatGodCommentsAsync(DateTime statDate)
    {
        try
        {
            // 查询所有有评论的帖子
            var postsWithComments = await _commentRepository.Context.Queryable<Comment>()
                .Where(c => !c.IsDeleted && c.IsEnabled && c.ParentId == null)
                .GroupBy(c => c.PostId)
                .Select(g => g.PostId)
                .ToListAsync();

            if (!postsWithComments.Any())
            {
                Log.Information("[CommentHighlight] 没有找到有父评论的帖子");
                return 0;
            }

            var godComments = new List<CommentHighlight>();

            // 遍历每个帖子，找出神评
            foreach (var postId in postsWithComments)
            {
                // 查询该帖子的所有父评论，按点赞数降序、创建时间降序
                var topComments = await _commentRepository.Context.Queryable<Comment>()
                    .Where(c => c.PostId == postId &&
                               c.ParentId == null &&
                               !c.IsDeleted &&
                               c.IsEnabled)
                    .OrderByDescending(c => c.LikeCount)
                    .ThenByDescending(c => c.CreateTime)
                    .Take(5) // 取前5名，用于排名
                    .ToListAsync();

                if (!topComments.Any())
                {
                    continue;
                }

                // 检查是否需要追加新的神评
                var existingHighlight = await _highlightRepository.QueryFirstAsync(
                    h => h.PostId == postId &&
                         h.HighlightType == 1 &&
                         h.IsCurrent);

                var currentTopComment = topComments.First();

                // 如果当前神评与历史记录不同，或者点赞数有变化，则追加新记录
                bool shouldAdd = existingHighlight == null ||
                                existingHighlight.CommentId != currentTopComment.Id ||
                                existingHighlight.LikeCount != currentTopComment.LikeCount;

                if (shouldAdd)
                {
                    // 将之前的记录标记为非当前
                    if (existingHighlight != null)
                    {
                        await _highlightRepository.UpdateColumnsAsync(
                            h => new CommentHighlight { IsCurrent = false },
                            h => h.PostId == postId && h.HighlightType == 1 && h.IsCurrent);
                    }

                    // 添加新记录（可能有多个并列第一）
                    var rank = 1;
                    var topLikeCount = topComments.First().LikeCount;

                    foreach (var comment in topComments)
                    {
                        // 只记录点赞数最高的（可能有多个并列）
                        if (comment.LikeCount < topLikeCount)
                        {
                            break;
                        }

                        godComments.Add(new CommentHighlight
                        {
                            PostId = postId,
                            CommentId = comment.Id,
                            ParentCommentId = null,
                            HighlightType = 1, // 神评
                            StatDate = statDate,
                            LikeCount = comment.LikeCount,
                            Rank = rank,
                            ContentSnapshot = comment.Content,
                            AuthorId = comment.AuthorId,
                            AuthorName = comment.AuthorName,
                            IsCurrent = true,
                            TenantId = comment.TenantId,
                            CreateTime = DateTime.Now,
                            CreateBy = "CommentHighlightJob"
                        });

                        rank++;
                    }
                }
            }

            // 批量插入
            if (godComments.Any())
            {
                await _highlightRepository.AddAsync(godComments);
                Log.Information("[CommentHighlight] 新增神评记录：{Count} 条", godComments.Count);
            }

            return godComments.Count;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CommentHighlight] 统计神评时发生异常");
            return 0;
        }
    }

    /// <summary>
    /// 统计沙发（每个父评论下子评论中点赞数最高的）
    /// </summary>
    private async Task<int> StatSofasAsync(DateTime statDate)
    {
        try
        {
            // 查询所有有子评论的父评论
            var parentsWithChildren = await _commentRepository.Context.Queryable<Comment>()
                .Where(c => !c.IsDeleted && c.IsEnabled && c.ParentId != null)
                .GroupBy(c => c.ParentId)
                .Select(g => g.ParentId)
                .ToListAsync();

            if (!parentsWithChildren.Any())
            {
                Log.Information("[CommentHighlight] 没有找到有子评论的父评论");
                return 0;
            }

            var sofas = new List<CommentHighlight>();

            // 遍历每个父评论，找出沙发
            foreach (var parentId in parentsWithChildren)
            {
                if (!parentId.HasValue) continue;

                // 查询该父评论下的所有子评论，按点赞数降序、创建时间降序
                var topChildren = await _commentRepository.Context.Queryable<Comment>()
                    .Where(c => c.ParentId == parentId.Value &&
                               !c.IsDeleted &&
                               c.IsEnabled)
                    .OrderByDescending(c => c.LikeCount)
                    .ThenByDescending(c => c.CreateTime)
                    .Take(5) // 取前5名
                    .ToListAsync();

                if (!topChildren.Any())
                {
                    continue;
                }

                // 获取父评论信息（用于获取 PostId）
                var parentComment = await _commentRepository.QueryByIdAsync(parentId.Value);
                if (parentComment == null) continue;

                // 检查是否需要追加新的沙发
                var existingHighlight = await _highlightRepository.QueryFirstAsync(
                    h => h.ParentCommentId == parentId.Value &&
                         h.HighlightType == 2 &&
                         h.IsCurrent);

                var currentTopChild = topChildren.First();

                bool shouldAdd = existingHighlight == null ||
                                existingHighlight.CommentId != currentTopChild.Id ||
                                existingHighlight.LikeCount != currentTopChild.LikeCount;

                if (shouldAdd)
                {
                    // 将之前的记录标记为非当前
                    if (existingHighlight != null)
                    {
                        await _highlightRepository.UpdateColumnsAsync(
                            h => new CommentHighlight { IsCurrent = false },
                            h => h.ParentCommentId == parentId.Value &&
                                 h.HighlightType == 2 &&
                                 h.IsCurrent);
                    }

                    // 添加新记录
                    var rank = 1;
                    var topLikeCount = topChildren.First().LikeCount;

                    foreach (var child in topChildren)
                    {
                        if (child.LikeCount < topLikeCount)
                        {
                            break;
                        }

                        sofas.Add(new CommentHighlight
                        {
                            PostId = parentComment.PostId,
                            CommentId = child.Id,
                            ParentCommentId = parentId.Value,
                            HighlightType = 2, // 沙发
                            StatDate = statDate,
                            LikeCount = child.LikeCount,
                            Rank = rank,
                            ContentSnapshot = child.Content,
                            AuthorId = child.AuthorId,
                            AuthorName = child.AuthorName,
                            IsCurrent = true,
                            TenantId = child.TenantId,
                            CreateTime = DateTime.Now,
                            CreateBy = "CommentHighlightJob"
                        });

                        rank++;
                    }
                }
            }

            // 批量插入
            if (sofas.Any())
            {
                await _highlightRepository.AddAsync(sofas);
                Log.Information("[CommentHighlight] 新增沙发记录：{Count} 条", sofas.Count);
            }

            return sofas.Count;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CommentHighlight] 统计沙发时发生异常");
            return 0;
        }
    }
}
