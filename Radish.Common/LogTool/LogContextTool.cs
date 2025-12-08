using Serilog.Context;
using SqlSugar;

namespace Radish.Common.LogTool;

public class LogContextTool: IDisposable
{
    /// <summary>
    /// 日志根目录（解决方案根目录的 Log 文件夹）
    /// </summary>
    public static readonly string BaseLogs = GetSolutionLogDirectory();

    /// <summary>
    /// 当前项目名称（从运行目录自动识别）
    /// </summary>
    public static readonly string ProjectName = GetProjectName();

    static LogContextTool()
    {
        if (!Directory.Exists(BaseLogs))
        {
            Directory.CreateDirectory(BaseLogs);
        }
    }

    private readonly Stack<IDisposable> _disposableStack = new Stack<IDisposable>();
    public static LogContextTool Create => new();
    public static readonly string BasePathLogs = BaseLogs;

    public const string LogSource = "LogSource";
    public const string AopSql = "AopSql";
    public const string SerilogDebug = "SerilogDebug";
    public static readonly string SqlOutToConsole = "OutToConsole";
    public static readonly string SqlOutToFile = "SqlOutToFile";
    public static readonly string OutToDb = "OutToDb";
    public static readonly string SugarActionType = "SugarActionType";

    public static readonly string FileMessageTemplate =
        "{NewLine}Date：{Timestamp:yyyy-MM-dd HH:mm:ss.fff}{NewLine}LogLevel：{Level}{NewLine}Message：{Message}{NewLine}{Exception}" +
        new string('-', 100);
    
    public static string Combine(string path1)
    {
        return Path.Combine(BaseLogs, path1);
    }

    public static string Combine(string path1, string path2)
    {
        return Path.Combine(BaseLogs, path1, path2);
    }

    public static string Combine(string path1, string path2, string path3)
    {
        return Path.Combine(BaseLogs, path1, path2, path3);
    }
    
    public void AddStock(IDisposable disposable)
    {
        _disposableStack.Push(disposable);
    }

    public IDisposable SqlAopPushProperty(ISqlSugarClient db)
    {
        // 这个的意思是，给 SqlSugar 日志上下文中添加一个自定义特性 AopSql，方便 Serilog 识别哪个日志是 AopSql
        AddStock(LogContext.PushProperty(LogSource, AopSql));

        return this;
    }


    public void Dispose()
    {
        while (_disposableStack.Count > 0)
        {
            _disposableStack.Pop().Dispose();
        }
    }

    /// <summary>
    /// 获取解决方案根目录的 Log 文件夹路径
    /// 通过向上查找 .slnx 或 .sln 文件来定位解决方案根目录
    /// </summary>
    private static string GetSolutionLogDirectory()
    {
        try
        {
            // 从当前应用程序运行目录开始查找
            var currentDir = new DirectoryInfo(AppContext.BaseDirectory);

            // 向上查找，最多查找 5 层（防止无限循环）
            for (int i = 0; i < 5 && currentDir != null; i++)
            {
                // 查找 .slnx 或 .sln 文件
                var solutionFiles = currentDir.GetFiles("*.slnx")
                    .Concat(currentDir.GetFiles("*.sln"))
                    .ToArray();

                if (solutionFiles.Length > 0)
                {
                    // 找到解决方案根目录，返回其下的 Log 文件夹路径
                    return Path.Combine(currentDir.FullName, "Log");
                }

                currentDir = currentDir.Parent;
            }
        }
        catch
        {
            // 如果查找失败，使用相对路径作为降级方案
        }

        // 降级方案：使用相对路径
        return "Log";
    }

    /// <summary>
    /// 获取当前项目名称（从运行目录解析）
    /// 例如：Radish.Api, Radish.Gateway, Radish.Auth
    /// </summary>
    private static string GetProjectName()
    {
        try
        {
            // AppContext.BaseDirectory 通常是：/path/to/Radish.Api/bin/Debug/net10.0/
            var baseDir = new DirectoryInfo(AppContext.BaseDirectory);

            // 向上查找，寻找包含 .csproj 文件的目录
            var currentDir = baseDir;
            for (int i = 0; i < 5 && currentDir != null; i++)
            {
                var projectFiles = currentDir.GetFiles("*.csproj");
                if (projectFiles.Length > 0)
                {
                    // 使用 .csproj 文件名（不含扩展名）作为项目名称
                    return Path.GetFileNameWithoutExtension(projectFiles[0].Name);
                }
                currentDir = currentDir.Parent;
            }

            // 如果找不到 .csproj，使用包含 bin 目录的父目录名称
            var binParent = baseDir.Parent?.Parent?.Parent;
            if (binParent != null)
            {
                return binParent.Name;
            }
        }
        catch
        {
            // 如果解析失败，返回默认值
        }

        return "Unknown";
    }
}