using System.Globalization;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;

namespace Radish.Service;

/// <summary>
/// 系统设置统一读取入口
/// </summary>
public class SystemSettingProvider : ISystemSettingProvider
{
    private readonly ISystemConfigRepository _systemConfigRepository;

    public SystemSettingProvider(ISystemConfigRepository systemConfigRepository)
    {
        _systemConfigRepository = systemConfigRepository;
    }

    public async Task<string> GetEffectiveValueAsync(string key)
    {
        var definition = GetDefinitionOrThrow(key);
        var record = await _systemConfigRepository.GetByKeyAsync(definition.Key);
        var effectiveValue = SystemConfigValueNormalizer.GetEffectiveValue(definition, record);
        return SystemConfigValueNormalizer.NormalizeAndValidateValue(definition, effectiveValue);
    }

    public async Task<int> GetInt32Async(string key)
    {
        var definition = GetDefinitionOrThrow(key);
        if (!definition.ValueType.Equals("number", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"系统设置不是数字类型：{definition.Key}");
        }

        var normalizedValue = await GetEffectiveValueAsync(definition.Key);
        if (!int.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException($"系统设置值超出 Int32 范围：{definition.Key}");
        }

        return value;
    }

    private static SystemConfigDefinition GetDefinitionOrThrow(string key)
    {
        return SystemConfigDefaults.GetDefinitionByKey(key)
            ?? throw new InvalidOperationException($"系统设置定义不存在：{key}");
    }
}
