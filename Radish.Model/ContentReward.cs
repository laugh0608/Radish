using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>论坛帖子或评论的一次成功内容赞赏事实。</summary>
[SugarTable("ContentReward")]
[SugarIndex(
    "idx_content_reward_sender_target_unique",
    nameof(TenantId), OrderByType.Asc,
    nameof(SenderUserId), OrderByType.Asc,
    nameof(TargetType), OrderByType.Asc,
    nameof(TargetId), OrderByType.Asc,
    IsUnique = true)]
[SugarIndex(
    "idx_content_reward_transaction_unique",
    nameof(TenantId), OrderByType.Asc,
    nameof(CoinTransactionId), OrderByType.Asc,
    IsUnique = true)]
[SugarIndex(
    "idx_content_reward_target_time",
    nameof(TenantId), OrderByType.Asc,
    nameof(TargetType), OrderByType.Asc,
    nameof(TargetId), OrderByType.Asc,
    nameof(CreateTime), OrderByType.Desc)]
[SugarIndex(
    "idx_content_reward_sender_time",
    nameof(TenantId), OrderByType.Asc,
    nameof(SenderUserId), OrderByType.Asc,
    nameof(CreateTime), OrderByType.Desc)]
[SugarIndex(
    "idx_content_reward_sender_recipient_time",
    nameof(TenantId), OrderByType.Asc,
    nameof(SenderUserId), OrderByType.Asc,
    nameof(RecipientUserId), OrderByType.Asc,
    nameof(CreateTime), OrderByType.Desc)]
[SugarIndex(
    "idx_content_reward_post_time",
    nameof(TenantId), OrderByType.Asc,
    nameof(PostId), OrderByType.Asc,
    nameof(CreateTime), OrderByType.Desc)]
public sealed class ContentReward : RootEntityTKey<long>, ITenantEntity
{
    public long TenantId { get; set; }

    [SugarColumn(Length = 20, IsNullable = false)]
    public string TargetType { get; set; } = string.Empty;

    public long TargetId { get; set; }

    public long PostId { get; set; }

    public long SenderUserId { get; set; }

    public long RecipientUserId { get; set; }

    public long Amount { get; set; } = 1;

    [SugarColumn(Length = 30, IsNullable = false)]
    public string ReasonCode { get; set; } = string.Empty;

    public long CoinTransactionId { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    [SugarColumn(Length = 100, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    public long CreateId { get; set; }
}
