namespace Radish.Common.DbTool;

/// <summary>正式数据库 provider 边界；枚举中的历史值不代表运行时支持。</summary>
public static class DatabaseProviderPolicy
{
    public static void EnsureSupported(DataBaseType dbType, string configurationPath)
    {
        if (dbType is not (DataBaseType.Sqlite or DataBaseType.PostgreSql))
        {
            throw new InvalidOperationException(
                $"{configurationPath}:DbType={dbType} 不受支持；仅支持 Sqlite (2) 和 PostgreSql (4)。");
        }
    }
}
