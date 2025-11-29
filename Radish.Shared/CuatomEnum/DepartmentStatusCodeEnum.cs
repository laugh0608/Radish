namespace Radish.Shared.CuatomEnum;

/// <summary>
/// 部门状态码，目前仅约定 0 为“正常”。
/// 如未来有禁用/冻结等状态，可在此扩展。
/// </summary>
public enum DepartmentStatusCodeEnum
{
    /// <summary>
    /// 正常状态，对应历史代码中的 0。
    /// </summary>
    Normal = 0,
}
