namespace Radish.IService;

/// <summary>
/// 经验值计算器接口
/// </summary>
public interface IExperienceCalculator
{
    /// <summary>
    /// 计算指定等级升到下一级所需的经验值
    /// </summary>
    /// <param name="level">当前等级</param>
    /// <returns>升级所需经验值</returns>
    long CalculateExpRequired(int level);

    /// <summary>
    /// 计算达到指定等级所需的累计经验值
    /// </summary>
    /// <param name="level">目标等级</param>
    /// <returns>累计经验值</returns>
    long CalculateExpCumulative(int level);

    /// <summary>
    /// 批量计算所有等级的经验值配置
    /// </summary>
    /// <returns>等级 -> (ExpRequired, ExpCumulative) 映射</returns>
    Dictionary<int, (long ExpRequired, long ExpCumulative)> CalculateAllLevels();

    /// <summary>
    /// 获取当前使用的公式类型
    /// </summary>
    string GetFormulaType();

    /// <summary>
    /// 获取当前配置的摘要信息
    /// </summary>
    string GetConfigSummary();

    /// <summary>
    /// 清除计算器缓存（配置更新时调用）
    /// </summary>
    void ClearCache();
}
