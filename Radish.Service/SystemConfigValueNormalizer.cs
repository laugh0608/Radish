using System.Globalization;
using System.Text.Json;
using Radish.Model;

namespace Radish.Service;

internal static class SystemConfigValueNormalizer
{
    public static string NormalizeAndValidateValue(SystemConfigDefinition definition, string value)
    {
        var trimmedValue = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmedValue))
        {
            throw new InvalidOperationException("系统设置值不能为空");
        }

        return definition.ValueType.ToLowerInvariant() switch
        {
            "number" => NormalizeNumberValue(definition, trimmedValue),
            "boolean" => NormalizeBooleanValue(trimmedValue),
            "json" => NormalizeJsonValue(trimmedValue),
            _ => trimmedValue
        };
    }

    public static bool HasEnabledOverride(SystemConfigDefinition definition, SystemConfigRecord? record)
    {
        return record != null
            && record.IsEnabled
            && !string.IsNullOrWhiteSpace(record.Value)
            && !string.Equals(record.Value.Trim(), definition.DefaultValue, StringComparison.Ordinal);
    }

    public static string GetEffectiveValue(SystemConfigDefinition definition, SystemConfigRecord? record)
    {
        return HasEnabledOverride(definition, record) ? record!.Value.Trim() : definition.DefaultValue;
    }

    private static string NormalizeNumberValue(SystemConfigDefinition definition, string value)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            throw new InvalidOperationException("系统设置值必须是有效数字");
        }

        if (definition.RequiresInteger && decimal.Truncate(number) != number)
        {
            throw new InvalidOperationException("系统设置值必须是整数");
        }

        if (definition.MinNumberValue.HasValue && number < definition.MinNumberValue.Value)
        {
            throw new InvalidOperationException($"系统设置值不能小于 {FormatNumber(definition.MinNumberValue.Value)}");
        }

        if (definition.MaxNumberValue.HasValue && number > definition.MaxNumberValue.Value)
        {
            throw new InvalidOperationException($"系统设置值不能大于 {FormatNumber(definition.MaxNumberValue.Value)}");
        }

        return definition.RequiresInteger
            ? decimal.Truncate(number).ToString("0", CultureInfo.InvariantCulture)
            : number.ToString(CultureInfo.InvariantCulture);
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

    private static string FormatNumber(decimal value)
    {
        return value.ToString("0.#############################", CultureInfo.InvariantCulture);
    }
}
