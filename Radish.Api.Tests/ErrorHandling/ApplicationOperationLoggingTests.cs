using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Radish.Api.ErrorHandling;
using Microsoft.Extensions.Options;
using Moq;
using OpenIddict.Abstractions;
using Radish.Api.Controllers;
using Radish.Api.Services;
using Radish.Common.CacheTool;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Model.ViewModels.Client;
using Radish.Service;
using Radish.Shared.Constants;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class ApplicationOperationLoggingTests
{
    public static IEnumerable<object[]> Routes() => HangfireOutputLoggingTests.OutputRoutes();

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task CurrentUser_ShouldPreservePartialResultsAndKeepNormalRequestsQuiet(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = capture.SensitiveScope();
        var users = new Mock<IUserService>();
        var permissions = new List<string> { HttpBoundaryCapture.Secret };
        users.Setup(s => s.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>?>())).ReturnsAsync(new UserVo { VoDisplayName = HttpBoundaryCapture.Secret });
        users.Setup(s => s.GetPermissionKeysByRolesAsync(It.IsAny<IReadOnlyCollection<string>>())).ReturnsAsync(permissions);
        var attachments = new Mock<IAttachmentService>();
        attachments.Setup(s => s.GetLatestAvatarAssetAsync(123)).ReturnsAsync(new AttachmentAssetDto { Url = HttpBoundaryCapture.Secret });
        var current = Mock.Of<ICurrentUserAccessor>(a => a.Current == new CurrentUser { UserId = 123, UserName = HttpBoundaryCapture.Secret, Roles = new[] { "User" } });
        var controller = new UserController(users.Object, current, Mock.Of<IPostService>(), Mock.Of<ICommentService>(),
            Mock.Of<IUserBrowseHistoryService>(), Mock.Of<IUserTimePreferenceService>(), attachments.Object,
            Options.Create(new TimeOptions()), Mock.Of<IUserAdornmentService>(), Mock.Of<IPetService>(), Mock.Of<IExperienceService>());
        var normal = await controller.GetUserByHttpContext();
        Assert.True(normal.IsSuccess);
        Assert.Same(permissions, Assert.IsType<CurrentUserVo>(normal.ResponseData).VoPermissions);
        Assert.Empty(capture.Events);
        users.Setup(s => s.GetPermissionKeysByRolesAsync(It.IsAny<IReadOnlyCollection<string>>())).ThrowsAsync(new HangfireUnreadableException());
        attachments.Setup(s => s.GetLatestAvatarAssetAsync(123)).ThrowsAsync(new IOException(HttpBoundaryCapture.Secret));
        var partial = await controller.GetUserByHttpContext();
        Assert.True(partial.IsSuccess);
        var value = Assert.IsType<CurrentUserVo>(partial.ResponseData);
        Assert.Empty(value.VoPermissions);
        Assert.Null(value.VoAvatarUrl);
        Assert.Equal(new[] { "user.current_permissions_degraded", "user.current_avatar_degraded" }, capture.Codes);
        Assert.Equal(new[] { "other", "io" }, capture.Events.Select(e => e.FailureKind));
        capture.AssertSafe();
        capture.Clear();
        users.Setup(s => s.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>?>())).ThrowsAsync(new IOException(HttpBoundaryCapture.Secret));
        await Assert.ThrowsAsync<IOException>(() => controller.GetUserByHttpContext());
        Assert.Empty(capture.Events); // No new owner for failures outside the two original catch blocks.
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task ClientQueries_ShouldConsumeOnce_WhileWritesStillPropagate(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var scope = capture.SensitiveScope();
        using var code = LogContext.PushProperty("EventCode", "runtime.failed");
        var manager = new Mock<IOpenIddictApplicationManager>();
        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HangfireUnreadableException());
        manager.Setup(m => m.FindByClientIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HangfireUnreadableException());
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.QueryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HangfireUnreadableException());
        var controller = Client(manager, query.Object, capture);
        Assert.False((await controller.GetClients(keyword: HttpBoundaryCapture.Secret, cancellationToken: TestContext.Current.CancellationToken)).IsSuccess);
        Assert.False((await controller.GetClient(HttpBoundaryCapture.Secret)).IsSuccess);
        Assert.Equal(new[] { "client.management.list_failed", "client.management.detail_failed" }, capture.Codes);
        Assert.All(capture.Events, e => Assert.Equal("other", e.FailureKind));
        capture.AssertSafe();
        capture.Clear();
        foreach (var call in new Func<Task>[]
        {
            () => controller.CreateClient(CreateClientInput()),
            () => controller.UpdateClient(HttpBoundaryCapture.Secret, new UpdateClientDto()),
            () => controller.DeleteClient(HttpBoundaryCapture.Secret),
            () => controller.ResetClientSecret(HttpBoundaryCapture.Secret)
        })
        {
            var failure = await Assert.ThrowsAsync<BusinessException>(call);
            Assert.Equal(500, failure.StatusCode);
            Assert.IsType<HangfireUnreadableException>(failure.InnerException);
            Assert.Empty(capture.Events);
            using var services = new ServiceCollection().BuildServiceProvider();
            using var response = new MemoryStream();
            var context = new DefaultHttpContext { RequestServices = services };
            context.Request.Path = "/api/v1/Client/CreateClient";
            context.Response.Body = response;
            var handler = new ApiExceptionHandler(capture.Factory.CreateLogger<ApiExceptionHandler>());
            Assert.True(await handler.TryHandleAsync(context, failure, TestContext.Current.CancellationToken));
            Assert.Equal(500, context.Response.StatusCode);
            Assert.Equal("http.failed", Assert.Single(capture.Codes));
            capture.AssertSafe();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task ClientWrites_ShouldExposeOnlyFixedResults_AndKeepSecretInResponse(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var scope = capture.SensitiveScope();
        using var failure = LogContext.PushProperty("failureKind", "io");
        var app = new object();
        var manager = new Mock<IOpenIddictApplicationManager>();
        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(app);
        manager.Setup(m => m.GetPropertiesAsync(app, It.IsAny<CancellationToken>())).ReturnsAsync(ImmutableDictionary<string, JsonElement>.Empty);
        manager.Setup(m => m.GetClientIdAsync(app, It.IsAny<CancellationToken>())).ReturnsAsync(HttpBoundaryCapture.Secret);
        manager.Setup(m => m.GetClientTypeAsync(app, It.IsAny<CancellationToken>())).ReturnsAsync("confidential");
        manager.Setup(m => m.PopulateAsync(It.IsAny<OpenIddictApplicationDescriptor>(), app, It.IsAny<CancellationToken>()))
            .Callback<OpenIddictApplicationDescriptor, object, CancellationToken>((d, _, _) =>
            {
                d.ClientType = "confidential";
                d.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
                d.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "radish-api");
            }).Returns(ValueTask.CompletedTask);
        var controller = Client(manager, Mock.Of<IClientApplicationQueryService>(), capture);
        var created = await controller.CreateClient(CreateClientInput());
        Assert.True(created.IsSuccess);
        Assert.False(string.IsNullOrEmpty(created.ResponseData!.ClientSecret));
        Assert.True((await controller.UpdateClient(HttpBoundaryCapture.Secret, new UpdateClientDto())).IsSuccess);
        Assert.True((await controller.DeleteClient(HttpBoundaryCapture.Secret)).IsSuccess);
        var reset = await controller.ResetClientSecret(HttpBoundaryCapture.Secret);
        Assert.True(reset.IsSuccess);
        Assert.False(string.IsNullOrEmpty(reset.ResponseData!.ClientSecret));
        Assert.Equal(new[] { "client.management.created", "client.management.updated", "client.management.soft_deleted", "client.management.secret_reset" }, capture.Codes);
        Assert.All(capture.Events, e => { Assert.Null(e.FailureKind); Assert.False(e.Diagnostic); });
        capture.AssertSafe();
        var serialized = capture.Output.ToString() + string.Join(" ", capture.Sink.Events.Select(e => e.RenderMessage() + string.Join(" ", e.Properties.Values)));
        Assert.DoesNotContain(created.ResponseData.ClientSecret!, serialized);
        Assert.DoesNotContain(reset.ResponseData.ClientSecret!, serialized);
        manager.Verify(m => m.UpdateAsync(app, It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Bootstrap_ShouldReturnCreatedAfterRewardFailure(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var global = new ApplicationLogScope(capture.Logger);
        using var scope = capture.SensitiveScope();
        var repository = new Mock<IBootstrapRepository>();
        var created = BootstrapAdminCreationResult.Created(123, "Owner", "owner@example.test");
        repository.Setup(r => r.TryCreateFirstAdministratorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PublicIndexReservationPolicy>()))
            .ReturnsAsync(created);
        var coins = new Mock<ICoinService>();
        coins.Setup(c => c.GrantRegistrationRewardAsync(123)).ThrowsAsync(new HangfireUnreadableException());
        var settings = new Mock<ISystemSettingProvider>();
        settings.Setup(s => s.GetEffectiveValueAsync(SystemConfigDefaults.PublicIndexReservedIndexesKey)).ReturnsAsync("[]");
        settings.Setup(s => s.GetEffectiveValueAsync(SystemConfigDefaults.PublicIndexVanityRulesKey)).ReturnsAsync("{}");
        var service = new BootstrapService(repository.Object, coins.Object, settings.Object);
        var result = await service.CreateFirstAdministratorAsync(new BootstrapCreateAdminDto
        { DisplayName = "Owner", Email = "owner@example.test", Password = "Strong!Pass123", ConfirmPassword = "Strong!Pass123" });
        Assert.Same(created, result);
        Assert.Equal("bootstrap.reward_degraded", Assert.Single(capture.Codes));
        Assert.Equal("other", Assert.Single(capture.Events).FailureKind);
        coins.Verify(c => c.GrantRegistrationRewardAsync(123), Times.Once);
        capture.AssertSafe();
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Dedup_ShouldKeepCacheSemanticsWithoutKeysOrRepeatedSuccessOutput(bool candidate, string environment, bool audit)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, audit: audit);
        using var scope = capture.SensitiveScope();
        var cache = new Mock<ICaching>();
        var service = new NotificationDedupService(cache.Object, capture.Factory.CreateLogger<NotificationDedupService>());
        var key = "notification:dedup:123:" + HttpBoundaryCapture.Secret + ":456";
        cache.Setup(c => c.ExistsAsync(key)).ReturnsAsync(true);
        Assert.True(await service.ShouldDedupAsync(123, HttpBoundaryCapture.Secret, 456));
        await service.RecordDedupKeyAsync(123, HttpBoundaryCapture.Secret, 456, 42);
        await service.ClearDedupKeyAsync(123, HttpBoundaryCapture.Secret, 456);
        cache.Verify(c => c.SetStringAsync(key, "1", TimeSpan.FromSeconds(42)), Times.Once);
        cache.Verify(c => c.RemoveAsync(key), Times.Once);
        Assert.Empty(capture.Events);
        cache.Setup(c => c.ExistsAsync(key)).ThrowsAsync(new HangfireUnreadableException());
        cache.Setup(c => c.SetStringAsync(key, "1", It.IsAny<TimeSpan>())).ThrowsAsync(new HangfireUnreadableException());
        cache.Setup(c => c.RemoveAsync(key)).ThrowsAsync(new HangfireUnreadableException());
        Assert.False(await service.ShouldDedupAsync(123, HttpBoundaryCapture.Secret, 456));
        await service.RecordDedupKeyAsync(123, HttpBoundaryCapture.Secret, 456);
        await service.ClearDedupKeyAsync(123, HttpBoundaryCapture.Secret, 456);
        Assert.Equal(new[] { "notification.dedup.check_failed", "notification.dedup.record_failed", "notification.dedup.clear_failed" }, capture.Codes);
        Assert.All(capture.Events, e => Assert.Equal("other", e.FailureKind));
        capture.AssertSafe();
    }

    private static ClientController Client(Mock<IOpenIddictApplicationManager> manager, IClientApplicationQueryService query, HttpBoundaryCapture capture) =>
        new(manager.Object, query, Mock.Of<ICurrentUserAccessor>(a => a.Current == new CurrentUser { UserId = 123 }), capture.Factory.CreateLogger<ClientController>());

    private static CreateClientDto CreateClientInput() => new()
    {
        ClientId = HttpBoundaryCapture.Secret, DisplayName = HttpBoundaryCapture.Secret, ClientType = "confidential",
        GrantTypes = ["client_credentials"], Scopes = ["radish-api"], Description = HttpBoundaryCapture.Secret
    };
}

internal sealed class ApplicationLogScope : IDisposable
{
    private readonly Serilog.ILogger _previous = Log.Logger;
    public ApplicationLogScope(Serilog.ILogger logger) => Log.Logger = logger;
    public void Dispose() => Log.Logger = _previous;
}
