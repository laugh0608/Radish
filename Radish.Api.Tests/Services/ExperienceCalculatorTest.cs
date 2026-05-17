using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Radish.Extension.ExperienceExtension;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

public class ExperienceCalculatorTest
{
    [Fact]
    public void CalculateAllLevels_ShouldUseDefaultCacheExpiration_WhenConfiguredExpirationIsInvalid()
    {
        IDistributedCache distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var calculator = new ExperienceCalculator(
            Options.Create(new ExperienceCalculatorOptions
            {
                EnableCache = true,
                CacheExpirationMinutes = 0,
                MaxLevel = 2
            }),
            distributedCache);

        var result = calculator.CalculateAllLevels();

        result.Keys.ShouldBe([0, 1, 2]);
    }
}
