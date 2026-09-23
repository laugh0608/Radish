using System.Diagnostics;
using Radish.Common.LogTool;
using Radish.Common.CoreTool;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.Infrastructure.FileStorage;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Serilog;

namespace Radish.Service.Jobs;

/// <summary>
/// 文件清理定时任务
/// </summary>
/// <remarks>
/// 负责清理软删除文件、临时文件、回收站文件和孤立附件
/// </remarks>
public class FileCleanupJob
{
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IAttachmentReferenceInspector _attachmentReferenceInspector;
    private readonly IFileStorage _fileStorage;
    private readonly TimeProvider _timeProvider;
    private readonly BusinessCalendar _businessCalendar;
    private readonly string _tempPath;
    private readonly string _chunkTempPath;
    private readonly string _recycleBinPath;

    public FileCleanupJob(
        IBaseRepository<Attachment> attachmentRepository,
        IAttachmentReferenceInspector attachmentReferenceInspector,
        IFileStorage fileStorage,
        TimeProvider timeProvider,
        BusinessCalendar businessCalendar,
        IOptions<ChunkedUploadOptions> chunkedUploadOptions)
        : this(
            attachmentRepository,
            attachmentReferenceInspector,
            fileStorage,
            timeProvider,
            businessCalendar,
            chunkedUploadOptions,
            AppPathTool.GetDataBasesPath())
    {
    }

    internal FileCleanupJob(
        IBaseRepository<Attachment> attachmentRepository,
        IAttachmentReferenceInspector attachmentReferenceInspector,
        IFileStorage fileStorage,
        TimeProvider timeProvider,
        BusinessCalendar businessCalendar,
        IOptions<ChunkedUploadOptions> chunkedUploadOptions,
        string dataBasesPath)
    {
        _attachmentRepository = attachmentRepository;
        _attachmentReferenceInspector = attachmentReferenceInspector;
        _fileStorage = fileStorage;
        _timeProvider = timeProvider;
        _businessCalendar = businessCalendar;

        _tempPath = Path.Combine(dataBasesPath, "Temp");
        _recycleBinPath = Path.Combine(dataBasesPath, "Recycle");
        var configuredChunkPath = string.IsNullOrWhiteSpace(chunkedUploadOptions.Value.TempChunkPath)
            ? "DataBases/Temp/Chunks"
            : chunkedUploadOptions.Value.TempChunkPath;
        var solutionRoot = AppPathTool.GetSolutionRootOrBasePath();
        _chunkTempPath = Path.IsPathRooted(configuredChunkPath)
            ? Path.GetFullPath(configuredChunkPath)
            : Path.GetFullPath(Path.Combine(solutionRoot, configuredChunkPath));

        if (!Directory.Exists(_tempPath))
        {
            Directory.CreateDirectory(_tempPath);
        }

        // 确保回收站目录存在
        if (!Directory.Exists(_recycleBinPath))
        {
            Directory.CreateDirectory(_recycleBinPath);
        }
    }

    #region 软删除文件清理

    /// <summary>
    /// 清理软删除的文件
    /// </summary>
    /// <param name="retentionDays">保留天数（默认 30 天）</param>
    /// <returns>清理的文件数量</returns>
    public async Task<int> CleanupDeletedFilesAsync(int retentionDays = 30)
    {
        using var summary = new FileCleanupSummary("file-deleted");
        try
        {
            var cutoffDate = GetUtcNow().AddDays(-retentionDays);

            // 查询需要清理的附件（软删除且超过保留期）
            // 使用 ModifyTime 判断删除时间（删除操作会更新 ModifyTime）
            var deletedAttachments = await _attachmentRepository.QueryAsync(a =>
                a.IsDeleted &&
                a.ModifyTime.HasValue &&
                a.ModifyTime.Value < cutoffDate);

            if (deletedAttachments == null || deletedAttachments.Count == 0)
            {
                return 0;
            }

            var cleanedCount = 0;

            foreach (var attachment in deletedAttachments)
            {
                try
                {
                    // 移动文件到回收站（而不是直接删除）
                    await MoveToRecycleBinAsync(summary, attachment.StoragePath, "deleted");

                    // 如果有缩略图，也移动到回收站
                    if (!string.IsNullOrWhiteSpace(attachment.ThumbnailPath))
                    {
                        await MoveToRecycleBinAsync(summary, attachment.ThumbnailPath, "deleted");
                    }

                    cleanedCount++;
                    summary.ProcessedCount++;
                }
                catch (Exception ex)
                {
                    summary.RecordFailure(ex);
                }
            }

            return cleanedCount;
        }
        catch (Exception ex)
        {
            summary.RecordFailure(ex);
            return 0;
        }
    }

    #endregion

    #region 临时文件清理

    /// <summary>
    /// 清理临时文件
    /// </summary>
    /// <param name="retentionHours">保留小时数（默认 2 小时）</param>
    /// <returns>清理的文件数量</returns>
    public async Task<int> CleanupTempFilesAsync(int retentionHours = 2)
    {
        using var summary = new FileCleanupSummary("file-temp");
        try
        {
            if (!Directory.Exists(_tempPath))
            {
                return 0;
            }

            var cutoffTime = GetUtcNow().AddHours(-retentionHours);
            var cleanedCount = 0;

            // 获取所有临时文件
            var tempFiles = Directory.GetFiles(_tempPath, "*", SearchOption.AllDirectories);

            foreach (var filePath in tempFiles)
            {
                if (PathContainmentTool.IsSameOrDescendant(_chunkTempPath, filePath))
                {
                    continue;
                }

                try
                {
                    var fileInfo = new FileInfo(filePath);

                    // 检查文件最后修改时间
                    if (fileInfo.LastWriteTimeUtc < cutoffTime)
                    {
                        // 移动到回收站（保持数据安全）
                        var relativePath = Path.GetRelativePath(_tempPath, filePath);
                        var moved = await MoveToRecycleBinAsync(
                            summary,
                            relativePath,
                            "temp",
                            _tempPath,
                            Path.Combine("Temp", relativePath));
                        if (moved)
                        {
                            cleanedCount++;
                            summary.ProcessedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    summary.RecordFailure(ex);
                }
            }

            // 清理空目录
            CleanupEmptyDirectories(_tempPath, summary);

            return cleanedCount;
        }
        catch (Exception ex)
        {
            summary.RecordFailure(ex);
            return 0;
        }
    }

    #endregion

    #region 回收站清理

    /// <summary>
    /// 清理回收站文件
    /// </summary>
    /// <param name="retentionDays">保留天数（默认 90 天）</param>
    /// <returns>清理的文件数量</returns>
    public async Task<int> CleanupRecycleBinAsync(int retentionDays = 90)
    {
        using var summary = new FileCleanupSummary("file-recycle");
        try
        {
            if (!Directory.Exists(_recycleBinPath))
            {
                return 0;
            }

            var cutoffTime = GetUtcNow().AddDays(-retentionDays);
            var cleanedCount = 0;

            // 获取回收站中的所有文件
            var files = Directory.GetFiles(_recycleBinPath, "*", SearchOption.AllDirectories);

            foreach (var filePath in files)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);

                    // 检查文件创建时间（移入回收站的时间）
                    if (fileInfo.CreationTimeUtc < cutoffTime)
                    {
                        // 永久删除文件
                        File.Delete(filePath);
                        cleanedCount++;
                        summary.ProcessedCount++;
                    }
                }
                catch (Exception ex)
                {
                    summary.RecordFailure(ex);
                }
            }

            // 清理空目录
            CleanupEmptyDirectories(_recycleBinPath, summary);

            return cleanedCount;
        }
        catch (Exception ex)
        {
            summary.RecordFailure(ex);
            return 0;
        }
    }

    #endregion

    #region 孤立附件清理

    /// <summary>
    /// 清理孤立附件（未关联业务对象的临时上传文件）
    /// </summary>
    /// <param name="retentionHours">保留小时数（默认 24 小时）</param>
    /// <returns>清理的文件数量</returns>
    public async Task<int> CleanupOrphanAttachmentsAsync(int retentionHours = 24)
    {
        using var summary = new FileCleanupSummary("file-orphan");
        try
        {
            var cutoffTime = GetUtcNow().AddHours(-retentionHours);

            // 查询孤立附件（未关联业务对象且超过保留期）
            var orphanAttachments = await _attachmentRepository.QueryAsync(a =>
                !a.IsDeleted &&
                !a.BusinessId.HasValue &&
                a.CreateTime < cutoffTime);

            if (orphanAttachments == null || orphanAttachments.Count == 0)
            {
                return 0;
            }

            var orphanAttachmentIds = orphanAttachments.Select(a => a.Id).ToList();
            var referencedAttachmentIds = await _attachmentReferenceInspector.GetReferencedAttachmentIdsAsync(orphanAttachmentIds);

            var safeToCleanupAttachments = orphanAttachments
                .Where(a => !referencedAttachmentIds.Contains(a.Id))
                .ToList();

            var skippedCount = orphanAttachments.Count - safeToCleanupAttachments.Count;
            summary.SkippedCount = skippedCount;

            if (safeToCleanupAttachments.Count == 0)
            {
                return 0;
            }

            var cleanedCount = 0;

            foreach (var attachment in safeToCleanupAttachments)
            {
                try
                {
                    // 移动文件到回收站
                    await MoveToRecycleBinAsync(summary, attachment.StoragePath, "orphan");

                    // 如果有缩略图，也移动到回收站
                    if (!string.IsNullOrWhiteSpace(attachment.ThumbnailPath))
                    {
                        await MoveToRecycleBinAsync(summary, attachment.ThumbnailPath, "orphan");
                    }

                    // 标记为已删除（保留数据库记录）
                    attachment.IsDeleted = true;
                    attachment.ModifyTime = GetUtcNow();
                    await _attachmentRepository.UpdateAsync(attachment);

                    cleanedCount++;
                    summary.ProcessedCount++;
                }
                catch (Exception ex)
                {
                    summary.RecordFailure(ex);
                }
            }

            return cleanedCount;
        }
        catch (Exception ex)
        {
            summary.RecordFailure(ex);
            return 0;
        }
    }

    #endregion

    #region 私有辅助方法

    /// <summary>
    /// 将文件移动到回收站
    /// </summary>
    /// <param name="relativePath">文件相对路径</param>
    /// <param name="category">分类（deleted/temp/orphan）</param>
    /// <param name="sourceBasePath">源文件基础路径（可选，用于临时文件等非标准路径）</param>
    /// <param name="targetRelativePath">回收站内的相对路径；默认与源相对路径一致。</param>
    private async Task<bool> MoveToRecycleBinAsync(
        FileCleanupSummary summary,
        string relativePath,
        string category,
        string? sourceBasePath = null,
        string? targetRelativePath = null)
    {
        // 如果提供了自定义基础路径，使用它；否则使用文件存储的路径
        var sourceFullPath = sourceBasePath != null
            ? Path.Combine(sourceBasePath, relativePath)
            : _fileStorage.GetFullPath(relativePath);

        if (!File.Exists(sourceFullPath))
        {
            summary.MissingCount++;
            return false;
        }

        // 构建回收站目标路径：DataBases/Recycle/{category}/{年月日}/{原始路径}
        var dateFolder = _businessCalendar.GetCurrentDate().ToString("yyyyMMdd");
        var targetPath = Path.Combine(
            _recycleBinPath,
            category,
            dateFolder,
            targetRelativePath ?? relativePath);

        // 确保目标目录存在
        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // 如果目标文件已存在，添加时间戳后缀
        if (File.Exists(targetPath))
        {
            var businessNow = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _businessCalendar.TimeZone);
            var timestamp = businessNow.ToString("HHmmss");
            var extension = Path.GetExtension(targetPath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(targetPath);
            targetPath = Path.Combine(
                Path.GetDirectoryName(targetPath) ?? "",
                $"{fileNameWithoutExt}_{timestamp}{extension}");
        }

        // 移动文件
        await Task.Run(() => File.Move(sourceFullPath, targetPath));
        summary.MovedCount++;
        return true;
    }

    /// <summary>
    /// 清理空目录
    /// </summary>
    /// <param name="rootPath">根目录</param>
    private void CleanupEmptyDirectories(string rootPath, FileCleanupSummary summary)
    {
        try
        {
            var directories = Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.Length); // 从最深的目录开始

            foreach (var dir in directories)
            {
                if (PathContainmentTool.IsSameOrDescendant(_chunkTempPath, dir))
                {
                    continue;
                }

                try
                {
                    if (Directory.GetFiles(dir).Length == 0 &&
                        Directory.GetDirectories(dir).Length == 0)
                    {
                        Directory.Delete(dir);
                        summary.RemovedDirectoryCount++;
                    }
                }
                catch (Exception ex)
                {
                    summary.RecordDirectoryFailure(ex);
                }
            }
        }
        catch (Exception ex)
        {
            summary.RecordDirectoryFailure(ex);
        }
    }

    /// <summary>局部批次计数；不保留异常对象、路径或附件身份，也不改变原有返回计数。</summary>
    private sealed class FileCleanupSummary(string jobKind) : IDisposable
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        private string? _failureKind;
        public int ProcessedCount { get; set; }
        public int MovedCount { get; set; }
        public int MissingCount { get; set; }
        public int SkippedCount { get; set; }
        public int RemovedDirectoryCount { get; set; }
        private int FailedCount { get; set; }
        private int DirectoryFailureCount { get; set; }

        public void RecordFailure(Exception exception)
        {
            FailedCount++;
            SetFailureKind(exception);
        }

        public void RecordDirectoryFailure(Exception exception)
        {
            DirectoryFailureCount++;
            SetFailureKind(exception);
        }

        private void SetFailureKind(Exception exception)
        {
            var kind = RuntimeFailureSummary.Classify(exception);
            _failureKind = _failureKind == null || _failureKind == kind ? kind : "other";
        }

        public void Dispose()
        {
            // 只有引用保护 / 分片排除或空目录扫描而无变更时不逐轮打印。
            if (ProcessedCount + MovedCount + MissingCount + RemovedDirectoryCount + FailedCount + DirectoryFailureCount == 0) return;
            var warned = MissingCount > 0 || DirectoryFailureCount > 0;
            var changed = MovedCount > 0 || RemovedDirectoryCount > 0 || ProcessedCount > 0;
            var outcome = FailedCount > 0 ? (changed ? "partial" : "failed") : warned ? "partial" : "succeeded";
            var code = FailedCount > 0 ? "job.cleanup.failed" : warned ? "job.cleanup.warning" : "job.cleanup.completed";
            var logger = Log.ForContext("EventCode", code).ForContext("SourceCategory", "job")
                .ForContext("jobKind", jobKind).ForContext("outcome", outcome)
                .ForContext("processedCount", ProcessedCount).ForContext("movedCount", MovedCount)
                .ForContext("missingCount", MissingCount).ForContext("skippedCount", SkippedCount)
                .ForContext("removedDirectoryCount", RemovedDirectoryCount).ForContext("failedCount", FailedCount)
                .ForContext("directoryFailureCount", DirectoryFailureCount)
                .ForContext("durationMs", Stopwatch.GetElapsedTime(_started).TotalMilliseconds);
            if (_failureKind != null) logger = logger.ForContext("failureKind", _failureKind);
            // 这里已消费异常并按原契约返回；不会再交给 Hangfire 自动重试。
            if (FailedCount > 0) logger.Error("File cleanup finished with failures");
            else if (warned) logger.Warning("File cleanup finished with warnings");
            else logger.Information("File cleanup completed");
        }
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }

    #endregion
}
