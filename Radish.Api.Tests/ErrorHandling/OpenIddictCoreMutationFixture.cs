using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.Core;

namespace Radish.Api.Tests.ErrorHandling;

public sealed class CoreLogEntity
{
    public string Status = "valid";
    public DateTimeOffset? RedemptionDate;
}

/// <summary>真实 Core manager 的存储 / 缓存边界替身，单独记录持久化返回点与可变实体状态。</summary>
internal sealed class OpenIddictCoreMutationFixture
{
    internal static readonly DateTimeOffset Now = new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
    internal readonly CoreLogEntity Entity = new();
    internal readonly List<string> Calls = new();
    internal readonly List<CancellationToken> Tokens = new();
    internal readonly OpenIddictTokenManager<CoreLogEntity> TokenManager;
    internal readonly OpenIddictAuthorizationManager<CoreLogEntity> AuthorizationManager;
    internal readonly Mock<IOpenIddictTokenStore<CoreLogEntity>> TokenStore = new(MockBehavior.Strict);
    internal readonly Mock<IOpenIddictTokenCache<CoreLogEntity>> TokenCache = new(MockBehavior.Strict);
    internal readonly Exception Failure;
    internal readonly Exception SecondaryFailure = new InvalidOperationException("secondary " + HttpBoundaryCapture.Secret);
    internal readonly string Fault;
    internal int Writes;
    internal string? PersistedStatus;
    internal int IdReads;

    internal OpenIddictCoreMutationFixture(ILoggerFactory factory, string fault = "none", bool disableCache = false)
    {
        Fault = fault;
        Failure = fault switch
        {
            "concurrency" => new OpenIddictExceptions.ConcurrencyException(HttpBoundaryCapture.Secret),
            "update-cancel" or "id-cancel" => new OperationCanceledException(HttpBoundaryCapture.Secret),
            "fatal" => new OutOfMemoryException(HttpBoundaryCapture.Secret),
            _ => new IOException(HttpBoundaryCapture.Secret)
        };
        var options = new OpenIddictOptions<OpenIddictCoreOptions>(new()
        {
            DisableEntityCaching = disableCache, TimeProvider = new FixedTime()
        });
        TokenStore.Setup(s => s.GetReferenceIdAsync(Entity, It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        TokenStore.Setup(s => s.GetTypeAsync(Entity, It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, CancellationToken ct) => Type("access_token", ct));
        TokenStore.Setup(s => s.GetStatusAsync(Entity, It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, CancellationToken ct) => Status(ct));
        TokenStore.Setup(s => s.SetStatusAsync(Entity, It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, string? status, CancellationToken ct) => SetStatus(status, ct));
        TokenStore.Setup(s => s.GetRedemptionDateAsync(Entity, It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, CancellationToken ct) =>
        {
            Touch("get-date", ct);
            return new ValueTask<DateTimeOffset?>(Entity.RedemptionDate);
        });
        TokenStore.Setup(s => s.SetRedemptionDateAsync(Entity, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, DateTimeOffset? date, CancellationToken ct) =>
        {
            Touch("set-date", ct);
            Entity.RedemptionDate = date;
            return default;
        });
        TokenStore.Setup(s => s.UpdateAsync(Entity, It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, CancellationToken ct) => Update(ct));
        TokenStore.Setup(s => s.GetIdAsync(Entity, It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, CancellationToken ct) => Id(ct));
        TokenStore.Setup(s => s.FindByIdAsync(HttpBoundaryCapture.Secret, It.IsAny<CancellationToken>())).ReturnsAsync(Entity);
        TokenCache.Setup(c => c.FindByIdAsync(HttpBoundaryCapture.Secret, It.IsAny<CancellationToken>())).ReturnsAsync(Entity);
        TokenCache.Setup(c => c.RemoveAsync(Entity, It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, CancellationToken ct) => Cache("cache-remove", ct));
        TokenCache.Setup(c => c.AddAsync(Entity, It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, CancellationToken ct) => Cache("cache-add", ct));
        TokenManager = new(TokenCache.Object, factory.CreateLogger<OpenIddictTokenManager<CoreLogEntity>>(), options, TokenStore.Object);

        var store = new Mock<IOpenIddictAuthorizationStore<CoreLogEntity>>(MockBehavior.Strict);
        var cache = new Mock<IOpenIddictAuthorizationCache<CoreLogEntity>>(MockBehavior.Strict);
        store.Setup(s => s.GetTypeAsync(Entity, It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, CancellationToken ct) => Type("permanent", ct));
        store.Setup(s => s.GetStatusAsync(Entity, It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, CancellationToken ct) => Status(ct));
        store.Setup(s => s.GetScopesAsync(Entity, It.IsAny<CancellationToken>())).ReturnsAsync(ImmutableArray<string>.Empty);
        store.Setup(s => s.SetStatusAsync(Entity, It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, string? status, CancellationToken ct) => SetStatus(status, ct));
        store.Setup(s => s.UpdateAsync(Entity, It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, CancellationToken ct) => Update(ct));
        store.Setup(s => s.GetIdAsync(Entity, It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, CancellationToken ct) => Id(ct));
        cache.Setup(c => c.RemoveAsync(Entity, It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, CancellationToken ct) => Cache("cache-remove", ct));
        cache.Setup(c => c.AddAsync(Entity, It.IsAny<CancellationToken>())).Returns((CoreLogEntity _, CancellationToken ct) => Cache("cache-add", ct));
        AuthorizationManager = new(cache.Object, factory.CreateLogger<OpenIddictAuthorizationManager<CoreLogEntity>>(), options, store.Object);
    }

    internal ValueTask<bool> Mutate(string operation, CancellationToken cancellationToken = default) => operation switch
    {
        "redeem" => TokenManager.TryRedeemAsync(Entity, cancellationToken),
        "reject" => TokenManager.TryRejectAsync(Entity, cancellationToken),
        "revoke" => TokenManager.TryRevokeAsync(Entity, cancellationToken),
        "authorization" => AuthorizationManager.TryRevokeAsync(Entity, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private void Touch(string phase, CancellationToken token)
    {
        Calls.Add(phase);
        Tokens.Add(token);
        if (Fault == phase) throw Failure;
    }
    private ValueTask<string?> Type(string value, CancellationToken token)
    {
        Touch("get-type", token);
        return new(Fault == "validation" ? null : value);
    }
    private ValueTask<string?> Status(CancellationToken token) { Touch("get-status", token); return new(Entity.Status); }
    private ValueTask SetStatus(string? status, CancellationToken token)
    {
        Touch("set-status", token);
        Entity.Status = status!;
        return default;
    }
    private ValueTask Update(CancellationToken token)
    {
        Touch("update", token);
        if (Fault is "concurrency" or "update-io" or "update-cancel" or "fatal" or "secondary-id") throw Failure;
        Writes++;
        PersistedStatus = Entity.Status;
        return default;
    }
    private ValueTask Cache(string phase, CancellationToken token) { Touch(phase, token); return default; }
    private ValueTask<string?> Id(CancellationToken token)
    {
        Touch("id", token);
        IdReads++;
        if (Fault == "secondary-id") throw SecondaryFailure;
        if (Fault is "id-always" or "id-cancel" || Fault == "id-once" && IdReads == 1) throw Failure;
        return new(HttpBoundaryCapture.Secret);
    }
    private sealed class FixedTime : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
