namespace Radish.Model;

/// <summary>固定文档同步摘要</summary>
public sealed class WikiBuiltInSyncSummary
{
    public bool IsSkipped { get; set; }

    public string? SkipReason { get; set; }

    public int MarkdownFileCount { get; set; }

    public int DescriptorCount { get; set; }

    public int GeneratedNodeCount { get; set; }

    public int SyncedCount { get; set; }

    public int CreatedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int RestoredCount { get; set; }

    public int ParentAdjustedCount { get; set; }

    public int SoftDeletedCount { get; set; }

    public int SkippedCount { get; set; }
}
