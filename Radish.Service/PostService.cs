using AutoMapper;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Service;

/// <summary>帖子服务实现</summary>
public class PostService : BaseService<Post, PostVo>, IPostService
{
    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<UserPostLike> _userPostLikeRepository;
    private readonly IBaseRepository<PostTag> _postTagRepository;
    private readonly IBaseRepository<Category> _categoryRepository;
    private readonly IBaseRepository<Tag> _tagRepository;
    private readonly ITagService _tagService;
    private readonly ICoinRewardService _coinRewardService;
    private readonly INotificationService _notificationService;
    private readonly INotificationDedupService _dedupService;
    private readonly IExperienceService _experienceService;

    public PostService(
        IMapper mapper,
        IBaseRepository<Post> baseRepository,
        IBaseRepository<UserPostLike> userPostLikeRepository,
        IBaseRepository<PostTag> postTagRepository,
        IBaseRepository<Category> categoryRepository,
        IBaseRepository<Tag> tagRepository,
        ITagService tagService,
        ICoinRewardService coinRewardService,
        INotificationService notificationService,
        INotificationDedupService dedupService,
        IExperienceService experienceService)
        : base(mapper, baseRepository)
    {
        _postRepository = baseRepository;
        _userPostLikeRepository = userPostLikeRepository;
        _postTagRepository = postTagRepository;
        _categoryRepository = categoryRepository;
        _tagRepository = tagRepository;
        _tagService = tagService;
        _coinRewardService = coinRewardService;
        _notificationService = notificationService;
        _dedupService = dedupService;
        _experienceService = experienceService;
    }

    /// <summary>
    /// 获取帖子详情（包含分类名称和标签）
    /// </summary>
    public async Task<PostVo?> GetPostDetailAsync(long postId)
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
            var tags = await _tagService.QueryAsync(t => tagIds.Contains(t.Id));
            postVo.VoTags = string.Join(", ", tags.Select(t => t.VoName));
        }

        return postVo;
    }

    /// <summary>
    /// 发布帖子
    /// </summary>
    public async Task<long> PublishPostAsync(Post post, List<string>? tagNames = null)
    {
        // 1. 插入帖子
        var postId = await AddAsync(post);

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
        if (tagNames != null && tagNames.Any())
        {
            foreach (var tagName in tagNames.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                // 获取或创建标签
                var tag = await _tagService.GetOrCreateTagAsync(tagName);

                // 创建帖子-标签关联
                var postTag = new PostTag(postId, tag.Id)
                {
                    CreateId = post.AuthorId,
                    CreateBy = post.AuthorName
                };
                await _postTagRepository.AddAsync(postTag);

                // 更新标签的帖子数量
                tag.PostCount++;
                await _tagRepository.UpdateAsync(tag);
            }
        }

        // 4. 🎁 发放经验值奖励（异步处理）
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
