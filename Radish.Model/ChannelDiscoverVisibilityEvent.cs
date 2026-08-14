using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>频道匿名公开摘要资格的追加式变更事件。</summary>
[SugarTable("ChannelDiscoverVisibilityEvent")]
[Tenant(configId: "Chat")]
[SugarIndex("idx_channel_discover_event_version", nameof(TenantId), OrderByType.Asc, nameof(ChannelId), OrderByType.Asc, nameof(ResultVersion), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_channel_discover_event_time", nameof(TenantId), OrderByType.Asc, nameof(ChannelId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
public sealed class ChannelDiscoverVisibilityEvent : RootEntityTKey<long>, ITenantEntity
{
    public long TenantId { get; set; }

    public long ChannelId { get; set; }

    public ChannelDiscoverVisibility FromVisibility { get; set; }

    public ChannelDiscoverVisibility ToVisibility { get; set; }

    public int ExpectedVersion { get; set; }

    public int ResultVersion { get; set; }

    [SugarColumn(Length = 500, IsNullable = false)]
    public string Reason { get; set; } = string.Empty;

    public long ActorUserId { get; set; }

    [SugarColumn(Length = 100, IsNullable = false)]
    public string ActorName { get; set; } = "System";

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
