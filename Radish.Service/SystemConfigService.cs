using System.Globalization;
using System.Text.Json;
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
    private readonly ISystemConfigRepository _systemConfigRepository;

    public SystemConfigService(ISystemConfigRepository systemConfigRepository)
    {
        _systemConfigRepository = systemConfigRepository;
    }

    public async Task<List<SystemConfigVo>> GetSystemConfigsAsync()
    {
        var records = await GetOverrideRecordsAsync();
        return SystemConfigDefaults.Definitions
            .OrderBy(item => item.Id)
            .Select(definition => MapToVo(definition, records.GetValueOrDefault(definition.Key)))
            .ToList();
    }

    public Task<List<string>> GetConfigCategoriesAsync()
    {
        var categories = SystemConfigDefaults.Definitions
            .Select(item => item.Category.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(categories);
    }

    public async Task<SystemConfigVo?> GetConfigByIdAsync(long id)
    {
        var definition = await GetDefinitionByRequestIdAsync(id);
        if (definition == null)
        {
            return null;
        }

        var record = await _systemConfigRepository.GetByKeyAsync(definition.Key);
        return MapToVo(definition, record);
    }

    public Task<SystemConfigVo> CreateConfigAsync(CreateSystemConfigDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        throw new InvalidOperationException("系统设置由代码注册，不支持通过 Console 新增未知配置");
    }

    public async Task<SystemConfigVo?> UpdateConfigAsync(long id, UpdateSystemConfigDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var definition = await GetDefinitionByRequestIdAsync(id);
        if (definition == null)
        {
            return null;
        }

        EnsureEditable(definition);

        if (!request.IsEnabled)
        {
            await _systemConfigRepository.DeleteByKeyAsync(definition.Key);
            return MapToVo(definition, null);
        }

        var normalizedValue = NormalizeAndValidateValue(definition, request.Value);
        if (string.Equals(normalizedValue, definition.DefaultValue, StringComparison.Ordinal))
        {
            await _systemConfigRepository.DeleteByKeyAsync(definition.Key);
            return MapToVo(definition, null);
        }

        var existedRecord = await _systemConfigRepository.GetByKeyAsync(definition.Key);
        var now = DateTime.Now;
        SystemConfigRecord updatedRecord;

        if (existedRecord == null)
        {
            updatedRecord = await _systemConfigRepository.CreateAsync(new SystemConfigRecord
            {
                Category = definition.Category,
                Key = definition.Key,
                Name = definition.Name,
                Value = normalizedValue,
                Description = definition.Description,
                Type = definition.ValueType,
                IsEnabled = true,
                CreateTime = now,
                ModifyTime = now
            });
        }
        else
        {
            existedRecord.Category = definition.Category;
            existedRecord.Name = definition.Name;
            existedRecord.Value = normalizedValue;
            existedRecord.Description = definition.Description;
            existedRecord.Type = definition.ValueType;
            existedRecord.IsEnabled = true;
            existedRecord.ModifyTime = now;

            updatedRecord = await _systemConfigRepository.UpdateAsync(existedRecord)
                ?? throw new InvalidOperationException("系统设置覆盖值更新失败");
        }

        return MapToVo(definition, updatedRecord);
    }

    public async Task<SystemConfigVo?> RestoreConfigDefaultAsync(long id)
    {
        var definition = await GetDefinitionByRequestIdAsync(id);
        if (definition == null)
        {
            return null;
        }

        EnsureEditable(definition);
        await _systemConfigRepository.DeleteByKeyAsync(definition.Key);
        return MapToVo(definition, null);
    }

    public async Task<bool> DeleteConfigAsync(long id)
    {
        var restored = await RestoreConfigDefaultAsync(id);
        return restored != null;
    }

    public async Task<PublicSiteSettingsVo> GetPublicSiteSettingsAsync()
    {
        var definition = SystemConfigDefaults.GetDefinitionByKey(SystemConfigDefaults.SiteFaviconKey)
            ?? throw new InvalidOperationException($"系统设置定义不存在：{SystemConfigDefaults.SiteFaviconKey}");
        var faviconRecord = await _systemConfigRepository.GetByKeyAsync(definition.Key);
        var usesDefaultFavicon = !HasEnabledOverride(definition, faviconRecord);
        var faviconUrl = usesDefaultFavicon
            ? definition.DefaultValue
            : faviconRecord!.Value.Trim();

        return new PublicSiteSettingsVo
        {
            VoSiteFaviconUrl = faviconUrl,
            VoUsingDefaultSiteFavicon = usesDefaultFavicon
        };
    }

    private async Task<Dictionary<string, SystemConfigRecord>> GetOverrideRecordsAsync()
    {
        var records = await _systemConfigRepository.GetAllAsync();
        return records
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.ModifyTime ?? item.CreateTime).First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<SystemConfigDefinition?> GetDefinitionByRequestIdAsync(long id)
    {
        var definition = SystemConfigDefaults.GetDefinitionById(id);
        if (definition != null)
        {
            return definition;
        }

        var record = await _systemConfigRepository.GetByIdAsync(id);
        return record == null ? null : SystemConfigDefaults.GetDefinitionByKey(record.Key);
    }

    private static void EnsureEditable(SystemConfigDefinition definition)
    {
        if (!definition.IsEditable)
        {
            throw new InvalidOperationException($"系统设置不允许在 Console 修改：{definition.Key}");
        }

        if (!definition.RiskLevel.Equals(SystemConfigRiskLevel.Low, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"当前仅允许修改低风险系统设置：{definition.Key}");
        }
    }

    private static string NormalizeAndValidateValue(SystemConfigDefinition definition, string value)
    {
        var trimmedValue = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmedValue))
        {
            throw new InvalidOperationException("系统设置值不能为空");
        }

        return definition.ValueType.ToLowerInvariant() switch
        {
            "number" => NormalizeNumberValue(trimmedValue),
            "boolean" => NormalizeBooleanValue(trimmedValue),
            "json" => NormalizeJsonValue(trimmedValue),
            _ => trimmedValue
        };
    }

    private static string NormalizeNumberValue(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            throw new InvalidOperationException("系统设置值必须是有效数字");
        }

        return number.ToString(CultureInfo.InvariantCulture);
    }

    private static string NormalizeBooleanValue(string value)
    {
        if (!bool.TryParse(value, out var boolValue))
        {
            throw new InvalidOperationException("系统设置值必须是 true 或 false");
        }

        return boolValue.ToString().ToLowerInvariant();
    }

    private static string NormalizeJsonValue(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return value;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("系统设置值必须是有效 JSON", ex);
        }
    }

    private static bool HasEnabledOverride(SystemConfigDefinition definition, SystemConfigRecord? record)
    {
        return record != null
            && record.IsEnabled
            && !string.IsNullOrWhiteSpace(record.Value)
            && !string.Equals(record.Value.Trim(), definition.DefaultValue, StringComparison.Ordinal);
    }

    private static SystemConfigVo MapToVo(SystemConfigDefinition definition, SystemConfigRecord? record)
    {
        var isOverridden = HasEnabledOverride(definition, record);
        var effectiveValue = isOverridden ? record!.Value.Trim() : definition.DefaultValue;

        return new SystemConfigVo
        {
            VoId = definition.Id,
            VoCategory = definition.Category,
            VoKey = definition.Key,
            VoName = definition.Name,
            VoValue = effectiveValue,
            VoDefaultValue = definition.DefaultValue,
            VoEffectiveValue = effectiveValue,
            VoDescription = definition.Description,
            VoType = definition.ValueType,
            VoIsEnabled = isOverridden,
            VoIsOverridden = isOverridden,
            VoRiskLevel = definition.RiskLevel,
            VoEffectiveMode = definition.EffectiveMode,
            VoIsEditable = definition.IsEditable,
            VoIsSensitive = definition.IsSensitive,
            VoCreateTime = isOverridden ? record?.CreateTime : null,
            VoModifyTime = isOverridden ? record?.ModifyTime : null
        };
    }
}
