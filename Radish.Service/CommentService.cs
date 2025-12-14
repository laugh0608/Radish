using AutoMapper;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Service;

/// <summary>评论服务实现</summary>
public class CommentService : BaseService<Comment, CommentVo>, ICommentService
{
    private readonly IBaseRepository<Comment> _commentRepository;
    private readonly IPostService _postService;

    public CommentService(
        IMapper mapper,
        IBaseRepository<Comment> baseRepository,
        IPostService postService)
        : base(mapper, baseRepository)
    {
        _commentRepository = baseRepository;
        _postService = postService;
    }

    /// <summary>
    /// 获取帖子的评论树
    /// </summary>
    public async Task<List<CommentVo>> GetCommentTreeAsync(long postId)
    {
        // 获取所有评论
        var comments = await QueryAsync(c => c.PostId == postId && c.IsEnabled && !c.IsDeleted);

        // 构建树形结构
        var commentMap = comments.ToDictionary(c => c.Id);
        var rootComments = new List<CommentVo>();

        foreach (var comment in comments)
        {
            if (comment.ParentId == null)
            {
                // 顶级评论
                rootComments.Add(comment);
            }
            else if (commentMap.TryGetValue(comment.ParentId.Value, out var parent))
            {
                // 子评论
                parent.Children ??= new List<CommentVo>();
                parent.Children.Add(comment);
            }
        }

        // 按时间排序
        return rootComments.OrderByDescending(c => c.IsTop)
                          .ThenBy(c => c.CreateTime)
                          .ToList();
    }

    /// <summary>
    /// 添加评论
    /// </summary>
    public async Task<long> AddCommentAsync(Comment comment)
    {
        // 1. 计算层级和路径
        if (comment.ParentId.HasValue)
        {
            var parentComment = await _commentRepository.QueryByIdAsync(comment.ParentId.Value);
            if (parentComment != null)
            {
                comment.Level = parentComment.Level + 1;
                comment.Path = string.IsNullOrEmpty(parentComment.Path)
                    ? $"{parentComment.Id}"
                    : $"{parentComment.Path}-{parentComment.Id}";
                comment.RootId = parentComment.RootId ?? parentComment.Id;

                // 更新父评论的回复数
                await UpdateReplyCountAsync(parentComment.Id, 1);
            }
        }

        // 2. 插入评论
        var commentId = await AddAsync(comment);

        // 3. 更新帖子的评论数
        await _postService.UpdateCommentCountAsync(comment.PostId, 1);

        return commentId;
    }

    /// <summary>
    /// 更新评论点赞次数
    /// </summary>
    public async Task UpdateLikeCountAsync(long commentId, int increment)
    {
        var comment = await _commentRepository.QueryByIdAsync(commentId);
        if (comment != null)
        {
            comment.LikeCount = Math.Max(0, comment.LikeCount + increment);
            await _commentRepository.UpdateAsync(comment);
        }
    }

    /// <summary>
    /// 更新评论回复次数
    /// </summary>
    public async Task UpdateReplyCountAsync(long commentId, int increment)
    {
        var comment = await _commentRepository.QueryByIdAsync(commentId);
        if (comment != null)
        {
            comment.ReplyCount = Math.Max(0, comment.ReplyCount + increment);
            await _commentRepository.UpdateAsync(comment);
        }
    }
}
