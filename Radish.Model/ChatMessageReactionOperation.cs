using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>聊天消息回应写入幂等事实。</summary>
[SugarTable("ChatMessageReactionOperation")]
[Tenant(configId: "Chat")]
[SugarIndex("idx_chat_reaction_operation_unique", nameof(TenantId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, nameof(ClientOperationId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_chat_reaction_operation_expiry", nameof(ExpiresAtUtc), OrderByType.Asc, nameof(Id), OrderByType.Asc)]
public sealed class ChatMessageReactionOperation : RootEntityTKey<long>, ITenantEntity
{
    public long TenantId { get; set; }

    public long UserId { get; set; }

    [SugarColumn(Length = 100, IsNullable = false)]
    public string ClientOperationId { get; set; } = string.Empty;

    public long ChannelId { get; set; }

    public long MessageId { get; set; }

    [SugarColumn(Length = 20, IsNullable = false)]
    public string EmojiType { get; set; } = string.Empty;

    [SugarColumn(Length = 200, IsNullable = false)]
    public string EmojiValue { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public long ResultRevision { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    [SugarColumn(Length = 100, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    public long CreateId { get; set; }
}
