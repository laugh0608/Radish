namespace Radish.Model.ViewModels;

/// <summary>批量新增表情结果</summary>
public class StickerBatchAddResultVo
{
    public long VoGroupId { get; set; }
    public int VoCreatedCount { get; set; }
    public List<long> VoStickerIds { get; set; } = new();
    public List<StickerBatchConflictVo> VoConflicts { get; set; } = new();
    public List<StickerBatchFailedItemVo> VoFailedItems { get; set; } = new();
}

/// <summary>批量新增冲突项</summary>
public class StickerBatchConflictVo
{
    public int VoRowIndex { get; set; }
    public string VoCode { get; set; } = string.Empty;
    public string VoMessage { get; set; } = string.Empty;
}

/// <summary>批量新增失败项</summary>
public class StickerBatchFailedItemVo
{
    public int VoRowIndex { get; set; }
    public long VoAttachmentId { get; set; }
    public string VoCode { get; set; } = string.Empty;
    public string VoMessage { get; set; } = string.Empty;
}

/// <summary>批量更新排序结果</summary>
public class StickerBatchUpdateSortResultVo
{
    public int VoUpdatedCount { get; set; }
}
