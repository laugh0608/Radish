namespace Radish.Api.ErrorHandling;

/// <summary>
/// 将对外消息格式参数限制为可安全序列化的短标量。
/// </summary>
public static class ApiMessageArgumentNormalizer
{
    private const int MaxArgumentCount = 8;
    private const int MaxStringLength = 256;
    private const string UnsupportedValue = "[unsupported]";

    public static object[] Normalize(IEnumerable<object>? messageArguments)
    {
        if (messageArguments == null)
        {
            return Array.Empty<object>();
        }

        return messageArguments
            .Take(MaxArgumentCount)
            .Select(NormalizeValue)
            .ToArray();
    }

    private static object NormalizeValue(object? value)
    {
        return value switch
        {
            string text => NormalizeString(text),
            char character => NormalizeString(character.ToString()),
            bool boolean => boolean,
            byte number => number,
            sbyte number => number,
            short number => number,
            ushort number => number,
            int number => number,
            uint number => number,
            long number => number,
            ulong number => number,
            float number when float.IsFinite(number) => number,
            double number when double.IsFinite(number) => number,
            decimal number => number,
            _ => UnsupportedValue
        };
    }

    private static string NormalizeString(string value)
    {
        var normalized = string.Concat(value.Select(character =>
            char.IsControl(character) ? ' ' : character));
        return normalized.Length <= MaxStringLength
            ? normalized
            : normalized[..MaxStringLength];
    }
}
