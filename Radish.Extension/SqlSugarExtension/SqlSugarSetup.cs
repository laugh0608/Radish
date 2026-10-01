using System.Collections.Concurrent;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Radish.Common.OptionTool;
using Radish.Common;
using Radish.Common.CacheTool;
using Radish.Common.CoreTool;
using Radish.Common.DbTool;
using Radish.Extension.AopExtension;
using Radish.Infrastructure.Tenant;
using Serilog;
using SqlSugar;

namespace Radish.Extension.SqlSugarExtension;

/// <summary>SqlSugar 启动服务</summary>
public static class SqlSugarSetup
{
    private const int SqliteBusyTimeoutMs = 60000;
    private static readonly Regex TableNameRegex = new(
        @"(?ix)
        (?:\bINSERT\s+INTO\b|\bFROM\b|\bUPDATE\b|\bDELETE\s+FROM\b)\s+
        (?:
            `(?<table>[^`]+)` |
            \[(?<table>[^\]]+)\] |
            ""(?<table>[^""]+)"" |
            (?<table>[A-Za-z_][A-Za-z0-9_\.]*)
        )",
        RegexOptions.Compiled);
    private static readonly ConcurrentDictionary<string, byte> InitializedSqliteWalDatabases =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConditionalWeakTable<IDbConnection, SqlitePragmaInitializationState>
        InitializedSqliteConnections = new();

    public static void AddSqlSugarSetup(this IServiceCollection services)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        services.AddLogging();

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
                DbType = (SqlSugar.DbType)m.DbType,
                IsAutoCloseConnection = true,
                MoreSettings = new ConnMoreSettings()
                {
                    IsAutoRemoveDataCache = true,
                    SqlServerCodeFirstNvarchar = true,
                    PgSqlIsAutoToLower = true,
                    PgSqlIsAutoToLowerCodeFirst = true,
                },
                // 通过自定义特性使用 SqlSugar 缓存服务
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

        EnsureSqliteProviderInitialized();

        if (BaseDbConfig.LogConfig is null)
        {
            throw new ApplicationException("未配置 Log 库连接");
        }

        // SqlSugarScope 是线程安全，可使用单例注入
        // 参考：https://www.donet5.com/Home/Doc?typeId=1181
        services.AddSingleton<ISqlSugarClient>(o =>
        {
            var loggingOptions = App.GetConfig<SqlAopLogOptions>() ?? new SqlAopLogOptions();
            if (loggingOptions.SlowQueryThresholdMs <= 0 || loggingOptions.SlowConnectionThresholdMs <= 0)
                throw new InvalidOperationException("SQL logging duration thresholds must be positive.");
            var diagnosticsEnabled = o.GetService<IHostEnvironment>()?.IsDevelopment() == true &&
                App.Configuration["RadishLogging:Mode"] == "Development" &&
                App.Configuration.GetValue<bool>("RadishLogging:Diagnostics");
            var sqlLogging = new SqlSugarAop(o.GetRequiredService<ILogger<SqlSugarAop>>(), loggingOptions, diagnosticsEnabled);
            return new SqlSugarScope(BaseDbConfig.AllConfigs, db =>
            {
                BaseDbConfig.AllConfigs.ForEach(config =>
                {
                    var configId = config.ConfigId?.ToString()
                        ?? throw new InvalidOperationException("数据库连接配置缺少 ConfigId");
                    var dbProvider = db.GetConnectionScope(configId);

                    // 只对非 Log 库配置实体数据权限（多租户）
                    if (!SqlSugarConst.LogConfigId.Equals(configId, StringComparison.OrdinalIgnoreCase))
                    {
                        RepositorySetting.SetTenantEntityFilter(dbProvider);
                    }

                    // PostgreSQL 时间参数规范化是持久化契约，不依赖 SQL 日志是否启用。
                    // Log 库仍不写 SQL 日志，避免 Serilog 数据库 Sink 递归。
                    dbProvider.Aop.OnLogExecuting = (s, parameters) =>
                    {
                        PostgreSqlDateTimeParameterNormalizer.Normalize(config, parameters);
                        if (diagnosticsEnabled && loggingOptions.Enabled &&
                            !SqlSugarConst.LogConfigId.Equals(configId, StringComparison.OrdinalIgnoreCase))
                        {
                            sqlLogging.Executing(ResolveSqlAopUser(), ExtractTableName(s),
                                ResolveOperateName(dbProvider), parameters?.Length ?? 0);
                        }
                    };

                    if (!SqlSugarConst.LogConfigId.Equals(configId, StringComparison.OrdinalIgnoreCase))
                    {
                        dbProvider.Aop.OnLogExecuted = (s, parameters) =>
                        {
                            var operate = ResolveOperateName(dbProvider);
                            if (string.Equals(operate, nameof(SugarActionType.Query), StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }

                            sqlLogging.Executed(operate, parameters?.Length ?? 0, dbProvider.Ado.SqlExecutionTime);
                        };

                        dbProvider.Aop.OnGetDataReadered = (s, parameters, elapsed) =>
                        {
                            sqlLogging.Executed(ResolveOperateName(dbProvider), parameters?.Length ?? 0, elapsed);
                        };

                        dbProvider.Aop.CheckConnectionExecuted = (connection, elapsed) =>
                        {
                            if (config.DbType == SqlSugar.DbType.Sqlite)
                            {
                                ApplySqlitePragmas(connection, config);
                            }

                            sqlLogging.ConnectionChecked(elapsed);
                        };

                        // 不在数据库回调记录异常；恢复 / 最终失败的调用边界拥有记录责任。
                    }
                });
            });
        });
    }

    private static void EnsureSqliteProviderInitialized()
    {
        if (BaseDbConfig.AllConfigs.Any(config => config.DbType == SqlSugar.DbType.Sqlite))
        {
            SQLitePCL.Batteries_V2.Init();
        }
    }

    /// <summary>
    /// 解析 SQL AOP 日志中的操作人
    /// </summary>
    /// <remarks>
    /// - 有登录上下文：返回真实用户名
    /// - 有 HTTP 请求但未登录：返回 Anonymous
    /// - 无 HTTP 上下文（如 Hangfire/后台任务）：返回 System
    /// </remarks>
    private static string ResolveSqlAopUser()
    {
        try
        {
            var userName = App.CurrentUser.UserName;
            if (!string.IsNullOrWhiteSpace(userName))
            {
                return userName;
            }

            return App.HttpContext == null ? "System" : "Anonymous";
        }
        catch (ObjectDisposedException)
        {
            return "System";
        }
    }

    private static string ResolveOperateName(ISqlSugarClient dbProvider)
    {
        return Enum.GetName(typeof(SugarActionType), dbProvider.SugarActionType) ?? "Unknown";
    }

    private static void ApplySqlitePragmas(IDbConnection connection, ConnectionConfig config)
    {
        if (connection.State != ConnectionState.Open)
        {
            return;
        }

        var initializationState = InitializedSqliteConnections.GetValue(
            connection,
            static _ => new SqlitePragmaInitializationState());
        lock (initializationState)
        {
            if (initializationState.IsInitialized)
            {
                return;
            }

            if (!ExecuteSqlitePragma(
                    connection,
                    $"PRAGMA busy_timeout = {SqliteBusyTimeoutMs};",
                    expectScalarResult: false) ||
                !ExecuteSqlitePragma(
                    connection,
                    "PRAGMA synchronous = NORMAL;",
                    expectScalarResult: false))
            {
                return;
            }

            var sqliteWalKey = $"{config.ConfigId}:{connection.ConnectionString}";
            if (!InitializedSqliteWalDatabases.ContainsKey(sqliteWalKey))
            {
                if (!ExecuteSqlitePragma(connection, "PRAGMA journal_mode = WAL;", expectScalarResult: true))
                {
                    return;
                }

                InitializedSqliteWalDatabases.TryAdd(sqliteWalKey, 0);
            }

            initializationState.IsInitialized = true;
        }
    }

    private static bool ExecuteSqlitePragma(IDbConnection connection, string sql, bool expectScalarResult)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = Math.Max(SqliteBusyTimeoutMs / 1000, 1);

            if (expectScalarResult)
            {
                _ = command.ExecuteScalar();
            }
            else
            {
                _ = command.ExecuteNonQuery();
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[SqlSugar] SQLite PRAGMA 执行失败，Sql: {Sql}", sql);
            return false;
        }
    }

    private static string ExtractTableName(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return string.Empty;
        }

        var match = TableNameRegex.Match(sql);
        if (!match.Success)
        {
            return string.Empty;
        }

        var rawTableName = match.Groups["table"].Value.Trim();
        if (string.IsNullOrWhiteSpace(rawTableName))
        {
            return string.Empty;
        }

        var normalizedTableName = rawTableName.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return normalizedTableName?.Trim('`', '"', '[', ']') ?? string.Empty;
    }

    private sealed class SqlitePragmaInitializationState
    {
        public bool IsInitialized { get; set; }
    }
}
