using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>聊天消息表情回应。</summary>
[SugarTable("ChatMessageReaction")]
[Tenant(configId: "Chat")]
[SugarIndex("idx_chat_reaction_message", nameof(TenantId), OrderByType.Asc, nameof(MessageId), OrderByType.Asc, nameof(IsDeleted), OrderByType.Asc)]
[SugarIndex("idx_chat_reaction_channel_message", nameof(TenantId), OrderByType.Asc, nameof(ChannelId), OrderByType.Asc, nameof(MessageId), OrderByType.Asc)]
[SugarIndex("idx_chat_reaction_unique", nameof(TenantId), OrderByType.Asc, nameof(MessageId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, nameof(EmojiType), OrderByType.Asc, nameof(EmojiValue), OrderByType.Asc, IsUnique = true)]
public sealed class ChatMessageReaction : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    public long TenantId { get; set; }

    public long ChannelId { get; set; }

    public long MessageId { get; set; }

    public long UserId { get; set; }

    [SugarColumn(Length = 100, IsNullable = false)]
    public string UserName { get; set; } = string.Empty;

    [SugarColumn(Length = 20, IsNullable = false)]
    public string EmojiType { get; set; } = string.Empty;

    [SugarColumn(Length = 200, IsNullable = false)]
    public string EmojiValue { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public long? StickerAttachmentId { get; set; }

    public bool IsDeleted { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? DeletedAt { get; set; }

    [SugarColumn(Length = 100, IsNullable = true)]
    public string? DeletedBy { get; set; }

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
