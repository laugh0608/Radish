using SqlSugar;

namespace Radish.Extension.SqlSugarExtension;

public static class PostgreSqlDateTimeParameterNormalizer
{
    public static void Normalize(ConnectionConfig config, SugarParameter[]? parameters)
    {
        if (config.DbType != DbType.PostgreSQL || parameters == null || parameters.Length == 0)
        {
            return;
        }

        foreach (var parameter in parameters)
        {
            parameter.Value = NormalizeValue(parameter.Value);
        }
    }

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            DBNull _ => value,
            DateTime dateTime => NormalizeDateTime(dateTime),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime(),
            DateTime[] dateTimes => dateTimes.Select(NormalizeDateTime).ToArray(),
            DateTime?[] nullableDateTimes => nullableDateTimes
                .Select(item => item.HasValue ? NormalizeDateTime(item.Value) : item)
                .ToArray(),
            DateTimeOffset[] dateTimeOffsets => dateTimeOffsets.Select(item => item.ToUniversalTime()).ToArray(),
            DateTimeOffset?[] nullableDateTimeOffsets => nullableDateTimeOffsets
                .Select(item => item.HasValue ? item.Value.ToUniversalTime() : item)
                .ToArray(),
            object[] objectValues => objectValues.Select(NormalizeValue).ToArray(),
            IEnumerable<DateTime> dateTimeValues => dateTimeValues.Select(NormalizeDateTime).ToArray(),
            IEnumerable<DateTimeOffset> dateTimeOffsetValues => dateTimeOffsetValues
                .Select(item => item.ToUniversalTime())
                .ToArray(),
            _ => value
        };
    }

    private static DateTime NormalizeDateTime(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
