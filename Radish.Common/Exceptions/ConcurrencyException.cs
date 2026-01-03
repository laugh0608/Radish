namespace Radish.Common.Exceptions;

/// <summary>
/// 并发冲突异常
/// </summary>
/// <remarks>
/// 当乐观锁检测到数据已被其他操作修改时抛出此异常。
/// 用于替代 EntityFrameworkCore 的 DbUpdateConcurrencyException，
/// 保持项目架构统一（API 项目仅使用 SqlSugar）。
/// </remarks>
public class ConcurrencyException : Exception
{
    /// <summary>
    /// 初始化并发冲突异常
    /// </summary>
    public ConcurrencyException()
        : base("并发冲突：数据已被其他操作修改")
    {
    }

    /// <summary>
    /// 初始化并发冲突异常（带自定义消息）
    /// </summary>
    /// <param name="message">异常消息</param>
    public ConcurrencyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 初始化并发冲突异常（带内部异常）
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
