using Microsoft.AspNetCore.SignalR;
using Radish.Common.HttpContextTool;
using Radish.Model.ViewModels;

namespace Radish.Api.Hubs;

/// <summary>评论实时 SignalR Hub</summary>
public class CommentHub : Hub
{
    private readonly IClaimsPrincipalNormalizer _claimsPrincipalNormalizer;

    public CommentHub(IClaimsPrincipalNormalizer claimsPrincipalNormalizer)
    {
        _claimsPrincipalNormalizer = claimsPrincipalNormalizer;
    }

    public static string BuildPostGroup(long postId)
    {
        return $"post-comments:{postId}";
    }

    public async Task JoinPost(long postId)
    {
        if (postId <= 0)
        {
            throw new HubException("帖子 Id 无效");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, BuildPostGroup(postId));
    }

    public async Task LeavePost(long postId)
    {
        if (postId <= 0)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildPostGroup(postId));
    }

    public async Task StartTyping(long postId, long? commentId = null)
    {
        if (postId <= 0)
        {
            return;
        }

        var currentUser = GetCurrentUser();
        if (!currentUser.IsAuthenticated || currentUser.UserId <= 0)
        {
            return;
        }

        await Clients.OthersInGroup(BuildPostGroup(postId)).SendAsync("CommentTyping", new CommentTypingEventVo
        {
            VoPostId = postId,
            VoCommentId = commentId,
            VoUserId = currentUser.UserId,
            VoUserName = string.IsNullOrWhiteSpace(currentUser.UserName) ? "Unknown" : currentUser.UserName,
            VoEventTime = DateTime.UtcNow
        });
    }

    private CurrentUser GetCurrentUser()
    {
        return _claimsPrincipalNormalizer.Normalize(Context.User, GetAccessToken());
    }

    private string? GetAccessToken()
    {
        var httpContext = Context.GetHttpContext();
        var accessToken = httpContext?.Request.Query["access_token"].ToString();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            return accessToken;
        }

        var authorization = httpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return null;
        }

        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : authorization;
    }
}
