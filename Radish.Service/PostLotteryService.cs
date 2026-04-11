using Radish.Common.AttributeTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Radish.Service;

/// <summary>帖子抽奖服务</summary>
public class PostLotteryService : IPostLotteryService
{
    private static readonly TimeSpan MinManualDrawLeadTime = TimeSpan.FromHours(1);

    private readonly IPostService _postService;
    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<PostLottery> _postLotteryRepository;
    private readonly IBaseRepository<PostLotteryWinner> _postLotteryWinnerRepository;
    private readonly IBaseRepository<Comment> _commentRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PostLotteryService> _logger;

    public PostLotteryService(
        IPostService postService,
        IBaseRepository<Post> postRepository,
        IBaseRepository<PostLottery> postLotteryRepository,
        IBaseRepository<PostLotteryWinner> postLotteryWinnerRepository,
        IBaseRepository<Comment> commentRepository,
        INotificationService notificationService,
        ILogger<PostLotteryService> logger)
    {
        _postService = postService;
        _postRepository = postRepository;
        _postLotteryRepository = postLotteryRepository;
        _postLotteryWinnerRepository = postLotteryWinnerRepository;
        _commentRepository = commentRepository;
        _notificationService = notificationService;
        _logger = logger;
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

        var post = await GetPostOrThrowAsync(postId);
        if (post.AuthorId != userId)
        {
            throw new InvalidOperationException("只有发帖者可以开奖");
        }

        var lottery = await GetLotteryOrThrowAsync(postId);
        EnsureManualDrawAllowed(post, lottery);

        return await ExecuteDrawAsync(
            post,
            lottery,
            DateTime.UtcNow,
            NormalizeOperatorName(userId, userName),
            userId,
            allowEmptyParticipants: false);
    }

    /// <summary>按帖子自动开奖</summary>
    [UseTran]
    public async Task<PostLotteryVo> AutoDrawByPostIdAsync(long postId)
    {
        if (postId <= 0)
        {
            throw new ArgumentException("帖子ID必须大于0", nameof(postId));
        }

        var post = await GetPostOrThrowAsync(postId);
        var lottery = await GetLotteryOrThrowAsync(postId);
        EnsureAutoDrawAllowed(lottery);

        return await ExecuteDrawAsync(
            post,
            lottery,
            lottery.DrawTime!.Value,
            "LotteryAutoDrawJob",
            0,
            allowEmptyParticipants: true);
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

    private async Task NotifyWinnersAsync(
        Post post,
        PostLottery lottery,
        IReadOnlyCollection<PostLotteryWinner> winners,
        string operatorName,
        long operatorUserId,
        int actualWinnerCount)
    {
        var receiverUserIds = winners
            .Select(winner => winner.UserId)
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();

        if (receiverUserIds.Count == 0)
        {
            return;
        }

        var normalizedPostTitle = string.IsNullOrWhiteSpace(post.Title)
            ? $"帖子 {post.Id}"
            : post.Title.Trim();
        var normalizedPrizeName = string.IsNullOrWhiteSpace(lottery.PrizeName)
            ? "抽奖奖品"
            : lottery.PrizeName.Trim();

        await _notificationService.CreateNotificationAsync(new Model.DtoModels.CreateNotificationDto
        {
            Type = NotificationType.LotteryWon,
            Title = "抽奖开奖结果",
            Content = $"你在帖子《{normalizedPostTitle}》的抽奖“{normalizedPrizeName}”中中奖啦。",
            Priority = (int)NotificationPriority.High,
            BusinessType = BusinessType.Post,
            BusinessId = post.Id,
            TriggerId = operatorUserId,
            TriggerName = operatorName,
            TriggerAvatar = null,
            ReceiverUserIds = receiverUserIds,
            TenantId = lottery.TenantId,
            ExtData = JsonSerializer.Serialize(new
            {
                postId = post.Id.ToString(),
                lotteryId = lottery.Id.ToString(),
                prizeName = normalizedPrizeName,
                winnerCount = actualWinnerCount
            })
        });
    }

    private async Task<Post> GetPostOrThrowAsync(long postId)
    {
        var post = await _postRepository.QueryFirstAsync(p => p.Id == postId && !p.IsDeleted && p.IsPublished);
        if (post == null)
        {
            throw new InvalidOperationException("帖子不存在");
        }

        return post;
    }

    private async Task<PostLottery> GetLotteryOrThrowAsync(long postId)
    {
        var lottery = await _postLotteryRepository.QueryFirstAsync(l => l.PostId == postId && !l.IsDeleted);
        if (lottery == null)
        {
            throw new InvalidOperationException("当前帖子未配置抽奖");
        }

        if (lottery.IsDrawn)
        {
            throw new InvalidOperationException("该抽奖已经开过奖");
        }

        if (!lottery.DrawTime.HasValue)
        {
            throw new InvalidOperationException("当前抽奖未配置截止时间");
        }

        return lottery;
    }

    private void EnsureManualDrawAllowed(Post post, PostLottery lottery)
    {
        var publishTimeUtc = NormalizeToUtc(post.PublishTime ?? post.CreateTime);
        var manualDrawAvailableAt = publishTimeUtc.Add(MinManualDrawLeadTime);
        if (DateTime.UtcNow < manualDrawAvailableAt)
        {
            throw new InvalidOperationException("发帖满 1 小时后才可提前开奖");
        }

        if (DateTime.UtcNow >= lottery.DrawTime!.Value)
        {
            throw new InvalidOperationException("已到自动开奖时间，请等待系统开奖");
        }
    }

    private void EnsureAutoDrawAllowed(PostLottery lottery)
    {
        if (DateTime.UtcNow < lottery.DrawTime!.Value)
        {
            throw new InvalidOperationException("未到自动开奖时间");
        }
    }

    private async Task<PostLotteryVo> ExecuteDrawAsync(
        Post post,
        PostLottery lottery,
        DateTime drawAtUtc,
        string operatorName,
        long operatorUserId,
        bool allowEmptyParticipants)
    {
        var candidateComments = await LoadCandidateCommentsAsync(post.Id, post.AuthorId, drawAtUtc);
        var candidates = candidateComments
            .GroupBy(comment => comment.AuthorId)
            .Select(group => group
                .OrderBy(comment => comment.CreateTime)
                .ThenBy(comment => comment.Id)
                .First())
            .ToList();

        if (candidates.Count == 0 && !allowEmptyParticipants)
        {
            throw new InvalidOperationException("当前还没有符合条件的参与者");
        }

        var actualWinnerCount = candidates.Count == 0
            ? 0
            : Math.Min(Math.Max(1, lottery.WinnerCount), candidates.Count);
        var winnerCandidates = actualWinnerCount == 0
            ? new List<Comment>()
            : candidates
                .OrderBy(_ => Guid.NewGuid())
                .Take(actualWinnerCount)
                .ToList();

        var winners = winnerCandidates
            .Select(candidate => new PostLotteryWinner
            {
                LotteryId = lottery.Id,
                PostId = post.Id,
                UserId = candidate.AuthorId,
                UserName = string.IsNullOrWhiteSpace(candidate.AuthorName) ? $"User-{candidate.AuthorId}" : candidate.AuthorName.Trim(),
                CommentId = candidate.Id,
                CommentContentSnapshot = string.IsNullOrWhiteSpace(candidate.Content)
                    ? null
                    : candidate.Content.Trim()[..Math.Min(candidate.Content.Trim().Length, 500)],
                DrawnAt = drawAtUtc,
                TenantId = lottery.TenantId,
                CreateTime = DateTime.Now,
                CreateBy = operatorName,
                CreateId = operatorUserId > 0 ? operatorUserId : 0
            })
            .ToList();

        if (winners.Count > 0)
        {
            await _postLotteryWinnerRepository.AddRangeAsync(winners);
        }

        lottery.IsDrawn = true;
        lottery.DrawnAt = drawAtUtc;
        lottery.ParticipantCount = candidates.Count;
        lottery.ModifyTime = DateTime.Now;
        lottery.ModifyBy = operatorName;
        lottery.ModifyId = operatorUserId > 0 ? operatorUserId : null;
        await _postLotteryRepository.UpdateAsync(lottery);

        try
        {
            await NotifyWinnersAsync(post, lottery, winners, operatorName, operatorUserId, actualWinnerCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[PostLotteryService] 开奖成功，但发送中奖通知失败：PostId={PostId}, LotteryId={LotteryId}",
                post.Id, lottery.Id);
        }

        var result = await GetByPostIdAsync(post.Id, operatorUserId > 0 ? operatorUserId : null);
        if (result.VoLottery == null)
        {
            throw new InvalidOperationException("抽奖结果刷新失败");
        }

        return result.VoLottery;
    }

    private static string NormalizeOperatorName(long operatorUserId, string? operatorName)
    {
        if (!string.IsNullOrWhiteSpace(operatorName))
        {
            return operatorName.Trim();
        }

        return operatorUserId > 0 ? $"User-{operatorUserId}" : "System";
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
