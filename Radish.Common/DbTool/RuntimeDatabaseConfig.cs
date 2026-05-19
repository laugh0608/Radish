using Microsoft.Extensions.Configuration;

namespace Radish.Common.DbTool;

public sealed record RuntimeDatabaseConfig(DataBaseType DbType, string ConnectionString);

public static class RuntimeDatabaseConfigResolver
{
    public static RuntimeDatabaseConfig Resolve(
        IConfiguration configuration,
        string sectionPath,
        string? legacyConnectionString,
        string defaultSqliteFileName)
    {
        var section = configuration.GetSection(sectionPath);
        var dbType = ResolveDbType(section, legacyConnectionString);
        var configuredConnectionString = section.GetValue<string>("ConnectionString");
        var connectionString = string.IsNullOrWhiteSpace(configuredConnectionString)
            ? legacyConnectionString
            : configuredConnectionString;

        if (dbType == DataBaseType.Sqlite)
        {
            return new RuntimeDatabaseConfig(
                dbType,
                NormalizeSqliteConnectionString(connectionString, defaultSqliteFileName));
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"{sectionPath}:ConnectionString 不能为空。");
        }

        return new RuntimeDatabaseConfig(dbType, connectionString);
    }

    public static string ResolveSqliteFilePath(string connectionString, string defaultSqliteFileName)
    {
        var normalizedConnectionString = NormalizeSqliteConnectionString(connectionString, defaultSqliteFileName);
        var dataSource = ExtractSqliteDataSource(normalizedConnectionString);
        return Path.IsPathRooted(dataSource)
            ? dataSource
            : ResolveDataBasesPath(dataSource);
    }

    private static DataBaseType ResolveDbType(IConfigurationSection section, string? legacyConnectionString)
    {
        var configuredDbType = section.GetValue<string>("DbType");
        if (!string.IsNullOrWhiteSpace(configuredDbType))
        {
            if (int.TryParse(configuredDbType, out var dbTypeValue) &&
                Enum.IsDefined(typeof(DataBaseType), dbTypeValue))
            {
                return (DataBaseType)dbTypeValue;
            }

            if (Enum.TryParse<DataBaseType>(configuredDbType, ignoreCase: true, out var dbType))
            {
                return dbType;
            }

            throw new InvalidOperationException($"{section.Path}:DbType={configuredDbType} 不是受支持的数据库类型。");
        }

        return LooksLikePostgreSqlConnectionString(legacyConnectionString)
            ? DataBaseType.PostgreSql
            : DataBaseType.Sqlite;
    }

    private static string NormalizeSqliteConnectionString(string? connectionString, string defaultSqliteFileName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return $"Data Source={ResolveDataBasesPath(defaultSqliteFileName)}";
        }

        var dataSource = ExtractSqliteDataSource(connectionString);
        var absolutePath = Path.IsPathRooted(dataSource)
            ? dataSource
            : ResolveDataBasesPath(dataSource);

        return $"Data Source={absolutePath}";
    }

    private static string ExtractSqliteDataSource(string connectionString)
    {
        var segments = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            var separatorIndex = segment.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim();
            if (!key.Equals("Data Source", StringComparison.OrdinalIgnoreCase) &&
                !key.Equals("DataSource", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return segment[(separatorIndex + 1)..].Trim();
        }

        return connectionString.Trim();
    }

    private static string ResolveDataBasesPath(string fileNameOrRelativePath)
    {
        var solutionRoot = FindSolutionRoot();
        var normalizedPath = fileNameOrRelativePath.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var dbDirectory = Path.Combine(solutionRoot, "DataBases");
        Directory.CreateDirectory(dbDirectory);

        if (normalizedPath.StartsWith($"DataBases{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(solutionRoot, normalizedPath);
        }

        return Path.Combine(dbDirectory, normalizedPath);
    }

    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Radish.slnx")))
        {
            currentDir = currentDir.Parent;
        }

        return currentDir?.FullName ?? Environment.CurrentDirectory;
    }

    private static bool LooksLikePostgreSqlConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        return connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("Username=", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase);
    }
}
