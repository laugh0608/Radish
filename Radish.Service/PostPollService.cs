using Radish.Common.AttributeTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Service;

/// <summary>帖子投票服务</summary>
public class PostPollService : IPostPollService
{
    private readonly IPostService _postService;
    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<PostPoll> _postPollRepository;
    private readonly IBaseRepository<PostPollOption> _postPollOptionRepository;
    private readonly IBaseRepository<PostPollVote> _postPollVoteRepository;

    public PostPollService(
        IPostService postService,
        IBaseRepository<Post> postRepository,
        IBaseRepository<PostPoll> postPollRepository,
        IBaseRepository<PostPollOption> postPollOptionRepository,
        IBaseRepository<PostPollVote> postPollVoteRepository)
    {
        _postService = postService;
        _postRepository = postRepository;
        _postPollRepository = postPollRepository;
        _postPollOptionRepository = postPollOptionRepository;
        _postPollVoteRepository = postPollVoteRepository;
    }

    /// <summary>
    /// 按帖子获取投票详情
    /// </summary>
    public async Task<PollVoteResultVo> GetByPostIdAsync(long postId, long? viewerUserId = null)
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

        if (!post.VoHasPoll || post.VoPoll == null)
        {
            throw new InvalidOperationException("当前帖子未配置投票");
        }

        return new PollVoteResultVo
        {
            VoPostId = postId,
            VoPoll = post.VoPoll
        };
    }

    /// <summary>
    /// 提交投票
    /// </summary>
    [UseTran]
    public async Task<PostPollVo> VoteAsync(long userId, string userName, VotePollDto request)
    {
        if (userId <= 0)
        {
            throw new InvalidOperationException("请先登录后再投票");
        }

        if (request.PostId <= 0)
        {
            throw new ArgumentException("帖子ID必须大于0", nameof(request));
        }

        if (request.OptionId <= 0)
        {
            throw new ArgumentException("投票选项ID必须大于0", nameof(request));
        }

        var post = await _postRepository.QueryFirstAsync(p => p.Id == request.PostId && !p.IsDeleted);
        if (post == null)
        {
            throw new InvalidOperationException("帖子不存在");
        }

        var poll = await _postPollRepository.QueryFirstAsync(p => p.PostId == request.PostId && !p.IsDeleted);
        if (poll == null)
        {
            throw new InvalidOperationException("当前帖子未配置投票");
        }

        if (IsPollClosed(poll))
        {
            throw new InvalidOperationException("投票已截止");
        }

        var option = await _postPollOptionRepository.QueryFirstAsync(o =>
            o.Id == request.OptionId &&
            o.PollId == poll.Id &&
            !o.IsDeleted);
        if (option == null)
        {
            throw new InvalidOperationException("投票选项不存在");
        }

        var hasVoted = await _postPollVoteRepository.QueryExistsAsync(v => v.PollId == poll.Id && v.UserId == userId);
        if (hasVoted)
        {
            throw new InvalidOperationException("你已经投过票");
        }

        var operatorName = string.IsNullOrWhiteSpace(userName) ? $"User-{userId}" : userName.Trim();
        var now = DateTime.Now;

        await _postPollVoteRepository.AddAsync(new PostPollVote
        {
            PollId = poll.Id,
            PostId = request.PostId,
            OptionId = option.Id,
            UserId = userId,
            UserName = operatorName,
            TenantId = poll.TenantId,
            CreateBy = operatorName,
            CreateId = userId,
            CreateTime = now
        });

        option.VoteCount += 1;
        option.ModifyTime = now;
        option.ModifyBy = operatorName;
        option.ModifyId = userId;
        await _postPollOptionRepository.UpdateAsync(option);

        poll.TotalVoteCount += 1;
        poll.ModifyTime = now;
        poll.ModifyBy = operatorName;
        poll.ModifyId = userId;
        await _postPollRepository.UpdateAsync(poll);

        var result = await GetByPostIdAsync(request.PostId, userId);
        if (result.VoPoll == null)
        {
            throw new InvalidOperationException("投票结果刷新失败");
        }

        return result.VoPoll;
    }

    private static bool IsPollClosed(PostPoll poll)
    {
        return poll.IsClosed || (poll.EndTime.HasValue && poll.EndTime.Value <= DateTime.Now);
    }
}
