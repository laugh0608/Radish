using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Radish.Common.AttributeTool;
using Radish.Common.CacheTool;
using Radish.Infrastructure.FileStorage;
using Radish.Infrastructure.ImageProcessing;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using SqlSugar;

namespace Radish.Service;

/// <summary>表情包服务实现</summary>
public class StickerService : BaseService<StickerGroup, StickerGroupVo>, IStickerService
{
    private const string GroupsCacheKeyPrefix = "sticker:groups:";
    private static readonly TimeSpan GroupsCacheTtl = TimeSpan.FromMinutes(30);
    private static readonly Regex CodeRegex = new("^[a-z0-9_]+$", RegexOptions.Compiled);

    private readonly IBaseRepository<StickerGroup> _stickerGroupRepository;
    private readonly IBaseRepository<Sticker> _stickerRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly ICaching _caching;
    private readonly IFileStorage _fileStorage;
    private readonly IImageProcessor _imageProcessor;
    private readonly ILogger<StickerService> _logger;

    public StickerService(
        IMapper mapper,
        IBaseRepository<StickerGroup> baseRepository,
        IBaseRepository<Sticker> stickerRepository,
        IBaseRepository<Attachment> attachmentRepository,
        ICaching caching,
        IFileStorage fileStorage,
        IImageProcessor imageProcessor,
        ILogger<StickerService> logger)
        : base(mapper, baseRepository)
    {
        _stickerGroupRepository = baseRepository;
        _stickerRepository = stickerRepository;
        _attachmentRepository = attachmentRepository;
        _caching = caching;
        _fileStorage = fileStorage;
        _imageProcessor = imageProcessor;
        _logger = logger;
    }

    public async Task<List<StickerGroupVo>> GetGroupsAsync(long tenantId)
    {
        var normalizedTenantId = NormalizeTenantId(tenantId);
        var cacheKey = BuildGroupsCacheKey(normalizedTenantId);

        try
        {
            var cached = await _caching.GetAsync<List<StickerGroupVo>>(cacheKey);
            if (cached != null)
            {
                return cached;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取表情包分组缓存失败，cacheKey={CacheKey}", cacheKey);
        }

        var groups = await _stickerGroupRepository.QueryWithOrderAsync(
            BuildGroupTenantPredicate(normalizedTenantId, includeDisabled: false),
            g => g.Sort,
            OrderByType.Asc);

        if (groups.Count == 0)
        {
            await TrySetGroupsCacheAsync(cacheKey, new List<StickerGroupVo>());
            return new List<StickerGroupVo>();
        }

        var groupIds = groups.Select(g => g.Id).ToList();
        var stickers = await _stickerRepository.QueryAsync(
            s => groupIds.Contains(s.GroupId) && s.IsEnabled && !s.IsDeleted);

        var stickerLookup = stickers
            .OrderBy(s => s.Sort)
            .ThenBy(s => s.Id)
            .GroupBy(s => s.GroupId)
            .ToDictionary(g => g.Key, g => Mapper.Map<List<StickerVo>>(g.ToList()));

        var groupVos = Mapper.Map<List<StickerGroupVo>>(groups);
        foreach (var groupVo in groupVos)
        {
            if (!stickerLookup.TryGetValue(groupVo.VoId, out var stickerVos))
            {
                groupVo.VoStickers = new List<StickerVo>();
                groupVo.VoStickerCount = 0;
                continue;
            }

            groupVo.VoStickers = stickerVos;
            groupVo.VoStickerCount = stickerVos.Count;
        }

        await TrySetGroupsCacheAsync(cacheKey, groupVos);
        return groupVos;
    }

    public async Task<StickerGroupVo?> GetGroupDetailAsync(long tenantId, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalizedCode = code.Trim().ToLowerInvariant();
        var normalizedTenantId = NormalizeTenantId(tenantId);
        var group = await _stickerGroupRepository.QueryFirstAsync(
            g => (normalizedTenantId <= 0 ? g.TenantId == 0 : (g.TenantId == normalizedTenantId || g.TenantId == 0))
                 && g.Code == normalizedCode
                 && g.IsEnabled
                 && !g.IsDeleted);

        if (group == null)
        {
            return null;
        }

        var stickers = await _stickerRepository.QueryAsync(
            s => s.GroupId == group.Id && s.IsEnabled && !s.IsDeleted);
        var stickerVos = Mapper.Map<List<StickerVo>>(stickers.OrderBy(s => s.Sort).ThenBy(s => s.Id).ToList());

        var groupVo = Mapper.Map<StickerGroupVo>(group);
        groupVo.VoStickers = stickerVos;
        groupVo.VoStickerCount = stickerVos.Count;
        return groupVo;
    }

    public async Task<bool> RecordUseAsync(long tenantId, string emojiType, string emojiValue, long operatorId = 0, string? operatorName = null)
    {
        if (string.IsNullOrWhiteSpace(emojiType) || string.IsNullOrWhiteSpace(emojiValue))
        {
            return false;
        }

        var normalizedType = emojiType.Trim().ToLowerInvariant();
        if (normalizedType == "unicode")
        {
            return true;
        }

        if (normalizedType != "sticker")
        {
            return false;
        }

        if (!TryParseStickerEmojiValue(emojiValue, out var groupCode, out var stickerCode))
        {
            return false;
        }

        var normalizedTenantId = NormalizeTenantId(tenantId);
        var group = await _stickerGroupRepository.QueryFirstAsync(
            g => (normalizedTenantId <= 0 ? g.TenantId == 0 : (g.TenantId == normalizedTenantId || g.TenantId == 0))
                 && g.Code == groupCode
                 && g.IsEnabled
                 && !g.IsDeleted);
        if (group == null)
        {
            return false;
        }

        var sticker = await _stickerRepository.QueryFirstAsync(
            s => s.GroupId == group.Id && s.Code == stickerCode && s.IsEnabled && !s.IsDeleted);
        if (sticker == null)
        {
            return false;
        }

        var modifier = NormalizeOperatorName(operatorName);
        var affected = await _stickerRepository.UpdateColumnsAsync(
            s => new Sticker
            {
                UseCount = s.UseCount + 1,
                ModifyTime = DateTime.Now,
                ModifyBy = modifier,
                ModifyId = operatorId > 0 ? operatorId : null
            },
            s => s.Id == sticker.Id);

        if (affected > 0)
        {
            await InvalidateGroupsCacheAsync(group.TenantId);
        }

        return affected > 0;
    }

    public async Task<long> CreateGroupAsync(long tenantId, CreateStickerGroupDto createDto, long operatorId, string operatorName)
    {
        ArgumentNullException.ThrowIfNull(createDto);

        var normalizedTenantId = NormalizeTenantId(tenantId);
        var normalizedCode = NormalizeCodeOnly(createDto.Code);
        if (!IsValidCode(normalizedCode))
        {
            throw new ArgumentException("分组标识符格式不正确，仅允许小写字母、数字和下划线。", nameof(createDto.Code));
        }

        var duplicated = await _stickerGroupRepository.QueryExistsAsync(
            g => g.TenantId == normalizedTenantId && g.Code == normalizedCode);
        if (duplicated)
        {
            throw new InvalidOperationException("分组标识符已存在");
        }

        var creator = NormalizeOperatorName(operatorName);
        var entity = new StickerGroup
        {
            Name = createDto.Name.Trim(),
            Code = normalizedCode,
            Description = createDto.Description?.Trim(),
            CoverImageUrl = createDto.CoverImageUrl?.Trim(),
            GroupType = createDto.GroupType,
            IsEnabled = createDto.IsEnabled,
            Sort = createDto.Sort,
            TenantId = normalizedTenantId,
            CreateTime = DateTime.Now,
            CreateBy = creator,
            CreateId = operatorId,
            IsDeleted = false
        };

        var id = await _stickerGroupRepository.AddAsync(entity);
        await InvalidateGroupsCacheAsync(normalizedTenantId);
        return id;
    }

    public async Task<bool> UpdateGroupAsync(long id, UpdateStickerGroupDto updateDto, long operatorId, string operatorName)
    {
        if (id <= 0)
        {
            throw new ArgumentException("分组ID无效", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(updateDto);

        var entity = await _stickerGroupRepository.QueryByIdAsync(id);
        if (entity == null || entity.IsDeleted)
        {
            return false;
        }

        entity.Name = updateDto.Name.Trim();
        entity.Description = updateDto.Description?.Trim();
        entity.CoverImageUrl = updateDto.CoverImageUrl?.Trim();
        entity.GroupType = updateDto.GroupType;
        entity.IsEnabled = updateDto.IsEnabled;
        entity.Sort = updateDto.Sort;
        entity.ModifyTime = DateTime.Now;
        entity.ModifyBy = NormalizeOperatorName(operatorName);
        entity.ModifyId = operatorId;

        var updated = await _stickerGroupRepository.UpdateAsync(entity);
        if (updated)
        {
            await InvalidateGroupsCacheAsync(entity.TenantId);
        }

        return updated;
    }

    public async Task<bool> DeleteGroupAsync(long id, long operatorId, string operatorName)
    {
        if (id <= 0)
        {
            throw new ArgumentException("分组ID无效", nameof(id));
        }

        var entity = await _stickerGroupRepository.QueryByIdAsync(id);
        if (entity == null || entity.IsDeleted)
        {
            return false;
        }

        var modifier = NormalizeOperatorName(operatorName);
        var deleted = await _stickerGroupRepository.SoftDeleteByIdAsync(id, modifier);
        if (!deleted)
        {
            return false;
        }

        await _stickerRepository.SoftDeleteAsync(s => s.GroupId == id, modifier);
        await _stickerGroupRepository.UpdateColumnsAsync(
            g => new StickerGroup
            {
                IsEnabled = false,
                ModifyTime = DateTime.Now,
                ModifyBy = modifier,
                ModifyId = operatorId
            },
            g => g.Id == id);

        await InvalidateGroupsCacheAsync(entity.TenantId);
        return true;
    }

    public async Task<bool> CheckGroupCodeAvailableAsync(long tenantId, string code)
    {
        var normalizedCode = NormalizeCodeOnly(code);
        if (!IsValidCode(normalizedCode))
        {
            return false;
        }

        var normalizedTenantId = NormalizeTenantId(tenantId);
        var exists = await _stickerGroupRepository.QueryExistsAsync(
            g => g.TenantId == normalizedTenantId && g.Code == normalizedCode);
        return !exists;
    }

    public async Task<long> AddStickerAsync(CreateStickerDto createDto, long operatorId, string operatorName)
    {
        ArgumentNullException.ThrowIfNull(createDto);

        var group = await _stickerGroupRepository.QueryByIdAsync(createDto.GroupId);
        if (group == null || group.IsDeleted)
        {
            throw new InvalidOperationException("分组不存在或已删除");
        }

        var normalizedCode = NormalizeCodeOnly(createDto.Code);
        if (!IsValidCode(normalizedCode))
        {
            throw new ArgumentException("表情标识符格式不正确，仅允许小写字母、数字和下划线。", nameof(createDto.Code));
        }

        var duplicated = await _stickerRepository.QueryExistsAsync(
            s => s.GroupId == createDto.GroupId && s.Code == normalizedCode);
        if (duplicated)
        {
            throw new InvalidOperationException("该分组内表情标识符已存在");
        }

        var (imageUrl, thumbnailUrl, isAnimated) = await ResolveStickerImageDataAsync(
            createDto.AttachmentId,
            createDto.ImageUrl,
            createDto.ThumbnailUrl,
            createDto.IsAnimated,
            group.Code,
            normalizedCode);

        var creator = NormalizeOperatorName(operatorName);
        var entity = new Sticker
        {
            GroupId = createDto.GroupId,
            Code = normalizedCode,
            Name = createDto.Name.Trim(),
            ImageUrl = imageUrl,
            ThumbnailUrl = thumbnailUrl,
            IsAnimated = isAnimated,
            AllowInline = createDto.AllowInline,
            AttachmentId = createDto.AttachmentId,
            UseCount = 0,
            Sort = createDto.Sort,
            IsEnabled = createDto.IsEnabled,
            IsDeleted = false,
            CreateTime = DateTime.Now,
            CreateBy = creator,
            CreateId = operatorId
        };

        var id = await _stickerRepository.AddAsync(entity);
        await InvalidateGroupsCacheAsync(group.TenantId);
        return id;
    }

    public async Task<bool> UpdateStickerAsync(long id, UpdateStickerDto updateDto, long operatorId, string operatorName)
    {
        if (id <= 0)
        {
            throw new ArgumentException("表情ID无效", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(updateDto);

        var entity = await _stickerRepository.QueryByIdAsync(id);
        if (entity == null || entity.IsDeleted)
        {
            return false;
        }

        if (updateDto.AttachmentId.HasValue || !string.IsNullOrWhiteSpace(updateDto.ImageUrl))
        {
            var groupCode = string.Empty;
            if (updateDto.AttachmentId.HasValue)
            {
                var group = await _stickerGroupRepository.QueryByIdAsync(entity.GroupId);
                groupCode = group?.Code ?? string.Empty;
            }

            var (imageUrl, thumbnailUrl, isAnimated) = await ResolveStickerImageDataAsync(
                updateDto.AttachmentId,
                updateDto.ImageUrl,
                updateDto.ThumbnailUrl,
                updateDto.IsAnimated,
                groupCode,
                entity.Code);
            entity.ImageUrl = imageUrl;
            entity.ThumbnailUrl = thumbnailUrl;
            entity.IsAnimated = isAnimated;
            entity.AttachmentId = updateDto.AttachmentId;
        }
        else
        {
            entity.ThumbnailUrl = string.IsNullOrWhiteSpace(updateDto.ThumbnailUrl) ? entity.ThumbnailUrl : updateDto.ThumbnailUrl.Trim();
            entity.IsAnimated = updateDto.IsAnimated;
        }

        entity.Name = updateDto.Name.Trim();
        entity.AllowInline = updateDto.AllowInline;
        entity.IsEnabled = updateDto.IsEnabled;
        entity.Sort = updateDto.Sort;
        entity.ModifyTime = DateTime.Now;
        entity.ModifyBy = NormalizeOperatorName(operatorName);
        entity.ModifyId = operatorId;

        var updated = await _stickerRepository.UpdateAsync(entity);
        if (updated)
        {
            await InvalidateGroupsCacheByGroupIdAsync(entity.GroupId);
        }

        return updated;
    }

    public async Task<bool> DeleteStickerAsync(long id, long operatorId, string operatorName)
    {
        if (id <= 0)
        {
            throw new ArgumentException("表情ID无效", nameof(id));
        }

        var entity = await _stickerRepository.QueryByIdAsync(id);
        if (entity == null || entity.IsDeleted)
        {
            return false;
        }

        var modifier = NormalizeOperatorName(operatorName);
        var deleted = await _stickerRepository.SoftDeleteByIdAsync(id, modifier);
        if (!deleted)
        {
            return false;
        }

        await _stickerRepository.UpdateColumnsAsync(
            s => new Sticker
            {
                IsEnabled = false,
                ModifyTime = DateTime.Now,
                ModifyBy = modifier,
                ModifyId = operatorId
            },
            s => s.Id == id);

        await InvalidateGroupsCacheByGroupIdAsync(entity.GroupId);
        return true;
    }

    public async Task<bool> CheckStickerCodeAvailableAsync(long groupId, string code)
    {
        if (groupId <= 0)
        {
            return false;
        }

        var normalizedCode = NormalizeCodeOnly(code);
        if (!IsValidCode(normalizedCode))
        {
            return false;
        }

        var exists = await _stickerRepository.QueryExistsAsync(
            s => s.GroupId == groupId && s.Code == normalizedCode);
        return !exists;
    }

    public async Task<List<StickerVo>> GetGroupStickersAsync(long groupId, bool includeDisabled = true)
    {
        if (groupId <= 0)
        {
            return new List<StickerVo>();
        }

        var stickers = await _stickerRepository.QueryAsync(
            s => s.GroupId == groupId
                 && !s.IsDeleted
                 && (includeDisabled || s.IsEnabled));

        return Mapper.Map<List<StickerVo>>(stickers.OrderBy(s => s.Sort).ThenBy(s => s.Id).ToList());
    }

    public StickerNormalizeCodeVo NormalizeCode(string filename)
    {
        var normalizedCode = NormalizeCodeCore(filename, out var reasons);
        return new StickerNormalizeCodeVo
        {
            VoOriginalFileName = filename ?? string.Empty,
            VoNormalizedCode = normalizedCode,
            VoIsChanged = !string.Equals(
                Path.GetFileNameWithoutExtension(filename ?? string.Empty),
                normalizedCode,
                StringComparison.Ordinal),
            VoChangeReasons = reasons
        };
    }

    [UseTran(Propagation = Propagation.Required)]
    public async Task<StickerBatchAddResultVo> BatchAddStickersAsync(BatchAddStickersDto request, long operatorId, string operatorName)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GroupId <= 0)
        {
            throw new ArgumentException("分组ID无效", nameof(request.GroupId));
        }

        if (request.Stickers == null || request.Stickers.Count == 0)
        {
            throw new ArgumentException("表情列表不能为空", nameof(request.Stickers));
        }

        if (request.Stickers.Count > 50)
        {
            throw new ArgumentException("单次批量新增表情数不能超过50个", nameof(request.Stickers));
        }

        var group = await _stickerGroupRepository.QueryByIdAsync(request.GroupId);
        if (group == null || group.IsDeleted)
        {
            throw new InvalidOperationException("分组不存在或已删除");
        }

        var result = new StickerBatchAddResultVo
        {
            VoGroupId = request.GroupId
        };

        var normalizedItems = new List<(int rowIndex, BatchAddStickerItemDto item, string code, string name)>();
        for (var i = 0; i < request.Stickers.Count; i++)
        {
            var rowIndex = i;
            var item = request.Stickers[i];
            var normalizedCode = NormalizeCodeOnly(item.Code);
            var normalizedName = item.Name?.Trim() ?? string.Empty;

            if (item.AttachmentId <= 0)
            {
                result.VoConflicts.Add(new StickerBatchConflictVo
                {
                    VoRowIndex = rowIndex,
                    VoCode = normalizedCode,
                    VoMessage = "附件ID无效"
                });
                continue;
            }

            if (!IsValidCode(normalizedCode))
            {
                result.VoConflicts.Add(new StickerBatchConflictVo
                {
                    VoRowIndex = rowIndex,
                    VoCode = normalizedCode,
                    VoMessage = "标识符格式不正确，仅允许小写字母、数字和下划线"
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                result.VoConflicts.Add(new StickerBatchConflictVo
                {
                    VoRowIndex = rowIndex,
                    VoCode = normalizedCode,
                    VoMessage = "显示名不能为空"
                });
                continue;
            }

            normalizedItems.Add((rowIndex, item, normalizedCode, normalizedName));
        }

        if (result.VoConflicts.Count > 0)
        {
            return result;
        }

        var duplicateRows = normalizedItems
            .GroupBy(x => x.code)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var duplicate in duplicateRows)
        {
            foreach (var row in duplicate)
            {
                result.VoConflicts.Add(new StickerBatchConflictVo
                {
                    VoRowIndex = row.rowIndex,
                    VoCode = row.code,
                    VoMessage = "与本次批量上传中的其他表情重复"
                });
            }
        }

        if (result.VoConflicts.Count > 0)
        {
            return result;
        }

        var requestCodes = normalizedItems.Select(x => x.code).Distinct().ToList();
        var existingStickers = await _stickerRepository.QueryAsync(
            s => s.GroupId == request.GroupId && requestCodes.Contains(s.Code));
        if (existingStickers.Count > 0)
        {
            var existingCodes = existingStickers.Select(s => s.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var row in normalizedItems.Where(x => existingCodes.Contains(x.code)))
            {
                result.VoConflicts.Add(new StickerBatchConflictVo
                {
                    VoRowIndex = row.rowIndex,
                    VoCode = row.code,
                    VoMessage = "与已有表情重复"
                });
            }

            return result;
        }

        var creator = NormalizeOperatorName(operatorName);
        var entitiesToInsert = new List<Sticker>(normalizedItems.Count);
        foreach (var row in normalizedItems.OrderBy(x => x.rowIndex))
        {
            try
            {
                var (imageUrl, thumbnailUrl, isAnimated) = await ResolveStickerImageDataAsync(
                    row.item.AttachmentId,
                    imageUrl: null,
                    thumbnailUrl: null,
                    isAnimated: false,
                    groupCode: group.Code,
                    stickerCode: row.code);

                entitiesToInsert.Add(new Sticker
                {
                    GroupId = request.GroupId,
                    Code = row.code,
                    Name = row.name,
                    ImageUrl = imageUrl,
                    ThumbnailUrl = thumbnailUrl,
                    IsAnimated = isAnimated,
                    AllowInline = row.item.AllowInline,
                    AttachmentId = row.item.AttachmentId,
                    Sort = row.rowIndex,
                    UseCount = 0,
                    IsEnabled = true,
                    IsDeleted = false,
                    CreateTime = DateTime.Now,
                    CreateBy = creator,
                    CreateId = operatorId
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "批量新增表情构建失败，GroupId={GroupId}, RowIndex={RowIndex}", request.GroupId, row.rowIndex);
                result.VoFailedItems.Add(new StickerBatchFailedItemVo
                {
                    VoRowIndex = row.rowIndex,
                    VoAttachmentId = row.item.AttachmentId,
                    VoCode = row.code,
                    VoMessage = ex.Message
                });
            }
        }

        if (result.VoFailedItems.Count > 0)
        {
            return result;
        }

        foreach (var entity in entitiesToInsert)
        {
            var id = await _stickerRepository.AddAsync(entity);
            result.VoStickerIds.Add(id);
        }

        result.VoCreatedCount = result.VoStickerIds.Count;
        await InvalidateGroupsCacheAsync(group.TenantId);
        return result;
    }

    [UseTran(Propagation = Propagation.Required)]
    public async Task<int> BatchUpdateSortAsync(List<StickerSortItemDto> items, long operatorId, string operatorName)
    {
        if (items == null || items.Count == 0)
        {
            return 0;
        }

        var normalizedItems = items
            .Where(i => i.Id > 0)
            .GroupBy(i => i.Id)
            .Select(g => g.Last())
            .ToList();

        if (normalizedItems.Count == 0)
        {
            return 0;
        }

        var idList = normalizedItems.Select(i => i.Id).ToList();
        var stickers = await _stickerRepository.QueryByIdsAsync(idList);
        if (stickers.Count == 0)
        {
            return 0;
        }

        var sortMap = normalizedItems.ToDictionary(i => i.Id, i => Math.Max(0, i.Sort));
        var modifier = NormalizeOperatorName(operatorName);
        var now = DateTime.Now;

        foreach (var sticker in stickers)
        {
            if (!sortMap.TryGetValue(sticker.Id, out var sort))
            {
                continue;
            }

            sticker.Sort = sort;
            sticker.ModifyTime = now;
            sticker.ModifyBy = modifier;
            sticker.ModifyId = operatorId;
        }

        var affected = await _stickerRepository.UpdateRangeAsync(stickers);

        var groupIds = stickers.Select(s => s.GroupId).Distinct().ToList();
        foreach (var groupId in groupIds)
        {
            await InvalidateGroupsCacheByGroupIdAsync(groupId);
        }

        return affected;
    }

    private async Task<(string imageUrl, string? thumbnailUrl, bool isAnimated)> ResolveStickerImageDataAsync(
        long? attachmentId,
        string? imageUrl,
        string? thumbnailUrl,
        bool isAnimated,
        string? groupCode = null,
        string? stickerCode = null)
    {
        var resolvedImageUrl = imageUrl?.Trim();
        var resolvedThumbnailUrl = thumbnailUrl?.Trim();
        var resolvedAnimated = isAnimated;

        if (attachmentId.HasValue)
        {
            var attachment = await _attachmentRepository.QueryByIdAsync(attachmentId.Value);
            if (attachment == null || attachment.IsDeleted)
            {
                throw new InvalidOperationException("关联附件不存在或已删除");
            }

            if (string.IsNullOrWhiteSpace(resolvedImageUrl))
            {
                resolvedImageUrl = attachment.Url;
            }

            if (string.IsNullOrWhiteSpace(resolvedThumbnailUrl))
            {
                resolvedThumbnailUrl = !string.IsNullOrWhiteSpace(attachment.ThumbnailPath)
                    ? ResolveFileUrl(attachment.ThumbnailPath)
                    : attachment.Url;
            }

            if (!resolvedAnimated)
            {
                resolvedAnimated = string.Equals(attachment.Extension, ".gif", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(attachment.MimeType, "image/gif", StringComparison.OrdinalIgnoreCase);
            }

            if (string.IsNullOrWhiteSpace(attachment.ThumbnailPath) &&
                !string.IsNullOrWhiteSpace(groupCode) &&
                !string.IsNullOrWhiteSpace(stickerCode))
            {
                var generatedThumbnailUrl = await TryGenerateStickerThumbnailAsync(attachment, groupCode, stickerCode);
                if (!string.IsNullOrWhiteSpace(generatedThumbnailUrl))
                {
                    resolvedThumbnailUrl = generatedThumbnailUrl;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedImageUrl))
        {
            throw new ArgumentException("图片地址不能为空");
        }

        return (resolvedImageUrl, resolvedThumbnailUrl, resolvedAnimated);
    }

    private async Task<string?> TryGenerateStickerThumbnailAsync(Attachment attachment, string groupCode, string stickerCode)
    {
        if (string.IsNullOrWhiteSpace(attachment.StoragePath))
        {
            return null;
        }

        try
        {
            if (!await _fileStorage.ExistsAsync(attachment.StoragePath))
            {
                return null;
            }

            await using var sourceStream = await _fileStorage.DownloadAsync(attachment.StoragePath);
            if (sourceStream == null)
            {
                return null;
            }

            var thumbnailRelativePath = $"stickers/thumbnails/{groupCode}/{stickerCode}.jpg";
            var thumbnailFullPath = _fileStorage.GetFullPath(thumbnailRelativePath);
            var directory = Path.GetDirectoryName(thumbnailFullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var result = await _imageProcessor.GenerateThumbnailAsync(sourceStream, thumbnailFullPath, 96, 96, 85);
            if (!result.Success)
            {
                _logger.LogWarning("生成表情缩略图失败：AttachmentId={AttachmentId}, Error={Error}", attachment.Id, result.ErrorMessage);
                return null;
            }

            return _fileStorage.GetFileUrl(thumbnailRelativePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "生成表情缩略图异常，AttachmentId={AttachmentId}", attachment.Id);
            return null;
        }
    }

    private string ResolveFileUrl(string pathOrUrl)
    {
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out _))
        {
            return pathOrUrl;
        }

        return _fileStorage.GetFileUrl(pathOrUrl);
    }

    private async Task InvalidateGroupsCacheByGroupIdAsync(long groupId)
    {
        var group = await _stickerGroupRepository.QueryByIdAsync(groupId);
        if (group == null)
        {
            return;
        }

        await InvalidateGroupsCacheAsync(group.TenantId);
    }

    private async Task InvalidateGroupsCacheAsync(long tenantId)
    {
        var cacheKey = BuildGroupsCacheKey(NormalizeTenantId(tenantId));
        try
        {
            await _caching.RemoveAsync(cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "失效表情包缓存失败，cacheKey={CacheKey}", cacheKey);
        }
    }

    private async Task TrySetGroupsCacheAsync(string cacheKey, List<StickerGroupVo> value)
    {
        try
        {
            await _caching.SetAsync(cacheKey, value, GroupsCacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入表情包缓存失败，cacheKey={CacheKey}", cacheKey);
        }
    }

    private static Expression<Func<StickerGroup, bool>> BuildGroupTenantPredicate(long tenantId, bool includeDisabled)
    {
        if (tenantId <= 0)
        {
            return includeDisabled
                ? g => g.TenantId == 0 && !g.IsDeleted
                : g => g.TenantId == 0 && g.IsEnabled && !g.IsDeleted;
        }

        return includeDisabled
            ? g => (g.TenantId == tenantId || g.TenantId == 0) && !g.IsDeleted
            : g => (g.TenantId == tenantId || g.TenantId == 0) && g.IsEnabled && !g.IsDeleted;
    }

    private static bool TryParseStickerEmojiValue(string emojiValue, out string groupCode, out string stickerCode)
    {
        groupCode = string.Empty;
        stickerCode = string.Empty;

        var parts = emojiValue.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        groupCode = NormalizeCodeOnly(parts[0]);
        stickerCode = NormalizeCodeOnly(parts[1]);
        return IsValidCode(groupCode) && IsValidCode(stickerCode);
    }

    private static long NormalizeTenantId(long tenantId)
    {
        return tenantId > 0 ? tenantId : 0;
    }

    private static string BuildGroupsCacheKey(long tenantId)
    {
        return $"{GroupsCacheKeyPrefix}{tenantId}";
    }

    private static string NormalizeOperatorName(string? operatorName)
    {
        return string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();
    }

    private static string NormalizeCodeOnly(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToLowerInvariant();
    }

    private static bool IsValidCode(string code)
    {
        return !string.IsNullOrWhiteSpace(code) && code.Length <= 100 && CodeRegex.IsMatch(code);
    }

    private static string NormalizeCodeCore(string filename, out List<string> reasons)
    {
        var reasonSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var safeFileName = filename?.Trim() ?? string.Empty;
        var withoutExt = Path.GetFileNameWithoutExtension(safeFileName);
        var normalized = withoutExt;

        var lower = normalized.ToLowerInvariant();
        if (!string.Equals(normalized, lower, StringComparison.Ordinal))
        {
            reasonSet.Add("lowercase");
            normalized = lower;
        }

        var replaced = Regex.Replace(normalized, @"[\s\-.]+", "_");
        if (!string.Equals(normalized, replaced, StringComparison.Ordinal))
        {
            reasonSet.Add("replace_space_hyphen_dot");
            normalized = replaced;
        }

        var removedInvalid = Regex.Replace(normalized, @"[^a-z0-9_]", string.Empty);
        if (!string.Equals(normalized, removedInvalid, StringComparison.Ordinal))
        {
            reasonSet.Add("removed_non_ascii");
            normalized = removedInvalid;
        }

        var collapsed = Regex.Replace(normalized, @"_+", "_").Trim('_');
        if (!string.Equals(normalized, collapsed, StringComparison.Ordinal))
        {
            reasonSet.Add("collapse_underscores");
            normalized = collapsed;
        }

        if (normalized.Length > 100)
        {
            normalized = normalized[..100];
            reasonSet.Add("truncate_100");
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "sticker";
            reasonSet.Add("fallback_default");
        }

        if (!string.Equals(safeFileName, filename, StringComparison.Ordinal))
        {
            reasonSet.Add("trim_input");
        }

        reasons = reasonSet.ToList();
        return normalized;
    }
}
