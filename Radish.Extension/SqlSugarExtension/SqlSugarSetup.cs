using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common;
using Radish.Common.CacheTool;
using Radish.Common.CoreTool;
using Radish.Common.DbTool;
using Radish.Infrastructure.Tenant;
using SqlSugar;

namespace Radish.Extension.SqlSugarExtension;

/// <summary>SqlSugar 启动服务</summary>
public static class SqlSugarSetup
{
    public static void AddSqlSugarSetup(this IServiceCollection services)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        // 默认添加主数据库连接
        if (!string.IsNullOrEmpty(AppSettingsTool.RadishApp("MainDb")))
        {
            MainDb.CurrentDbConnId = AppSettingsTool.RadishApp("MainDb");
        }

        BaseDbConfig.MutiConnectionString.allDbs.ForEach(m =>
        {
            var config = new ConnectionConfig()
            {
                ConfigId = m.ConnId.ObjToString().ToLower(),
                ConnectionString = m.ConnectionString,
                DbType = (DbType)m.DbType,
                IsAutoCloseConnection = true,
                MoreSettings = new ConnMoreSettings()
                {
                    IsAutoRemoveDataCache = true,
                    SqlServerCodeFirstNvarchar = true,
                },
                // 通过自定义特性识别数据库链接配置
                ConfigureExternalServices = new ConfigureExternalServices()
                {
                    DataInfoCacheService = new SqlSugarCache(),
                },
                InitKeyType = InitKeyType.Attribute
            };
            if (SqlSugarConst.LogConfigId.ToLower().Equals(m.ConnId.ToLower()))
            {
                BaseDbConfig.LogConfig = config;
            }
            else
            {
                BaseDbConfig.ValidConfig.Add(config);
            }

            BaseDbConfig.AllConfigs.Add(config);
        });

        if (BaseDbConfig.LogConfig is null)
        {
            throw new ApplicationException("未配置 Log 库连接");
        }

        // SqlSugarScope 是线程安全，可使用单例注入
        // 参考：https://www.donet5.com/Home/Doc?typeId=1181
        services.AddSingleton<ISqlSugarClient>(o =>
        {
            // return new SqlSugarScope(BaseDbConfig.AllConfigs);
            return new SqlSugarScope(BaseDbConfig.AllConfigs, db =>
            {
                BaseDbConfig.ValidConfig.ForEach(config =>
                {
                    var dbProvider = db.GetConnectionScope((string)config.ConfigId);
                    // 配置实体数据权限（多租户）
                    RepositorySetting.SetTenantEntityFilter(dbProvider);
            
                    // 打印 SQL 语句
                    // dbProvider.Aop.OnLogExecuting = (s, parameters) =>
                    // {
                    //     SqlSugarAop.OnLogExecuting(dbProvider, App.User?.Name.ObjToString(), ExtractTableName(s),
                    //         Enum.GetName(typeof(SugarActionType), dbProvider.SugarActionType), s, parameters,
                    //         config);
                    // };
                });
            });
        });
    }

    private static string ExtractTableName(string sql)
    {
        // 匹配 SQL 语句中的表名的正则表达式
        // string regexPattern = @"\s*(?:UPDATE|DELETE\s+FROM|SELECT\s+\*\s+FROM)\s+(\w+)";
        string regexPattern = @"(?i)(?:FROM|UPDATE|DELETE\s+FROM)\s+`(.+?)`";
        Regex regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
        Match match = regex.Match(sql);

        return match.Success ?
            // 提取匹配到的表名
            match.Groups[1].Value :
            // 如果没有匹配到表名，则返回空字符串或者抛出异常等处理
            string.Empty;
    }
}
