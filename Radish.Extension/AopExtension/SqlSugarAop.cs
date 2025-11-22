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
            Console.WriteLine(logConsole);
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