namespace Radish.Model.ViewModels;

/// <summary>
/// 事务传播测试结果视图模型
/// </summary>
public class TransactionTestResultVo
{
    /// <summary>
    /// 测试结果
    /// </summary>
    public object? VoResult { get; set; }

    /// <summary>
    /// 测试消息
    /// </summary>
    public string VoMessage { get; set; } = string.Empty;
}