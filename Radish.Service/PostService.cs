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
    private readonly IBaseRepository<PostEditHistory> _postEditHistoryRepository;
    private readonly ForumEditHistoryOptions _editHistoryOptions;

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
        IExperienceService experienceService,
        IBaseRepository<PostEditHistory> postEditHistoryRepository,
        IOptions<ForumEditHistoryOptions> editHistoryOptions)
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
        _postEditHistoryRepository = postEditHistoryRepository;
        _editHistoryOptions = editHistoryOptions.Value;
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
            var tags = await _tagService.QueryAsync(t => tagIds.Contains(t.Id) && t.IsEnabled && !t.IsDeleted);
            postVo.VoTags = string.Join(", ", tags.Select(t => t.VoName));
        }

        return postVo;
    }

    /// <summary>
    /// 发布帖子
    /// </summary>
    [UseTran]
    public async Task<long> PublishPostAsync(Post post, List<string>? tagNames = null, bool allowCreateTag = true)
    {
        var normalizedTagNames = NormalizeTagNamesOrThrow(tagNames, nameof(tagNames), "发布帖子时至少需要一个标签");
        var operatorName = string.IsNullOrWhiteSpace(post.AuthorName) ? "System" : post.AuthorName;

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
        await SyncPostTagsAsync(postId, post.AuthorId, operatorName, normalizedTagNames, allowCreateTag);

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
        await SyncPostTagsAsync(postId, operatorId, safeOperatorName, normalizedTagNames, allowCreateTag);

        if (historyEnabled)
        {
            await TrimPostHistoryAsync(postId, Math.Max(1, postOptions.MaxHistoryRecords));
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
