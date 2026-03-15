using AutoMapper;
using Radish.Common.AttributeTool;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;

namespace Radish.Service;

/// <summary>帖子服务实现</summary>
public class PostService : BaseService<Post, PostVo>, IPostService
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
    private readonly ITagService _tagService;
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
        IPostRepository? postCustomRepository = null,
        IBaseRepository<Attachment>? attachmentRepository = null)
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
        _tagService = tagService;
        _coinRewardService = coinRewardService;
        _notificationService = notificationService;
        _dedupService = dedupService;
        _experienceService = experienceService;
        _postEditHistoryRepository = postEditHistoryRepository;
        _editHistoryOptions = editHistoryOptions.Value;
        _attachmentRepository = attachmentRepository;
    }

    /// <summary>
    /// 获取帖子详情（包含分类名称和标签）
    /// </summary>
    public async Task<PostVo?> GetPostDetailAsync(long postId, long? viewerUserId = null, string answerSort = "default")
    {
        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted)
        {
            return null;
        }

        var postVo = Mapper.Map<PostVo>(post);

        // 获取分类名称
        if (post.CategoryId > 0)
        {
            var category = await _categoryRepository.QueryByIdAsync(post.CategoryId);
            if (category != null)
            {
                postVo.VoCategoryName = category.Name;
            }
        }

        // 获取标签
        var postTags = await _postTagRepository.QueryAsync(pt => pt.PostId == postId);
        if (postTags.Any())
        {
            var tagIds = postTags.Select(pt => pt.TagId).ToList();
            var tags = await _tagService.QueryAsync(t => tagIds.Contains(t.Id) && t.IsEnabled && !t.IsDeleted);
            postVo.VoTags = string.Join(", ", tags.Select(t => t.VoName));
        }

        var pollVo = await BuildPostPollVoAsync(postId, viewerUserId);
        if (pollVo != null)
        {
            postVo.VoHasPoll = true;
            postVo.VoPollTotalVoteCount = pollVo.VoTotalVoteCount;
            postVo.VoPollIsClosed = pollVo.VoIsClosed;
            postVo.VoPoll = pollVo;
        }

        postVo.VoQuestion = await BuildPostQuestionVoAsync(postId, answerSort);
        FillPostQuestionSummary(postVo, postVo.VoQuestion);

        return postVo;
    }

    /// <summary>
    /// 分页获取问答帖子列表
    /// </summary>
    public async Task<(List<PostVo> data, int totalCount)> GetQuestionPostPageAsync(
        long? categoryId = null,
        int pageIndex = 1,
        int pageSize = 20,
        string sortBy = "newest",
        string? keyword = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        bool? isSolved = null)
    {
        if (_postCustomRepository == null)
        {
            throw new InvalidOperationException("帖子仓储未配置，无法查询问答帖子列表");
        }

        var (posts, totalCount) = await _postCustomRepository.QueryQuestionPostPageAsync(
            categoryId,
            keyword,
            startTime,
            endTime,
            isSolved,
            pageIndex,
            pageSize,
            sortBy);

        return (Mapper.Map<List<PostVo>>(posts), totalCount);
    }

    /// <summary>
    /// 批量回填帖子列表所需的轻量元数据
    /// </summary>
    public async Task FillPostListMetadataAsync(List<PostVo> posts)
    {
        if (posts.Count == 0)
        {
            return;
        }

        var postIds = posts
            .Select(post => post.VoId)
            .Where(postId => postId > 0)
            .Distinct()
            .ToList();

        if (postIds.Count == 0)
        {
            return;
        }

        var categoryIds = posts
            .Where(post => post.VoCategoryId > 0)
            .Select(post => post.VoCategoryId)
            .Distinct()
            .ToList();

        Dictionary<long, string> categoryNameMap = new();
        if (categoryIds.Count > 0)
        {
            categoryNameMap = (await _categoryRepository.QueryAsync(category =>
                    categoryIds.Contains(category.Id) &&
                    category.IsEnabled &&
                    !category.IsDeleted))
                .GroupBy(category => category.Id)
                .ToDictionary(group => group.Key, group => group.First().Name);
        }

        var postTags = await _postTagRepository.QueryAsync(postTag => postIds.Contains(postTag.PostId));
        var tagIds = postTags
            .Select(postTag => postTag.TagId)
            .Distinct()
            .ToList();

        Dictionary<long, string> tagNameMap = new();
        if (tagIds.Count > 0)
        {
            tagNameMap = (await _tagRepository.QueryAsync(tag =>
                    tagIds.Contains(tag.Id) &&
                    tag.IsEnabled &&
                    !tag.IsDeleted))
                .GroupBy(tag => tag.Id)
                .ToDictionary(group => group.Key, group => group.First().Name);
        }

        var tagTextMap = postTags
            .GroupBy(postTag => postTag.PostId)
            .ToDictionary(
                group => group.Key,
                group => string.Join(", ",
                    group.Select(postTag => tagNameMap.GetValueOrDefault(postTag.TagId))
                        .Where(tagName => !string.IsNullOrWhiteSpace(tagName))));

        var pollMap = (await _postPollRepository.QueryAsync(poll =>
                postIds.Contains(poll.PostId) &&
                !poll.IsDeleted))
            .GroupBy(poll => poll.PostId)
            .ToDictionary(group => group.Key, group => group.First());

        var questionMap = (await _postQuestionRepository.QueryAsync(question =>
                postIds.Contains(question.PostId) &&
                !question.IsDeleted))
            .GroupBy(question => question.PostId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var post in posts)
        {
            if (string.IsNullOrWhiteSpace(post.VoCategoryName) &&
                categoryNameMap.TryGetValue(post.VoCategoryId, out var categoryName))
            {
                post.VoCategoryName = categoryName;
            }

            if (tagTextMap.TryGetValue(post.VoId, out var tags))
            {
                post.VoTags = tags;
            }

            if (!pollMap.TryGetValue(post.VoId, out var poll))
            {
                post.VoHasPoll = false;
                post.VoPollTotalVoteCount = 0;
                post.VoPollIsClosed = false;
            }
            else
            {
                post.VoHasPoll = true;
                post.VoPollTotalVoteCount = poll.TotalVoteCount;
                post.VoPollIsClosed = IsPollClosed(poll);
            }

            FillPostQuestionSummary(post, questionMap.GetValueOrDefault(post.VoId));
        }
    }

    /// <summary>
    /// 为问答帖提交回答
    /// </summary>
    [UseTran]
    public async Task<PostQuestionVo> AddAnswerAsync(long postId, string content, long authorId, string authorName, long tenantId)
    {
        var trimmedContent = content?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedContent))
        {
            throw new ArgumentException("回答内容不能为空", nameof(content));
        }

        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted || !post.IsPublished)
        {
            throw new InvalidOperationException("帖子不存在");
        }

        var question = await _postQuestionRepository.QueryFirstAsync(q => q.PostId == postId && !q.IsDeleted);
        if (question == null)
        {
            throw new InvalidOperationException("当前帖子不是问答帖");
        }

        var safeAuthorName = string.IsNullOrWhiteSpace(authorName) ? "System" : authorName;
        var answerId = await _postAnswerRepository.AddAsync(new PostAnswer
        {
            PostId = postId,
            AuthorId = authorId,
            AuthorName = safeAuthorName,
            Content = trimmedContent,
            IsAccepted = false,
            TenantId = tenantId,
            CreateBy = safeAuthorName,
            CreateId = authorId
        });

        question.AnswerCount++;
        question.ModifyTime = DateTime.Now;
        question.ModifyBy = safeAuthorName;
        question.ModifyId = authorId;
        await _postQuestionRepository.UpdateAsync(question);

        await BindReferencedAttachmentsAsync(trimmedContent, BusinessType.Comment, answerId, authorId, safeAuthorName, tenantId);

        return await BuildPostQuestionVoAsync(postId)
            ?? throw new InvalidOperationException("问答详情不存在");
    }

    /// <summary>
    /// 采纳问答帖中的回答
    /// </summary>
    [UseTran]
    public async Task<PostQuestionVo> AcceptAnswerAsync(long postId, long answerId, long operatorId, string operatorName)
    {
        if (postId <= 0)
        {
            throw new ArgumentException("帖子ID必须大于0", nameof(postId));
        }

        if (answerId <= 0)
        {
            throw new ArgumentException("回答ID必须大于0", nameof(answerId));
        }

        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted || !post.IsPublished)
        {
            throw new InvalidOperationException("帖子不存在");
        }

        if (post.AuthorId != operatorId)
        {
            throw new InvalidOperationException("只有提问者可以采纳答案");
        }

        var question = await _postQuestionRepository.QueryFirstAsync(q => q.PostId == postId && !q.IsDeleted);
        if (question == null)
        {
            throw new InvalidOperationException("当前帖子不是问答帖");
        }

        if (question.IsSolved || question.AcceptedAnswerId.HasValue)
        {
            throw new InvalidOperationException("当前问题已采纳答案");
        }

        var answer = await _postAnswerRepository.QueryFirstAsync(a => a.Id == answerId && a.PostId == postId && !a.IsDeleted);
        if (answer == null)
        {
            throw new InvalidOperationException("回答不存在");
        }

        if (answer.AuthorId == operatorId)
        {
            throw new InvalidOperationException("不能采纳自己的回答");
        }

        var safeOperatorName = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName;

        answer.IsAccepted = true;
        answer.ModifyTime = DateTime.Now;
        answer.ModifyBy = safeOperatorName;
        answer.ModifyId = operatorId;
        await _postAnswerRepository.UpdateAsync(answer);

        question.IsSolved = true;
        question.AcceptedAnswerId = answerId;
        question.ModifyTime = DateTime.Now;
        question.ModifyBy = safeOperatorName;
        question.ModifyId = operatorId;
        await _postQuestionRepository.UpdateAsync(question);

        return await BuildPostQuestionVoAsync(postId)
            ?? throw new InvalidOperationException("问答详情不存在");
    }

    /// <summary>
    /// 发布帖子
    /// </summary>
    [UseTran]
    public async Task<long> PublishPostAsync(
        Post post,
        CreatePollDto? poll = null,
        bool isQuestion = false,
        List<string>? tagNames = null,
        bool allowCreateTag = true)
    {
        var normalizedTagNames = NormalizeTagNamesOrThrow(tagNames, nameof(tagNames), "发布帖子时至少需要一个标签");
        var operatorName = string.IsNullOrWhiteSpace(post.AuthorName) ? "System" : post.AuthorName;

        // 1. 插入帖子
        var postId = await AddAsync(post);

        await BindReferencedAttachmentsAsync(post.Content, BusinessType.Post, postId, post.AuthorId, operatorName, post.TenantId);

        // 2. 更新分类的帖子数量
        if (post.CategoryId > 0)
        {
            var category = await _categoryRepository.QueryByIdAsync(post.CategoryId);
            if (category != null)
            {
                category.PostCount++;
                await _categoryRepository.UpdateAsync(category);
            }
        }

        // 3. 处理标签
        await SyncPostTagsAsync(postId, post.AuthorId, operatorName, normalizedTagNames, allowCreateTag);

        // 4. 处理附带投票
        if (poll != null)
        {
            await CreatePostPollAsync(post, postId, poll, operatorName);
        }

        // 5. 处理问答帖标记
        if (isQuestion)
        {
            await CreatePostQuestionAsync(post, postId, operatorName);
        }

        // 6. 🎁 发放经验值奖励（异步处理）
        _ = Task.Run(async () =>
        {
            try
            {
                Serilog.Log.Information("准备发放发帖经验值：PostId={PostId}, UserId={UserId}", postId, post.AuthorId);

                // 4.1 发放发帖经验值（POST_CREATE: +20 经验）
                var grantResult = await _experienceService.GrantExperienceAsync(
                    userId: post.AuthorId,
                    amount: 20,
                    expType: "POST_CREATE",
                    businessType: "Post",
                    businessId: postId,
                    remark: "发布帖子");

                if (grantResult)
                {
                    Serilog.Log.Information("发帖经验值发放成功：PostId={PostId}, UserId={UserId}, Amount=20",
                        postId, post.AuthorId);
                }
                else
                {
                    Serilog.Log.Warning("发帖经验值发放失败：PostId={PostId}, UserId={UserId}",
                        postId, post.AuthorId);
                }

                // 4.2 检查是否首次发帖，发放额外奖励
                var userPostCount = await _postRepository.QueryCountAsync(p =>
                    p.AuthorId == post.AuthorId && !p.IsDeleted);

                Serilog.Log.Information("用户帖子数量统计：UserId={UserId}, PostCount={PostCount}",
                    post.AuthorId, userPostCount);

                if (userPostCount == 1) // 首次发帖
                {
                    Serilog.Log.Information("检测到首次发帖，准备发放额外奖励：UserId={UserId}", post.AuthorId);

                    var firstPostResult = await _experienceService.GrantExperienceAsync(
                        userId: post.AuthorId,
                        amount: 30,
                        expType: "FIRST_POST",
                        businessType: "Post",
                        businessId: postId,
                        remark: "首次发帖奖励");

                    if (firstPostResult)
                    {
                        Serilog.Log.Information("首次发帖经验值奖励发放成功：PostId={PostId}, UserId={UserId}, Amount=30",
                            postId, post.AuthorId);
                    }
                    else
                    {
                        Serilog.Log.Warning("首次发帖经验值奖励发放失败：PostId={PostId}, UserId={UserId}",
                            postId, post.AuthorId);
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "发放发帖经验值失败：PostId={PostId}, UserId={UserId}, Message={Message}, StackTrace={StackTrace}",
                    postId, post.AuthorId, ex.Message, ex.StackTrace);
            }
        });

        return postId;
    }

    private async Task<PostPollVo?> BuildPostPollVoAsync(long postId, long? viewerUserId)
    {
        var poll = await _postPollRepository.QueryFirstAsync(p => p.PostId == postId && !p.IsDeleted);
        if (poll == null)
        {
            return null;
        }

        var pollVo = Mapper.Map<PostPollVo>(poll);
        pollVo.VoPollId = poll.Id;
        pollVo.VoIsClosed = IsPollClosed(poll);

        var options = await _postPollOptionRepository.QueryAsync(o => o.PollId == poll.Id && !o.IsDeleted);
        var orderedOptions = options
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Id)
            .ToList();

        var totalVoteCount = poll.TotalVoteCount > 0
            ? poll.TotalVoteCount
            : orderedOptions.Sum(option => option.VoteCount);

        pollVo.VoTotalVoteCount = totalVoteCount;
        pollVo.VoOptions = orderedOptions
            .Select(option =>
            {
                var optionVo = Mapper.Map<PostPollOptionVo>(option);
                optionVo.VoOptionId = option.Id;
                optionVo.VoVotePercent = totalVoteCount <= 0
                    ? 0
                    : Math.Round(option.VoteCount * 100m / totalVoteCount, 2);
                return optionVo;
            })
            .ToList();

        if (viewerUserId.HasValue && viewerUserId.Value > 0)
        {
            var vote = await _postPollVoteRepository.QueryFirstAsync(v => v.PollId == poll.Id && v.UserId == viewerUserId.Value);
            if (vote != null)
            {
                pollVo.VoHasVoted = true;
                pollVo.VoSelectedOptionId = vote.OptionId;
            }
        }

        return pollVo;
    }

    private async Task CreatePostPollAsync(Post post, long postId, CreatePollDto poll, string operatorName)
    {
        var question = poll.Question?.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("投票问题不能为空", nameof(poll));
        }

        if (poll.EndTime.HasValue && poll.EndTime.Value <= DateTime.UtcNow)
        {
            throw new ArgumentException("投票截止时间必须晚于当前时间", nameof(poll));
        }

        var normalizedOptions = NormalizePollOptionsOrThrow(poll.Options);

        var postPoll = new PostPoll
        {
            PostId = postId,
            Question = question,
            EndTime = poll.EndTime,
            IsClosed = false,
            TotalVoteCount = 0,
            TenantId = post.TenantId,
            CreateBy = operatorName,
            CreateId = post.AuthorId
        };

        var pollId = await _postPollRepository.AddAsync(postPoll);

        var optionEntities = normalizedOptions
            .Select((option, index) => new PostPollOption
            {
                PollId = pollId,
                OptionText = option.OptionText,
                SortOrder = option.SortOrder ?? index + 1,
                VoteCount = 0,
                TenantId = post.TenantId,
                CreateBy = operatorName,
                CreateId = post.AuthorId
            })
            .ToList();

        await _postPollOptionRepository.AddRangeAsync(optionEntities);
    }

    private async Task CreatePostQuestionAsync(Post post, long postId, string operatorName)
    {
        await _postQuestionRepository.AddAsync(new PostQuestion
        {
            PostId = postId,
            IsSolved = false,
            AcceptedAnswerId = null,
            AnswerCount = 0,
            TenantId = post.TenantId,
            CreateBy = operatorName,
            CreateId = post.AuthorId
        });
    }

    private async Task<PostQuestionVo?> BuildPostQuestionVoAsync(long postId, string answerSort = "default")
    {
        var question = await _postQuestionRepository.QueryFirstAsync(q => q.PostId == postId && !q.IsDeleted);
        if (question == null)
        {
            return null;
        }

        var questionVo = Mapper.Map<PostQuestionVo>(question);
        var answers = await _postAnswerRepository.QueryAsync(answer => answer.PostId == postId && !answer.IsDeleted);
        var normalizedAnswerSort = answerSort?.Trim().ToLowerInvariant() ?? "default";
        var orderedAnswers = normalizedAnswerSort switch
        {
            "latest" => answers
                .OrderByDescending(answer => answer.CreateTime)
                .ThenByDescending(answer => answer.Id),
            _ => answers
                .OrderByDescending(answer => answer.IsAccepted)
                .ThenBy(answer => answer.CreateTime)
                .ThenBy(answer => answer.Id)
        };

        questionVo.VoAnswers = orderedAnswers
            .Select(answer => Mapper.Map<PostAnswerVo>(answer))
            .ToList();

        return questionVo;
    }

    private static void FillPostQuestionSummary(PostVo postVo, PostQuestion? question)
    {
        if (question == null)
        {
            postVo.VoIsQuestion = false;
            postVo.VoIsSolved = false;
            postVo.VoAnswerCount = 0;
            return;
        }

        postVo.VoIsQuestion = true;
        postVo.VoIsSolved = question.IsSolved;
        postVo.VoAnswerCount = question.AnswerCount;
    }

    private static void FillPostQuestionSummary(PostVo postVo, PostQuestionVo? question)
    {
        if (question == null)
        {
            postVo.VoIsQuestion = false;
            postVo.VoIsSolved = false;
            postVo.VoAnswerCount = 0;
            return;
        }

        postVo.VoIsQuestion = true;
        postVo.VoIsSolved = question.VoIsSolved;
        postVo.VoAnswerCount = question.VoAnswerCount;
    }

    private static bool IsPollClosed(PostPoll poll)
    {
        return poll.IsClosed || (poll.EndTime.HasValue && poll.EndTime.Value <= DateTime.UtcNow);
    }

    private static List<PollOptionDto> NormalizePollOptionsOrThrow(List<PollOptionDto>? options)
    {
        if (options == null)
        {
            throw new ArgumentException("投票选项不能为空", nameof(options));
        }

        var normalizedOptions = options
            .Where(option => !string.IsNullOrWhiteSpace(option.OptionText))
            .Select((option, index) => new PollOptionDto
            {
                OptionText = option.OptionText.Trim(),
                SortOrder = option.SortOrder ?? index + 1
            })
            .ToList();

        if (normalizedOptions.Count < MinPollOptionCount || normalizedOptions.Count > MaxPollOptionCount)
        {
            throw new ArgumentException($"投票选项数量必须在 {MinPollOptionCount} 到 {MaxPollOptionCount} 个之间", nameof(options));
        }

        var distinctCount = normalizedOptions
            .Select(option => option.OptionText)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (distinctCount != normalizedOptions.Count)
        {
            throw new ArgumentException("投票选项不能重复", nameof(options));
        }

        return normalizedOptions;
    }

    /// <summary>
    /// 更新帖子及标签
    /// </summary>
    [UseTran]
    public async Task UpdatePostAsync(
        long postId,
        string title,
        string content,
        long? categoryId,
        List<string>? tagNames,
        bool allowCreateTag,
        long operatorId,
        string operatorName,
        bool isAdmin = false)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("帖子标题不能为空", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("帖子内容不能为空", nameof(content));
        }

        var normalizedTagNames = NormalizeTagNamesOrThrow(tagNames, nameof(tagNames), "编辑帖子时至少需要一个标签");

        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted)
        {
            throw new InvalidOperationException("帖子不存在");
        }

        var postOptions = _editHistoryOptions.Post;
        var historyEnabled = _editHistoryOptions.Enable && postOptions.EnableHistory;
        var historyEditCount = await _postEditHistoryRepository.QueryCountAsync(h => h.PostId == postId);
        var existingEditCount = Math.Max(post.EditCount, historyEditCount);

        if (!isAdmin || !_editHistoryOptions.AdminOverride.BypassEditCountLimit)
        {
            if (existingEditCount >= Math.Max(0, postOptions.MaxEditCount))
            {
                throw new InvalidOperationException("帖子编辑次数已达上限，无法继续编辑");
            }
        }

        var targetCategoryId = categoryId ?? post.CategoryId;
        if (targetCategoryId > 0 && targetCategoryId != post.CategoryId)
        {
            var oldCategory = await _categoryRepository.QueryByIdAsync(post.CategoryId);
            if (oldCategory != null)
            {
                oldCategory.PostCount = Math.Max(0, oldCategory.PostCount - 1);
                await _categoryRepository.UpdateAsync(oldCategory);
            }

            var newCategory = await _categoryRepository.QueryByIdAsync(targetCategoryId);
            if (newCategory != null)
            {
                newCategory.PostCount++;
                await _categoryRepository.UpdateAsync(newCategory);
            }
        }

        var safeOperatorName = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName;
        var trimmedTitle = title.Trim();
        var trimmedContent = content.Trim();
        var nextEditSequence = existingEditCount + 1;

        if (historyEnabled)
        {
            if (nextEditSequence <= Math.Max(0, postOptions.HistorySaveEditCount))
            {
                await _postEditHistoryRepository.AddAsync(new PostEditHistory
                {
                    PostId = postId,
                    EditSequence = nextEditSequence,
                    OldTitle = post.Title,
                    NewTitle = trimmedTitle,
                    OldContent = post.Content,
                    NewContent = trimmedContent,
                    EditorId = operatorId,
                    EditorName = safeOperatorName,
                    EditedAt = DateTime.Now,
                    TenantId = post.TenantId,
                    CreateTime = DateTime.Now,
                    CreateBy = safeOperatorName,
                    CreateId = operatorId
                });
            }
        }

        post.Title = trimmedTitle;
        post.Content = trimmedContent;
        post.CategoryId = targetCategoryId;
        post.EditCount = nextEditSequence;
        post.ModifyTime = DateTime.Now;
        post.ModifyBy = safeOperatorName;
        post.ModifyId = operatorId;

        await _postRepository.UpdateAsync(post);
        await BindReferencedAttachmentsAsync(trimmedContent, BusinessType.Post, postId, operatorId, safeOperatorName, post.TenantId);
        await SyncPostTagsAsync(postId, operatorId, safeOperatorName, normalizedTagNames, allowCreateTag);

        if (historyEnabled)
        {
            await TrimPostHistoryAsync(postId, Math.Max(1, postOptions.MaxHistoryRecords));
        }
    }

    private async Task BindReferencedAttachmentsAsync(
        string? content,
        string businessType,
        long businessId,
        long operatorId,
        string operatorName,
        long tenantId)
    {
        if (_attachmentRepository == null || businessId <= 0 || operatorId <= 0)
        {
            return;
        }

        var referencedUrls = AttachmentReferenceHelper.ExtractUploadUrls(content);
        if (referencedUrls.Count == 0)
        {
            return;
        }

        var normalizedTenantId = tenantId > 0 ? tenantId : 0;
        var attachments = await _attachmentRepository.QueryAsync(a =>
            !a.IsDeleted &&
            !a.BusinessId.HasValue &&
            a.TenantId == normalizedTenantId &&
            a.UploaderId == operatorId);

        foreach (var attachment in attachments.Where(a => AttachmentReferenceHelper.IsAttachmentReferenced(a, referencedUrls)))
        {
            attachment.BusinessType = businessType;
            attachment.BusinessId = businessId;
            attachment.ModifyTime = DateTime.Now;
            attachment.ModifyBy = operatorName;
            attachment.ModifyId = operatorId;
            await _attachmentRepository.UpdateAsync(attachment);
        }
    }

    public async Task<(List<PostEditHistoryVo> histories, int total)> GetPostEditHistoryPageAsync(long postId, int pageIndex, int pageSize)
    {
        var safePageIndex = pageIndex < 1 ? 1 : pageIndex;
        var safePageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var (histories, total) = await _postEditHistoryRepository.QueryPageAsync(
            h => h.PostId == postId,
            safePageIndex,
            safePageSize,
            h => h.EditSequence,
            SqlSugar.OrderByType.Desc,
            h => h.CreateTime,
            SqlSugar.OrderByType.Desc);

        return (Mapper.Map<List<PostEditHistoryVo>>(histories), total);
    }

    private async Task TrimPostHistoryAsync(long postId, int maxHistoryRecords)
    {
        var histories = await _postEditHistoryRepository.QueryWithOrderAsync(
            h => h.PostId == postId,
            h => h.EditSequence,
            SqlSugar.OrderByType.Desc);

        if (histories.Count <= maxHistoryRecords)
        {
            return;
        }

        var removeIds = histories
            .Skip(maxHistoryRecords)
            .Select(h => h.Id)
            .ToList();

#pragma warning disable CS0618
        await _postEditHistoryRepository.DeleteByIdsAsync(removeIds);
#pragma warning restore CS0618
    }

    private static List<string> NormalizeTagNamesOrThrow(List<string>? tagNames, string parameterName, string emptyMessage)
    {
        if (tagNames == null)
        {
            throw new ArgumentException(emptyMessage, parameterName);
        }

        var normalizedTagNames = tagNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedTagNames.Count is < 1 or > 5)
        {
            throw new ArgumentException("标签数量必须在 1 到 5 个之间", parameterName);
        }

        return normalizedTagNames;
    }

    private async Task<Tag> ResolveTagByNameAsync(string tagName, bool allowCreateTag)
    {
        var existingTags = await _tagRepository.QueryAsync(t => t.Name == tagName && t.IsEnabled && !t.IsDeleted);
        var tag = existingTags.FirstOrDefault();
        if (tag != null)
        {
            return tag;
        }

        if (!allowCreateTag)
        {
            throw new InvalidOperationException($"标签不存在且当前用户无权限创建：{tagName}");
        }

        return await _tagService.GetOrCreateTagAsync(tagName);
    }

    private async Task SyncPostTagsAsync(
        long postId,
        long operatorId,
        string operatorName,
        List<string> normalizedTagNames,
        bool allowCreateTag)
    {
        var existingPostTags = await _postTagRepository.QueryAsync(pt => pt.PostId == postId);
        var existingTagIdSet = existingPostTags.Select(pt => pt.TagId).ToHashSet();

        var desiredTags = new Dictionary<long, Tag>();
        foreach (var tagName in normalizedTagNames)
        {
            var tag = await ResolveTagByNameAsync(tagName, allowCreateTag);
            if (!desiredTags.ContainsKey(tag.Id))
            {
                desiredTags[tag.Id] = tag;
            }
        }

        var desiredTagIdSet = desiredTags.Keys.ToHashSet();

        var relationsToRemove = existingPostTags
            .Where(pt => !desiredTagIdSet.Contains(pt.TagId))
            .ToList();

        if (relationsToRemove.Any())
        {
            var removeCountByTagId = relationsToRemove
                .GroupBy(pt => pt.TagId)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var relation in relationsToRemove)
            {
#pragma warning disable CS0618
                await _postTagRepository.DeleteByIdAsync(relation.Id);
#pragma warning restore CS0618
            }

            var removedTagIds = removeCountByTagId.Keys.ToList();
            var removedTags = await _tagRepository.QueryAsync(t => removedTagIds.Contains(t.Id) && !t.IsDeleted);
            foreach (var removedTag in removedTags)
            {
                if (!removeCountByTagId.TryGetValue(removedTag.Id, out var removeCount))
                {
                    continue;
                }

                removedTag.PostCount = Math.Max(0, removedTag.PostCount - removeCount);
                await _tagRepository.UpdateAsync(removedTag);
            }
        }

        var tagIdsToAdd = desiredTagIdSet
            .Where(tagId => !existingTagIdSet.Contains(tagId))
            .ToList();

        foreach (var tagId in tagIdsToAdd)
        {
            var tag = desiredTags[tagId];
            var postTag = new PostTag(postId, tag.Id)
            {
                CreateId = operatorId,
                CreateBy = operatorName
            };

            await _postTagRepository.AddAsync(postTag);
            tag.PostCount++;
            await _tagRepository.UpdateAsync(tag);
        }
    }

    /// <summary>
    /// 更新帖子浏览次数
    /// </summary>
    public async Task IncrementViewCountAsync(long postId)
    {
        var post = await _postRepository.QueryByIdAsync(postId);
        if (post != null)
        {
            post.ViewCount++;
            await _postRepository.UpdateAsync(post);
        }
    }

    /// <summary>
    /// 更新帖子点赞次数
    /// </summary>
    public async Task UpdateLikeCountAsync(long postId, int increment)
    {
        var post = await _postRepository.QueryByIdAsync(postId);
        if (post != null)
        {
            post.LikeCount = Math.Max(0, post.LikeCount + increment);
            await _postRepository.UpdateAsync(post);
        }
    }

    /// <summary>
    /// 更新帖子评论次数
    /// </summary>
    public async Task UpdateCommentCountAsync(long postId, int increment)
    {
        var post = await _postRepository.QueryByIdAsync(postId);
        if (post != null)
        {
            post.CommentCount = Math.Max(0, post.CommentCount + increment);
            await _postRepository.UpdateAsync(post);
        }
    }

    /// <summary>
    /// 切换帖子点赞状态（点赞/取消点赞）
    /// </summary>
    public async Task<PostLikeResultDto> ToggleLikeAsync(long userId, long postId)
    {
        // 1. 检查帖子是否存在
        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted)
        {
            throw new InvalidOperationException("帖子不存在或已被删除");
        }

        // 2. 检查是否已点赞（排除软删除的记录）
        var existingLikes = await _userPostLikeRepository.QueryAsync(
            x => x.UserId == userId && x.PostId == postId && !x.IsDeleted);

        // 同时检查是否有被软删除的点赞记录
        var deletedLikes = await _userPostLikeRepository.QueryAsync(
            x => x.UserId == userId && x.PostId == postId && x.IsDeleted);

        bool isLiked;
        int likeCountDelta;

        if (existingLikes.Any())
        {
            // 取消点赞（软删除）
            await _userPostLikeRepository.UpdateColumnsAsync(
                l => new UserPostLike { IsDeleted = true },
                l => l.Id == existingLikes.First().Id);
            isLiked = false;
            likeCountDelta = -1;
        }
        else if (deletedLikes.Any())
        {
            // 恢复之前的点赞记录
            await _userPostLikeRepository.UpdateColumnsAsync(
                l => new UserPostLike {
                    IsDeleted = false,
                    LikedAt = DateTime.UtcNow // 更新点赞时间
                },
                l => l.Id == deletedLikes.First().Id);
            isLiked = true;
            likeCountDelta = 1;
        }
        else
        {
            // 添加新的点赞记录
            var newLike = new UserPostLike
            {
                UserId = userId,
                PostId = postId,
                LikedAt = DateTime.UtcNow
            };
            await _userPostLikeRepository.AddAsync(newLike);
            isLiked = true;
            likeCountDelta = 1;
        }

        // 3. 更新帖子的点赞计数
        post.LikeCount = Math.Max(0, post.LikeCount + likeCountDelta);
        await _postRepository.UpdateAsync(post);

        // 4. 🎁 发放点赞奖励（仅在点赞时，不在取消点赞时发放）
        if (isLiked)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // 4.1 发放萝卜币奖励
                    var rewardResult = await _coinRewardService.GrantLikeRewardAsync(
                        postId,
                        post.AuthorId,
                        userId);

                    if (rewardResult.IsSuccess)
                    {
                        Serilog.Log.Information("帖子点赞萝卜币奖励发放成功：PostId={PostId}, 作者={AuthorId}, 点赞者={LikerId}",
                            postId, post.AuthorId, userId);
                    }

                    // 4.2 发放经验值奖励
                    Serilog.Log.Information("准备发放帖子点赞经验值：PostId={PostId}, 作者={AuthorId}, 点赞者={LikerId}",
                        postId, post.AuthorId, userId);

                    // 4.2.1 被点赞者获得 +2 经验
                    var receiverExpResult = await _experienceService.GrantExperienceAsync(
                        userId: post.AuthorId,
                        amount: 2,
                        expType: "RECEIVE_LIKE",
                        businessType: "Post",
                        businessId: postId,
                        remark: "帖子被点赞");

                    if (receiverExpResult)
                    {
                        Serilog.Log.Information("帖子被点赞经验值发放成功：PostId={PostId}, 作者={AuthorId}, Amount=2",
                            postId, post.AuthorId);
                    }
                    else
                    {
                        Serilog.Log.Warning("帖子被点赞经验值发放失败：PostId={PostId}, 作者={AuthorId}",
                            postId, post.AuthorId);
                    }

                    // 4.2.2 点赞者获得 +1 经验
                    var giverExpResult = await _experienceService.GrantExperienceAsync(
                        userId: userId,
                        amount: 1,
                        expType: "GIVE_LIKE",
                        businessType: "Post",
                        businessId: postId,
                        remark: "点赞帖子");

                    if (giverExpResult)
                    {
                        Serilog.Log.Information("点赞帖子经验值发放成功：PostId={PostId}, 点赞者={LikerId}, Amount=1",
                            postId, userId);
                    }
                    else
                    {
                        Serilog.Log.Warning("点赞帖子经验值发放失败：PostId={PostId}, 点赞者={LikerId}",
                            postId, userId);
                    }

                    // 4.3 发送点赞通知（不给自己发通知）
                    if (post.AuthorId != userId)
                    {
                        // 检查是否应该去重
                        var shouldDedup = await _dedupService.ShouldDedupAsync(
                            post.AuthorId,
                            NotificationType.PostLiked,
                            postId);

                        if (!shouldDedup)
                        {
                            try
                            {
                                await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                                {
                                    Type = NotificationType.PostLiked,
                                    Title = "帖子被点赞",
                                    Content = $"你的帖子《{post.Title}》收到了一个赞",
                                    Priority = (int)NotificationPriority.Low,
                                    BusinessType = BusinessType.Post,
                                    BusinessId = postId,
                                    TriggerId = userId,
                                    TriggerName = null, // TODO: 从用户上下文获取用户名
                                    TriggerAvatar = null, // TODO: 从用户表查询头像
                                    ReceiverUserIds = new List<long> { post.AuthorId }
                                });

                                // 记录去重键（5分钟内不重复通知）
                                await _dedupService.RecordDedupKeyAsync(
                                    post.AuthorId,
                                    NotificationType.PostLiked,
                                    postId,
                                    windowSeconds: 300);

                                Serilog.Log.Information("帖子点赞通知发送成功：PostId={PostId}, 接收者={ReceiverId}",
                                    postId, post.AuthorId);
                            }
                            catch (Exception notifyEx)
                            {
                                Serilog.Log.Error(notifyEx, "发送帖子点赞通知失败：PostId={PostId}, 接收者={ReceiverId}",
                                    postId, post.AuthorId);
                            }
                        }
                        else
                        {
                            Serilog.Log.Debug("帖子点赞通知被去重：PostId={PostId}, 接收者={ReceiverId}",
                                postId, post.AuthorId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "发放帖子点赞奖励失败：PostId={PostId}, AuthorId={AuthorId}, LikerId={LikerId}, Message={Message}",
                        postId, post.AuthorId, userId, ex.Message);
                }
            });
        }

        return new PostLikeResultDto
        {
            IsLiked = isLiked,
            LikeCount = post.LikeCount
        };
    }
}
