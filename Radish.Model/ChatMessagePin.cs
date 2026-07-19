using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>聊天频道共享消息置顶状态。</summary>
[SugarTable("ChatMessagePin")]
[Tenant(configId: "Chat")]
[SugarIndex("idx_chat_message_pin_channel", nameof(TenantId), OrderByType.Asc, nameof(ChannelId), OrderByType.Asc, nameof(IsDeleted), OrderByType.Asc, nameof(PinnedAt), OrderByType.Desc)]
[SugarIndex("idx_chat_message_pin_message", nameof(TenantId), OrderByType.Asc, nameof(MessageId), OrderByType.Asc, nameof(IsDeleted), OrderByType.Asc)]
[SugarIndex("idx_chat_message_pin_unique", nameof(TenantId), OrderByType.Asc, nameof(ChannelId), OrderByType.Asc, nameof(MessageId), OrderByType.Asc, IsUnique = true)]
public sealed class ChatMessagePin : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    public long TenantId { get; set; }

    public long ChannelId { get; set; }

    public long MessageId { get; set; }

    public long PinnedByUserId { get; set; }

    [SugarColumn(Length = 100, IsNullable = false)]
    public string PinnedByName { get; set; } = string.Empty;

    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime PinnedAt { get; set; }

    public bool IsDeleted { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? DeletedAt { get; set; }

    [SugarColumn(Length = 100, IsNullable = true)]
    public string? DeletedBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? DeletedByUserId { get; set; }

    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    [SugarColumn(Length = 100, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    public long CreateId { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ModifyTime { get; set; }

    [SugarColumn(Length = 100, IsNullable = true)]
    public string? ModifyBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }
}
