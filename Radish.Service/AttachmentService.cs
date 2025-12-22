using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.Infrastructure.FileStorage;
using Radish.Infrastructure.ImageProcessing;
using InfraWatermarkOptions = Radish.Infrastructure.ImageProcessing.WatermarkOptions;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Serilog;

namespace Radish.Service;

/// <summary>附件服务实现</summary>
public class AttachmentService : BaseService<Attachment, AttachmentVo>, IAttachmentService
{
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IImageProcessor _imageProcessor;
    private readonly FileStorageOptions _fileStorageOptions;

    public AttachmentService(
        IMapper mapper,
        IBaseRepository<Attachment> baseRepository,
        IFileStorage fileStorage,
        IImageProcessor imageProcessor,
        IOptions<FileStorageOptions> fileStorageOptions)
        : base(mapper, baseRepository)
    {
        _attachmentRepository = baseRepository;
        _fileStorage = fileStorage;
        _imageProcessor = imageProcessor;
        _fileStorageOptions = fileStorageOptions.Value;
    }

    #region Upload

    /// <summary>
    /// 上传文件
    /// </summary>
    public async Task<AttachmentVo?> UploadFileAsync(
        IFormFile file,
        FileUploadOptionsDto optionsDto,
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
            if (optionsDto.CalculateHash)
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = await sha256.ComputeHashAsync(memoryStream);
                fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                // 只有在没有特殊处理选项时才启用去重
                // 如果用户指定了水印、多尺寸等处理，则不去重，允许上传新文件
                var hasSpecialProcessing = optionsDto.AddWatermark || optionsDto.GenerateMultipleSizes;

                if (!hasSpecialProcessing)
                {
                    // 检查是否已存在相同文件（去重）
                    var existingAttachment = await _attachmentRepository.QueryFirstAsync(
                        a => a.FileHash == fileHash && !a.IsDeleted
                    );

                    if (existingAttachment != null)
                    {
                        // 验证物理文件是否存在
                        var physicalFileExists = await _fileStorage.ExistsAsync(existingAttachment.StoragePath);
                        if (physicalFileExists)
                        {
                            Log.Information("文件已存在，返回已有记录：{FileHash}", fileHash);
                            return Mapper.Map<AttachmentVo>(existingAttachment);
                        }
                        else
                        {
                            // 物理文件不存在，删除旧记录并继续上传
                            Log.Warning("文件记录存在但物理文件缺失，删除旧记录：{AttachmentId}, {FileHash}", existingAttachment.Id, fileHash);
                            await _attachmentRepository.DeleteByIdAsync(existingAttachment.Id);
                        }
                    }
                }
                else
                {
                    Log.Information("用户指定了特殊处理选项（水印/多尺寸），跳过去重检查");
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
                    optionsDto
                );
            }

            if (!uploadResult.Success)
            {
                Log.Error("文件上传失败：{ErrorMessage}", uploadResult.ErrorMessage);
                return null;
            }

            // 4. 如果是图片，添加水印（在其他处理之前）
            if (optionsDto.AddWatermark && IsImageFile(extension))
            {
                // 检查配置文件中的水印开关
                if (_fileStorageOptions.Watermark?.Enable == true)
                {
                    await AddWatermarkAsync(uploadResult.StoragePath, optionsDto.WatermarkText);
                }
                else
                {
                    Log.Information("水印功能未在配置文件中启用，跳过水印处理");
                }
            }

            // 4.5. 如果是图片，生成缩略图
            if (optionsDto.GenerateThumbnail && IsImageFile(extension))
            {
                await GenerateThumbnailAsync(uploadResult.StoragePath, uploadResult.ThumbnailPath);
            }

            // 4.6. 如果是图片，生成多尺寸
            Dictionary<string, string>? multipleSizes = null;
            if (optionsDto.GenerateMultipleSizes && IsImageFile(extension))
            {
                multipleSizes = await GenerateMultipleSizesAsync(uploadResult.StoragePath);
            }

            // 5. 如果是图片，移除 EXIF
            if (optionsDto.RemoveExif && IsImageFile(extension))
            {
                await RemoveExifAsync(uploadResult.StoragePath);
            }

            // 6. 保存到数据库
            var attachment = new Attachment
            {
                OriginalName = string.IsNullOrWhiteSpace(optionsDto.OriginalFileName) ? fileName : optionsDto.OriginalFileName,
                StoredName = uploadResult.StoredName,
                Extension = extension,
                FileSize = uploadResult.FileSize,
                MimeType = contentType,
                StorageType = "Local", // 当前只支持本地存储
                StoragePath = uploadResult.StoragePath,
                ThumbnailPath = uploadResult.ThumbnailPath,
                SmallPath = multipleSizes?.GetValueOrDefault("small"),
                MediumPath = multipleSizes?.GetValueOrDefault("medium"),
                LargePath = multipleSizes?.GetValueOrDefault("large"),
                Url = uploadResult.Url,
                FileHash = fileHash,
                UploaderId = uploaderId,
                UploaderName = uploaderName,
                BusinessType = optionsDto.BusinessType,
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
    /// 添加水印
    /// </summary>
    private async Task AddWatermarkAsync(string filePath, string? watermarkText)
    {
        if (string.IsNullOrWhiteSpace(watermarkText))
        {
            watermarkText = "Radish";
        }

        try
        {
            var fullPath = _fileStorage.GetFullPath(filePath);

            // 使用 DataBases/Temp 目录存放临时文件
            var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataBases", "Temp");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            var tempFileName = $"{Path.GetFileNameWithoutExtension(fullPath)}_{Guid.NewGuid():N}{Path.GetExtension(fullPath)}";
            var tempPath = Path.Combine(tempDir, tempFileName);

            // 使用配置文件中的水印设置
            var watermarkConfig = _fileStorageOptions.Watermark?.Text ?? new TextWatermarkOptions();

            // 解析水印位置
            var position = watermarkConfig.Position?.ToLowerInvariant() switch
            {
                "topleft" => WatermarkPosition.TopLeft,
                "topright" => WatermarkPosition.TopRight,
                "bottomleft" => WatermarkPosition.BottomLeft,
                "bottomright" => WatermarkPosition.BottomRight,
                "center" => WatermarkPosition.Center,
                _ => WatermarkPosition.BottomRight
            };

            var watermarkOptions = new InfraWatermarkOptions
            {
                Type = _fileStorageOptions.Watermark?.Type?.ToLowerInvariant() == "image"
                    ? WatermarkType.Image
                    : WatermarkType.Text,
                Text = watermarkText,
                FontSize = watermarkConfig.FontSize,
                Opacity = (float)watermarkConfig.Opacity,
                Position = position,
                Color = watermarkConfig.Color ?? "#FFFFFF",
                Padding = 10
            };

            // 修复文件锁问题：先读取源文件到内存，关闭文件流后再处理
            byte[] fileBytes;
            await using (var sourceStream = File.OpenRead(fullPath))
            {
                fileBytes = new byte[sourceStream.Length];
                await sourceStream.ReadAsync(fileBytes, 0, fileBytes.Length);
            }

            // 使用内存流处理图片
            await using (var memoryStream = new MemoryStream(fileBytes))
            {
                var result = await _imageProcessor.AddWatermarkAsync(memoryStream, tempPath, watermarkOptions);
                if (result.Success)
                {
                    // 替换原文件（添加重试机制）
                    ReplaceFileWithRetry(tempPath, fullPath, 3);
                    Log.Information("水印已添加：{FilePath}, 文本：{WatermarkText}", filePath, watermarkText);
                }
                else
                {
                    Log.Warning("水印添加失败：{ErrorMessage}", result.ErrorMessage);
                    // 清理临时文件
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "添加水印时发生异常：{FilePath}", filePath);
        }
    }

    /// <summary>
    /// 带重试机制的文件替换
    /// </summary>
    private void ReplaceFileWithRetry(string sourcePath, string targetPath, int maxRetries)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                // 如果目标文件存在，先删除
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                // 移动文件
                File.Move(sourcePath, targetPath);
                return; // 成功，退出重试循环
            }
            catch (IOException ex) when (i < maxRetries - 1)
            {
                Log.Warning("文件替换失败，正在重试 ({0}/{1}): {2}", i + 1, maxRetries, ex.Message);
                Thread.Sleep(100); // 等待 100ms 后重试
            }
        }

        // 最后一次重试仍然失败，抛出异常
        throw new IOException($"无法替换文件：{targetPath}");
    }

    /// <summary>
    /// 移除 EXIF 信息
    /// </summary>
    private async Task RemoveExifAsync(string filePath)
    {
        try
        {
            var fullPath = _fileStorage.GetFullPath(filePath);

            // 使用 DataBases/Temp 目录存放临时文件
            var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataBases", "Temp");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            var tempFileName = $"{Path.GetFileNameWithoutExtension(fullPath)}_{Guid.NewGuid():N}{Path.GetExtension(fullPath)}";
            var tempPath = Path.Combine(tempDir, tempFileName);

            // 修复文件锁问题：先读取源文件到内存，关闭文件流后再处理
            byte[] fileBytes;
            await using (var sourceStream = File.OpenRead(fullPath))
            {
                fileBytes = new byte[sourceStream.Length];
                await sourceStream.ReadAsync(fileBytes, 0, fileBytes.Length);
            }

            // 使用内存流处理图片
            await using (var memoryStream = new MemoryStream(fileBytes))
            {
                var removed = await _imageProcessor.RemoveExifAsync(memoryStream, tempPath);
                if (removed)
                {
                    // 替换原文件（添加重试机制）
                    ReplaceFileWithRetry(tempPath, fullPath, 3);
                    Log.Information("EXIF 信息已移除：{FilePath}", filePath);
                }
                else
                {
                    // 清理临时文件
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "移除 EXIF 信息时发生异常：{FilePath}", filePath);
        }
    }

    /// <summary>
    /// 生成多尺寸图片
    /// </summary>
    private async Task<Dictionary<string, string>> GenerateMultipleSizesAsync(string sourcePath)
    {
        var result = new Dictionary<string, string>();

        try
        {
            var sourceFullPath = _fileStorage.GetFullPath(sourcePath);
            var directory = Path.GetDirectoryName(sourceFullPath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFullPath);
            var extension = Path.GetExtension(sourceFullPath);

            // 定义尺寸配置
            var sizes = new List<ImageSize>
            {
                new() { Name = "small", Width = 400, Height = 300, KeepAspectRatio = true, Quality = 85 },
                new() { Name = "medium", Width = 800, Height = 600, KeepAspectRatio = true, Quality = 85 },
                new() { Name = "large", Width = 1200, Height = 900, KeepAspectRatio = true, Quality = 85 }
            };

            await using var sourceStream = File.OpenRead(sourceFullPath);
            // 注意：baseOutputPath 应该包含扩展名，这样 ImageProcessor 才能正确识别格式
            var baseOutputPath = Path.Combine(directory ?? "", fileNameWithoutExt + extension);

            var results = await _imageProcessor.GenerateMultipleSizesAsync(
                sourceStream,
                baseOutputPath,
                sizes
            );

            // 转换为相对路径
            var sourceRelativePath = sourcePath;
            foreach (var sizeResult in results.Where(r => r.Success))
            {
                var sizeName = Path.GetFileNameWithoutExtension(sizeResult.OutputPath)
                    .Replace(fileNameWithoutExt + "_", "");

                // 计算相对路径（相对于存储根目录）
                var fullPath = sizeResult.OutputPath;
                var srcFullPath = _fileStorage.GetFullPath(sourceRelativePath);
                var srcDirectory = Path.GetDirectoryName(srcFullPath);
                var relativePath = Path.GetRelativePath(srcDirectory ?? "", fullPath);

                // 组合为完整的相对路径
                var finalRelativePath = Path.Combine(Path.GetDirectoryName(sourceRelativePath) ?? "", relativePath)
                    .Replace("\\", "/");

                result[sizeName] = finalRelativePath;

                Log.Information("生成 {SizeName} 尺寸成功：{Path}", sizeName, finalRelativePath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "生成多尺寸图片时发生异常：{SourcePath}", sourcePath);
        }

        return result;
    }

    #endregion
}
