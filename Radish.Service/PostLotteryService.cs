using Radish.Common.AttributeTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Service;

/// <summary>帖子抽奖服务</summary>
public class PostLotteryService : IPostLotteryService
{
    private readonly IPostService _postService;
    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<PostLottery> _postLotteryRepository;
    private readonly IBaseRepository<PostLotteryWinner> _postLotteryWinnerRepository;
    private readonly IBaseRepository<Comment> _commentRepository;

    public PostLotteryService(
        IPostService postService,
        IBaseRepository<Post> postRepository,
        IBaseRepository<PostLottery> postLotteryRepository,
        IBaseRepository<PostLotteryWinner> postLotteryWinnerRepository,
        IBaseRepository<Comment> commentRepository)
    {
        _postService = postService;
        _postRepository = postRepository;
        _postLotteryRepository = postLotteryRepository;
        _postLotteryWinnerRepository = postLotteryWinnerRepository;
        _commentRepository = commentRepository;
    }

    /// <summary>按帖子获取抽奖详情</summary>
    public async Task<LotteryResultVo> GetByPostIdAsync(long postId, long? viewerUserId = null)
    {
        if (postId <= 0)
        {
            throw new ArgumentException("帖子ID必须大于0", nameof(postId));
        }

        var post = await _postService.GetPostDetailAsync(postId, viewerUserId);
        if (post == null)
        {
            throw new InvalidOperationException("帖子不存在");
        }

        if (!post.VoHasLottery || post.VoLottery == null)
        {
            throw new InvalidOperationException("当前帖子未配置抽奖");
        }

        return new LotteryResultVo
        {
            VoPostId = postId,
            VoLottery = post.VoLottery
        };
    }

    /// <summary>手动开奖</summary>
    [UseTran]
    public async Task<PostLotteryVo> DrawAsync(long postId, long userId, string userName)
    {
        if (userId <= 0)
        {
            throw new InvalidOperationException("请先登录后再开奖");
        }

        if (postId <= 0)
        {
            throw new ArgumentException("帖子ID必须大于0", nameof(postId));
        }

        var post = await _postRepository.QueryFirstAsync(p => p.Id == postId && !p.IsDeleted && p.IsPublished);
        if (post == null)
        {
            throw new InvalidOperationException("帖子不存在");
        }

        if (post.AuthorId != userId)
        {
            throw new InvalidOperationException("只有发帖者可以开奖");
        }

        var lottery = await _postLotteryRepository.QueryFirstAsync(l => l.PostId == postId && !l.IsDeleted);
        if (lottery == null)
        {
            throw new InvalidOperationException("当前帖子未配置抽奖");
        }

        if (lottery.IsDrawn)
        {
            throw new InvalidOperationException("该抽奖已经开过奖");
        }

        var now = DateTime.UtcNow;
        if (lottery.DrawTime.HasValue && lottery.DrawTime.Value > now)
        {
            throw new InvalidOperationException("未到可开奖时间");
        }

        var candidateComments = await LoadCandidateCommentsAsync(postId, post.AuthorId, now);
        if (candidateComments.Count == 0)
        {
            throw new InvalidOperationException("当前还没有符合条件的参与者");
        }

        var candidates = candidateComments
            .GroupBy(comment => comment.AuthorId)
            .Select(group => group
                .OrderBy(comment => comment.CreateTime)
                .ThenBy(comment => comment.Id)
                .First())
            .ToList();

        var safeOperatorName = string.IsNullOrWhiteSpace(userName) ? $"User-{userId}" : userName.Trim();
        var actualWinnerCount = Math.Min(Math.Max(1, lottery.WinnerCount), candidates.Count);
        var winnerCandidates = candidates
            .OrderBy(_ => Guid.NewGuid())
            .Take(actualWinnerCount)
            .ToList();

        var winners = winnerCandidates
            .Select(candidate => new PostLotteryWinner
            {
                LotteryId = lottery.Id,
                PostId = postId,
                UserId = candidate.AuthorId,
                UserName = string.IsNullOrWhiteSpace(candidate.AuthorName) ? $"User-{candidate.AuthorId}" : candidate.AuthorName.Trim(),
                CommentId = candidate.Id,
                CommentContentSnapshot = string.IsNullOrWhiteSpace(candidate.Content)
                    ? null
                    : candidate.Content.Trim()[..Math.Min(candidate.Content.Trim().Length, 500)],
                DrawnAt = now,
                TenantId = lottery.TenantId,
                CreateTime = DateTime.Now,
                CreateBy = safeOperatorName,
                CreateId = userId
            })
            .ToList();

        await _postLotteryWinnerRepository.AddRangeAsync(winners);

        lottery.IsDrawn = true;
        lottery.DrawnAt = now;
        lottery.ParticipantCount = candidates.Count;
        lottery.ModifyTime = DateTime.Now;
        lottery.ModifyBy = safeOperatorName;
        lottery.ModifyId = userId;
        await _postLotteryRepository.UpdateAsync(lottery);

        var result = await GetByPostIdAsync(postId, userId);
        if (result.VoLottery == null)
        {
            throw new InvalidOperationException("抽奖结果刷新失败");
        }

        return result.VoLottery;
    }

    private async Task<List<Comment>> LoadCandidateCommentsAsync(long postId, long postAuthorId, DateTime cutoffTimeUtc)
    {
        return await _commentRepository.QueryAsync(comment =>
            comment.PostId == postId &&
            comment.ParentId == null &&
            comment.AuthorId > 0 &&
            comment.AuthorId != postAuthorId &&
            comment.CreateTime <= cutoffTimeUtc &&
            comment.IsEnabled &&
            !comment.IsDeleted);
    }
}
