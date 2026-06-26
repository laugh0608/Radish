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
    private readonly ISystemConfigChangeLogRepository _systemConfigChangeLogRepository;

    public SystemConfigService(
        ISystemConfigRepository systemConfigRepository,
        ISystemConfigChangeLogRepository systemConfigChangeLogRepository)
    {
        _systemConfigRepository = systemConfigRepository;
        _systemConfigChangeLogRepository = systemConfigChangeLogRepository;
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

    public async Task<SystemConfigVo?> UpdateConfigAsync(long id, UpdateSystemConfigDto request, SystemConfigChangeContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var definition = await GetDefinitionByRequestIdAsync(id);
        if (definition == null)
        {
            return null;
        }

        EnsureEditable(definition);
        EnsureChangeConfirmation(definition, request.Reason, request.ConfirmRiskLevel, request.ConfirmKey);

        var existedRecord = await _systemConfigRepository.GetByKeyAsync(definition.Key);
        var effectiveOverrideRecord = GetEffectiveOverrideRecord(definition, existedRecord);
        EnsureExpectedVersion(effectiveOverrideRecord, request.ExpectedVersion);
        var oldEffectiveValue = GetEffectiveValue(definition, existedRecord);

        if (!request.IsEnabled)
        {
            await _systemConfigRepository.DeleteByKeyAsync(definition.Key);
            await CreateChangeLogIfChangedAsync(
                definition,
                SystemConfigChangeAction.RestoreDefault,
                oldEffectiveValue,
                definition.DefaultValue,
                request.Reason,
                request.ConfirmRiskLevel,
                request.ConfirmKey,
                context);
            return MapToVo(definition, null);
        }

        var normalizedValue = SystemConfigValueNormalizer.NormalizeAndValidateValue(definition, request.Value);
        if (string.Equals(normalizedValue, definition.DefaultValue, StringComparison.Ordinal))
        {
            await _systemConfigRepository.DeleteByKeyAsync(definition.Key);
            await CreateChangeLogIfChangedAsync(
                definition,
                SystemConfigChangeAction.RestoreDefault,
                oldEffectiveValue,
                definition.DefaultValue,
                request.Reason,
                request.ConfirmRiskLevel,
                request.ConfirmKey,
                context);
            return MapToVo(definition, null);
        }

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
                Version = 1,
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
            existedRecord.Version = effectiveOverrideRecord == null
                ? 1
                : NormalizeRecordVersion(existedRecord) + 1;
            existedRecord.ModifyTime = now;

            updatedRecord = await _systemConfigRepository.UpdateAsync(existedRecord)
                ?? throw new InvalidOperationException("系统设置覆盖值更新失败");
        }

        await CreateChangeLogIfChangedAsync(
            definition,
            SystemConfigChangeAction.UpdateOverride,
            oldEffectiveValue,
            normalizedValue,
            request.Reason,
            request.ConfirmRiskLevel,
            request.ConfirmKey,
            context);

        return MapToVo(definition, updatedRecord);
    }

    public async Task<SystemConfigVo?> RestoreConfigDefaultAsync(long id, RestoreSystemConfigDefaultDto? request = null, SystemConfigChangeContext? context = null)
    {
        var definition = await GetDefinitionByRequestIdAsync(id);
        if (definition == null)
        {
            return null;
        }

        EnsureEditable(definition);
        EnsureChangeConfirmation(definition, request?.Reason, request?.ConfirmRiskLevel, request?.ConfirmKey);
        var existedRecord = await _systemConfigRepository.GetByKeyAsync(definition.Key);
        EnsureExpectedVersion(GetEffectiveOverrideRecord(definition, existedRecord), request?.ExpectedVersion ?? 0);
        var oldEffectiveValue = GetEffectiveValue(definition, existedRecord);
        await _systemConfigRepository.DeleteByKeyAsync(definition.Key);
        await CreateChangeLogIfChangedAsync(
            definition,
            SystemConfigChangeAction.RestoreDefault,
            oldEffectiveValue,
            definition.DefaultValue,
            request?.Reason,
            request?.ConfirmRiskLevel,
            request?.ConfirmKey,
            context);
        return MapToVo(definition, null);
    }

    public async Task<bool> DeleteConfigAsync(long id, SystemConfigChangeContext? context = null)
    {
        var restored = await RestoreConfigDefaultAsync(
            id,
            new RestoreSystemConfigDefaultDto { Reason = "通过兼容删除接口恢复默认" },
            context);
        return restored != null;
    }

    public async Task<List<SystemConfigChangeLogVo>?> GetConfigChangeLogsAsync(long id, int take = 20)
    {
        var definition = await GetDefinitionByRequestIdAsync(id);
        if (definition == null)
        {
            return null;
        }

        var records = await _systemConfigChangeLogRepository.GetByKeyAsync(definition.Key, take);
        return records.Select(MapChangeLogToVo).ToList();
    }

    public async Task<PublicSiteSettingsVo> GetPublicSiteSettingsAsync()
    {
        var definition = SystemConfigDefaults.GetDefinitionByKey(SystemConfigDefaults.SiteFaviconKey)
            ?? throw new InvalidOperationException($"系统设置定义不存在：{SystemConfigDefaults.SiteFaviconKey}");
        var faviconRecord = await _systemConfigRepository.GetByKeyAsync(definition.Key);
        var usesDefaultFavicon = !SystemConfigValueNormalizer.HasEnabledOverride(definition, faviconRecord);
        var faviconUrl = usesDefaultFavicon
            ? definition.DefaultValue
            : SystemConfigValueNormalizer.GetEffectiveValue(definition, faviconRecord);

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

        if (!definition.RiskLevel.Equals(SystemConfigRiskLevel.Low, StringComparison.OrdinalIgnoreCase) &&
            !definition.RiskLevel.Equals(SystemConfigRiskLevel.Medium, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"当前仅允许修改 Low / Medium 风险系统设置：{definition.Key}");
        }
    }

    private static void EnsureChangeConfirmation(
        SystemConfigDefinition definition,
        string? reason,
        string? confirmRiskLevel,
        string? confirmKey)
    {
        if (definition.RiskLevel.Equals(SystemConfigRiskLevel.Low, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.Equals(confirmRiskLevel?.Trim(), definition.RiskLevel, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"系统设置 {definition.Key} 需要确认风险等级 {definition.RiskLevel}");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException($"系统设置 {definition.Key} 需要填写修改原因");
        }

        if (!string.Equals(confirmKey?.Trim(), definition.Key, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"系统设置 {definition.Key} 需要确认设置键");
        }
    }

    private static string GetEffectiveValue(SystemConfigDefinition definition, SystemConfigRecord? record)
    {
        return SystemConfigValueNormalizer.GetEffectiveValue(definition, record);
    }

    private async Task CreateChangeLogIfChangedAsync(
        SystemConfigDefinition definition,
        string actionType,
        string oldValue,
        string newValue,
        string? reason,
        string? confirmRiskLevel,
        string? confirmKey,
        SystemConfigChangeContext? context)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return;
        }

        await _systemConfigChangeLogRepository.CreateAsync(new SystemConfigChangeLogRecord
        {
            Category = definition.Category,
            Key = definition.Key,
            Name = definition.Name,
            ActionType = actionType,
            OldValue = FormatAuditValue(definition, oldValue),
            NewValue = FormatAuditValue(definition, newValue),
            DefaultValue = FormatAuditValue(definition, definition.DefaultValue) ?? string.Empty,
            Reason = NormalizeChangeReason(reason, actionType),
            RiskLevel = definition.RiskLevel,
            EffectiveMode = definition.EffectiveMode,
            ConfirmRiskLevel = NormalizeAuditText(confirmRiskLevel, 40),
            ConfirmKey = NormalizeAuditText(confirmKey, 200),
            OperatorUserId = context?.OperatorUserId,
            OperatorUserName = NormalizeAuditText(context?.OperatorUserName, 100),
            RequestIp = NormalizeAuditText(context?.RequestIp, 100),
            UserAgent = NormalizeAuditText(context?.UserAgent, 500),
            CreateTime = DateTime.Now
        });
    }

    private static SystemConfigVo MapToVo(SystemConfigDefinition definition, SystemConfigRecord? record)
    {
        var isOverridden = SystemConfigValueNormalizer.HasEnabledOverride(definition, record);
        var effectiveValue = GetEffectiveValue(definition, record);

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
            VoImpactSummary = definition.ImpactSummary,
            VoType = definition.ValueType,
            VoMinNumberValue = definition.MinNumberValue,
            VoMaxNumberValue = definition.MaxNumberValue,
            VoRequiresInteger = definition.RequiresInteger,
            VoIsEnabled = isOverridden,
            VoIsOverridden = isOverridden,
            VoRiskLevel = definition.RiskLevel,
            VoEffectiveMode = definition.EffectiveMode,
            VoIsEditable = definition.IsEditable,
            VoIsSensitive = definition.IsSensitive,
            VoVersion = isOverridden ? NormalizeRecordVersion(record) : 0,
            VoCreateTime = isOverridden ? record?.CreateTime : null,
            VoModifyTime = isOverridden ? record?.ModifyTime : null
        };
    }

    private static void EnsureExpectedVersion(SystemConfigRecord? record, int expectedVersion)
    {
        var currentVersion = record == null ? 0 : NormalizeRecordVersion(record);
        if (currentVersion != expectedVersion)
        {
            throw new InvalidOperationException("系统设置已被其他管理员修改，请刷新后重试");
        }
    }

    private static int NormalizeRecordVersion(SystemConfigRecord? record)
    {
        return record == null ? 0 : Math.Max(1, record.Version);
    }

    private static SystemConfigRecord? GetEffectiveOverrideRecord(SystemConfigDefinition definition, SystemConfigRecord? record)
    {
        return SystemConfigValueNormalizer.HasEnabledOverride(definition, record) ? record : null;
    }

    private static SystemConfigChangeLogVo MapChangeLogToVo(SystemConfigChangeLogRecord record)
    {
        return new SystemConfigChangeLogVo
        {
            VoId = record.Id,
            VoCategory = record.Category,
            VoKey = record.Key,
            VoName = record.Name,
            VoActionType = record.ActionType,
            VoOldValue = record.OldValue,
            VoNewValue = record.NewValue,
            VoDefaultValue = record.DefaultValue,
            VoReason = record.Reason,
            VoRiskLevel = record.RiskLevel,
            VoEffectiveMode = record.EffectiveMode,
            VoConfirmRiskLevel = record.ConfirmRiskLevel,
            VoConfirmKey = record.ConfirmKey,
            VoOperatorUserId = record.OperatorUserId,
            VoOperatorUserName = record.OperatorUserName,
            VoRequestIp = record.RequestIp,
            VoUserAgent = record.UserAgent,
            VoCreateTime = record.CreateTime
        };
    }

    private static string NormalizeChangeReason(string? reason, string actionType)
    {
        var normalizedReason = NormalizeAuditText(reason, 500);
        if (!string.IsNullOrWhiteSpace(normalizedReason))
        {
            return normalizedReason;
        }

        return actionType.Equals(SystemConfigChangeAction.RestoreDefault, StringComparison.Ordinal)
            ? "恢复默认"
            : "未填写";
    }

    private static string? FormatAuditValue(SystemConfigDefinition definition, string? value)
    {
        if (value == null)
        {
            return null;
        }

        return definition.IsSensitive ? "***" : NormalizeAuditText(value, 2000);
    }

    private static string? NormalizeAuditText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmedValue = value.Trim();
        return trimmedValue.Length <= maxLength ? trimmedValue : trimmedValue[..maxLength];
    }
}
