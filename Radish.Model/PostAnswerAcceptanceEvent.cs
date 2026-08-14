using Radish.Model.Root;
using Radish.Shared.Constants;
using SqlSugar;

namespace Radish.Model;

/// <summary>问题采纳状态的追加式变更事件。</summary>
[SugarTable("PostAnswerAcceptanceEvent")]
[SugarIndex(
    "idx_answeracceptanceevent_question_revision",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(PostQuestionId),
    OrderByType.Asc,
    nameof(AcceptanceRevision),
    OrderByType.Asc,
    IsUnique = true)]
public sealed class PostAnswerAcceptanceEvent : RootEntityTKey<long>, ITenantEntity
{
    public long TenantId { get; set; }
    public long PostId { get; set; }
    public long PostQuestionId { get; set; }
    public int AcceptanceRevision { get; set; }

    [SugarColumn(Length = 40, IsNullable = false)]
    public string EventType { get; set; } = PostAnswerAcceptanceEventTypes.Accepted;

    [SugarColumn(IsNullable = true)]
    public long? PreviousAnswerId { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? PreviousAnswerContentRevision { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? CurrentAnswerId { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? CurrentAnswerContentRevision { get; set; }
    public long OperatorId { get; set; }

    [SugarColumn(Length = 100, IsNullable = false)]
    public string OperatorName { get; set; } = "System";

    [SugarColumn(Length = 80, IsNullable = true)]
    public string? ReasonCode { get; set; }

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
