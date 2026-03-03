using System;
using System.Threading.Tasks;
using Radish.Common.DbTool;
using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

internal static partial class InitialDataSeeder
{
    /// <summary>初始化聊天室默认频道数据</summary>
    private static async Task SeedChatChannelsAsync(ISqlSugarClient db)
    {
        var sqlSugarScope = db as SqlSugarScope
            ?? throw new InvalidOperationException("[Radish.DbMigrate] ISqlSugarClient 不是 SqlSugarScope，无法切换到 Chat 连接。");

        var chatDb = sqlSugarScope.GetConnectionScope(SqlSugarConst.ChatConfigId);
        chatDb.CodeFirst.InitTables<Channel>();
        chatDb.CodeFirst.InitTables<ChannelMessage>();
        chatDb.CodeFirst.InitTables<ChannelMember>();
        Console.WriteLine("[Radish.DbMigrate] 已同步 Chat 库 Channel/ChannelMessage/ChannelMember 表结构。");

        var defaultChannels = new[]
        {
            new { Id = 93000L, TenantId = 30000L, Name = "综合闲聊", Slug = "general", Description = "日常交流与轻松讨论", Icon = "💬", Sort = 0 },
            new { Id = 93001L, TenantId = 30000L, Name = "技术讨论", Slug = "tech-talk", Description = "前后端与工程实践讨论", Icon = "🛠️", Sort = 1 },
            new { Id = 93010L, TenantId = 30001L, Name = "测试闲聊", Slug = "general", Description = "测试租户默认频道", Icon = "🧪", Sort = 0 }
        };

        foreach (var channelMeta in defaultChannels)
        {
            var exists = await chatDb.Queryable<Channel>()
                .AnyAsync(c => c.TenantId == channelMeta.TenantId && c.Slug == channelMeta.Slug && !c.IsDeleted);
            if (exists)
            {
                Console.WriteLine($"[Radish.DbMigrate] 租户 {channelMeta.TenantId} 频道 slug={channelMeta.Slug} 已存在，跳过。");
                continue;
            }

            Console.WriteLine($"[Radish.DbMigrate] 创建默认频道 Id={channelMeta.Id}, Name={channelMeta.Name}, TenantId={channelMeta.TenantId}...");

            await chatDb.Insertable(new Channel
            {
                Id = channelMeta.Id,
                TenantId = channelMeta.TenantId,
                Name = channelMeta.Name,
                Slug = channelMeta.Slug,
                Description = channelMeta.Description,
                IconEmoji = channelMeta.Icon,
                Type = ChannelType.Public,
                IsEnabled = true,
                Sort = channelMeta.Sort,
                LastMessageId = null,
                LastMessageTime = null,
                IsDeleted = false,
                CreateTime = DateTime.Now,
                CreateBy = "System",
                CreateId = 0
            }).ExecuteCommandAsync();
        }
    }
}
