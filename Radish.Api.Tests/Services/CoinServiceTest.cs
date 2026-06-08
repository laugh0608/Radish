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
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.Security;
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
    private readonly Mock<IUserBalanceRepository> _userBalanceRepositoryMock;
    private readonly Mock<IBaseRepository<User>> _userRepositoryMock;
    private readonly Mock<IBaseRepository<CoinTransaction>> _coinTransactionRepositoryMock;
    private readonly Mock<IBaseRepository<BalanceChangeLog>> _balanceChangeLogRepositoryMock;

    public CoinServiceTest()
    {
        _mapperMock = new Mock<IMapper>();
        _userBalanceRepositoryMock = new Mock<IUserBalanceRepository>();
        _userRepositoryMock = new Mock<IBaseRepository<User>>();
        _coinTransactionRepositoryMock = new Mock<IBaseRepository<CoinTransaction>>();
        _balanceChangeLogRepositoryMock = new Mock<IBaseRepository<BalanceChangeLog>>();

        _userRepositoryMock
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync((Expression<Func<User, bool>> expression) =>
            {
                var predicate = expression.Compile();
                var users = new[]
                {
                    new User { Id = 1, IsDeleted = false },
                    new User { Id = 2, IsDeleted = false },
                    new User { Id = 3, IsDeleted = false },
                    new User { Id = 123456, IsDeleted = false },
                    new User { Id = 654321, IsDeleted = false },
                    new User { Id = 999999, IsDeleted = false }
                };
                return users.FirstOrDefault(predicate);
            });

        _userBalanceRepositoryMock
            .Setup(r => r.QueryByUserIdIncludingDeletedAsync(It.IsAny<long>()))
            .ReturnsAsync((UserBalance?)null);
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
            UserId = userId,
            Balance = 50000,
            FrozenBalance = 0,
            TotalEarned = 50000,
            TotalSpent = 0,
            Version = 1
        };

        var expectedVo = new UserBalanceVo
        {
            VoUserId = userId,
            VoBalance = 50000,
            VoBalanceDisplay = "50.000",
            VoFrozenBalance = 0,
            VoFrozenBalanceDisplay = "0.000"
        };

        _userBalanceRepositoryMock
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBalance, bool>>>() ))
            .ReturnsAsync(userBalance);

        _mapperMock
            .Setup(m => m.Map<UserBalanceVo>(userBalance))
            .Returns(expectedVo);

        var service = CreateCoinService();

        // Act
        var result = await service.GetBalanceAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.VoUserId);
        Assert.Equal(50000, result.VoBalance);
        Assert.Equal("50.000", result.VoBalanceDisplay);
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
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBalance, bool>>>() ))
            .ReturnsAsync((UserBalance?)null);

        var initializedBalance = new UserBalance
        {
            UserId = userId,
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
            VoUserId = userId,
            VoBalance = 0,
            VoBalanceDisplay = "0.000"
        };

        _mapperMock
            .Setup(m => m.Map<UserBalanceVo>(It.IsAny<UserBalance>()))
            .Returns(expectedVo);

        var service = CreateCoinService();

        // Act
        var result = await service.GetBalanceAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.VoUserId);
        Assert.Equal(0, result.VoBalance);
        _userBalanceRepositoryMock.Verify(r => r.AddAsync(It.IsAny<UserBalance>()), Times.Once);
    }

    [Fact]
    public async Task GetBalanceAsync_ShouldThrowInvalidOperationException_WhenUserMissing()
    {
        const long userId = 888888;
        var service = CreateCoinService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetBalanceAsync(userId));

        Assert.Contains("用户不存在或已删除", exception.Message, StringComparison.OrdinalIgnoreCase);
        _userBalanceRepositoryMock.Verify(r => r.AddAsync(It.IsAny<UserBalance>()), Times.Never);
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
            new() { UserId = 1, Balance = 10000 },
            new() { UserId = 2, Balance = 20000 },
            new() { UserId = 3, Balance = 30000 }
        };

        _userBalanceRepositoryMock
            .Setup(r => r.QueryAsync(It.IsAny<Expression<Func<UserBalance, bool>>>()))
            .ReturnsAsync(userBalances);

        _mapperMock
            .Setup(m => m.Map<UserBalanceVo>(It.IsAny<UserBalance>()))
            .Returns<UserBalance>(ub => new UserBalanceVo
            {
                VoUserId = ub.UserId,
                VoBalance = ub.Balance
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
    [InlineData(0, null, "发放金额必须大于 0")]
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

    [Fact]
    public async Task GrantRegistrationRewardAsync_ShouldReturnExistingTransaction_WhenRewardAlreadyGranted()
    {
        const long userId = 123456;
        var existingTransaction = new CoinTransaction
        {
            ToUserId = userId,
            TransactionType = "SYSTEM_GRANT",
            BusinessType = "UserRegistration",
            BusinessId = userId,
            Status = "SUCCESS",
            TransactionNo = "TXN_EXISTING_REGISTER"
        };

        _coinTransactionRepositoryMock
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>()))
            .ReturnsAsync(existingTransaction);

        var service = CreateCoinService();

        var transactionNo = await service.GrantRegistrationRewardAsync(userId);

        Assert.Equal("TXN_EXISTING_REGISTER", transactionNo);
        _coinTransactionRepositoryMock.Verify(r => r.AddAsync(It.IsAny<CoinTransaction>()), Times.Never);
    }

    [Fact]
    public async Task GrantRegistrationRewardAsync_ShouldGrantDefaultCarrots_WhenRewardMissing()
    {
        const long userId = 123456;
        var userBalance = new UserBalance
        {
            UserId = userId,
            Balance = 0,
            TotalEarned = 0,
            Version = 0,
            TenantId = 0
        };
        CoinTransaction? createdTransaction = null;

        _coinTransactionRepositoryMock
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>()))
            .ReturnsAsync((CoinTransaction?)null);
        _userBalanceRepositoryMock
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBalance, bool>>>()))
            .ReturnsAsync(userBalance);
        _coinTransactionRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<CoinTransaction>()))
            .Callback<CoinTransaction>(transaction => createdTransaction = transaction)
            .ReturnsAsync((CoinTransaction transaction) => transaction.Id);
        _userBalanceRepositoryMock
            .Setup(r => r.UpdateColumnsAsync(
                It.IsAny<Expression<Func<UserBalance, UserBalance>>>(),
                It.IsAny<Expression<Func<UserBalance, bool>>>()))
            .ReturnsAsync((
                Expression<Func<UserBalance, UserBalance>> updateExpression,
                Expression<Func<UserBalance, bool>> whereExpression) =>
            {
                if (!whereExpression.Compile()(userBalance))
                {
                    return 0;
                }

                var patch = updateExpression.Compile()(userBalance);
                userBalance.Balance = patch.Balance;
                userBalance.TotalEarned = patch.TotalEarned;
                userBalance.Version = patch.Version;
                return 1;
            });
        _balanceChangeLogRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<BalanceChangeLog>()))
            .ReturnsAsync(9001);
        _coinTransactionRepositoryMock
            .Setup(r => r.UpdateColumnsAsync(
                It.IsAny<Expression<Func<CoinTransaction, CoinTransaction>>>(),
                It.IsAny<Expression<Func<CoinTransaction, bool>>>()))
            .ReturnsAsync(1);

        var service = CreateCoinService();

        var transactionNo = await service.GrantRegistrationRewardAsync(userId);

        Assert.NotNull(createdTransaction);
        Assert.Equal(transactionNo, createdTransaction!.TransactionNo);
        Assert.Equal(userId, createdTransaction.ToUserId);
        Assert.Equal(50, createdTransaction.Amount);
        Assert.Equal("SYSTEM_GRANT", createdTransaction.TransactionType);
        Assert.Equal("UserRegistration", createdTransaction.BusinessType);
        Assert.Equal(userId, createdTransaction.BusinessId);
        Assert.Equal(50, userBalance.Balance);
        Assert.Equal(50, userBalance.TotalEarned);
    }

    [Theory]
    [InlineData(0, "扣除金额必须大于 0")]
    [InlineData(-100, "扣除金额必须大于 0")]
    public async Task ConsumeCoinAsync_ShouldThrowArgumentException_WhenAmountInvalid(
        long amount,
        string expectedErrorMessage)
    {
        const long userId = 123456;
        var service = CreateCoinService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await service.ConsumeCoinAsync(userId, amount, "Order", 7001, "购买商品")
        );

        Assert.Contains(expectedErrorMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConsumeCoinAsync_ShouldCreateConsumeTransactionAndDeductBalance()
    {
        const long userId = 123456;
        const long amount = 300;
        const long businessId = 7001;
        const long initialBalance = 1000;
        const long initialSpent = 200;

        var userBalance = new UserBalance
        {
            UserId = userId,
            Balance = initialBalance,
            TotalSpent = initialSpent,
            Version = 2,
            TenantId = 0
        };

        CoinTransaction? createdTransaction = null;
        BalanceChangeLog? createdBalanceChangeLog = null;

        _userBalanceRepositoryMock
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<UserBalance, bool>>>() ))
            .ReturnsAsync((Expression<Func<UserBalance, bool>> expression) =>
            {
                var predicate = expression.Compile();
                return predicate(userBalance) ? userBalance : null;
            });

        _coinTransactionRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<CoinTransaction>()))
            .Callback<CoinTransaction>(transaction => createdTransaction = transaction)
            .ReturnsAsync((CoinTransaction transaction) => transaction.Id);

        _userBalanceRepositoryMock
            .Setup(r => r.UpdateColumnsAsync(
                It.IsAny<Expression<Func<UserBalance, UserBalance>>>(),
                It.IsAny<Expression<Func<UserBalance, bool>>>()))
            .ReturnsAsync((
                Expression<Func<UserBalance, UserBalance>> updateExpression,
                Expression<Func<UserBalance, bool>> whereExpression) =>
            {
                var predicate = whereExpression.Compile();
                if (!predicate(userBalance))
                {
                    return 0;
                }

                var patch = updateExpression.Compile()(userBalance);
                userBalance.Balance = patch.Balance;
                userBalance.TotalSpent = patch.TotalSpent;
                userBalance.Version = patch.Version;
                userBalance.ModifyTime = patch.ModifyTime;
                userBalance.ModifyBy = patch.ModifyBy;
                userBalance.ModifyId = patch.ModifyId;
                return 1;
            });

        _balanceChangeLogRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<BalanceChangeLog>()))
            .Callback<BalanceChangeLog>(log => createdBalanceChangeLog = log)
            .ReturnsAsync(9001);

        _coinTransactionRepositoryMock
            .Setup(r => r.UpdateColumnsAsync(
                It.IsAny<Expression<Func<CoinTransaction, CoinTransaction>>>(),
                It.IsAny<Expression<Func<CoinTransaction, bool>>>()))
            .ReturnsAsync(1);

        var service = CreateCoinService();

        var result = await service.ConsumeCoinAsync(userId, amount, "Order", businessId, "购买商品：测试");

        Assert.NotNull(createdTransaction);
        Assert.Equal(createdTransaction!.Id, result.transactionId);
        Assert.Equal(createdTransaction.TransactionNo, result.transactionNo);
        Assert.Equal(userId, createdTransaction.FromUserId);
        Assert.Null(createdTransaction.ToUserId);
        Assert.Equal(amount, createdTransaction.Amount);
        Assert.Equal("CONSUME", createdTransaction.TransactionType);
        Assert.Equal("Order", createdTransaction.BusinessType);
        Assert.Equal(businessId, createdTransaction.BusinessId);

        Assert.Equal(initialBalance - amount, userBalance.Balance);
        Assert.Equal(initialSpent + amount, userBalance.TotalSpent);

        Assert.NotNull(createdBalanceChangeLog);
        Assert.Equal(-amount, createdBalanceChangeLog!.ChangeAmount);
        Assert.Equal(initialBalance, createdBalanceChangeLog.BalanceBefore);
        Assert.Equal(initialBalance - amount, createdBalanceChangeLog.BalanceAfter);
        Assert.Equal("CONSUME", createdBalanceChangeLog.ChangeType);
    }

    #endregion

    #region 交易记录查询测试

    [Fact]
    public async Task GetTransactionsAsync_ShouldFilterByBusinessContext()
    {
        const long userId = 123456;
        const long orderId = 7001;
        var transactions = new List<CoinTransaction>
        {
            new()
            {
                Id = 101,
                FromUserId = userId,
                Amount = 300,
                TransactionType = "CONSUME",
                Status = "SUCCESS",
                BusinessType = "Order",
                BusinessId = orderId,
                CreateTime = new DateTime(2026, 5, 30, 10, 0, 0)
            },
            new()
            {
                Id = 102,
                FromUserId = userId,
                Amount = 300,
                TransactionType = "CONSUME",
                Status = "SUCCESS",
                BusinessType = "Order",
                BusinessId = 8001,
                CreateTime = new DateTime(2026, 5, 30, 11, 0, 0)
            },
            new()
            {
                Id = 103,
                ToUserId = userId,
                Amount = 100,
                TransactionType = "ADMIN_ADJUST",
                Status = "SUCCESS",
                BusinessType = "Admin",
                BusinessId = 1,
                CreateTime = new DateTime(2026, 5, 30, 12, 0, 0)
            }
        };

        _coinTransactionRepositoryMock
            .Setup(r => r.QueryPageAsync(
                It.IsAny<Expression<Func<CoinTransaction, bool>>>(),
                1,
                20,
                It.IsAny<Expression<Func<CoinTransaction, object>>>(),
                OrderByType.Desc))
            .ReturnsAsync((
                Expression<Func<CoinTransaction, bool>> whereExpression,
                int pageIndex,
                int pageSize,
                Expression<Func<CoinTransaction, object>>? orderByExpression,
                OrderByType orderByType) =>
            {
                var predicate = NormalizeCoinTransactionExpression(whereExpression).Compile();
                var filtered = transactions.Where(predicate).ToList();
                return (filtered, filtered.Count);
            });

        _mapperMock
            .Setup(m => m.Map<List<CoinTransactionVo>>(It.IsAny<List<CoinTransaction>>()))
            .Returns((List<CoinTransaction> source) => source.Select(transaction => new CoinTransactionVo
            {
                VoId = transaction.Id,
                VoFromUserId = transaction.FromUserId,
                VoToUserId = transaction.ToUserId,
                VoAmount = transaction.Amount,
                VoTransactionType = transaction.TransactionType,
                VoStatus = transaction.Status,
                VoBusinessType = transaction.BusinessType,
                VoBusinessId = transaction.BusinessId,
                VoCreateTime = transaction.CreateTime
            }).ToList());

        var service = CreateCoinService();

        var result = await service.GetTransactionsAsync(
            userId,
            1,
            20,
            "CONSUME",
            "SUCCESS",
            "Order",
            orderId);

        Assert.Equal(1, result.DataCount);
        Assert.Single(result.Data);
        Assert.Equal(101, result.Data[0].VoId);
        Assert.Equal("Order", result.Data[0].VoBusinessType);
        Assert.Equal(orderId, result.Data[0].VoBusinessId);
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

    [Fact]
    public async Task AdminAdjustBalanceAsync_ShouldThrowInvalidOperationException_WhenUserMissing()
    {
        const long userId = 888888;
        const long operatorId = 1;
        const string operatorName = "admin";
        var service = CreateCoinService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AdminAdjustBalanceAsync(userId, 500, "测试调账", operatorId, operatorName));

        Assert.Contains("用户不存在或已删除", exception.Message, StringComparison.OrdinalIgnoreCase);
        _userBalanceRepositoryMock.Verify(r => r.AddAsync(It.IsAny<UserBalance>()), Times.Never);
    }

    [Fact]
    public async Task TransferAsync_ShouldThrowUpgradePrompt_WhenPaymentPasscodeIsLegacy()
    {
        const long fromUserId = 123456;
        const long toUserId = 654321;
        const long amount = 100;

        var paymentPasswordServiceMock = new Mock<IPaymentPasswordService>(MockBehavior.Strict);
        paymentPasswordServiceMock
            .Setup(service => service.VerifyPaymentPasswordAsync(fromUserId, It.IsAny<VerifyPaymentPasswordRequest>()))
            .ReturnsAsync(new PaymentPasswordVerifyResult
            {
                IsSuccess = false,
                ErrorCode = PaymentPasscodeErrorCodes.UpgradeRequired,
                ErrorMessage = PaymentPasscodeRules.UpgradeRequiredErrorMessage,
                RequiresPasscodeUpgrade = true
            });

        var service = CreateCoinService(paymentPasswordServiceMock.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TransferAsync(fromUserId, toUserId, amount, "274958", "测试转账"));

        Assert.Equal(PaymentPasscodeRules.UpgradeRequiredErrorMessage, exception.Message);
        paymentPasswordServiceMock.VerifyAll();
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 创建 CoinService 实例（用于测试）
    /// </summary>
    private CoinService CreateCoinService(IPaymentPasswordService? paymentPasswordService = null)
    {
        return new CoinService(
            _mapperMock.Object,
            _userBalanceRepositoryMock.Object,
            _userRepositoryMock.Object,
            _coinTransactionRepositoryMock.Object,
            _balanceChangeLogRepositoryMock.Object,
            paymentPasswordService ?? new Mock<IPaymentPasswordService>().Object
        );
    }

    private static Expression<Func<CoinTransaction, bool>> NormalizeCoinTransactionExpression(
        Expression<Func<CoinTransaction, bool>> expression)
    {
        var parameter = Expression.Parameter(typeof(CoinTransaction), "transaction");
        var body = new CoinTransactionParameterReplaceVisitor(parameter).Visit(expression.Body);

        return Expression.Lambda<Func<CoinTransaction, bool>>(body!, parameter);
    }

    private sealed class CoinTransactionParameterReplaceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;

        public CoinTransactionParameterReplaceVisitor(ParameterExpression parameter)
        {
            _parameter = parameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node.Type == typeof(CoinTransaction) ? _parameter : base.VisitParameter(node);
        }
    }

    #endregion
}
