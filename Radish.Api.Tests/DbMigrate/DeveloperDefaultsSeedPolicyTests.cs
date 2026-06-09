using System;
using Radish.DbMigrate;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.DbMigrate;

public sealed class DeveloperDefaultsSeedPolicyTests
{
    [Theory(DisplayName = "关闭开发默认种子时不受部署阶段影响")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("local")]
    [InlineData("test")]
    [InlineData("production")]
    public void Evaluate_ShouldSkipDeveloperDefaults_WhenSwitchDisabled(string? stage)
    {
        var decision = DeveloperDefaultsSeedPolicy.Evaluate("false", stage);

        decision.ShouldSeed.ShouldBeFalse();
    }

    [Theory(DisplayName = "仅 local/test 阶段允许开启开发默认种子")]
    [InlineData("local")]
    [InlineData("LOCAL")]
    [InlineData(" test ")]
    public void Evaluate_ShouldAllowDeveloperDefaults_ForSafeStages(string stage)
    {
        var decision = DeveloperDefaultsSeedPolicy.Evaluate("true", stage);

        decision.ShouldSeed.ShouldBeTrue();
        decision.Stage.ShouldBe(stage.Trim().ToLowerInvariant());
    }

    [Theory(DisplayName = "生产或未知阶段开启开发默认种子时应失败")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("production")]
    [InlineData("staging")]
    public void Evaluate_ShouldRejectDeveloperDefaults_ForProductionOrUnknownStage(string? stage)
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            DeveloperDefaultsSeedPolicy.Evaluate("true", stage));

        exception.Message.ShouldContain("拒绝创建开发默认账号");
    }
}
