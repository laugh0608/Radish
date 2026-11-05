using Radish.Model.LogModels;

namespace Radish.IRepository;

public interface IRuntimeLogIngestRepository
{
    /// <summary>原子提交一个内部批次；返回按输入顺序的已存实体，null 表示同键异内容拒收。</summary>
    Task<IReadOnlyList<RuntimeLogEventRecord?>> CommitAsync(IReadOnlyList<RuntimeLogEventRecord> events,
        IReadOnlyList<RuntimeLogRejectionBucket> rejections, CancellationToken cancellationToken);
}
