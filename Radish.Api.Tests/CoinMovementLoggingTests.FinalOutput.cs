using System;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Repository.UnitOfWorks;
using Radish.Service;
using Serilog.Events;
using Xunit;
using static Radish.Api.Tests.RewardOutputAssertions;

namespace Radish.Api.Tests;

public sealed partial class CoinMovementLoggingTests
{
    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task FinalOutput_ShouldKeepTransferAndMissingCompletionBoundaries(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = Pollute();
        using var activity = new Activity("coin-results").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var f = new Fixture();
        var calls = 0;
        f.Idempotency.Setup(s => s.CompleteSuccessAsync(It.IsAny<OperationIdempotencyCompletionRequest>()))
            .Returns(() => ++calls == 1 ? Task.FromException(new IOException(Secret)) : Task.CompletedTask);
        Assert.False(string.IsNullOrEmpty(await f.Coin.TransferAsync(1, 2, 23, "274958", Secret, Secret)));
        Assert.Equal(2, calls);
        f.Transactions.Verify(r => r.AddAsync(It.IsAny<CoinTransaction>()), Times.Once);
        Assert.Equal(77, f.Accounts[0].Balance);
        Assert.Equal(123, f.Accounts[1].Balance);
        Check(capture, typeof(CoinService), "coin.transfer_completion_recovered", LogEventLevel.Warning, "io");

        var records = new Mock<IBaseRepository<OperationIdempotencyRecord>>();
        var service = new OperationIdempotencyService(records.Object, Mock.Of<IUnitOfWorkManage>(), TimeProvider.System);
        await service.CompleteSuccessAsync(new OperationIdempotencyCompletionRequest { RecordId = 99 });
        Check(capture, typeof(OperationIdempotencyService), "idempotency.completion_missing", LogEventLevel.Warning, null, ("completionKind", "success"));
        await service.CompleteFailureAsync(99, Secret, Secret);
        Check(capture, typeof(OperationIdempotencyService), "idempotency.completion_missing", LogEventLevel.Warning, null, ("completionKind", "failure"));
        records.Verify(r => r.UpdateAsync(It.IsAny<OperationIdempotencyRecord>()), Times.Never);

        var transactions = new Mock<IBaseRepository<CoinTransaction>>();
        transactions.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<CoinTransaction, bool>>>())).ThrowsAsync(new IOException(Secret));
        var rewards = new CoinRewardService(Mock.Of<ICoinService>(), transactions.Object,
            new BusinessCalendar(TimeProvider.System, Options.Create(new TimeOptions { DefaultTimeZoneId = "Asia/Shanghai" })));
        Assert.True(await rewards.CheckRewardExistsAsync(Secret, 7, 1));
        Check(capture, typeof(CoinRewardService), "reward.existence_check_failed", LogEventLevel.Error, "io");
    }
}
