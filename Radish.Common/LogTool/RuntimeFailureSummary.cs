namespace Radish.Common.LogTool;

/// <summary>异常只映射固定类别，不访问 Message、Data、ToString 或用户定义类型名。</summary>
public static class RuntimeFailureSummary
{
    public static string Classify(Exception exception) => exception switch
    {
        AggregateException => "aggregate",
        System.Data.Common.DbException or SqlSugar.SqlSugarException => "database",
        OperationCanceledException => "cancelled",
        TimeoutException => "timeout",
        IOException => "io",
        UnauthorizedAccessException => "access-denied",
        ArgumentException => "argument",
        InvalidOperationException => "invalid-operation",
        _ => "other"
    };
}
