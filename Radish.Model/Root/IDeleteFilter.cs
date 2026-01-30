namespace Radish.Model.Root;

/// <summary>
/// 软删除过滤器接口
/// </summary>
/// <remarks>
/// 实现此接口的实体将支持软删除功能，包括：
/// - 软删除标记
/// - 删除时间记录
/// - 删除操作者记录
/// </remarks>
public interface IDeleteFilter
{
    /// <summary>是否已删除</summary>
    /// <remarks>true: 已软删除, false: 正常状态</remarks>
    bool IsDeleted { get; set; }

    /// <summary>删除时间</summary>
    /// <remarks>软删除时自动设置，恢复时清空</remarks>
    DateTime? DeletedAt { get; set; }

    /// <summary>删除操作者</summary>
    /// <remarks>执行软删除操作的用户名或系统标识</remarks>
    string? DeletedBy { get; set; }
}