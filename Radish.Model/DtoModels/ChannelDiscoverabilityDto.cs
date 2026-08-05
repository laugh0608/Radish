namespace Radish.Model.DtoModels;

/// <summary>把频道匿名发现摘要资格设置为目标状态。</summary>
public sealed class UpdateChannelDiscoverVisibilityDto
{
    public ChannelDiscoverVisibility DiscoverVisibility { get; set; }

    public int ExpectedVersion { get; set; }

    public string? Reason { get; set; }
}
