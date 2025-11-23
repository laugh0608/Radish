using Radish.Common.LogTool;
using Serilog;
using SqlSugar;

namespace Radish.Extension.AopExtension;

public class SqlSugarAop
{
    public static void OnLogExecuting(ISqlSugarClient sqlSugarScopeProvider, string user, string table, string operate,
        string sql, SugarParameter[] p, ConnectionConfig config)
    {
        try
        {
            var logConsole = string.Format(
                $"------------------ \r\n User:[{user}]  Table:[{table}]  Operate:[{operate}] " +
                $"ConnId:[{config.ConfigId}]【SQL语句】: " +
                $"\r\n {UtilMethods.GetNativeSql(sql, p)}");
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
}