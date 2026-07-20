using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>将既有 Chat 附件统一收口为非公开资源。</summary>
internal sealed class ChatAttachmentPrivacySchemaMigration : ISchemaMigration
{
    private const string AttachmentTable = "Attachment";

    public static ChatAttachmentPrivacySchemaMigration Instance { get; } = new();

    public string MigrationId => "20260718_002_chat_attachment_privacy";

    public string Scope => "Main";

    public string Description => "将既有 Chat 附件统一收口为非公开资源";

    public string ChecksumSource =>
        "20260718_002_chat_attachment_privacy|Main|Attachment.BusinessType=Chat.IsPublic=false-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(AttachmentTable, false))
        {
            return;
        }

        db.Updateable<Attachment>()
            .SetColumns(attachment => new Attachment { IsPublic = false })
            .Where(attachment => attachment.BusinessType == "Chat" && attachment.IsPublic)
            .ExecuteCommand();
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(AttachmentTable, false))
        {
            return [];
        }

        var publicChatAttachmentCount = db.Queryable<Attachment>()
            .Where(attachment => attachment.BusinessType == "Chat" && attachment.IsPublic)
            .Count();
        return publicChatAttachmentCount > 0
            ? [$"发现 {publicChatAttachmentCount} 个公开 Chat 附件；apply 将只修改访问标记，不改动文件内容或业务绑定。"]
            : [];
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(AttachmentTable, false))
        {
            return [];
        }

        var publicChatAttachmentCount = db.Queryable<Attachment>()
            .Where(attachment => attachment.BusinessType == "Chat" && attachment.IsPublic)
            .Count();
        return publicChatAttachmentCount > 0
            ? [$"仍存在 {publicChatAttachmentCount} 个公开 Chat 附件。"]
            : [];
    }
}
