using Radish.Common.AttributeTool;
using Radish.Model.ViewModels;

namespace Radish.Service;

public partial class PostService
{
    /// <summary>
    /// 设置帖子置顶状态
    /// </summary>
    [UseTran]
    public async Task<PostVo> SetTopAsync(long postId, bool isTop, long operatorId, string operatorName)
    {
        if (postId <= 0)
        {
            throw new ArgumentException("帖子ID必须大于0", nameof(postId));
        }

        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted || !post.IsPublished)
        {
            throw new InvalidOperationException("帖子不存在");
        }

        if (post.IsTop != isTop)
        {
            post.IsTop = isTop;
            post.ModifyTime = DateTime.Now;
            post.ModifyBy = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName;
            post.ModifyId = operatorId > 0 ? operatorId : (long?)null;
            await _postRepository.UpdateAsync(post);
        }

        return await GetPostDetailAsync(postId)
               ?? throw new InvalidOperationException("帖子不存在");
    }
}
