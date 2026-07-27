using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>论坛作者内容版本快照、查询与恢复服务。</summary>
public interface IForumContentRevisionService
{
    Task<ForumContentRevisionWriteResult> AppendPostRevisionAsync(
        long postId,
        string sourceType,
        long? restoredFromRevisionId,
        long editorId,
        string editorName);

    Task<ForumContentRevisionWriteResult> AppendCommentRevisionAsync(
        long commentId,
        string sourceType,
        long? restoredFromRevisionId,
        long editorId,
        string editorName);

    Task<ForumContentRevisionWriteResult> GetCurrentPostRevisionAsync(long postId);

    Task<ForumContentRevisionWriteResult> GetCurrentCommentRevisionAsync(long commentId);

    Task<PostContentRevisionListVo> GetPostRevisionListAsync(
        long postId,
        long viewerId,
        bool isAdmin,
        int pageIndex,
        int pageSize);

    Task<CommentContentRevisionListVo> GetCommentRevisionListAsync(
        long commentId,
        long viewerId,
        bool isAdmin,
        int pageIndex,
        int pageSize);

    Task<PostContentRevisionDetailVo> GetPostRevisionDetailAsync(
        long revisionId,
        long viewerId,
        bool isAdmin);

    Task<CommentContentRevisionDetailVo> GetCommentRevisionDetailAsync(
        long revisionId,
        long viewerId,
        bool isAdmin);

    Task<ForumContentRevisionWriteResult> RestorePostAsync(
        long postId,
        long revisionId,
        int expectedContentRevision,
        long operatorId,
        string operatorName,
        bool isAdmin);

    Task<ForumContentRevisionWriteResult> RestoreCommentAsync(
        long commentId,
        long revisionId,
        int expectedContentRevision,
        long operatorId,
        string operatorName,
        bool isAdmin);
}
