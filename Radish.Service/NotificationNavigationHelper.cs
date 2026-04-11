using System.Text.Json;

namespace Radish.Service;

internal static class NotificationNavigationHelper
{
    public static string BuildForumNavigationExtData(long postId, long? commentId = null)
    {
        if (postId <= 0)
        {
            throw new ArgumentException("帖子ID必须大于0", nameof(postId));
        }

        return JsonSerializer.Serialize(new
        {
            app = "forum",
            postId = postId.ToString(),
            commentId = commentId > 0 ? commentId.Value.ToString() : null
        });
    }

    public static string BuildForumLotteryNavigationExtData(
        long postId,
        long lotteryId,
        string prizeName,
        int winnerCount)
    {
        if (postId <= 0)
        {
            throw new ArgumentException("帖子ID必须大于0", nameof(postId));
        }

        if (lotteryId <= 0)
        {
            throw new ArgumentException("抽奖ID必须大于0", nameof(lotteryId));
        }

        return JsonSerializer.Serialize(new
        {
            app = "forum",
            postId = postId.ToString(),
            lotteryId = lotteryId.ToString(),
            prizeName,
            winnerCount
        });
    }
}
