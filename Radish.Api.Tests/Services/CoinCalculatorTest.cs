using System;
using System.Collections.Generic;
using System.Linq;
using Radish.Common.Utils;
using Xunit;

namespace Radish.Api.Tests.Services;

/// <summary>
/// CoinCalculator 单元测试
/// </summary>
/// <remarks>
/// 测试萝卜币计算工具类的各种计算场景，确保：
/// 1. 舍入规则正确（向下取整）
/// 2. 差额计算准确
/// 3. 批量分配无遗漏
/// 4. 累积计算器正常工作
/// </remarks>
public class CoinCalculatorTest
{
    #region 单位转换测试

    [Theory]
    [InlineData(100, 1.00)]
    [InlineData(12345, 123.45)]
    [InlineData(1, 0.01)]
    [InlineData(0, 0.00)]
    public void ToWhiteRadish_ShouldConvertCorrectly(long carrot, decimal expectedWhiteRadish)
    {
        // Act
        var result = CoinCalculator.ToWhiteRadish(carrot);

        // Assert
        Assert.Equal(expectedWhiteRadish, result);
    }

    [Theory]
    [InlineData(1.00, 100)]
    [InlineData(123.45, 12345)]
    [InlineData(123.456, 12345)]  // 向下取整
    [InlineData(0.999, 99)]       // 向下取整
    public void ToCarrot_ShouldFloorCorrectly(decimal whiteRadish, long expectedCarrot)
    {
        // Act
        var result = CoinCalculator.ToCarrot(whiteRadish);

        // Assert
        Assert.Equal(expectedCarrot, result);
    }

    [Theory]
    [InlineData(0, "0 胡萝卜")]
    [InlineData(1, "1 胡萝卜")]
    [InlineData(999, "999 胡萝卜")]
    [InlineData(1000, "10 白萝卜")]
    [InlineData(1050, "10 白萝卜 50 胡萝卜")]
    [InlineData(12345, "123 白萝卜 45 胡萝卜")]
    [InlineData(50000, "500 白萝卜")]
    public void FormatDisplay_ShouldFormatCorrectly(long carrot, string expected)
    {
        // Act
        var result = CoinCalculator.FormatDisplay(carrot);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, "0.00 白萝卜")]
    [InlineData(1, "0.01 白萝卜")]
    [InlineData(999, "9.99 白萝卜")]
    [InlineData(1000, "10.00 白萝卜")]
    [InlineData(12345, "123.45 白萝卜")]
    public void FormatAsWhiteRadish_ShouldFormatCorrectly(long carrot, string expected)
    {
        // Act
        var result = CoinCalculator.FormatAsWhiteRadish(carrot);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region 比例计算测试

    [Theory]
    [InlineData(100, 0.055, 5.5, 5, 0.5)]  // 5.5% 手续费
    [InlineData(100, 0.1, 10, 10, 0)]      // 10% 手续费（无差额）
    [InlineData(50, 0.02, 1, 1, 0)]        // 2% 手续费
    [InlineData(33, 0.03, 0.99, 0, 0.99)]  // 不足 1 胡萝卜
    public void CalculateByRate_ShouldCalculateCorrectly(
        long amount, decimal rate, decimal expectedTheoretical, long expectedActual, decimal expectedDiff)
    {
        // Act
        var result = CoinCalculator.CalculateByRate(amount, rate);

        // Assert
        Assert.Equal(expectedTheoretical, result.TheoreticalAmount);
        Assert.Equal(expectedActual, result.ActualAmount);
        Assert.Equal(expectedDiff, result.RoundingDiff);
    }

    [Fact]
    public void CalculateByRate_ShouldThrow_WhenAmountIsNegative()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => CoinCalculator.CalculateByRate(-100, 0.05m));
    }

    [Fact]
    public void CalculateByRate_ShouldThrow_WhenRateIsInvalid()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => CoinCalculator.CalculateByRate(100, -0.05m));
        Assert.Throws<ArgumentException>(() => CoinCalculator.CalculateByRate(100, 1.5m));
    }

    #endregion

    #region 手续费计算测试

    [Theory]
    [InlineData(100, 0.055, 5)]   // 5.5% → 5 胡萝卜
    [InlineData(50, 0.01, 0)]     // 1% → 0.5 胡萝卜（不足 1 免收）
    [InlineData(200, 0.005, 1)]   // 0.5% → 1 胡萝卜
    [InlineData(10, 0.05, 0)]     // 0.5 胡萝卜（不足 1 免收）
    public void CalculateFee_ShouldApplyMinimumFeeRule(
        long amount, decimal feeRate, long expectedFee)
    {
        // Act
        var result = CoinCalculator.CalculateFee(amount, feeRate);

        // Assert
        Assert.Equal(expectedFee, result.ActualAmount);
    }

    [Fact]
    public void CalculateFee_ShouldRecordFullDiffWhenFeeWaived()
    {
        // Arrange
        long amount = 50;
        decimal feeRate = 0.01m;  // 理论手续费 0.5

        // Act
        var result = CoinCalculator.CalculateFee(amount, feeRate);

        // Assert
        Assert.Equal(0, result.ActualAmount);           // 免收
        Assert.Equal(0.5m, result.TheoreticalAmount);   // 理论值保留
        Assert.Equal(0.5m, result.RoundingDiff);        // 全部记为差额
    }

    #endregion

    #region 批量均分测试

    [Theory]
    [InlineData(100, 3, new long[] { 34, 33, 33 })]
    [InlineData(100, 4, new long[] { 25, 25, 25, 25 })]
    [InlineData(10, 3, new long[] { 4, 3, 3 })]
    [InlineData(1, 5, new long[] { 1, 0, 0, 0, 0 })]
    public void DistributeEqually_ShouldDistributeCorrectly(
        long totalAmount, int count, long[] expectedShares)
    {
        // Act
        var result = CoinCalculator.DistributeEqually(totalAmount, count);

        // Assert
        Assert.Equal(count, result.Count);
        Assert.Equal(expectedShares, result.ToArray());

        // 验证总和不变
        Assert.Equal(totalAmount, result.Sum());
    }

    [Fact]
    public void DistributeEqually_ShouldThrow_WhenCountIsZero()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => CoinCalculator.DistributeEqually(100, 0));
    }

    #endregion

    #region 按权重分配测试

    [Fact]
    public void DistributeByWeight_ShouldDistributeCorrectly()
    {
        // Arrange
        long totalAmount = 100;
        var weights = new List<int> { 3, 2, 1 };  // 权重比 3:2:1

        // Act
        var results = CoinCalculator.DistributeByWeight(totalAmount, weights);

        // Assert
        Assert.Equal(3, results.Count);

        // 验证总和不变
        var totalAllocated = results.Sum(r => r.ActualAmount);
        Assert.Equal(totalAmount, totalAllocated);

        // 验证分配比例接近权重比
        Assert.Equal(50, results[0].ActualAmount);  // 3/6 * 100 = 50
        Assert.Equal(33, results[1].ActualAmount);  // 2/6 * 100 ≈ 33
        Assert.Equal(17, results[2].ActualAmount);  // 1/6 * 100 ≈ 17
    }

    [Fact]
    public void DistributeByWeight_ShouldHandleRemainder()
    {
        // Arrange
        long totalAmount = 10;
        var weights = new List<int> { 1, 1, 1 };  // 均等权重，但总数不能整除

        // Act
        var results = CoinCalculator.DistributeByWeight(totalAmount, weights);

        // Assert
        var totalAllocated = results.Sum(r => r.ActualAmount);
        Assert.Equal(totalAmount, totalAllocated);

        // 余额应该分配给舍入差额最大的项
        Assert.Equal(4, results[0].ActualAmount);
        Assert.Equal(3, results[1].ActualAmount);
        Assert.Equal(3, results[2].ActualAmount);
    }

    [Fact]
    public void DistributeByWeight_ShouldThrow_WhenWeightsAreInvalid()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            CoinCalculator.DistributeByWeight(100, null!));

        Assert.Throws<ArgumentException>(() =>
            CoinCalculator.DistributeByWeight(100, new List<int>()));

        Assert.Throws<ArgumentException>(() =>
            CoinCalculator.DistributeByWeight(100, new List<int> { 1, 0, -1 }));
    }

    #endregion

    #region 累积计算器测试

    [Fact]
    public void AccumulativeCalculator_ShouldAccumulateCorrectly()
    {
        // Arrange
        var calculator = new CoinCalculator.AccumulativeCalculator();

        // Act & Assert
        // 第一次：0.3，未满 1
        var settled1 = calculator.Add(0.3m);
        Assert.Equal(0, settled1);
        Assert.Equal(0.3m, calculator.Accumulated);

        // 第二次：0.3 + 0.8 = 1.1，结算 1，剩余 0.1
        var settled2 = calculator.Add(0.8m);
        Assert.Equal(1, settled2);
        Assert.Equal(0.1m, calculator.Accumulated);

        // 第三次：0.1 + 0.5 = 0.6，未满 1
        var settled3 = calculator.Add(0.5m);
        Assert.Equal(0, settled3);
        Assert.Equal(0.6m, calculator.Accumulated);

        // 第四次：0.6 + 2.9 = 3.5，结算 3，剩余 0.5
        var settled4 = calculator.Add(2.9m);
        Assert.Equal(3, settled4);
        Assert.Equal(0.5m, calculator.Accumulated);
    }

    [Fact]
    public void AccumulativeCalculator_Reset_ShouldClearAccumulated()
    {
        // Arrange
        var calculator = new CoinCalculator.AccumulativeCalculator();
        calculator.Add(0.7m);

        // Act
        calculator.Reset();

        // Assert
        Assert.Equal(0, calculator.Accumulated);

        // 重置后应该能正常累积
        var settled = calculator.Add(0.5m);
        Assert.Equal(0, settled);
        Assert.Equal(0.5m, calculator.Accumulated);
    }

    #endregion

    #region 验证工具测试

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void IsValidAmount_ShouldValidateCorrectly(long amount, bool expected)
    {
        // Act
        var result = CoinCalculator.IsValidAmount(amount);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(100, 50, true)]
    [InlineData(100, 100, true)]
    [InlineData(100, 101, false)]
    [InlineData(0, 1, false)]
    public void HasSufficientBalance_ShouldCheckCorrectly(
        long balance, long amount, bool expected)
    {
        // Act
        var result = CoinCalculator.HasSufficientBalance(balance, amount);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region 实际场景测试

    [Fact]
    public void Scenario_TransferWithFee_ShouldCalculateCorrectly()
    {
        // Arrange - 转账 100 胡萝卜，手续费 5.5%
        long transferAmount = 100;
        decimal feeRate = 0.055m;

        // Act
        var feeResult = CoinCalculator.CalculateFee(transferAmount, feeRate);
        long actualFee = feeResult.ActualAmount;
        long actualTransfer = transferAmount - actualFee;

        // Assert
        Assert.Equal(5, actualFee);           // 手续费 5 胡萝卜（向下取整）
        Assert.Equal(95, actualTransfer);     // 实际到账 95 胡萝卜
        Assert.Equal(5.5m, feeResult.TheoreticalAmount);  // 理论手续费 5.5
        Assert.Equal(0.5m, feeResult.RoundingDiff);       // 舍入差额 0.5
    }

    [Fact]
    public void Scenario_GodCommentRewardDistribution_ShouldDistributeCorrectly()
    {
        // Arrange - 100 胡萝卜奖励池，按点赞数（50:30:20）分配
        long totalReward = 100;
        var likeWeights = new List<int> { 50, 30, 20 };

        // Act
        var allocations = CoinCalculator.DistributeByWeight(totalReward, likeWeights);

        // Assert
        Assert.Equal(3, allocations.Count);
        Assert.Equal(totalReward, allocations.Sum(a => a.ActualAmount));

        // 验证分配比例
        Assert.Equal(50, allocations[0].ActualAmount);  // 50/100 * 100 = 50
        Assert.Equal(30, allocations[1].ActualAmount);  // 30/100 * 100 = 30
        Assert.Equal(20, allocations[2].ActualAmount);  // 20/100 * 100 = 20
    }

    [Fact]
    public void Scenario_DailyInterestAccumulation_ShouldAccumulateCorrectly()
    {
        // Arrange - 每日利息 0.05%，1000 胡萝卜本金
        long principal = 1000;
        decimal dailyRate = 0.0005m;
        var calculator = new CoinCalculator.AccumulativeCalculator();
        var dailyInterests = new List<long>();

        // Act - 模拟 10 天
        for (int day = 1; day <= 10; day++)
        {
            decimal dailyInterest = principal * dailyRate;  // 0.5 胡萝卜/天
            long settled = calculator.Add(dailyInterest);
            dailyInterests.Add(settled);
        }

        // Assert
        // 第1天：0.5（未满 1） → 0
        // 第2天：1.0（满 1）→ 1
        // 第3天：0.5（未满 1）→ 0
        // 第4天：1.0（满 1）→ 1
        // ...
        Assert.Equal(new long[] { 0, 1, 0, 1, 0, 1, 0, 1, 0, 1 }, dailyInterests.ToArray());

        // 总共发放 5 胡萝卜利息
        Assert.Equal(5, dailyInterests.Sum());
    }

    #endregion
}
