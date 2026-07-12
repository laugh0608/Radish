using System;
using System.Linq;
using Radish.Extension.SqlSugarExtension;
using SqlSugar;

namespace Radish.Api.Tests.Repositories;

internal static class PostgreSqlIntegrationSqlSugarFactory
{
    public static SqlSugarClient CreateClient(ConnectionConfig config)
    {
        EnsurePostgreSql(config);
        var db = new SqlSugarClient(config);
        ConfigureProvider(db, config);
        return db;
    }

    public static SqlSugarScope CreateScope(params ConnectionConfig[] configs)
    {
        foreach (var config in configs)
        {
            EnsurePostgreSql(config);
        }

        return new SqlSugarScope(configs.ToList(), db =>
        {
            foreach (var config in configs)
            {
                var configId = config.ConfigId?.ToString()
                    ?? throw new InvalidOperationException("PostgreSQL 集成测试连接缺少 ConfigId。");
                ConfigureProvider(db.GetConnectionScope(configId), config);
            }
        });
    }

    private static void ConfigureProvider(ISqlSugarClient db, ConnectionConfig config)
    {
        db.Aop.OnLogExecuting = (_, parameters) =>
            PostgreSqlDateTimeParameterNormalizer.Normalize(config, parameters);
    }

    private static void EnsurePostgreSql(ConnectionConfig config)
    {
        if (config.DbType != DbType.PostgreSQL)
        {
            throw new ArgumentException("该工厂只允许创建 PostgreSQL 集成测试连接。", nameof(config));
        }
    }
}
