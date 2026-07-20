using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Radish.Common.AttributeTool;
using Radish.Common.CoreTool;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.Infrastructure.FileStorage;
using Radish.Infrastructure.ImageProcessing;
using InfraWatermarkOptions = Radish.Infrastructure.ImageProcessing.WatermarkOptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using Radish.Shared.Constants;
using Serilog;

namespace Radish.Service;

/// <summary>附件服务实现</summary>
public class AttachmentService : BaseService<Attachment, AttachmentVo>, IAttachmentService
{
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IImageProcessor _imageProcessor;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly IChatChannelAccessService _chatChannelAccessService;
    private readonly FileStorageOptions _fileStorageOptions;
    private readonly string _tempPath;

    public AttachmentService(
        IMapper mapper,
        IBaseRepository<Attachment> baseRepository,
        IFileStorage fileStorage,
        IImageProcessor imageProcessor,
        IAttachmentUrlResolver attachmentUrlResolver,
        IChatChannelAccessService chatChannelAccessService,
        IOptions<FileStorageOptions> fileStorageOptions)
        : base(mapper, baseRepository)
    {
        _attachmentRepository = baseRepository;
        _fileStorage = fileStorage;
        _imageProcessor = imageProcessor;
        _attachmentUrlResolver = attachmentUrlResolver;
        _chatChannelAccessService = chatChannelAccessService;
        _fileStorageOptions = fileStorageOptions.Value;
        _tempPath = Path.Combine(AppPathTool.GetDataBasesPath(), "Temp");

        if (!Directory.Exists(_tempPath))
        {
            Directory.CreateDirectory(_tempPath);
        }
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
        FileUploadResult? successfulUpload = null;
        Dictionary<string, string>? generatedSizes = null;
        var generatedPaths = new HashSet<string>(StringComparer.Ordinal);
        var attachmentPersisted = false;

        try
        {
            var normalizedTenantId = NormalizeTenantId(App.CurrentUser.TenantId);

            if (!AttachmentBusinessTypes.TryNormalize(optionsDto.BusinessType, out var normalizedBusinessType))
            {
                throw new BusinessException(
                    "不支持该附件业务类型",
                    StatusCodes.Status400BadRequest,
                    AttachmentErrorCodes.BusinessTypeUnsupported,
                    AttachmentErrorCodes.ResolveMessageKey(AttachmentErrorCodes.BusinessTypeUnsupported));
            }

            optionsDto.BusinessType = normalizedBusinessType;

            // 1. 基础校验
            if (file == null || file.Length == 0)
            {
                Log.Warning("上传文件为空");
                throw new BusinessException(
                    "文件不能为空",
                    StatusCodes.Status400BadRequest,
                    AttachmentErrorCodes.FileEmpty,
                    AttachmentErrorCodes.ResolveMessageKey(AttachmentErrorCodes.FileEmpty));
            }

            var fileName = file.FileName;
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (!FileStoragePolicy.IsAllowedForBusinessType(
                    _fileStorageOptions,
                    extension,
                    AttachmentBusinessTypes.RequiresImage(normalizedBusinessType)))
            {
                throw new BusinessException(
                    "该附件业务类型不支持此文件格式",
                    StatusCodes.Status415UnsupportedMediaType,
                    AttachmentErrorCodes.UnsupportedMediaType,
                    AttachmentErrorCodes.ResolveMessageKey(AttachmentErrorCodes.UnsupportedMediaType));
            }

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

                // 当前附件记录没有持久化处理指纹。任何会改变原图或派生文件的处理都必须跳过去重，
                // 避免返回缺少本次缩略图、尺寸、水印或 EXIF 清理语义的历史产物。
                var hasSpecialProcessing = optionsDto.AddWatermark
                                           || optionsDto.GenerateThumbnail
                                           || optionsDto.GenerateMultipleSizes
                                           || optionsDto.RemoveExif;

                if (_fileStorageOptions.Deduplication.Enable && !hasSpecialProcessing)
                {
                    // 附件记录同时承载上传者与业务归属，只有完整归属一致时才可复用。
                    // 已绑定到具体业务的数据不能作为下一次上传草稿返回，否则后续关联会篡改旧业务归属。
                    var existingAttachment = await _attachmentRepository.QueryFirstAsync(
                        a => a.FileHash == fileHash
                             && a.TenantId == normalizedTenantId
                             && a.UploaderId == uploaderId
                             && a.BusinessType == normalizedBusinessType
                             && a.BusinessId == null
                             && a.IsEnabled
                             && !a.IsDeleted
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
                            // 物理文件不存在，软删除旧记录并继续上传
                            Log.Warning("文件记录存在但物理文件缺失，软删除旧记录：{AttachmentId}, {FileHash}", existingAttachment.Id, fileHash);
                            await _attachmentRepository.SoftDeleteByIdAsync(existingAttachment.Id, "System");
                        }
                    }
                }
                else
                {
                    Log.Information(
                        _fileStorageOptions.Deduplication.Enable
                            ? "当前上传包含图片处理语义，跳过去重检查"
                            : "文件去重已禁用，跳过去重检查");
                }
            }

            // 3. 上传文件到存储
            FileUploadResult uploadResult;
            await using (var stream = file.OpenReadStream())
            {
                uploadResult = await _fileStorage.UploadAsync(
                    stream,
                    fileName,
                    optionsDto
                );
            }

            if (!uploadResult.Success)
            {
                Log.Error("文件上传失败：{FailureKind}, {ErrorMessage}", uploadResult.FailureKind, uploadResult.ErrorMessage);
                throw CreateUploadFailureException(uploadResult);
            }

            successfulUpload = uploadResult;

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
            if (optionsDto.GenerateMultipleSizes && IsImageFile(extension))
            {
                generatedSizes = await GenerateMultipleSizesAsync(uploadResult.StoragePath, generatedPaths);
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
                MimeType = uploadResult.ContentType,
                StorageType = "Local", // 当前只支持本地存储
                StoragePath = uploadResult.StoragePath,
                ThumbnailPath = uploadResult.ThumbnailPath,
                SmallPath = generatedSizes?.GetValueOrDefault("small"),
                MediumPath = generatedSizes?.GetValueOrDefault("medium"),
                LargePath = generatedSizes?.GetValueOrDefault("large"),
                FileHash = fileHash,
                UploaderId = uploaderId,
                UploaderName = uploaderName,
                BusinessType = optionsDto.BusinessType,
                // 上传只创建未绑定附件；业务归属必须由对应域服务在目标对象校验后设置。
                BusinessId = null,
                TenantId = normalizedTenantId,
                IsPublic = normalizedBusinessType != AttachmentBusinessTypes.Chat,
                DownloadCount = 0
            };

            var attachmentId = await AddAsync(attachment);
            if (attachmentId <= 0)
            {
                throw CreateProcessingFailedException();
            }

            attachment.Id = attachmentId;
            attachmentPersisted = true;

            Log.Information("文件上传成功：{AttachmentId}, 路径：{StoragePath}", attachmentId, uploadResult.StoragePath);

            return Mapper.Map<AttachmentVo>(attachment);
        }
        catch (BusinessException)
        {
            if (!attachmentPersisted)
            {
                await CleanupFailedUploadAsync(successfulUpload, generatedPaths);
            }

            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "上传文件时发生异常：{FileName}", file?.FileName);
            if (!attachmentPersisted)
            {
                await CleanupFailedUploadAsync(successfulUpload, generatedPaths);
            }

            throw;
        }
    }

    #endregion

    #region Delete

    /// <summary>
    /// 删除文件（软删除：标记为已删除，物理文件通过定时任务清理）
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

            // 2. 软删除：标记为已删除
            var dbDeleted = await UpdateColumnsAsync(
                a => new Attachment
                {
                    IsDeleted = true,
                    ModifyTime = DateTime.Now,
                    ModifyBy = string.IsNullOrWhiteSpace(App.CurrentUser.UserName) ? "System" : App.CurrentUser.UserName,
                    ModifyId = App.CurrentUser.UserId
                },
                a => a.Id == attachmentId);

            if (dbDeleted > 0)
            {
                Log.Information("附件软删除成功：{AttachmentId}，物理文件将通过定时任务清理", attachmentId);
                return true;
            }
            else
            {
                Log.Warning("附件软删除失败：{AttachmentId}", attachmentId);
                return false;
            }
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
        var normalizedTenantId = NormalizeTenantId(App.CurrentUser.TenantId);

        var attachments = await _attachmentRepository.QueryAsync(
            a => a.BusinessType == businessType
                 && a.BusinessId == businessId
                 && a.TenantId == normalizedTenantId
                 && !a.IsDeleted
        );

        return Mapper.Map<List<AttachmentVo>>(attachments);
    }

    public async Task<AttachmentVo?> GetAccessibleByIdAsync(
        long attachmentId,
        long tenantId,
        long? requestUserId = null,
        List<string>? requestUserRoles = null)
    {
        if (attachmentId <= 0)
        {
            return null;
        }

        var attachment = await _attachmentRepository.QueryFirstAsync(candidate =>
            candidate.Id == attachmentId && candidate.IsEnabled && !candidate.IsDeleted);
        if (attachment == null ||
            !await CanReadAttachmentAsync(attachment, tenantId, requestUserId, requestUserRoles))
        {
            return null;
        }

        return Mapper.Map<AttachmentVo>(attachment);
    }

    public async Task<List<AttachmentVo>> GetAccessibleByBusinessAsync(
        string businessType,
        long businessId,
        long tenantId,
        long? requestUserId = null,
        List<string>? requestUserRoles = null)
    {
        var normalizedTenantId = NormalizeTenantId(tenantId);
        var attachments = await _attachmentRepository.QueryAsync(candidate =>
            candidate.BusinessType == businessType &&
            candidate.BusinessId == businessId &&
            candidate.TenantId == normalizedTenantId &&
            candidate.IsEnabled &&
            !candidate.IsDeleted);
        var accessibleAttachments = new List<Attachment>(attachments.Count);
        foreach (var attachment in attachments)
        {
            if (await CanReadAttachmentAsync(attachment, tenantId, requestUserId, requestUserRoles))
            {
                accessibleAttachments.Add(attachment);
            }
        }

        return Mapper.Map<List<AttachmentVo>>(accessibleAttachments);
    }

    public async Task<AttachmentAssetDto?> GetAttachmentAssetAsync(long attachmentId)
    {
        if (attachmentId <= 0)
        {
            return null;
        }

        var attachment = await _attachmentRepository.QueryFirstAsync(a => a.Id == attachmentId && !a.IsDeleted);
        return attachment == null ? null : MapToAssetDto(attachment);
    }

    public async Task<AttachmentAssetDto?> GetLatestAvatarAssetAsync(long userId)
    {
        if (userId <= 0)
        {
            return null;
        }

        var avatarMap = await GetLatestAvatarAssetMapAsync(new[] { userId });
        return avatarMap.GetValueOrDefault(userId);
    }

    public async Task<Dictionary<long, AttachmentAssetDto>> GetLatestAvatarAssetMapAsync(IReadOnlyCollection<long> userIds)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<long, AttachmentAssetDto>();
        }

        var normalizedUserIds = userIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (normalizedUserIds.Count == 0)
        {
            return new Dictionary<long, AttachmentAssetDto>();
        }

        var attachments = await _attachmentRepository.QueryAsync(attachment =>
            attachment.BusinessType == "Avatar" &&
            attachment.BusinessId.HasValue &&
            normalizedUserIds.Contains(attachment.BusinessId.Value) &&
            attachment.IsEnabled &&
            !attachment.IsDeleted);

        return attachments
            .Where(attachment =>
                attachment.BusinessId.HasValue &&
                attachment.UploaderId == attachment.BusinessId.Value &&
                FileStoragePolicy.IsImageExtension(_fileStorageOptions, attachment.Extension) &&
                !string.IsNullOrWhiteSpace(attachment.MimeType) &&
                attachment.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(attachment => attachment.CreateTime)
            .ThenByDescending(attachment => attachment.Id)
            .GroupBy(attachment => attachment.BusinessId!.Value)
            .ToDictionary(
                group => group.Key,
                group => MapToAssetDto(group.First()));
    }

    #endregion

    private static long NormalizeTenantId(long tenantId)
    {
        return tenantId > 0 ? tenantId : 0;
    }

    #region Update

    /// <summary>
    /// 设置或清空当前用户头像。
    /// </summary>
    [UseTran]
    public async Task SetCurrentAvatarAsync(long attachmentId, long userId, string modifierName)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "用户标识必须有效。");
        }

        if (attachmentId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attachmentId), "附件标识不能为负数。");
        }

        var now = DateTime.UtcNow;
        var normalizedModifierName = string.IsNullOrWhiteSpace(modifierName) ? "System" : modifierName.Trim();
        var tenantId = NormalizeTenantId(App.CurrentUser.TenantId);
        if (attachmentId == 0)
        {
            await UpdateColumnsAsync(
                attachment => new Attachment
                {
                    BusinessId = null,
                    ModifyTime = now,
                    ModifyBy = normalizedModifierName,
                    ModifyId = userId
                },
                attachment => attachment.TenantId == tenantId &&
                              attachment.BusinessType == AttachmentBusinessTypes.Avatar &&
                              attachment.BusinessId == userId &&
                              !attachment.IsDeleted);
            return;
        }

        var attachment = await _attachmentRepository.QueryByIdAsync(attachmentId);
        if (attachment == null || attachment.TenantId != tenantId)
        {
            throw new BusinessException(
                "附件不存在",
                StatusCodes.Status404NotFound,
                AttachmentErrorCodes.AttachmentNotFound,
                AttachmentErrorCodes.ResolveMessageKey(AttachmentErrorCodes.AttachmentNotFound));
        }

        if (attachment.UploaderId != userId)
        {
            throw new BusinessException(
                "无权设置该附件为头像",
                StatusCodes.Status403Forbidden,
                AttachmentErrorCodes.UploadForbidden,
                AttachmentErrorCodes.ResolveMessageKey(AttachmentErrorCodes.UploadForbidden));
        }

        if (!attachment.IsEnabled ||
            attachment.IsDeleted ||
            attachment.BusinessType != AttachmentBusinessTypes.Avatar ||
            !FileStoragePolicy.IsImageExtension(_fileStorageOptions, attachment.Extension) ||
            string.IsNullOrWhiteSpace(attachment.MimeType) ||
            !attachment.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(
                "该附件不能设置为头像",
                StatusCodes.Status415UnsupportedMediaType,
                AttachmentErrorCodes.ImageTypeUnsupported,
                AttachmentErrorCodes.ResolveMessageKey(AttachmentErrorCodes.ImageTypeUnsupported));
        }

        await UpdateColumnsAsync(
            candidate => new Attachment
            {
                BusinessId = null,
                ModifyTime = now,
                ModifyBy = normalizedModifierName,
                ModifyId = userId
            },
            candidate => candidate.TenantId == tenantId &&
                         candidate.BusinessType == AttachmentBusinessTypes.Avatar &&
                         candidate.BusinessId == userId &&
                         candidate.Id != attachmentId &&
                         !candidate.IsDeleted);

        attachment.BusinessId = userId;
        attachment.ModifyTime = now;
        attachment.ModifyBy = normalizedModifierName;
        attachment.ModifyId = userId;

        if (!await _attachmentRepository.UpdateAsync(attachment))
        {
            throw CreateProcessingFailedException();
        }
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
    public async Task<(Stream? stream, AttachmentAssetDto? attachment)> GetDownloadStreamAsync(
        long attachmentId,
        long? requestUserId = null,
        List<string>? requestUserRoles = null,
        AttachmentUrlVariant variant = AttachmentUrlVariant.Original,
        long? requestTenantId = null)
    {
        try
        {
            // 1. 查询附件信息
            var attachment = await _attachmentRepository.QueryByIdAsync(attachmentId);
            if (attachment == null)
            {
                return (null, null);
            }

            if (attachment.IsDeleted || !attachment.IsEnabled)
            {
                return (null, null);
            }

            // 2. Chat 资源始终按频道权限判定；其他非公开资源保留上传者与管理角色规则。
            var tenantId = requestTenantId ?? App.CurrentUser.TenantId;
            if (!await CanReadAttachmentAsync(attachment, tenantId, requestUserId, requestUserRoles))
            {
                Log.Warning("用户 {UserId} 尝试访问无权限附件：{AttachmentId}（业务类型：{BusinessType}）", requestUserId, attachmentId, attachment.BusinessType);
                return (null, null);
            }

            // 3. 获取文件流
            var filePath = ResolveAttachmentPath(attachment, variant);
            var stream = await _fileStorage.DownloadAsync(filePath);
            if (stream == null)
            {
                Log.Warning("文件流获取失败：{StoragePath}", filePath);
                return (null, null);
            }

            // 4. 增加下载次数。该服务为 scoped，不能在请求外的 Task.Run 中继续复用。
            await IncrementDownloadCountAsync(attachmentId);

            return (stream, MapToAssetDto(attachment));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取下载流时发生异常：{AttachmentId}", attachmentId);
            return (null, null);
        }
    }

    private async Task<bool> CanReadAttachmentAsync(
        Attachment attachment,
        long tenantId,
        long? requestUserId,
        List<string>? requestUserRoles)
    {
        var isUploader = requestUserId.HasValue && attachment.UploaderId == requestUserId.Value;
        if (attachment.BusinessType == AttachmentBusinessTypes.Chat)
        {
            if (!attachment.BusinessId.HasValue)
            {
                return isUploader ||
                       requestUserId.HasValue &&
                       await _chatChannelAccessService.CanAccessChatAttachmentAsync(
                           NormalizeTenantId(tenantId),
                           requestUserId.Value,
                           attachment.Id);
            }

            return requestUserId.HasValue &&
                   await _chatChannelAccessService.CanAccessChatAttachmentAsync(
                       NormalizeTenantId(tenantId),
                       requestUserId.Value,
                       attachment.Id,
                       attachment.BusinessId.Value);
        }

        return attachment.IsPublic || isUploader || UserRoleHelper.IsSystemOrAdmin(requestUserRoles);
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

    private static string ResolveAttachmentPath(Attachment attachment, AttachmentUrlVariant variant)
    {
        if (variant == AttachmentUrlVariant.Thumbnail &&
            !string.IsNullOrWhiteSpace(attachment.ThumbnailPath))
        {
            return attachment.ThumbnailPath;
        }

        return attachment.StoragePath;
    }

    private AttachmentAssetDto MapToAssetDto(Attachment attachment)
    {
        return new AttachmentAssetDto
        {
            AttachmentId = attachment.Id,
            OriginalName = attachment.OriginalName,
            MimeType = attachment.MimeType,
            UploaderId = attachment.UploaderId,
            BusinessType = attachment.BusinessType,
            BusinessId = attachment.BusinessId,
            Url = _attachmentUrlResolver.ResolveAttachmentUrl(attachment.Id),
            ThumbnailUrl = _attachmentUrlResolver.ResolveAttachmentUrl(attachment.Id, AttachmentUrlVariant.Thumbnail),
            CreateTime = attachment.CreateTime
        };
    }

    /// <summary>
    /// 生成缩略图
    /// </summary>
    private async Task GenerateThumbnailAsync(string sourcePath, string? thumbnailPath)
    {
        if (string.IsNullOrWhiteSpace(thumbnailPath))
        {
            throw CreateProcessingFailedException();
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
                throw CreateProcessingFailedException();
            }
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "生成缩略图时发生异常：{SourcePath}", sourcePath);
            throw CreateProcessingFailedException(ex);
        }
    }

    /// <summary>
    /// 添加水印
    /// </summary>
    private async Task AddWatermarkAsync(string filePath, string? watermarkText)
    {
        string? tempPath = null;
        if (string.IsNullOrWhiteSpace(watermarkText))
        {
            watermarkText = "Radish";
        }

        try
        {
            var fullPath = _fileStorage.GetFullPath(filePath);

            var tempFileName = $"{Path.GetFileNameWithoutExtension(fullPath)}_{Guid.NewGuid():N}{Path.GetExtension(fullPath)}";
            tempPath = Path.Combine(_tempPath, tempFileName);

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
            var fileBytes = await File.ReadAllBytesAsync(fullPath);

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
                    throw CreateProcessingFailedException();
                }
            }
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "添加水印时发生异常：{FilePath}", filePath);
            throw CreateProcessingFailedException(ex);
        }
        finally
        {
            DeletePhysicalFileIfExists(tempPath);
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
        string? tempPath = null;
        try
        {
            var fullPath = _fileStorage.GetFullPath(filePath);

            var tempFileName = $"{Path.GetFileNameWithoutExtension(fullPath)}_{Guid.NewGuid():N}{Path.GetExtension(fullPath)}";
            tempPath = Path.Combine(_tempPath, tempFileName);

            // 修复文件锁问题：先读取源文件到内存，关闭文件流后再处理
            var fileBytes = await File.ReadAllBytesAsync(fullPath);

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
                    throw CreateProcessingFailedException();
                }
            }
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "移除 EXIF 信息时发生异常：{FilePath}", filePath);
            throw CreateProcessingFailedException(ex);
        }
        finally
        {
            DeletePhysicalFileIfExists(tempPath);
        }
    }

    /// <summary>
    /// 生成多尺寸图片
    /// </summary>
    private async Task<Dictionary<string, string>> GenerateMultipleSizesAsync(
        string sourcePath,
        ISet<string> generatedPaths)
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

            var sourceDirectory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            foreach (var size in sizes)
            {
                generatedPaths.Add(Path.Combine(
                        sourceDirectory,
                        $"{Path.GetFileNameWithoutExtension(sourcePath)}_{size.Name}{Path.GetExtension(sourcePath)}")
                    .Replace('\\', '/'));
            }

            await using var sourceStream = File.OpenRead(sourceFullPath);
            // 注意：baseOutputPath 应该包含扩展名，这样 ImageProcessor 才能正确识别格式
            var baseOutputPath = Path.Combine(directory ?? "", fileNameWithoutExt + extension);

            var results = await _imageProcessor.GenerateMultipleSizesAsync(
                sourceStream,
                baseOutputPath,
                sizes
            );

            if (results.Count != sizes.Count || results.Any(sizeResult => !sizeResult.Success))
            {
                Log.Warning(
                    "多尺寸图片生成失败：{SuccessCount}/{ExpectedCount}",
                    results.Count(sizeResult => sizeResult.Success),
                    sizes.Count);
                foreach (var sizeResult in results)
                {
                    DeletePhysicalFileIfExists(sizeResult.OutputPath);
                }

                throw CreateProcessingFailedException();
            }

            // 转换为相对路径
            var sourceRelativePath = sourcePath;
            foreach (var sizeResult in results)
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
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "生成多尺寸图片时发生异常：{SourcePath}", sourcePath);
            throw CreateProcessingFailedException(ex);
        }

        return result;
    }

    private async Task CleanupFailedUploadAsync(
        FileUploadResult? uploadResult,
        IReadOnlyCollection<string> generatedPaths)
    {
        if (uploadResult == null)
        {
            return;
        }

        var paths = new[]
            {
                uploadResult.StoragePath,
                uploadResult.ThumbnailPath
            }
            .Concat(generatedPaths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var path in paths)
        {
            try
            {
                var deleted = await _fileStorage.DeleteAsync(path!);
                if (!deleted && await _fileStorage.ExistsAsync(path!))
                {
                    Log.Warning("失败上传文件未能清理：{StoragePath}", path);
                }
            }
            catch (Exception cleanupException)
            {
                Log.Warning(
                    cleanupException,
                    "清理失败上传文件时发生异常：{StoragePath}",
                    path);
            }
        }
    }

    private static void DeletePhysicalFileIfExists(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "清理图片处理临时文件失败：{Path}", path);
        }
    }

    private static BusinessException CreateUploadFailureException(FileUploadResult uploadResult)
    {
        var (statusCode, errorCode, message) = uploadResult.FailureKind switch
        {
            FileUploadFailureKind.FileTooLarge => (
                StatusCodes.Status413PayloadTooLarge,
                AttachmentErrorCodes.FileTooLarge,
                uploadResult.ErrorMessage ?? "文件大小超过限制"),
            FileUploadFailureKind.InvalidBusinessType => (
                StatusCodes.Status400BadRequest,
                AttachmentErrorCodes.BusinessTypeUnsupported,
                "不支持该附件业务类型"),
            FileUploadFailureKind.UnsupportedType => (
                StatusCodes.Status415UnsupportedMediaType,
                AttachmentErrorCodes.UnsupportedMediaType,
                uploadResult.ErrorMessage ?? "不支持该文件类型"),
            FileUploadFailureKind.ContentMismatch => (
                StatusCodes.Status415UnsupportedMediaType,
                AttachmentErrorCodes.ContentMismatch,
                uploadResult.ErrorMessage ?? "文件内容与扩展名不匹配"),
            FileUploadFailureKind.StorageFailed => (
                StatusCodes.Status500InternalServerError,
                AttachmentErrorCodes.StorageFailed,
                "文件存储失败，请稍后重试"),
            _ => (
                StatusCodes.Status500InternalServerError,
                AttachmentErrorCodes.StorageFailed,
                "文件存储失败，请稍后重试")
        };

        return new BusinessException(
            message,
            statusCode,
            errorCode,
            AttachmentErrorCodes.ResolveMessageKey(errorCode),
            uploadResult.MessageArguments);
    }

    private static BusinessException CreateProcessingFailedException(Exception? innerException = null)
    {
        const string message = "文件处理失败，请稍后重试";
        var messageKey = AttachmentErrorCodes.ResolveMessageKey(AttachmentErrorCodes.ProcessingFailed);

        return innerException == null
            ? new BusinessException(
                message,
                StatusCodes.Status500InternalServerError,
                AttachmentErrorCodes.ProcessingFailed,
                messageKey)
            : new BusinessException(
                message,
                innerException,
                StatusCodes.Status500InternalServerError,
                AttachmentErrorCodes.ProcessingFailed,
                messageKey);
    }

    #endregion
}
