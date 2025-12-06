using Serilog;
using SqlSugar;

namespace Radish.Common.DbTool;

public class BaseDbConfig
{
    /// <summary>所有库配置</summary>
    public static readonly List<ConnectionConfig> AllConfigs = new();

    /// <summary>有效的库连接(排除 Log 库)</summary>
    public static List<ConnectionConfig> ValidConfig = new();

    public static ConnectionConfig MainConfig;
    public static ConnectionConfig LogConfig; // Log 日志库

    public static bool IsMulti => ValidConfig.Count > 1;

    /**
     * @laugh0608 20251116
     * 目前是多库操作，默认加载的是 appsettings.json 设置为 true 的第一个 Db 连接
     *
     * 直接在单个配置中可以配置从库
     *
     * 增加主库备用连接，配置方式为 ConfigId 为主库的 ConfigId+随便数字，只要不重复就好
     *
     * 主库在无法连接后会自动切换到备用链接
     */
    public static (List<MutiDbOperate> allDbs, List<MutiDbOperate> slaveDbs) MutiConnectionString => MutiInitConn();

    public static (List<MutiDbOperate>, List<MutiDbOperate>) MutiInitConn()
    {
        List<MutiDbOperate> listDatabase = AppSettingsTool.RadishApp<MutiDbOperate>("Databases")
            .Where(i => i.Enabled).ToList();
        var mainDbId = AppSettingsTool.RadishApp(new string[] { "MainDb" }).ToString();
        var mainDbModel = listDatabase.Single(d => d.ConnId == mainDbId);
        listDatabase.Remove(mainDbModel);
        listDatabase.Insert(0, mainDbModel);

        foreach (var i in listDatabase) SpecialDbString(i);
        return (listDatabase, mainDbModel.Slaves);
    }

    /// <summary>
    /// 查找解决方案根目录（包含 Radish.slnx 的目录）
    /// </summary>
    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Radish.slnx")))
        {
            currentDir = currentDir.Parent;
        }
        return currentDir?.FullName ?? Environment.CurrentDirectory;
    }

    private static string DifDbConnOfSecurity(params string[] conn)
    {
        foreach (var item in conn)
        {
            try
            {
                if (File.Exists(item))
                {
                    return File.ReadAllText(item).Trim();
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception.Message);
            }
        }

        return conn[conn.Length - 1];
    }

    /// <summary>定制 Db 连接字符串</summary>
    /// <remarks>目的是保证安全：优先从本地 txt 文件获取，若没有文件则从 appsettings.json 中获取</remarks>
    /// <param name="mutiDbOperate"></param>
    /// <returns></returns>
    private static MutiDbOperate SpecialDbString(MutiDbOperate mutiDbOperate)
    {
        if (mutiDbOperate.DbType == DataBaseType.Sqlite)
        {
            // 使用解决方案根目录下的 DataBases 文件夹存放 SQLite 数据库
            var solutionRoot = FindSolutionRoot();
            var dbDirectory = Path.Combine(solutionRoot, "DataBases");
            Directory.CreateDirectory(dbDirectory); // 确保目录存在
            mutiDbOperate.ConnectionString =
                $"DataSource=" + Path.Combine(dbDirectory, mutiDbOperate.ConnectionString);
        }
        else if (mutiDbOperate.DbType == DataBaseType.SqlServer)
        {
            mutiDbOperate.ConnectionString = DifDbConnOfSecurity(@"./dbCountPsw1_SqlserverConn.txt",
                mutiDbOperate.ConnectionString);
        }
        else if (mutiDbOperate.DbType == DataBaseType.MySql)
        {
            mutiDbOperate.ConnectionString =
                DifDbConnOfSecurity(@"./dbCountPsw1_MySqlConn.txt", mutiDbOperate.ConnectionString);
        }
        else if (mutiDbOperate.DbType == DataBaseType.Oracle)
        {
            mutiDbOperate.ConnectionString =
                DifDbConnOfSecurity(@"./dbCountPsw1_OracleConn.txt", mutiDbOperate.ConnectionString);
        }

        return mutiDbOperate;
    }
}

/// <summary>数据库类型枚举</summary>
public enum DataBaseType
{
    MySql = 0,
    SqlServer = 1,
    Sqlite = 2,
    Oracle = 3,
    PostgreSql = 4,
    DaMeng = 5, // 达梦
    Kdbndp = 6, // 金仓
}

/// <summary>数据库配置选项模型</summary>
public class MutiDbOperate
{
    /// <summary>连接启用开关</summary>
    public bool Enabled { get; set; }

    /// <summary>连接 Id</summary>
    public string ConnId { get; set; }

    /// <summary>从库执行级别，越大越先执行</summary>
    public int HitRate { get; set; }

    /// <summary>连接字符串</summary>
    public string ConnectionString { get; set; }

    /// <summary>数据库类型</summary>
    public DataBaseType DbType { get; set; }

    /// <summary>从库</summary>
    public List<MutiDbOperate> Slaves { get; set; }
}