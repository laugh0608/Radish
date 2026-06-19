using Microsoft.AspNetCore.SignalR;
using Radish.Api.Hubs;
using Radish.Model.ViewModels;

namespace Radish.Api.Services;

public class CommentRealtimePushService
{
    private readonly IHubContext<CommentHub> _hubContext;
    private readonly ILogger<CommentRealtimePushService> _logger;

    public CommentRealtimePushService(
        IHubContext<CommentHub> hubContext,
        ILogger<CommentRealtimePushService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task PushCreatedAsync(CommentVo comment)
    {
        return PushCommentEventAsync("CommentCreated", comment);
    }

    public Task PushUpdatedAsync(CommentVo comment)
    {
        return PushCommentEventAsync("CommentUpdated", comment);
    }

    public async Task PushDeletedAsync(long postId, long commentId, long? parentCommentId, long? rootCommentId)
    {
        await PushAsync("CommentDeleted", postId, new CommentRealtimeEventVo
        {
            VoPostId = postId,
            VoCommentId = commentId,
            VoParentCommentId = parentCommentId,
            VoRootCommentId = rootCommentId,
            VoEventTime = DateTime.UtcNow
        });
    }

    public async Task PushLikeChangedAsync(long postId, long commentId, long? parentCommentId, long? rootCommentId, int likeCount)
    {
        await PushAsync("CommentLikeChanged", postId, new CommentRealtimeEventVo
        {
            VoPostId = postId,
            VoCommentId = commentId,
            VoParentCommentId = parentCommentId,
            VoRootCommentId = rootCommentId,
            VoLikeCount = likeCount,
            VoEventTime = DateTime.UtcNow
        });
    }

    public async Task PushHighlightChangedAsync(CommentHighlightRecheckResultVo result)
    {
        if (!result.VoChanged || result.VoPostId <= 0)
        {
            return;
        }

        await PushAsync("CommentHighlightsChanged", result.VoPostId, result);
    }

    private async Task PushCommentEventAsync(string eventName, CommentVo comment)
    {
        if (comment.VoPostId <= 0 || comment.VoId <= 0)
        {
            return;
        }

        await PushAsync(eventName, comment.VoPostId, new CommentRealtimeEventVo
        {
            VoPostId = comment.VoPostId,
            VoCommentId = comment.VoId,
            VoParentCommentId = comment.VoParentId,
            VoRootCommentId = comment.VoRootId,
            VoComment = comment,
            VoLikeCount = comment.VoLikeCount,
            VoEventTime = DateTime.UtcNow
        });
    }

    private async Task PushAsync(string eventName, long postId, object payload)
    {
        try
        {
            await _hubContext.Clients.Group(CommentHub.BuildPostGroup(postId)).SendAsync(eventName, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CommentRealtimePushService] 推送评论事件失败，EventName: {EventName}, PostId: {PostId}",
                eventName,
                postId);
        }
    }
}
