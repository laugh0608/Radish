using System.Text.Json;

namespace Radish.Model;

/// <summary>
/// PublicIndex 自动分配时需要跳过的保留号策略。
/// </summary>
public sealed class PublicIndexReservationPolicy
{
    private static readonly HashSet<string> SupportedVanityRuleKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "repeatedDigits",
        "ascendingSequence",
        "descendingSequence",
        "palindrome"
    };

    private readonly HashSet<long> _reservedIndexes;

    private PublicIndexReservationPolicy(
        IEnumerable<long> reservedIndexes,
        PublicIndexVanityRules vanityRules)
    {
        _reservedIndexes = reservedIndexes.ToHashSet();
        VanityRules = vanityRules;
    }

    public static PublicIndexReservationPolicy Empty { get; } =
        new(Array.Empty<long>(), PublicIndexVanityRules.Disabled);

    public IReadOnlySet<long> ReservedIndexes => _reservedIndexes;

    public PublicIndexVanityRules VanityRules { get; }

    public static PublicIndexReservationPolicy FromSettings(
        string reservedIndexesValue,
        string vanityRulesValue)
    {
        return new PublicIndexReservationPolicy(
            ParseReservedIndexes(reservedIndexesValue),
            ParseVanityRules(vanityRulesValue));
    }

    public bool ShouldReserve(long publicIndex)
    {
        if (publicIndex < User.PublicIndexStart)
        {
            return false;
        }

        return _reservedIndexes.Contains(publicIndex) || VanityRules.Matches(publicIndex);
    }

    public long FindNextAvailableAfter(long baseline)
    {
        if (baseline == long.MaxValue)
        {
            throw new InvalidOperationException("PublicIndex 可分配空间已耗尽");
        }

        var candidate = Math.Max(User.PublicIndexStart, baseline + 1);
        while (ShouldReserve(candidate))
        {
            if (candidate == long.MaxValue)
            {
                throw new InvalidOperationException("PublicIndex 可分配空间已耗尽");
            }

            candidate++;
        }

        return candidate;
    }

    private static IReadOnlyCollection<long> ParseReservedIndexes(string value)
    {
        using var document = ParseJson(value, "PublicIndex 显式保留列表必须是有效 JSON");
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("PublicIndex 显式保留列表必须是 JSON 数组");
        }

        var indexes = new HashSet<long>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var publicIndex = ReadPublicIndex(item);
            if (publicIndex < User.PublicIndexStart)
            {
                throw new InvalidOperationException($"PublicIndex 保留号不能小于 {User.PublicIndexStart}：{publicIndex}");
            }

            if (!indexes.Add(publicIndex))
            {
                throw new InvalidOperationException($"PublicIndex 保留号存在重复值：{publicIndex}");
            }
        }

        return indexes;
    }

    private static PublicIndexVanityRules ParseVanityRules(string value)
    {
        using var document = ParseJson(value, "PublicIndex 靓号规则必须是有效 JSON");
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("PublicIndex 靓号规则必须是 JSON 对象");
        }

        var repeatedDigits = false;
        var ascendingSequence = false;
        var descendingSequence = false;
        var palindrome = false;

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!SupportedVanityRuleKeys.Contains(property.Name))
            {
                throw new InvalidOperationException($"PublicIndex 靓号规则不支持：{property.Name}");
            }

            if (property.Value.ValueKind != JsonValueKind.True &&
                property.Value.ValueKind != JsonValueKind.False)
            {
                throw new InvalidOperationException($"PublicIndex 靓号规则 {property.Name} 必须是布尔值");
            }

            var enabled = property.Value.GetBoolean();
            if (property.Name.Equals("repeatedDigits", StringComparison.OrdinalIgnoreCase))
            {
                repeatedDigits = enabled;
            }
            else if (property.Name.Equals("ascendingSequence", StringComparison.OrdinalIgnoreCase))
            {
                ascendingSequence = enabled;
            }
            else if (property.Name.Equals("descendingSequence", StringComparison.OrdinalIgnoreCase))
            {
                descendingSequence = enabled;
            }
            else if (property.Name.Equals("palindrome", StringComparison.OrdinalIgnoreCase))
            {
                palindrome = enabled;
            }
        }

        return new PublicIndexVanityRules(
            repeatedDigits,
            ascendingSequence,
            descendingSequence,
            palindrome);
    }

    private static JsonDocument ParseJson(string value, string errorMessage)
    {
        try
        {
            return JsonDocument.Parse(value);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(errorMessage, ex);
        }
    }

    private static long ReadPublicIndex(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.Number)
        {
            if (item.TryGetInt64(out var publicIndex))
            {
                return publicIndex;
            }

            throw new InvalidOperationException("PublicIndex 保留号超出 Int64 范围");
        }

        if (item.ValueKind == JsonValueKind.String &&
            long.TryParse(item.GetString(), out var stringPublicIndex))
        {
            return stringPublicIndex;
        }

        throw new InvalidOperationException("PublicIndex 保留号必须是整数或整数字符串");
    }
}

public sealed record PublicIndexVanityRules(
    bool RepeatedDigits,
    bool AscendingSequence,
    bool DescendingSequence,
    bool Palindrome)
{
    public static PublicIndexVanityRules Disabled { get; } = new(false, false, false, false);

    public bool Matches(long publicIndex)
    {
        var value = publicIndex.ToString();
        return (RepeatedDigits && IsRepeatedDigits(value)) ||
               (AscendingSequence && IsConsecutiveSequence(value, step: 1)) ||
               (DescendingSequence && IsConsecutiveSequence(value, step: -1)) ||
               (Palindrome && IsPalindrome(value));
    }

    private static bool IsRepeatedDigits(string value)
    {
        return value.Length >= 4 && value.All(item => item == value[0]);
    }

    private static bool IsConsecutiveSequence(string value, int step)
    {
        if (value.Length < 4)
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (value[index] - value[index - 1] != step)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPalindrome(string value)
    {
        if (value.Length < 4)
        {
            return false;
        }

        for (var index = 0; index < value.Length / 2; index++)
        {
            if (value[index] != value[value.Length - index - 1])
            {
                return false;
            }
        }

        return true;
    }
}
