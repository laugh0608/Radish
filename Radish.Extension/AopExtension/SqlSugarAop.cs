using Radish.Common.CoreTool;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Serilog;
using SqlSugar;
using System.Globalization;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace Radish.Extension.AopExtension;

public class SqlSugarAop
{
    private const int ParameterPreviewLimit = 120;
    private const int SlowSqlThresholdMs = 1000;
    private const int SlowConnectionThresholdMs = 500;

    private static readonly HashSet<string> DefaultOmittedFieldNames =
    [
        "MarkdownContent",
        "Content",
        "Body",
        "HtmlContent",
        "RequestBody",
        "ResponseBody",
        "OldContent",
        "NewContent",
        "ContentSnapshot"
    ];

    private static readonly Regex InlineAssignmentRegex = new(
        "(?<column>(?:`|\"|\\[)?(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:`|\"|\\])?)\\s*=\\s*'(?<value>(?:''|[^'])*)'",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static void OnLogExecuting(ISqlSugarClient sqlSugarScopeProvider, string user, string table, string operate,
        string sql, SugarParameter[] p, ConnectionConfig config)
    {
        try
        {
            var options = ResolveOptions();
            if (!ShouldLog(options, user, table, operate))
            {
                return;
            }

            var sanitizedSql = SanitizeSqlText(sql, options);
            var formattedParameters = FormatParameters(p, options);
            var logConsole =
                $"------------------ \r\n User:[{user}]  Table:[{table}]  Operate:[{operate}] " +
                $"ConnId:[{config.ConfigId}]【SQL模板】: " +
                $"\r\n {sanitizedSql}" +
                $"\r\n【SQL参数】: {formattedParameters}";

            using (LogContextTool.Create.SqlAopPushProperty(sqlSugarScopeProvider))
            {
                Log.Information(logConsole);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error occured OnLogExecuting:" + e);
        }
    }

    public static void OnQueryExecuted(ISqlSugarClient sqlSugarScopeProvider, string user, string table, string operate,
        string sql, SugarParameter[] parameters, ConnectionConfig config, TimeSpan elapsed)
    {
        try
        {
            var options = ResolveOptions();
            if (!ShouldLog(options, user, table, operate) || elapsed.TotalMilliseconds < SlowSqlThresholdMs)
            {
                return;
            }

            using (LogContextTool.Create.SqlAopPushProperty(sqlSugarScopeProvider))
            {
                Log.Warning(
                    "[SqlSugar] 检测到慢查询，ConnId: {ConnId}, User: {User}, Table: {Table}, Operate: {Operate}, ElapsedMs: {ElapsedMs}, Sql: {Sql}, Params: {Params}",
                    config.ConfigId,
                    user,
                    table,
                    operate,
                    elapsed.TotalMilliseconds,
                    SanitizeSqlText(sql, options),
                    FormatParameters(parameters, options));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error occured OnQueryExecuted:" + e);
        }
    }

    public static void OnCommandExecuted(ISqlSugarClient sqlSugarScopeProvider, string user, string table, string operate,
        string sql, SugarParameter[] parameters, ConnectionConfig config, TimeSpan elapsed)
    {
        try
        {
            var options = ResolveOptions();
            if (!ShouldLog(options, user, table, operate) || elapsed.TotalMilliseconds < SlowSqlThresholdMs)
            {
                return;
            }

            using (LogContextTool.Create.SqlAopPushProperty(sqlSugarScopeProvider))
            {
                Log.Warning(
                    "[SqlSugar] 检测到慢命令，ConnId: {ConnId}, User: {User}, Table: {Table}, Operate: {Operate}, ElapsedMs: {ElapsedMs}, Sql: {Sql}, Params: {Params}",
                    config.ConfigId,
                    user,
                    table,
                    operate,
                    elapsed.TotalMilliseconds,
                    SanitizeSqlText(sql, options),
                    FormatParameters(parameters, options));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error occured OnCommandExecuted:" + e);
        }
    }

    public static void OnConnectionChecked(ConnectionConfig config, IDbConnection connection, TimeSpan elapsed)
    {
        try
        {
            if (elapsed.TotalMilliseconds < SlowConnectionThresholdMs)
            {
                return;
            }

            Log.Warning(
                "[SqlSugar] 检测到慢连接检查，ConnId: {ConnId}, DbType: {DbType}, Database: {Database}, ElapsedMs: {ElapsedMs}",
                config.ConfigId,
                config.DbType,
                connection.Database,
                elapsed.TotalMilliseconds);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error occured OnConnectionChecked:" + e);
        }
    }

    private static string FormatParameters(SugarParameter[] parameters, SqlAopLogOptions? options = null)
    {
        options ??= ResolveOptions();

        if (parameters == null || parameters.Length == 0)
        {
            return "[]";
        }

        var builder = new StringBuilder("[");
        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(parameter.ParameterName);
            builder.Append('=');
            builder.Append(FormatParameterValue(parameter.ParameterName, parameter.Value, options));
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static string FormatParameterValue(string? parameterName, object? value, SqlAopLogOptions options)
    {
        if (value == null || value == DBNull.Value)
        {
            return "null";
        }

        if (value is byte[] bytes)
        {
            return $"<byte[{bytes.Length}]>";
        }

        if (value is string text)
        {
            return FormatStringValue(parameterName, text, options);
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string FormatStringValue(string? parameterName, string value, SqlAopLogOptions options)
    {
        var normalized = value.Replace("\r", "\\r").Replace("\n", "\\n");
        if (ShouldOmitText(parameterName, normalized.Length, options))
        {
            return CreateOmittedTextPlaceholder(normalized.Length);
        }

        if (normalized.Length <= ParameterPreviewLimit)
        {
            return normalized;
        }

        return $"<text len={normalized.Length} preview=\"{normalized[..ParameterPreviewLimit]}...\">";
    }

    private static string SanitizeSqlText(string sql, SqlAopLogOptions? options = null)
    {
        options ??= ResolveOptions();
        if (string.IsNullOrWhiteSpace(sql))
        {
            return sql;
        }

        var sanitizedAssignments = InlineAssignmentRegex.Replace(sql, match =>
        {
            var fieldName = match.Groups["name"].Value;
            var value = match.Groups["value"].Value.Replace("''", "'");
            if (!ShouldOmitText(fieldName, value.Length, options))
            {
                return match.Value;
            }

            return $"{match.Groups["column"].Value}='{CreateOmittedTextPlaceholder(value.Length)}'";
        });

        if (!options.OmitLargeText)
        {
            return sanitizedAssignments;
        }

        return ReplaceLargeStringLiterals(sanitizedAssignments, GetLargeTextThreshold(options));
    }

    private static string ReplaceLargeStringLiterals(string sql, int largeTextThreshold)
    {
        var builder = new StringBuilder(sql.Length);

        for (var index = 0; index < sql.Length; index++)
        {
            if (sql[index] != '\'')
            {
                builder.Append(sql[index]);
                continue;
            }

            var start = index;
            var literalLength = 0;
            index++;

            while (index < sql.Length)
            {
                if (sql[index] == '\'')
                {
                    if (index + 1 < sql.Length && sql[index + 1] == '\'')
                    {
                        literalLength++;
                        index += 2;
                        continue;
                    }

                    break;
                }

                literalLength++;
                index++;
            }

            var end = index < sql.Length ? index : sql.Length - 1;
            var literal = sql[start..(end + 1)];
            if (literalLength > largeTextThreshold)
            {
                builder.Append('\'');
                builder.Append(CreateOmittedTextPlaceholder(literalLength));
                builder.Append('\'');
                continue;
            }

            builder.Append(literal);
        }

        return builder.ToString();
    }

    private static bool ShouldLog(SqlAopLogOptions? options, string? user, string? table, string? operate)
    {
        options ??= ResolveOptions();
        if (!options.Enabled)
        {
            return false;
        }

        if (ContainsIgnoreCase(options.SkipUsers, user) || ContainsIgnoreCase(options.SkipTables, table))
        {
            return false;
        }

        return operate?.Trim().ToUpperInvariant() switch
        {
            "QUERY" => options.LogQuery,
            "INSERT" => options.LogInsert,
            "UPDATE" => options.LogUpdate,
            "DELETE" => options.LogDelete,
            _ => true
        };
    }

    private static bool ShouldOmitText(string? name, int textLength, SqlAopLogOptions options)
    {
        var normalizedName = NormalizeParameterName(name);
        if (!string.IsNullOrWhiteSpace(normalizedName) && IsOmittedField(options, normalizedName))
        {
            return true;
        }

        return options.OmitLargeText && textLength > GetLargeTextThreshold(options);
    }

    private static bool IsOmittedField(SqlAopLogOptions options, string normalizedName)
    {
        if (DefaultOmittedFieldNames.Contains(normalizedName))
        {
            return true;
        }

        return ContainsIgnoreCase(options.OmittedFields, normalizedName);
    }

    private static bool ContainsIgnoreCase(IEnumerable<string>? values, string? target)
    {
        if (values == null || string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        return values.Any(value => string.Equals(value?.Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static int GetLargeTextThreshold(SqlAopLogOptions options)
    {
        return options.LargeTextThreshold > 0 ? options.LargeTextThreshold : 256;
    }

    private static string NormalizeParameterName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var normalized = name.Trim().TrimStart('@', ':', '?', '$');
        return Regex.Replace(normalized, "\\d+$", string.Empty);
    }

    private static string CreateOmittedTextPlaceholder(int length)
    {
        return $"<text len={length} omitted>";
    }

    private static SqlAopLogOptions ResolveOptions()
    {
        return App.GetConfig<SqlAopLogOptions>() ?? new SqlAopLogOptions();
    }
}
