using Radish.Infrastructure.FileStorage;
using Radish.IRepository;
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
    private readonly IFileStorage _fileStorage;
    private readonly string _recycleBinPath;

    public FileCleanupJob(
        IBaseRepository<Attachment> attachmentRepository,
        IFileStorage fileStorage)
    {
        _attachmentRepository = attachmentRepository;
        _fileStorage = fileStorage;

        // 回收站路径
        var rootPath = Path.IsPathRooted("DataBases/Uploads")
            ? "DataBases/Uploads"
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataBases/Uploads");
        _recycleBinPath = Path.Combine(Path.GetDirectoryName(rootPath) ?? "DataBases", "回收站");

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
        try
        {
            Log.Information("[FileCleanup] 开始清理软删除文件，保留天数：{RetentionDays}", retentionDays);

            var cutoffDate = DateTime.Now.AddDays(-retentionDays);

            // 查询需要清理的附件（软删除且超过保留期）
            // 使用 ModifyTime 判断删除时间（删除操作会更新 ModifyTime）
            var deletedAttachments = await _attachmentRepository.QueryAsync(a =>
                a.IsDeleted &&
                a.ModifyTime.HasValue &&
                a.ModifyTime.Value < cutoffDate);

            if (deletedAttachments == null || deletedAttachments.Count == 0)
            {
                Log.Information("[FileCleanup] 没有需要清理的软删除文件");
                return 0;
            }

            var cleanedCount = 0;

            foreach (var attachment in deletedAttachments)
            {
                try
                {
                    // 移动文件到回收站（而不是直接删除）
                    await MoveToRecycleBinAsync(attachment.StoragePath, "deleted");

                    // 如果有缩略图，也移动到回收站
                    if (!string.IsNullOrWhiteSpace(attachment.ThumbnailPath))
                    {
                        await MoveToRecycleBinAsync(attachment.ThumbnailPath, "deleted");
                    }

                    cleanedCount++;
                    Log.Information("[FileCleanup] 已将文件移至回收站：{FilePath}", attachment.StoragePath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[FileCleanup] 移动文件到回收站失败：{FilePath}", attachment.StoragePath);
                }
            }

            Log.Information("[FileCleanup] 软删除文件清理完成，共处理 {Count} 个文件", cleanedCount);
            return cleanedCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[FileCleanup] 清理软删除文件时发生异常");
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
        try
        {
            Log.Information("[FileCleanup] 开始清理临时文件，保留小时数：{RetentionHours}", retentionHours);

            // 使用统一的 DataBases/Temp 目录
            var tempPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataBases", "Temp");

            if (!Directory.Exists(tempPath))
            {
                Log.Information("[FileCleanup] 临时文件目录不存在：{TempPath}", tempPath);
                return 0;
            }

            var cutoffTime = DateTime.Now.AddHours(-retentionHours);
            var cleanedCount = 0;

            // 获取所有临时文件
            var tempFiles = Directory.GetFiles(tempPath, "*", SearchOption.AllDirectories);

            foreach (var filePath in tempFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);

                    // 检查文件最后修改时间
                    if (fileInfo.LastWriteTime < cutoffTime)
                    {
                        // 直接删除临时文件（不需要移动到回收站）
                        File.Delete(filePath);
                        cleanedCount++;
                        Log.Information("[FileCleanup] 已删除临时文件：{FilePath}", filePath);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[FileCleanup] 删除临时文件失败：{FilePath}", filePath);
                }
            }

            // 清理空目录
            CleanupEmptyDirectories(tempPath);

            Log.Information("[FileCleanup] 临时文件清理完成，共处理 {Count} 个文件", cleanedCount);
            return cleanedCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[FileCleanup] 清理临时文件时发生异常");
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
        try
        {
            Log.Information("[FileCleanup] 开始清理回收站，保留天数：{RetentionDays}", retentionDays);

            if (!Directory.Exists(_recycleBinPath))
            {
                Log.Information("[FileCleanup] 回收站目录不存在");
                return 0;
            }

            var cutoffTime = DateTime.Now.AddDays(-retentionDays);
            var cleanedCount = 0;

            // 获取回收站中的所有文件
            var files = Directory.GetFiles(_recycleBinPath, "*", SearchOption.AllDirectories);

            foreach (var filePath in files)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);

                    // 检查文件创建时间（移入回收站的时间）
                    if (fileInfo.CreationTime < cutoffTime)
                    {
                        // 永久删除文件
                        File.Delete(filePath);
                        cleanedCount++;
                        Log.Information("[FileCleanup] 已永久删除回收站文件：{FilePath}", filePath);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[FileCleanup] 删除回收站文件失败：{FilePath}", filePath);
                }
            }

            // 清理空目录
            CleanupEmptyDirectories(_recycleBinPath);

            Log.Information("[FileCleanup] 回收站清理完成，共删除 {Count} 个文件", cleanedCount);
            return cleanedCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[FileCleanup] 清理回收站时发生异常");
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
        try
        {
            Log.Information("[FileCleanup] 开始清理孤立附件，保留小时数：{RetentionHours}", retentionHours);

            var cutoffTime = DateTime.Now.AddHours(-retentionHours);

            // 查询孤立附件（未关联业务对象且超过保留期）
            var orphanAttachments = await _attachmentRepository.QueryAsync(a =>
                !a.IsDeleted &&
                !a.BusinessId.HasValue &&
                a.CreateTime < cutoffTime);

            if (orphanAttachments == null || orphanAttachments.Count == 0)
            {
                Log.Information("[FileCleanup] 没有需要清理的孤立附件");
                return 0;
            }

            var cleanedCount = 0;

            foreach (var attachment in orphanAttachments)
            {
                try
                {
                    // 移动文件到回收站
                    await MoveToRecycleBinAsync(attachment.StoragePath, "orphan");

                    // 如果有缩略图，也移动到回收站
                    if (!string.IsNullOrWhiteSpace(attachment.ThumbnailPath))
                    {
                        await MoveToRecycleBinAsync(attachment.ThumbnailPath, "orphan");
                    }

                    // 标记为已删除（保留数据库记录）
                    attachment.IsDeleted = true;
                    attachment.ModifyTime = DateTime.Now;
                    await _attachmentRepository.UpdateAsync(attachment);

                    cleanedCount++;
                    Log.Information("[FileCleanup] 已将孤立附件移至回收站：{FilePath}", attachment.StoragePath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[FileCleanup] 移动孤立附件到回收站失败：{FilePath}", attachment.StoragePath);
                }
            }

            Log.Information("[FileCleanup] 孤立附件清理完成，共处理 {Count} 个文件", cleanedCount);
            return cleanedCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[FileCleanup] 清理孤立附件时发生异常");
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
    private async Task MoveToRecycleBinAsync(string relativePath, string category)
    {
        var sourceFullPath = _fileStorage.GetFullPath(relativePath);

        if (!File.Exists(sourceFullPath))
        {
            Log.Warning("[FileCleanup] 文件不存在，跳过：{FilePath}", sourceFullPath);
            return;
        }

        // 构建回收站目标路径：回收站/{category}/{年月日}/{原始路径}
        var dateFolder = DateTime.Now.ToString("yyyyMMdd");
        var targetPath = Path.Combine(_recycleBinPath, category, dateFolder, relativePath);

        // 确保目标目录存在
        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // 如果目标文件已存在，添加时间戳后缀
        if (File.Exists(targetPath))
        {
            var timestamp = DateTime.Now.ToString("HHmmss");
            var extension = Path.GetExtension(targetPath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(targetPath);
            targetPath = Path.Combine(
                Path.GetDirectoryName(targetPath) ?? "",
                $"{fileNameWithoutExt}_{timestamp}{extension}");
        }

        // 移动文件
        await Task.Run(() => File.Move(sourceFullPath, targetPath));
    }

    /// <summary>
    /// 清理空目录
    /// </summary>
    /// <param name="rootPath">根目录</param>
    private void CleanupEmptyDirectories(string rootPath)
    {
        try
        {
            var directories = Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.Length); // 从最深的目录开始

            foreach (var dir in directories)
            {
                try
                {
                    if (Directory.GetFiles(dir).Length == 0 &&
                        Directory.GetDirectories(dir).Length == 0)
                    {
                        Directory.Delete(dir);
                        Log.Debug("[FileCleanup] 已删除空目录：{Directory}", dir);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[FileCleanup] 删除空目录失败：{Directory}", dir);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[FileCleanup] 清理空目录时发生异常");
        }
    }

    #endregion
}
