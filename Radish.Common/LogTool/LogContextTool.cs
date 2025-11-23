using Serilog.Context;
using SqlSugar;

namespace Radish.Common.LogTool;

public class LogContextTool: IDisposable
{
    static LogContextTool()
    {
        if (!Directory.Exists(BaseLogs))
        {
            Directory.CreateDirectory(BaseLogs);
        }
    }

    private readonly Stack<IDisposable> _disposableStack = new Stack<IDisposable>();
    public static LogContextTool Create => new();
    public const string BaseLogs = "Log";
    public static readonly string BasePathLogs = @"Log";

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
}