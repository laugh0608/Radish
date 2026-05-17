using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Radish.Common.CacheTool;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Common;

public class CachingTests
{
    [Fact]
    public async Task SetStringAsync_ShouldUseDefaultExpiration_WhenExpirationIsZero()
    {
        var caching = CreateCaching();

        await caching.SetStringAsync("cache:zero", "value", TimeSpan.Zero);

        (await caching.GetStringAsync("cache:zero")).ShouldBe("value");
    }

    [Fact]
    public async Task SetAsync_ShouldUseDefaultExpiration_WhenExpirationIsNegative()
    {
        var caching = CreateCaching();

        await caching.SetAsync("cache:negative", new CachePayload("value"), TimeSpan.FromSeconds(-1));

        var payload = await caching.GetAsync<CachePayload>("cache:negative");
        payload.Value.ShouldBe("value");
    }

    private static Caching CreateCaching()
    {
        IDistributedCache distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return new Caching(distributedCache);
    }

    private sealed record CachePayload(string Value);
}
