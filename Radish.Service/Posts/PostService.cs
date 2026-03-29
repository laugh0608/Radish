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
    private readonly IBaseRepository<UserPostLike> _userPostLikeRepository;
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
    private readonly ICommentRepository? _commentCustomRepository;
    private readonly ITagService _tagService;
    private readonly IAttachmentService? _attachmentService;
    private readonly ICoinRewardService _coinRewardService;
    private readonly INotificationService _notificationService;
    private readonly INotificationDedupService _dedupService;
    private readonly IExperienceService _experienceService;
    private readonly IBaseRepository<PostEditHistory> _postEditHistoryRepository;
    private readonly ForumEditHistoryOptions _editHistoryOptions;
    private readonly IBaseRepository<Attachment>? _attachmentRepository;

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
        IOptions<ForumEditHistoryOptions> editHistoryOptions,
        IAttachmentService? attachmentService = null,
        IPostRepository? postCustomRepository = null,
        ICommentRepository? commentCustomRepository = null,
        IBaseRepository<Attachment>? attachmentRepository = null,
        IBaseRepository<PostLottery>? postLotteryRepository = null,
        IBaseRepository<PostLotteryWinner>? postLotteryWinnerRepository = null,
        IBaseRepository<Comment>? commentRepository = null)
        : base(mapper, baseRepository)
    {
        _postRepository = baseRepository;
        _postCustomRepository = postCustomRepository;
        _userPostLikeRepository = userPostLikeRepository;
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
        _commentCustomRepository = commentCustomRepository;
        _tagService = tagService;
        _attachmentService = attachmentService;
        _coinRewardService = coinRewardService;
        _notificationService = notificationService;
        _dedupService = dedupService;
        _experienceService = experienceService;
        _postEditHistoryRepository = postEditHistoryRepository;
        _editHistoryOptions = editHistoryOptions.Value;
        _attachmentRepository = attachmentRepository;
    }
}
