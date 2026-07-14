using AutoMapper;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service.Base;

namespace Radish.Service;

/// <summary>帖子服务实现</summary>
public partial class PostService : BaseService<Post, PostVo>, IPostService
{
    private const int MinPollOptionCount = 2;
    private const int MaxPollOptionCount = 6;

    private readonly IBaseRepository<Post> _postRepository;
    private readonly IPostRepository? _postCustomRepository;
    private readonly IBaseRepository<PostTag> _postTagRepository;
    private readonly IBaseRepository<Category> _categoryRepository;
    private readonly IBaseRepository<Tag> _tagRepository;
    private readonly IBaseRepository<PostPoll> _postPollRepository;
    private readonly IBaseRepository<PostPollOption> _postPollOptionRepository;
    private readonly IBaseRepository<PostPollVote> _postPollVoteRepository;
    private readonly IBaseRepository<PostQuestion> _postQuestionRepository;
    private readonly IBaseRepository<PostAnswer> _postAnswerRepository;
    private readonly IBaseRepository<PostLottery>? _postLotteryRepository;
    private readonly IBaseRepository<PostLotteryWinner>? _postLotteryWinnerRepository;
    private readonly IBaseRepository<Comment>? _commentRepository;
    private readonly IBaseRepository<CommentHighlight>? _commentHighlightRepository;
    private readonly ICommentRepository? _commentCustomRepository;
    private readonly ITagService _tagService;
    private readonly IAttachmentService _attachmentService;
    private readonly IBaseRepository<User>? _userRepository;
    private readonly IBaseRepository<PostEditHistory> _postEditHistoryRepository;
    private readonly ForumEditHistoryOptions _editHistoryOptions;
    private readonly IBaseRepository<Attachment>? _attachmentRepository;
    private readonly ISystemSettingProvider _systemSettingProvider;
    private readonly IReliableOutboxService? _reliableOutboxService;
    private readonly IUserAdornmentService? _userAdornmentService;

    private sealed record PostContentSettings(
        int MinTitleLength,
        int MaxTitleLength,
        int MinBodyLength,
        int MaxBodyLength,
        int MaxSummaryLength);

    public PostService(
        IMapper mapper,
        IBaseRepository<Post> baseRepository,
        IBaseRepository<UserPostLike> userPostLikeRepository,
        IBaseRepository<PostTag> postTagRepository,
        IBaseRepository<Category> categoryRepository,
        IBaseRepository<Tag> tagRepository,
        IBaseRepository<PostPoll> postPollRepository,
        IBaseRepository<PostPollOption> postPollOptionRepository,
        IBaseRepository<PostPollVote> postPollVoteRepository,
        IBaseRepository<PostQuestion> postQuestionRepository,
        IBaseRepository<PostAnswer> postAnswerRepository,
        ITagService tagService,
        ICoinRewardService coinRewardService,
        INotificationService notificationService,
        INotificationDedupService dedupService,
        IExperienceService experienceService,
        IBaseRepository<PostEditHistory> postEditHistoryRepository,
        IAttachmentService attachmentService,
        IOptions<ForumEditHistoryOptions> editHistoryOptions,
        ISystemSettingProvider systemSettingProvider,
        IPostRepository? postCustomRepository = null,
        ICommentRepository? commentCustomRepository = null,
        IBaseRepository<Attachment>? attachmentRepository = null,
        IBaseRepository<PostLottery>? postLotteryRepository = null,
        IBaseRepository<PostLotteryWinner>? postLotteryWinnerRepository = null,
        IBaseRepository<Comment>? commentRepository = null,
        IBaseRepository<CommentHighlight>? commentHighlightRepository = null,
        IBaseRepository<User>? userRepository = null,
        IReliableOutboxService? reliableOutboxService = null,
        IUserAdornmentService? userAdornmentService = null)
        : base(mapper, baseRepository)
    {
        _postRepository = baseRepository;
        _postCustomRepository = postCustomRepository;
        ArgumentNullException.ThrowIfNull(userPostLikeRepository);
        _postTagRepository = postTagRepository;
        _categoryRepository = categoryRepository;
        _tagRepository = tagRepository;
        _postPollRepository = postPollRepository;
        _postPollOptionRepository = postPollOptionRepository;
        _postPollVoteRepository = postPollVoteRepository;
        _postQuestionRepository = postQuestionRepository;
        _postAnswerRepository = postAnswerRepository;
        _postLotteryRepository = postLotteryRepository;
        _postLotteryWinnerRepository = postLotteryWinnerRepository;
        _commentRepository = commentRepository;
        _commentHighlightRepository = commentHighlightRepository;
        _commentCustomRepository = commentCustomRepository;
        _tagService = tagService;
        _attachmentService = attachmentService;
        _userRepository = userRepository;
        _postEditHistoryRepository = postEditHistoryRepository;
        _editHistoryOptions = editHistoryOptions.Value;
        _attachmentRepository = attachmentRepository;
        _systemSettingProvider = systemSettingProvider;
        _reliableOutboxService = reliableOutboxService;
        _userAdornmentService = userAdornmentService;
    }

    private async Task<PostContentSettings> ValidatePostContentSettingsAsync(string title, string content)
    {
        var settings = await GetPostContentSettingsAsync();
        var trimmedTitle = title.Trim();
        var trimmedContent = content.Trim();

        if (settings.MinTitleLength > settings.MaxTitleLength)
        {
            throw new InvalidOperationException("系统设置配置错误：帖子标题最小长度不能大于最大长度");
        }

        if (settings.MinBodyLength > settings.MaxBodyLength)
        {
            throw new InvalidOperationException("系统设置配置错误：帖子正文最小长度不能大于最大长度");
        }

        if (trimmedTitle.Length < settings.MinTitleLength)
        {
            throw new ArgumentException($"帖子标题不能少于 {settings.MinTitleLength} 个字符");
        }

        if (trimmedTitle.Length > settings.MaxTitleLength)
        {
            throw new ArgumentException($"帖子标题不能超过 {settings.MaxTitleLength} 个字符");
        }

        if (trimmedContent.Length < settings.MinBodyLength)
        {
            throw new ArgumentException($"帖子内容不能少于 {settings.MinBodyLength} 个字符");
        }

        if (trimmedContent.Length > settings.MaxBodyLength)
        {
            throw new ArgumentException($"帖子内容不能超过 {settings.MaxBodyLength} 个字符");
        }

        return settings;
    }

    private async Task<PostContentSettings> GetPostContentSettingsAsync()
    {
        var minTitleLength = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.PostTitleMinLengthKey);
        var maxTitleLength = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.PostTitleMaxLengthKey);
        var minBodyLength = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.PostBodyMinLengthKey);
        var maxBodyLength = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.PostBodyMaxLengthKey);
        var maxSummaryLength = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.PostSummaryMaxLengthKey);

        return new PostContentSettings(
            minTitleLength,
            maxTitleLength,
            minBodyLength,
            maxBodyLength,
            maxSummaryLength);
    }

    private static void ApplyPostSummarySettings(Post post, PostContentSettings settings)
    {
        post.Summary = BuildPostSummary(post.Content, settings.MaxSummaryLength);
    }

    private static string BuildPostSummary(string content, int maxSummaryLength)
    {
        var trimmedContent = content.Trim();
        if (trimmedContent.Length <= maxSummaryLength)
        {
            return trimmedContent;
        }

        const string suffix = "...";
        var contentLength = Math.Max(0, maxSummaryLength - suffix.Length);
        return string.Concat(trimmedContent.AsSpan(0, contentLength), suffix);
    }
}
