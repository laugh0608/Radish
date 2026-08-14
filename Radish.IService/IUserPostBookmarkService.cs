using Radish.Model.ViewModels;

namespace Radish.IService;

public interface IUserPostBookmarkService
{
    Task<PostBookmarkStateVo> SetStateAsync(
        long tenantId,
        long currentUserId,
        string currentUserName,
        string postPublicId,
        bool isBookmarked);

    Task<PostBookmarkStateVo> SetStateByLegacyPostIdAsync(
        long tenantId,
        long currentUserId,
        string currentUserName,
        long postId,
        bool isBookmarked);

    Task<VoPagedResult<UserPostBookmarkVo>> GetMineAsync(
        long tenantId,
        long currentUserId,
        int pageIndex,
        int pageSize);

    Task<PostBookmarkRemoveVo> RemoveAsync(
        long tenantId,
        long currentUserId,
        string currentUserName,
        string bookmarkPublicId);
}
