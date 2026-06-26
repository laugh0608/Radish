using Radish.Model;

namespace Radish.Service;

internal static class ForumDisplayNameHelper
{
    public static string Build(User user, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            return User.NormalizeDisplayName(user.UserName, user.Id);
        }

        return string.IsNullOrWhiteSpace(fallback) ? $"User-{user.Id}" : fallback.Trim();
    }

    public static Dictionary<long, string> BuildMap(IEnumerable<User> users)
    {
        return users
            .Where(user => user.Id > 0 && !user.IsDeleted)
            .GroupBy(user => user.Id)
            .ToDictionary(
                group => group.Key,
                group => Build(group.First(), $"User-{group.Key}"));
    }
}
