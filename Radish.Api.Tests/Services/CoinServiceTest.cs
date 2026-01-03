using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using JetBrains.Annotations;
using Moq;
using Radish.Common.Exceptions;
using Radish.Infrastructure;
using Radish.IRepository;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

/// <summary>
/// CoinService 单元测试
/// </summary>
/// <remarks>
/// 测试萝卜币服务的核心功能：
/// 1. 余额查询与初始化
/// 2. 萝卜币发放（含事务、乐观锁、重试）
/// 3. 管理员调账
/// 4. 交易记录查询
/// </remarks>
[TestSubject(typeof(CoinService))]
public class CoinServiceTest
{
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<IBaseRepository<UserBalance>> _userBalanceRepositoryMock;
    private readonly Mock<IBaseRepository<CoinTransaction>> _coinTransactionRepositoryMock;
    private readonly Mock<IBaseRepository<BalanceChangeLog>> _balanceChangeLogRepositoryMock;

    public CoinServiceTest()
    {
        _mapperMock = new Mock<IMapper>();
        _userBalanceRepositoryMock = new Mock<IBaseRepository<UserBalance>>();
        _coinTransactionRepositoryMock = new Mock<IBaseRepository<CoinTransaction>>();
        _balanceChangeLogRepositoryMock = new Mock<IBaseRepository<BalanceChangeLog>>();
    }

    #region 余额查询测试

    /// <summary>
    /// 测试获取已存在的用户余额
    /// </summary>
    [Fact]
    public async Task GetBalanceAsync_ShouldReturnBalance_WhenUserExists()
    {
        // Arrange
        const long userId = 123456;
        var userBalance = new UserBalance
        {
            Id = userId,
            Balance = 50000,
            FrozenBalance = 0,
            TotalEarned = 50000,
            TotalSpent = 0,
            Version = 1
        };

        var expectedVo = new UserBalanceVo
        {
            UserId = userId,
            Balance = 50000,
            BalanceDisplay = "50.000",
            FrozenBalance = 0,
            FrozenBalanceDisplay = "0.000"
        };

        _userBalanceRepositoryMock
            .Setup(r => r.QueryByIdAsync(userId))
            .ReturnsAsync(userBalance);

        _mapperMock
            .Setup(m => m.Map<UserBalanceVo>(userBalance))
            .Returns(expectedVo);

        var service = CreateCoinService();

        // Act
        var result = await service.GetBalanceAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(50000, result.Balance);
        Assert.Equal("50.000", result.BalanceDisplay);
    }

    /// <summary>
    /// 测试获取不存在的用户余额（应自动初始化）
    /// </summary>
    [Fact]
    public async Task GetBalanceAsync_ShouldInitializeBalance_WhenUserNotExists()
    {
        // Arrange
        const long userId = 999999;

        _userBalanceRepositoryMock
            .Setup(r => r.QueryByIdAsync(userId))
            .ReturnsAsync((UserBalance?)null);

        var initializedBalance = new UserBalance
        {
            Id = userId,
            Balance = 0,
            FrozenBalance = 0,
            TotalEarned = 0,
            TotalSpent = 0,
            Version = 0
        };

        _userBalanceRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<UserBalance>()))
            .ReturnsAsync(userId);

        var expectedVo = new UserBalanceVo
        {
            UserId = userId,
            Balance = 0,
            BalanceDisplay = "0.000"
        };

        _mapperMock
            .Setup(m => m.Map<UserBalanceVo>(It.IsAny<UserBalance>()))
            .Returns(expectedVo);

        var service = CreateCoinService();

        // Act
        var result = await service.GetBalanceAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(0, result.Balance);
        _userBalanceRepositoryMock.Verify(r => r.AddAsync(It.IsAny<UserBalance>()), Times.Once);
    }

    /// <summary>
    /// 测试批量获取用户余额
    /// </summary>
    [Fact]
    public async Task GetBalancesAsync_ShouldReturnMultipleBalances()
    {
        // Arrange
        var userIds = new List<long> { 1, 2, 3 };
        var userBalances = new List<UserBalance>
        {
            new() { Id = 1, Balance = 10000 },
            new() { Id = 2, Balance = 20000 },
            new() { Id = 3, Balance = 30000 }
        };

        _userBalanceRepositoryMock
            .Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserBalance, bool>>>()))
            .ReturnsAsync(userBalances);

        _mapperMock
            .Setup(m => m.Map<UserBalanceVo>(It.IsAny<UserBalance>()))
            .Returns<UserBalance>(ub => new UserBalanceVo
            {
                UserId = ub.Id,
                Balance = ub.Balance
            });

        var service = CreateCoinService();

        // Act
        var result = await service.GetBalancesAsync(userIds);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(userIds, id => Assert.True(result.ContainsKey(id)));
    }

    #endregion

    #region 萝卜币发放测试

    /// <summary>
    /// 测试发放萝卜币 - 参数验证
    /// </summary>
    [Theory]
    [InlineData(0, "SYSTEM_GRANT", "发放金额必须大于 0")]
    [InlineData(-100, "SYSTEM_GRANT", "发放金额必须大于 0")]
    [InlineData(100, "", "交易类型不能为空")]
    [InlineData(100, null, "交易类型不能为空")]
    public async Task GrantCoinAsync_ShouldThrowArgumentException_WhenParametersInvalid(
        long amount,
        string? transactionType,
        string expectedErrorMessage)
    {
        // Arrange
        const long userId = 123456;
        var service = CreateCoinService();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await service.GrantCoinAsync(userId, amount, transactionType!)
        );

        Assert.Contains(expectedErrorMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 管理员调账测试

    /// <summary>
    /// 测试管理员调账 - 参数验证
    /// </summary>
    [Theory]
    [InlineData(0, "活动奖励", "调整金额不能为 0")]
    [InlineData(1000, "", "调整原因不能为空")]
    [InlineData(1000, null, "调整原因不能为空")]
    public async Task AdminAdjustBalanceAsync_ShouldThrowArgumentException_WhenParametersInvalid(
        long deltaAmount,
        string? reason,
        string expectedErrorMessage)
    {
        // Arrange
        const long userId = 123456;
        const long operatorId = 1;
        const string operatorName = "admin";
        var service = CreateCoinService();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await service.AdminAdjustBalanceAsync(
                userId, deltaAmount, reason!, operatorId, operatorName)
        );

        Assert.Contains(expectedErrorMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 创建 CoinService 实例（用于测试）
    /// </summary>
    private CoinService CreateCoinService()
    {
        return new CoinService(
            _mapperMock.Object,
            _userBalanceRepositoryMock.Object,
            _coinTransactionRepositoryMock.Object,
            _balanceChangeLogRepositoryMock.Object
        );
    }

    #endregion
}
