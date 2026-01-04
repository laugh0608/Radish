using AutoMapper;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
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

    public PostService(
        IMapper mapper,
        IBaseRepository<Post> baseRepository,
        IBaseRepository<UserPostLike> userPostLikeRepository,
        IBaseRepository<PostTag> postTagRepository,
        IBaseRepository<Category> categoryRepository,
        IBaseRepository<Tag> tagRepository,
        ITagService tagService,
        ICoinRewardService coinRewardService)
        : base(mapper, baseRepository)
    {
        _postRepository = baseRepository;
        _userPostLikeRepository = userPostLikeRepository;
        _postTagRepository = postTagRepository;
        _categoryRepository = categoryRepository;
        _tagRepository = tagRepository;
        _tagService = tagService;
        _coinRewardService = coinRewardService;
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
                postVo.CategoryName = category.Name;
            }
        }

        // 获取标签
        var postTags = await _postTagRepository.QueryAsync(pt => pt.PostId == postId);
        if (postTags.Any())
        {
            var tagIds = postTags.Select(pt => pt.TagId).ToList();
            var tags = await _tagService.QueryAsync(t => tagIds.Contains(t.Id));
            postVo.Tags = string.Join(", ", tags.Select(t => t.Name));
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

        // 2. 检查是否已点赞
        var existingLikes = await _userPostLikeRepository.QueryAsync(
            x => x.UserId == userId && x.PostId == postId);

        bool isLiked;
        int likeCountDelta;

        if (existingLikes.Any())
        {
            // 取消点赞
            await _userPostLikeRepository.DeleteByIdAsync(existingLikes.First().Id);
            isLiked = false;
            likeCountDelta = -1;
        }
        else
        {
            // 添加点赞
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
                    var rewardResult = await _coinRewardService.GrantLikeRewardAsync(
                        postId,
                        post.AuthorId,
                        userId);

                    if (rewardResult.IsSuccess)
                    {
                        Serilog.Log.Information("帖子点赞奖励发放成功：PostId={PostId}, 作者={AuthorId}, 点赞者={LikerId}",
                            postId, post.AuthorId, userId);
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "发放帖子点赞奖励失败：PostId={PostId}, AuthorId={AuthorId}, LikerId={LikerId}",
                        postId, post.AuthorId, userId);
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
