using System;

namespace Radish.DbMigrate;

internal sealed record DeveloperDefaultsSeedDecision(bool ShouldSeed, string Stage, string Message);

internal static class DeveloperDefaultsSeedPolicy
{
    public const string EnabledConfigPath = "Seed:DeveloperDefaultsEnabled";
    public const string StageConfigPath = "RadishDeployment:Stage";

    public static DeveloperDefaultsSeedDecision Evaluate(string? enabledValue, string? stageValue)
    {
        var enabled = bool.TryParse(enabledValue, out var parsedEnabled) && parsedEnabled;
        var stage = NormalizeStage(stageValue);

        if (!enabled)
        {
            return new(
                false,
                stage,
                "Seed:DeveloperDefaultsEnabled=false，跳过 system/admin/test 开发默认账号、默认密码、默认头像和用户角色绑定。");
        }

        if (stage is "local" or "test")
        {
            return new(
                true,
                stage,
                $"Seed:DeveloperDefaultsEnabled=true 且 RadishDeployment:Stage={stage}，允许创建 system/admin/test 开发默认账号、默认密码、默认头像和用户角色绑定。");
        }

        var stageDescription = string.IsNullOrWhiteSpace(stage) ? "<未配置>" : stage;
        throw new InvalidOperationException(
            $"[Radish.DbMigrate] 拒绝创建开发默认账号：Seed:DeveloperDefaultsEnabled=true 只能用于 RadishDeployment:Stage=local/test，当前为 {stageDescription}。生产环境必须保持 false。");
    }

    private static string NormalizeStage(string? stageValue)
    {
        return string.IsNullOrWhiteSpace(stageValue)
            ? string.Empty
            : stageValue.Trim().ToLowerInvariant();
    }
}
