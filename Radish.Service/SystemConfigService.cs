using System.Text.RegularExpressions;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Service;

/// <summary>
/// 系统配置服务
/// </summary>
public class SystemConfigService : ISystemConfigService
{
    private static readonly Regex ConfigKeyRegex = new("^[a-zA-Z][a-zA-Z0-9._]*$", RegexOptions.Compiled);
    private readonly ISystemConfigRepository _systemConfigRepository;

    public SystemConfigService(ISystemConfigRepository systemConfigRepository)
    {
        _systemConfigRepository = systemConfigRepository;
    }

    public async Task<List<SystemConfigVo>> GetSystemConfigsAsync()
    {
        var records = await _systemConfigRepository.GetAllAsync();
        return records
            .OrderBy(item => item.Id)
            .Select(MapToVo)
            .ToList();
    }

    public async Task<List<string>> GetConfigCategoriesAsync()
    {
        var records = await _systemConfigRepository.GetAllAsync();
        return records
            .Select(item => item.Category.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<SystemConfigVo?> GetConfigByIdAsync(long id)
    {
        var record = await _systemConfigRepository.GetByIdAsync(id);
        return record == null ? null : MapToVo(record);
    }

    public async Task<SystemConfigVo> CreateConfigAsync(CreateSystemConfigDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedCategory = request.Category.Trim();
        var normalizedKey = request.Key.Trim();
        var normalizedName = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(normalizedCategory))
        {
            throw new InvalidOperationException("配置分类不能为空");
        }

        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            throw new InvalidOperationException("配置键不能为空");
        }

        if (!ConfigKeyRegex.IsMatch(normalizedKey))
        {
            throw new InvalidOperationException("配置键必须以字母开头，只能包含字母、数字、点和下划线");
        }

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("配置名称不能为空");
        }

        var existedRecord = await _systemConfigRepository.GetByKeyAsync(normalizedKey);
        if (existedRecord != null)
        {
            throw new InvalidOperationException($"配置键已存在：{normalizedKey}");
        }

        var record = new SystemConfigRecord
        {
            Category = normalizedCategory,
            Key = normalizedKey,
            Name = normalizedName,
            Value = request.Value.Trim(),
            Description = request.Description?.Trim(),
            Type = NormalizeConfigType(request.Type),
            IsEnabled = request.IsEnabled,
            CreateTime = DateTime.Now,
            ModifyTime = DateTime.Now
        };

        var createdRecord = await _systemConfigRepository.CreateAsync(record);
        return MapToVo(createdRecord);
    }

    public async Task<SystemConfigVo?> UpdateConfigAsync(long id, UpdateSystemConfigDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existedRecord = await _systemConfigRepository.GetByIdAsync(id);
        if (existedRecord == null)
        {
            return null;
        }

        existedRecord.Value = request.Value.Trim();
        existedRecord.Description = request.Description?.Trim();
        existedRecord.IsEnabled = request.IsEnabled;
        existedRecord.ModifyTime = DateTime.Now;

        var updatedRecord = await _systemConfigRepository.UpdateAsync(existedRecord);
        return updatedRecord == null ? null : MapToVo(updatedRecord);
    }

    public async Task<bool> DeleteConfigAsync(long id)
    {
        var existedRecord = await _systemConfigRepository.GetByIdAsync(id);
        if (existedRecord == null)
        {
            return false;
        }

        if (existedRecord.Key.Equals(SystemConfigDefaults.SiteFaviconKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("站点图标配置不能删除，可直接修改或恢复默认图标");
        }

        return await _systemConfigRepository.DeleteAsync(id);
    }

    public async Task<PublicSiteSettingsVo> GetPublicSiteSettingsAsync()
    {
        var faviconRecord = await _systemConfigRepository.GetByKeyAsync(SystemConfigDefaults.SiteFaviconKey);
        var configuredFaviconUrl = faviconRecord?.Value?.Trim();
        var usesDefaultFavicon = faviconRecord == null
            || !faviconRecord.IsEnabled
            || string.IsNullOrWhiteSpace(configuredFaviconUrl);

        var faviconUrl = usesDefaultFavicon
            ? SystemConfigDefaults.DefaultSiteFaviconPath
            : configuredFaviconUrl!;

        return new PublicSiteSettingsVo
        {
            VoSiteFaviconUrl = faviconUrl,
            VoUsingDefaultSiteFavicon = usesDefaultFavicon || faviconUrl.Equals(SystemConfigDefaults.DefaultSiteFaviconPath, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string NormalizeConfigType(string? configType)
    {
        var normalizedType = configType?.Trim().ToLowerInvariant();
        return normalizedType switch
        {
            "number" => "number",
            "boolean" => "boolean",
            "json" => "json",
            _ => "string"
        };
    }

    private static SystemConfigVo MapToVo(SystemConfigRecord record)
    {
        return new SystemConfigVo
        {
            VoId = record.Id,
            VoCategory = record.Category,
            VoKey = record.Key,
            VoName = record.Name,
            VoValue = record.Value,
            VoDescription = record.Description,
            VoType = NormalizeConfigType(record.Type),
            VoIsEnabled = record.IsEnabled,
            VoCreateTime = record.CreateTime,
            VoModifyTime = record.ModifyTime
        };
    }
}
