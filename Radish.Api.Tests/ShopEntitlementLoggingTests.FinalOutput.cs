using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.Exceptions;
using Radish.Model;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Serilog.Events;
using Xunit;
using static Radish.Api.Tests.ShopOutputAssertions;

namespace Radish.Api.Tests;

public sealed partial class ShopEntitlementLoggingTests
{
    [Theory]
    [MemberData(nameof(ShopOutputLoggingTests.OutputScopes), MemberType = typeof(ShopOutputLoggingTests))]
    public async Task FinalOutput_ShouldPreserveRenameWrappingAndControllerOwnership(bool candidate, string environment, bool audit, bool mel)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = Pollute(capture, mel);
        using var activity = new Activity("shop-inventory-results").SetIdFormat(ActivityIdFormat.W3C).Start();
        var f = new Fixture();
        Assert.Single(await f.Benefit.GetUserBenefitsAsync(1));
        Assert.Single(await f.Inventory.GetUserInventoryAsync(1));
        Assert.Empty(capture.Events);
        f.Item.ConsumableType = ConsumableType.RenameCard;
        var original = new InvalidOperationException(Secret);
        f.User.Setup(s => s.ChangeDisplayNameAsync(1, Secret, It.IsAny<UserDisplayNameChangeContext>())).ThrowsAsync(original);
        var failure = await Assert.ThrowsAsync<BusinessException>(() => f.Inventory.UseRenameCardAsync(1, RenameRequest()));
        Assert.Equal(400, failure.StatusCode);
        Assert.Same(original, failure.InnerException);
        f.InventoryCommands.Verify(r => r.TryDeductItemAsync(1, 3, 1), Times.Once);
        f.Operations.Verify(r => r.AddAsync(It.IsAny<ShopEntitlementOperation>()), Times.Never);
        f.Idempotency.Verify(s => s.CompleteSuccessAsync(It.IsAny<OperationIdempotencyCompletionRequest>()), Times.Never);
        Check(capture, new Expected(typeof(UserInventoryService), "inventory.rename_rejected", LogEventLevel.Warning, "invalid-operation"));
        f.User.Setup(s => s.ChangeDisplayNameAsync(1, Secret, It.IsAny<UserDisplayNameChangeContext>())).ThrowsAsync(new ArgumentException(Secret));
        Assert.Equal(400, (await f.Controller.UseRenameCard(RenameRequest())).StatusCode);
        Assert.Empty(capture.Events);
        f.User.Setup(s => s.ChangeDisplayNameAsync(1, Secret, It.IsAny<UserDisplayNameChangeContext>())).ThrowsAsync(new IOException(Secret));
        Assert.Equal(400, (await f.Controller.UseRenameCard(RenameRequest())).StatusCode);
        Check(capture, new Expected(typeof(ShopController), "inventory.rename_failed", LogEventLevel.Error, "io"));
    }

    [Theory]
    [MemberData(nameof(HangfireOutputLoggingTests.OutputRoutes), MemberType = typeof(HangfireOutputLoggingTests))]
    public async Task FinalOutput_ShouldPreserveRenameOutputFailureBeforeBusinessWrapping(bool candidate, string environment, bool audit)
    {
        using var broken = new HangfireBrokenWriter();
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit, output: broken);
        using var global = new ApplicationLogScope(capture.Logger);
        var outputFailure = new IOException(Secret);
        capture.Sink.EmitFailure = outputFailure;
        var f = new Fixture();
        f.Item.ConsumableType = ConsumableType.RenameCard;
        var original = new InvalidOperationException("rename");
        f.User.Setup(s => s.ChangeDisplayNameAsync(1, Secret, It.IsAny<UserDisplayNameChangeContext>())).ThrowsAsync(original);
        if (audit) await AssertAuditFailure(() => f.Inventory.UseRenameCardAsync(1, RenameRequest()), outputFailure);
        else
        {
            var failure = await Assert.ThrowsAsync<BusinessException>(() => f.Inventory.UseRenameCardAsync(1, RenameRequest()));
            Assert.Equal(400, failure.StatusCode);
            Assert.Same(original, failure.InnerException);
        }
        f.InventoryCommands.Verify(r => r.TryDeductItemAsync(1, 3, 1), Times.Once);
        f.Operations.Verify(r => r.AddAsync(It.IsAny<ShopEntitlementOperation>()), Times.Never);
        AssertOutputFailed(capture);
    }
}
