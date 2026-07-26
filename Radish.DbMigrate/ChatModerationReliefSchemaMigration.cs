using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>为 Chat 治理撤回与恢复建立来源标记。</summary>
internal sealed class ChatModerationReliefSchemaMigration : ISchemaMigration
{
    public static ChatModerationReliefSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260725_010_chat_moderation_relief";
    public string Scope => "Chat";
    public string Description => "建立 Chat 消息及联动对象的治理动作来源";
    public string ChecksumSource =>
        "20260725_010_chat_moderation_relief|Chat|" +
        "ChannelMessage-target-action-v1|Reaction-target-action-v1|Pin-target-action-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<ChannelMessage>();
        db.CodeFirst.InitTables<ChatMessageReaction>();
        db.CodeFirst.InitTables<ChatMessagePin>();
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable("ChannelMessage", false))
        {
            return [];
        }

        var deletedMessages = db.Queryable<ChannelMessage>().Where(item => item.IsDeleted).Count();
        return deletedMessages > 0
            ? [$"发现 {deletedMessages} 条历史已撤回消息；Chat 库没有独立 Case 事实，迁移保留其无来源状态，不猜测为治理动作。"]
            : [];
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        foreach (var (table, column) in new[]
                 {
                     ("ChannelMessage", nameof(ChannelMessage.ModerationTargetActionId)),
                     ("ChatMessageReaction", nameof(ChatMessageReaction.ModerationTargetActionId)),
                     ("ChatMessagePin", nameof(ChatMessagePin.ModerationTargetActionId))
                 })
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, table, column) == null)
            {
                issues.Add($"缺少列 {table}.{column}。");
            }
        }

        if (issues.Count > 0)
        {
            return issues;
        }

        var invalidMessages = db.Queryable<ChannelMessage>()
            .Where(item => item.ModerationTargetActionId != null && !item.IsDeleted)
            .Count();
        if (invalidMessages > 0)
        {
            issues.Add($"存在 {invalidMessages} 条带治理来源但未撤回的消息。");
        }

        return issues;
    }
}
