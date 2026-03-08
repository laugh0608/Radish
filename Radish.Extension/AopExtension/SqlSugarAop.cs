using Radish.Common.LogTool;
using Serilog;
using SqlSugar;
using System.Globalization;
using System.Text;

namespace Radish.Extension.AopExtension;

public class SqlSugarAop
{
    private const int ParameterPreviewLimit = 120;

    public static void OnLogExecuting(ISqlSugarClient sqlSugarScopeProvider, string user, string table, string operate,
        string sql, SugarParameter[] p, ConnectionConfig config)
    {
        try
        {
            var formattedParameters = FormatParameters(p);
            var logConsole =
                $"------------------ \r\n User:[{user}]  Table:[{table}]  Operate:[{operate}] " +
                $"ConnId:[{config.ConfigId}]【SQL模板】: " +
                $"\r\n {sql}" +
                $"\r\n【SQL参数】: {formattedParameters}";
            // 将日志输出到控制台
            // Console.WriteLine(logConsole);
            // 将日志输出到 Serilog 扩展中，通过 LogConfigExtension 来统一配置，所以上面的输出控制台就不需要了，不然会重复输出
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

    private static string FormatParameters(SugarParameter[] parameters)
    {
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
            builder.Append(FormatParameterValue(parameter.Value));
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static string FormatParameterValue(object? value)
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
            return FormatStringValue(text);
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string FormatStringValue(string value)
    {
        var normalized = value.Replace("\r", "\\r").Replace("\n", "\\n");
        if (normalized.Length <= ParameterPreviewLimit)
        {
            return normalized;
        }

        return $"<text len={normalized.Length} preview=\"{normalized[..ParameterPreviewLimit]}...\">";
    }
}
