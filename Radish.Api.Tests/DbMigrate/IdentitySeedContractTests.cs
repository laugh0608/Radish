using System.Linq;
using System.Reflection;
using Radish.DbMigrate;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.DbMigrate;

public sealed class IdentitySeedContractTests
{
    [Fact(DisplayName = "开发默认账号种子应固定邮箱展示名和保留 PublicIndex")]
    public void DeveloperDefaultUserSeeds_ShouldUseEmailDisplayNameAndReservedPublicIndexes()
    {
        var seeds = InitialDataSeeder.DeveloperDefaultUserSeeds;

        seeds.Count.ShouldBe(3);
        seeds.Select(seed => seed.Email).ShouldBe(["system@radishx.com", "admin@radishx.com", "test@radishx.com"]);
        seeds.Select(seed => seed.DisplayName).ShouldBe(["System", "Admin", "TestUser"]);
        seeds.Select(seed => seed.PublicIndex).ShouldBe([1L, 2L, 3L]);
    }

    [Theory(DisplayName = "DbMigrate 不应保留旧用户身份回填和保留号纠偏入口")]
    [InlineData("BackfillMissingUserPublicIds")]
    [InlineData("BackfillMissingUserPublicIndexes")]
    [InlineData("NormalizeReservedUserPublicIndexes")]
    [InlineData("CorrectSeedUserPublicIndex")]
    [InlineData("ResolveNextNormalUserPublicIndex")]
    [InlineData("ResolveReservedPublicIndex")]
    public void DbMigrateRunner_ShouldNotContainLegacyIdentityBackfillMethods(string methodName)
    {
        typeof(DbMigrateRunner)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            .ShouldBeNull();
    }
}
