using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>帖子轻回应服务接口</summary>
public interface IPostQuickReplyService : IBaseService<PostQuickReply, PostQuickReplyVo>
{
    /// <summary>获取帖子最近的轻回应</summary>
    Task<PostQuickReplyWallVo> GetRecentByPostIdAsync(long postId, int take);

    /// <summary>获取当前用户的轻回应分页</summary>
    Task<(List<UserPostQuickReplyVo> items, int total)> GetMinePageAsync(long userId, int pageIndex, int pageSize);

    /// <summary>创建轻回应</summary>
    Task<PostQuickReplyVo> CreateAsync(CreatePostQuickReplyDto request, long userId, string userName, long tenantId);

    /// <summary>删除轻回应</summary>
    Task DeleteAsync(long quickReplyId, long operatorId, string operatorName, bool isAdmin);
}
