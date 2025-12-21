using AutoMapper;
using Microsoft.AspNetCore.Http;
using Radish.Infrastructure.FileStorage;
using Radish.Infrastructure.ImageProcessing;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DTOs;
using Radish.Model.ViewModels;
using Serilog;

namespace Radish.Service;

/// <summary>附件服务实现</summary>
public class AttachmentService : BaseService<Attachment, AttachmentVo>, IAttachmentService
{
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IImageProcessor _imageProcessor;

    public AttachmentService(
        IMapper mapper,
        IBaseRepository<Attachment> baseRepository,
        IFileStorage fileStorage,
        IImageProcessor imageProcessor)
        : base(mapper, baseRepository)
    {
        _attachmentRepository = baseRepository;
        _fileStorage = fileStorage;
        _imageProcessor = imageProcessor;
    }

    #region Upload

    /// <summary>
    /// 上传文件
    /// </summary>
    public async Task<AttachmentVo?> UploadFileAsync(
        IFormFile file,
        FileUploadOptions options,
        long uploaderId,
        string uploaderName)
    {
        try
        {
            // 1. 基础校验
            if (file == null || file.Length == 0)
            {
                Log.Warning("上传文件为空");
                return null;
            }

            var fileName = file.FileName;
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var contentType = file.ContentType;

            Log.Information("开始上传文件：{FileName}, 大小：{FileSize} bytes", fileName, file.Length);

            // 2. 计算文件哈希（用于去重）
            string? fileHash = null;
            if (options.CalculateHash)
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = await sha256.ComputeHashAsync(memoryStream);
                fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                // 检查是否已存在相同文件（去重）
                var existingFile = await FindByHashAsync(fileHash);
                if (existingFile != null)
                {
                    Log.Information("文件已存在，返回已有记录：{FileHash}", fileHash);
                    return existingFile;
                }
            }

            // 3. 上传文件到存储
            FileUploadResult uploadResult;
            await using (var stream = file.OpenReadStream())
            {
                uploadResult = await _fileStorage.UploadAsync(
                    stream,
                    fileName,
                    contentType,
                    options
                );
            }

            if (!uploadResult.Success)
            {
                Log.Error("文件上传失败：{ErrorMessage}", uploadResult.ErrorMessage);
                return null;
            }

            // 4. 如果是图片，生成缩略图
            if (options.GenerateThumbnail && IsImageFile(extension))
            {
                await GenerateThumbnailAsync(uploadResult.StoragePath, uploadResult.ThumbnailPath);
            }

            // 5. 如果是图片，移除 EXIF
            if (options.RemoveExif && IsImageFile(extension))
            {
                await RemoveExifAsync(uploadResult.StoragePath);
            }

            // 6. 保存到数据库
            var attachment = new Attachment
            {
                OriginalName = string.IsNullOrWhiteSpace(options.OriginalFileName) ? fileName : options.OriginalFileName,
                StoredName = uploadResult.StoredName,
                Extension = extension,
                FileSize = uploadResult.FileSize,
                MimeType = contentType,
                StorageType = "Local", // 当前只支持本地存储
                StoragePath = uploadResult.StoragePath,
                ThumbnailPath = uploadResult.ThumbnailPath,
                Url = uploadResult.Url,
                FileHash = fileHash,
                UploaderId = uploaderId,
                UploaderName = uploaderName,
                BusinessType = options.BusinessType,
                IsPublic = true,
                DownloadCount = 0
            };

            var attachmentId = await AddAsync(attachment);
            attachment.Id = attachmentId;

            Log.Information("文件上传成功：{AttachmentId}, 路径：{StoragePath}", attachmentId, uploadResult.StoragePath);

            return Mapper.Map<AttachmentVo>(attachment);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "上传文件时发生异常：{FileName}", file?.FileName);
            return null;
        }
    }

    #endregion

    #region Delete

    /// <summary>
    /// 删除文件（同时删除物理文件和数据库记录）
    /// </summary>
    public async Task<bool> DeleteFileAsync(long attachmentId)
    {
        try
        {
            // 1. 查询附件信息
            var attachment = await _attachmentRepository.QueryByIdAsync(attachmentId);
            if (attachment == null)
            {
                Log.Warning("附件不存在：{AttachmentId}", attachmentId);
                return false;
            }

            // 2. 删除物理文件
            var fileDeleted = await _fileStorage.DeleteAsync(attachment.StoragePath);
            if (!fileDeleted)
            {
                Log.Warning("物理文件删除失败：{StoragePath}", attachment.StoragePath);
            }

            // 3. 删除数据库记录
            var dbDeleted = await DeleteByIdAsync(attachmentId);

            Log.Information("附件删除成功：{AttachmentId}", attachmentId);
            return dbDeleted;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除附件时发生异常：{AttachmentId}", attachmentId);
            return false;
        }
    }

    /// <summary>
    /// 批量删除文件
    /// </summary>
    public async Task<int> DeleteFilesAsync(List<long> attachmentIds)
    {
        int successCount = 0;

        foreach (var attachmentId in attachmentIds)
        {
            var deleted = await DeleteFileAsync(attachmentId);
            if (deleted)
            {
                successCount++;
            }
        }

        return successCount;
    }

    #endregion

    #region Query

    /// <summary>
    /// 根据业务类型和业务 ID 获取附件列表
    /// </summary>
    public async Task<List<AttachmentVo>> GetByBusinessAsync(string businessType, long businessId)
    {
        var attachments = await _attachmentRepository.QueryAsync(
            a => a.BusinessType == businessType && a.BusinessId == businessId && !a.IsDeleted
        );

        return Mapper.Map<List<AttachmentVo>>(attachments);
    }

    /// <summary>
    /// 根据文件哈希查找已存在的附件（去重）
    /// </summary>
    public async Task<AttachmentVo?> FindByHashAsync(string fileHash)
    {
        if (string.IsNullOrWhiteSpace(fileHash))
        {
            return null;
        }

        var attachment = await _attachmentRepository.QueryFirstAsync(
            a => a.FileHash == fileHash && !a.IsDeleted
        );

        return attachment == null ? null : Mapper.Map<AttachmentVo>(attachment);
    }

    #endregion

    #region Update

    /// <summary>
    /// 更新附件的业务关联
    /// </summary>
    public async Task<bool> UpdateBusinessAssociationAsync(long attachmentId, string businessType, long businessId)
    {
        var attachment = await _attachmentRepository.QueryByIdAsync(attachmentId);
        if (attachment == null)
        {
            return false;
        }

        attachment.BusinessType = businessType;
        attachment.BusinessId = businessId;

        return await UpdateAsync(attachment);
    }

    /// <summary>
    /// 增加下载次数
    /// </summary>
    public async Task IncrementDownloadCountAsync(long attachmentId)
    {
        try
        {
            var attachment = await _attachmentRepository.QueryByIdAsync(attachmentId);
            if (attachment != null)
            {
                attachment.DownloadCount++;
                await _attachmentRepository.UpdateAsync(attachment);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "更新下载次数失败：{AttachmentId}", attachmentId);
        }
    }

    #endregion

    #region Download

    /// <summary>
    /// 获取附件下载流
    /// </summary>
    public async Task<(Stream? stream, AttachmentVo? attachment)> GetDownloadStreamAsync(long attachmentId)
    {
        try
        {
            // 1. 查询附件信息
            var attachment = await _attachmentRepository.QueryByIdAsync(attachmentId);
            if (attachment == null || attachment.IsDeleted)
            {
                return (null, null);
            }

            // 2. 检查权限（如果不是公开文件，这里可以添加权限检查逻辑）
            if (!attachment.IsPublic)
            {
                // TODO: 权限检查
                Log.Warning("尝试下载非公开文件：{AttachmentId}", attachmentId);
                return (null, null);
            }

            // 3. 获取文件流
            var stream = await _fileStorage.DownloadAsync(attachment.StoragePath);
            if (stream == null)
            {
                Log.Warning("文件流获取失败：{StoragePath}", attachment.StoragePath);
                return (null, null);
            }

            // 4. 增加下载次数（异步，不影响下载）
            _ = Task.Run(async () => await IncrementDownloadCountAsync(attachmentId));

            var attachmentVo = Mapper.Map<AttachmentVo>(attachment);
            return (stream, attachmentVo);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取下载流时发生异常：{AttachmentId}", attachmentId);
            return (null, null);
        }
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// 判断是否为图片文件
    /// </summary>
    private bool IsImageFile(string extension)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
        return imageExtensions.Contains(extension.ToLowerInvariant());
    }

    /// <summary>
    /// 生成缩略图
    /// </summary>
    private async Task GenerateThumbnailAsync(string sourcePath, string? thumbnailPath)
    {
        if (string.IsNullOrWhiteSpace(thumbnailPath))
        {
            return;
        }

        try
        {
            var sourceFullPath = _fileStorage.GetFullPath(sourcePath);
            var thumbnailFullPath = _fileStorage.GetFullPath(thumbnailPath);

            await using var sourceStream = File.OpenRead(sourceFullPath);
            var result = await _imageProcessor.GenerateThumbnailAsync(
                sourceStream,
                thumbnailFullPath,
                150,
                150,
                85
            );

            if (!result.Success)
            {
                Log.Warning("缩略图生成失败：{ErrorMessage}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "生成缩略图时发生异常：{SourcePath}", sourcePath);
        }
    }

    /// <summary>
    /// 移除 EXIF 信息
    /// </summary>
    private async Task RemoveExifAsync(string filePath)
    {
        try
        {
            var fullPath = _fileStorage.GetFullPath(filePath);
            var tempPath = fullPath + ".tmp";

            await using (var sourceStream = File.OpenRead(fullPath))
            {
                var removed = await _imageProcessor.RemoveExifAsync(sourceStream, tempPath);
                if (removed)
                {
                    // 替换原文件
                    File.Delete(fullPath);
                    File.Move(tempPath, fullPath);
                    Log.Information("EXIF 信息已移除：{FilePath}", filePath);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "移除 EXIF 信息时发生异常：{FilePath}", filePath);
        }
    }

    #endregion
}
