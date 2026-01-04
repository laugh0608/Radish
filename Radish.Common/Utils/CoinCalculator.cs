namespace Radish.Common.Utils;

/// <summary>
/// 萝卜币计算工具类
/// </summary>
/// <remarks>
/// 核心原则：
/// 1. 最小单位：1 胡萝卜（不产生小数）
/// 2. 统一舍入：所有比例计算向下取整（Floor）
/// 3. 差额追踪：记录理论金额和实际金额的差异
/// 4. 白萝卜：仅供展示方便（1 白萝卜 = 100 胡萝卜）
/// </remarks>
public static class CoinCalculator
{
    #region 常量定义

    /// <summary>最小交易金额（胡萝卜）</summary>
    public const long MinTransactionAmount = 1;

    /// <summary>最小手续费（胡萝卜）</summary>
    public const long MinFeeAmount = 1;

    /// <summary>白萝卜与胡萝卜的换算比例</summary>
    public const long WhiteRadishRatio = 100;

    #endregion

    #region 单位转换与展示

    /// <summary>
    /// 胡萝卜转白萝卜（仅用于展示）
    /// </summary>
    /// <param name="carrotAmount">胡萝卜数量</param>
    /// <returns>白萝卜数量（保留 2 位小数）</returns>
    public static decimal ToWhiteRadish(long carrotAmount)
    {
        return carrotAmount / (decimal)WhiteRadishRatio;
    }

    /// <summary>
    /// 白萝卜转胡萝卜（向下取整）
    /// </summary>
    /// <param name="whiteRadishAmount">白萝卜数量</param>
    /// <returns>胡萝卜数量（向下取整）</returns>
    public static long ToCarrot(decimal whiteRadishAmount)
    {
        return (long)Math.Floor(whiteRadishAmount * WhiteRadishRatio);
    }

    /// <summary>
    /// 格式化金额显示
    /// </summary>
    /// <param name="carrotAmount">胡萝卜数量</param>
    /// <returns>格式化后的显示文本</returns>
    /// <remarks>
    /// 显示规则：
    /// - 小于 1000 胡萝卜：显示 "xxx 胡萝卜"
    /// - 大于等于 1000 胡萝卜：显示 "x 白萝卜 x 胡萝卜"
    /// </remarks>
    public static string FormatDisplay(long carrotAmount)
    {
        if (carrotAmount < 1000)
        {
            return $"{carrotAmount} 胡萝卜";
        }

        var whiteRadish = carrotAmount / WhiteRadishRatio;
        var remainingCarrot = carrotAmount % WhiteRadishRatio;

        if (remainingCarrot == 0)
        {
            return $"{whiteRadish} 白萝卜";
        }

        return $"{whiteRadish} 白萝卜 {remainingCarrot} 胡萝卜";
    }

    /// <summary>
    /// 格式化金额显示（简洁版，仅显示白萝卜）
    /// </summary>
    /// <param name="carrotAmount">胡萝卜数量</param>
    /// <returns>白萝卜显示（带小数）</returns>
    /// <remarks>用于需要紧凑显示的场景，始终显示为白萝卜（如 "123.45 白萝卜"）</remarks>
    public static string FormatAsWhiteRadish(long carrotAmount)
    {
        var whiteRadish = carrotAmount / (decimal)WhiteRadishRatio;
        return $"{whiteRadish:F2} 白萝卜";
    }

    #endregion

    #region 比例计算（统一舍入规则）

    /// <summary>
    /// 计算比例金额（向下取整）
    /// </summary>
    /// <param name="amount">基础金额（胡萝卜）</param>
    /// <param name="rate">比例（如 0.055 表示 5.5%）</param>
    /// <returns>计算结果（包含理论金额、实际金额、舍入差额）</returns>
    public static CoinCalculationResult CalculateByRate(long amount, decimal rate)
    {
        if (amount < 0)
            throw new ArgumentException("金额不能为负数", nameof(amount));

        if (rate < 0 || rate > 1)
            throw new ArgumentException("比例必须在 0-1 之间", nameof(rate));

        var theoretical = amount * rate;
        var actual = (long)Math.Floor(theoretical);
        var rounding = theoretical - actual;

        return new CoinCalculationResult
        {
            TheoreticalAmount = theoretical,
            ActualAmount = actual,
            RoundingDiff = rounding
        };
    }

    /// <summary>
    /// 计算手续费（向下取整，不足最小值时免收）
    /// </summary>
    /// <param name="amount">交易金额（胡萝卜）</param>
    /// <param name="feeRate">手续费率（如 0.01 表示 1%）</param>
    /// <returns>计算结果</returns>
    public static CoinCalculationResult CalculateFee(long amount, decimal feeRate)
    {
        var result = CalculateByRate(amount, feeRate);

        // 手续费不足最小值时免收
        if (result.ActualAmount < MinFeeAmount)
        {
            result.ActualAmount = 0;
            result.RoundingDiff = result.TheoreticalAmount; // 全部记为舍入差额
        }

        return result;
    }

    #endregion

    #region 批量分配（精确分配 + 余额处理）

    /// <summary>
    /// 批量均分金额（精确分配，余额按顺序分配）
    /// </summary>
    /// <param name="totalAmount">总金额（胡萝卜）</param>
    /// <param name="count">分配数量</param>
    /// <returns>每份金额列表（总和等于 totalAmount）</returns>
    public static List<long> DistributeEqually(long totalAmount, int count)
    {
        if (totalAmount < 0)
            throw new ArgumentException("总金额不能为负数", nameof(totalAmount));

        if (count <= 0)
            throw new ArgumentException("分配数量必须大于 0", nameof(count));

        var baseAmount = totalAmount / count;
        var remainder = totalAmount % count;

        var result = new List<long>(count);

        for (int i = 0; i < count; i++)
        {
            // 前 remainder 个人多得 1 胡萝卜
            result.Add(baseAmount + (i < remainder ? 1 : 0));
        }

        return result;
    }

    /// <summary>
    /// 按权重分配金额（精确分配，余额按权重比例分配）
    /// </summary>
    /// <param name="totalAmount">总金额（胡萝卜）</param>
    /// <param name="weights">权重列表（必须大于 0）</param>
    /// <returns>分配结果列表（总和等于 totalAmount）</returns>
    public static List<CoinAllocationResult> DistributeByWeight(long totalAmount, List<int> weights)
    {
        if (totalAmount < 0)
            throw new ArgumentException("总金额不能为负数", nameof(totalAmount));

        if (weights == null || weights.Count == 0)
            throw new ArgumentException("权重列表不能为空", nameof(weights));

        if (weights.Any(w => w <= 0))
            throw new ArgumentException("权重必须大于 0", nameof(weights));

        var totalWeight = weights.Sum();
        var results = new List<CoinAllocationResult>(weights.Count);
        var allocatedSum = 0L;

        for (int i = 0; i < weights.Count; i++)
        {
            var theoretical = totalAmount * weights[i] / (decimal)totalWeight;
            var actual = (long)Math.Floor(theoretical);
            var rounding = theoretical - actual;

            results.Add(new CoinAllocationResult
            {
                Index = i,
                Weight = weights[i],
                TheoreticalAmount = theoretical,
                ActualAmount = actual,
                RoundingDiff = rounding
            });

            allocatedSum += actual;
        }

        // 处理余额（按舍入差额降序分配）
        var remainder = totalAmount - allocatedSum;
        if (remainder > 0)
        {
            var sortedByRounding = results
                .OrderByDescending(r => r.RoundingDiff)
                .ThenByDescending(r => r.Weight)
                .Take((int)remainder)
                .ToList();

            foreach (var item in sortedByRounding)
            {
                item.ActualAmount++;
            }
        }

        return results;
    }

    #endregion

    #region 累积计算（适用于利息、分红等）

    /// <summary>
    /// 累积计算器（每次计算，累积到满 1 胡萝卜时结算）
    /// </summary>
    public class AccumulativeCalculator
    {
        private decimal _accumulated = 0;

        /// <summary>当前累积金额（未满 1 胡萝卜）</summary>
        public decimal Accumulated => _accumulated;

        /// <summary>
        /// 添加累积金额
        /// </summary>
        /// <param name="amount">金额</param>
        /// <returns>可结算的整数金额（胡萝卜）</returns>
        public long Add(decimal amount)
        {
            _accumulated += amount;
            var toSettle = (long)Math.Floor(_accumulated);
            _accumulated -= toSettle;
            return toSettle;
        }

        /// <summary>
        /// 重置累积金额
        /// </summary>
        public void Reset()
        {
            _accumulated = 0;
        }
    }

    #endregion

    #region 验证工具

    /// <summary>
    /// 验证金额是否有效（大于等于最小交易金额）
    /// </summary>
    public static bool IsValidAmount(long amount)
    {
        return amount >= MinTransactionAmount;
    }

    /// <summary>
    /// 验证余额是否充足
    /// </summary>
    public static bool HasSufficientBalance(long balance, long amount)
    {
        return balance >= amount;
    }

    #endregion
}

#region 结果类型

/// <summary>
/// 萝卜币计算结果
/// </summary>
public class CoinCalculationResult
{
    /// <summary>理论金额（精确计算结果）</summary>
    public decimal TheoreticalAmount { get; set; }

    /// <summary>实际金额（向下取整后的金额，胡萝卜）</summary>
    public long ActualAmount { get; set; }

    /// <summary>舍入差额（理论 - 实际）</summary>
    public decimal RoundingDiff { get; set; }
}

/// <summary>
/// 萝卜币分配结果
/// </summary>
public class CoinAllocationResult
{
    /// <summary>索引</summary>
    public int Index { get; set; }

    /// <summary>权重</summary>
    public int Weight { get; set; }

    /// <summary>理论金额（精确计算结果）</summary>
    public decimal TheoreticalAmount { get; set; }

    /// <summary>实际分配金额（胡萝卜）</summary>
    public long ActualAmount { get; set; }

    /// <summary>舍入差额（理论 - 实际）</summary>
    public decimal RoundingDiff { get; set; }
}

#endregion
