namespace Radish.Common.AttributeTool;

/// <summary>
/// 事务传播方式枚举
/// </summary>
public enum Propagation
{
    /// <summary>
    /// 默认：如果当前没有事务，就新建一个事务，如果已存在一个事务中，加入到这个事务中
    /// </summary>
    Required = 0,

    /// <summary>
    /// 使用当前事务，如果没有当前事务，就抛出异常
    /// </summary>
    Mandatory = 1,

    /// <summary>
    /// 以嵌套事务方式执行
    /// </summary>
    Nested = 2,

    /// <summary>
    /// 新建独立事务运行。如果当前存在事务，挂起当前事务，开启新的独立事务。
    /// 新事务提交或回滚后，恢复之前挂起的事务。
    /// </summary>
    RequiresNew = 3,
}
