using Radish.Model;

namespace Radish.IRepository;

public sealed record PostBookmarkStateCommand(
    long TenantId,
    long UserId,
    long PostId,
    string OperatorName,
    bool IsBookmarked,
    DateTime NowUtc);

public sealed record PostBookmarkWriteResult(
    UserPostBookmark? Bookmark,
    Post Post,
    bool IsBookmarked,
    int CollectCount,
    bool Changed);

public sealed record PostBookmarkRemoveResult(
    UserPostBookmark? Bookmark,
    bool Changed);

public sealed class PostBookmarkPostNotFoundException : Exception
{
}

public sealed class PostBookmarkPostUnavailableException : Exception
{
}

public sealed class PostBookmarkUserUnavailableException : Exception
{
}

public sealed class PostBookmarkStateConflictException : Exception
{
}

public interface IUserPostBookmarkRepository
{
    Task<Post?> QueryPostByPublicIdAsync(long tenantId, string postPublicId);

    Task<Post?> QueryPostByIdAsync(long tenantId, long postId);

    Task<PostBookmarkWriteResult> SetStateAsync(PostBookmarkStateCommand command);

    Task<PostBookmarkRemoveResult> RemoveAsync(
        long tenantId,
        long userId,
        string bookmarkPublicId,
        string operatorName,
        DateTime nowUtc);

    Task<(IReadOnlyList<UserPostBookmark> Items, int Total)> QueryMineAsync(
        long tenantId,
        long userId,
        int pageIndex,
        int pageSize);

    Task<UserPostBookmark?> QueryActiveAsync(long tenantId, long userId, long postId);
}
