using System.Collections.Concurrent;
using Radish.IService;

namespace Radish.Service;

/// <summary>基于进程内内存的聊天室在线状态服务</summary>
public class ChatPresenceService : IChatPresenceService
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<long, int>> ChannelUserConnectionCounter = new();
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> ConnectionChannelKeys = new();

    public void JoinChannel(string connectionId, long tenantId, long channelId, long userId)
    {
        var channelKey = BuildChannelKey(tenantId, channelId);

        var userCounter = ChannelUserConnectionCounter.GetOrAdd(channelKey, _ => new ConcurrentDictionary<long, int>());
        userCounter.AddOrUpdate(userId, 1, (_, count) => count + 1);

        var channelKeys = ConnectionChannelKeys.GetOrAdd(connectionId, _ => new ConcurrentDictionary<string, byte>());
        channelKeys.TryAdd(channelKey, 0);
    }

    public void LeaveChannel(string connectionId, long tenantId, long channelId, long userId)
    {
        var channelKey = BuildChannelKey(tenantId, channelId);
        DecreaseUserCounter(channelKey, userId);

        if (ConnectionChannelKeys.TryGetValue(connectionId, out var channelKeys))
        {
            channelKeys.TryRemove(channelKey, out _);
            if (channelKeys.IsEmpty)
            {
                ConnectionChannelKeys.TryRemove(connectionId, out _);
            }
        }
    }

    public void RemoveConnection(string connectionId, long userId)
    {
        if (!ConnectionChannelKeys.TryRemove(connectionId, out var channelKeys))
        {
            return;
        }

        foreach (var channelKey in channelKeys.Keys)
        {
            DecreaseUserCounter(channelKey, userId);
        }
    }

    public List<long> GetOnlineUserIds(long tenantId, long channelId)
    {
        var channelKey = BuildChannelKey(tenantId, channelId);
        if (!ChannelUserConnectionCounter.TryGetValue(channelKey, out var userCounter))
        {
            return new List<long>();
        }

        return userCounter
            .Where(item => item.Value > 0)
            .Select(item => item.Key)
            .Distinct()
            .ToList();
    }

    private static void DecreaseUserCounter(string channelKey, long userId)
    {
        if (!ChannelUserConnectionCounter.TryGetValue(channelKey, out var userCounter))
        {
            return;
        }

        if (userCounter.TryGetValue(userId, out var currentCount))
        {
            if (currentCount <= 1)
            {
                userCounter.TryRemove(userId, out _);
            }
            else
            {
                userCounter.TryUpdate(userId, currentCount - 1, currentCount);
            }
        }

        if (userCounter.IsEmpty)
        {
            ChannelUserConnectionCounter.TryRemove(channelKey, out _);
        }
    }

    private static string BuildChannelKey(long tenantId, long channelId)
    {
        return $"{tenantId}:{channelId}";
    }
}
