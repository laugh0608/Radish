using System.Reflection;
using Radish.Common.CoreTool;
using Serilog.Context;
using SqlSugar;

namespace Radish.Common.LogTool;

public class LogContextTool: IDisposable
{
    /// <summary>
    /// 日志根目录（解决方案根目录的 Logs 文件夹）
    /// </summary>
    public static readonly string BaseLogs = GetSolutionLogDirectory();

    /// <summary>
    /// 当前项目名称（优先从宿主入口程序集识别）
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
    /// 获取解决方案根目录的 Logs 文件夹路径
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
                    return Path.Combine(currentDir.FullName, "Logs");
                }

                currentDir = currentDir.Parent;
            }
        }
        catch
        {
            // 如果查找失败，使用相对路径作为降级方案
        }

        // 降级方案：使用相对路径
        return "Logs";
    }

    /// <summary>
    /// 获取当前项目名称
    /// 优先使用宿主入口程序集名，兼容发布目录 / Docker 容器；
    /// 若不可用，再回退到本地开发场景常见的项目文件扫描逻辑。
    /// 例如：Radish.Api, Radish.Gateway, Radish.Auth
    /// </summary>
    private static string GetProjectName()
    {
        try
        {
            var resolvedProjectName = ResolveProjectName(
                App.HostEnvironment?.ApplicationName,
                App.HostEnvironment?.ContentRootPath,
                Assembly.GetEntryAssembly()?.GetName().Name,
                AppContext.BaseDirectory);

            if (!string.IsNullOrWhiteSpace(resolvedProjectName))
            {
                return resolvedProjectName;
            }
        }
        catch
        {
            // 如果解析失败，返回默认值
        }

        return "Unknown";
    }

    private static string? ResolveProjectName(
        string? applicationName,
        string? contentRootPath,
        string? entryAssemblyName,
        string? baseDirectory)
    {
        var contentRootProjectName = FindProjectNameFromContentRoot(contentRootPath);
        if (!string.IsNullOrWhiteSpace(contentRootProjectName))
        {
            return contentRootProjectName;
        }

        var baseDirectoryProjectName = FindProjectNameFromBaseDirectory(baseDirectory);
        if (!string.IsNullOrWhiteSpace(baseDirectoryProjectName))
        {
            return baseDirectoryProjectName;
        }

        return PreferSpecificProjectName(
            NormalizeProjectName(applicationName),
            NormalizeProjectName(entryAssemblyName));
    }

    private static string? FindProjectNameFromContentRoot(string? contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(contentRootPath))
        {
            return null;
        }

        try
        {
            var contentRoot = new DirectoryInfo(contentRootPath);
            if (!contentRoot.Exists)
            {
                return null;
            }

            var projectFiles = contentRoot.GetFiles("*.csproj", SearchOption.TopDirectoryOnly);
            if (projectFiles.Length == 1)
            {
                return NormalizeProjectName(projectFiles[0].Name);
            }
        }
        catch
        {
            // 忽略内容根目录解析失败，继续使用其他候选来源
        }

        return null;
    }

    private static string? FindProjectNameFromBaseDirectory(string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return null;
        }

        try
        {
            // AppContext.BaseDirectory 通常是：/path/to/Radish.Api/bin/Debug/net10.0/
            var currentDir = new DirectoryInfo(baseDirectory);
            for (int i = 0; i < 5 && currentDir != null; i++)
            {
                var projectFiles = currentDir.GetFiles("*.csproj", SearchOption.TopDirectoryOnly);
                if (projectFiles.Length == 1)
                {
                    return NormalizeProjectName(projectFiles[0].Name);
                }

                currentDir = currentDir.Parent;
            }

            // 最后再回退到包含 bin 目录的父目录名称
            var baseDir = new DirectoryInfo(baseDirectory);
            var binParent = baseDir.Parent?.Parent?.Parent;
            if (!string.IsNullOrWhiteSpace(binParent?.Name))
            {
                return NormalizeProjectName(binParent.Name);
            }
        }
        catch
        {
            // 忽略运行目录解析失败，继续返回空值
        }

        return null;
    }

    private static string? NormalizeProjectName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var projectName = Path.GetFileNameWithoutExtension(value.Trim());
        return string.IsNullOrWhiteSpace(projectName) ? null : projectName;
    }

    private static string? PreferSpecificProjectName(string? primaryCandidate, string? secondaryCandidate)
    {
        if (IsSpecificProjectName(primaryCandidate))
        {
            return primaryCandidate;
        }

        if (IsSpecificProjectName(secondaryCandidate))
        {
            return secondaryCandidate;
        }

        return primaryCandidate ?? secondaryCandidate;
    }

    private static bool IsSpecificProjectName(string? projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return false;
        }

        return projectName.Contains('.', StringComparison.Ordinal);
    }
}
