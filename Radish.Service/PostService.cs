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
    private readonly IBaseRepository<PostTag> _postTagRepository;
    private readonly ITagService _tagService;
    private readonly ICategoryService _categoryService;

    public PostService(
        IMapper mapper,
        IBaseRepository<Post> baseRepository,
        IBaseRepository<PostTag> postTagRepository,
        ITagService tagService,
        ICategoryService categoryService)
        : base(mapper, baseRepository)
    {
        _postRepository = baseRepository;
        _postTagRepository = postTagRepository;
        _tagService = tagService;
        _categoryService = categoryService;
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
            var categories = await _categoryService.QueryAsync(c => c.Id == post.CategoryId);
            var category = categories.FirstOrDefault();
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
            await _categoryService.UpdatePostCountAsync(post.CategoryId, 1);
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
                await _tagService.UpdatePostCountAsync(tag.Id, 1);
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
}
