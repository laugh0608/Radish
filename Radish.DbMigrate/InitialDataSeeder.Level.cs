using Microsoft.Extensions.DependencyInjection;
using Radish.IService;
using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

internal static partial class InitialDataSeeder
{
    /// <summary>初始化等级配置（Lv.0-10 修仙体系）</summary>
    private static async Task SeedLevelConfigsAsync(ISqlSugarClient db, IServiceProvider services)
    {
        // 从 DI 容器获取经验值计算器
        var calculator = services.GetRequiredService<IExperienceCalculator>();

        // 使用动态计算器生成所有等级的经验值配置
        var levelExpData = calculator.CalculateAllLevels();

        Console.WriteLine($"[Radish.DbMigrate] 使用 {calculator.GetFormulaType()} 公式计算经验值");
        Console.WriteLine($"[Radish.DbMigrate] 配置摘要: {calculator.GetConfigSummary()}");

        // 11 级修仙体系配置（等级名称、颜色、描述）
        var levelMetadata = new[]
        {
            new { Level = 0, Name = "凡人", Color = "#9E9E9E", Desc = "新用户初始等级" },
            new { Level = 1, Name = "练气", Color = "#4CAF50", Desc = "开始修炼之路" },
            new { Level = 2, Name = "筑基", Color = "#2196F3", Desc = "打下坚实基础" },
            new { Level = 3, Name = "金丹", Color = "#FFC107", Desc = "凝聚金丹，小有所成" },
            new { Level = 4, Name = "元婴", Color = "#FF9800", Desc = "修成元婴，中流砥柱" },
            new { Level = 5, Name = "化神", Color = "#FF5722", Desc = "化神境界，神通初显" },
            new { Level = 6, Name = "炼虚", Color = "#9C27B0", Desc = "炼虚合道，高手境界" },
            new { Level = 7, Name = "合体", Color = "#673AB7", Desc = "合体大能，社区精英" },
            new { Level = 8, Name = "大乘", Color = "#3F51B5", Desc = "大乘期修士，德高望重" },
            new { Level = 9, Name = "渡劫", Color = "#E91E63", Desc = "渡劫飞升，传说人物" },
            new { Level = 10, Name = "飞升", Color = "#FFD700", Desc = "羽化飞升，至高荣耀" },
        };

        foreach (var meta in levelMetadata)
        {
            var exists = await db.Queryable<LevelConfig>().AnyAsync(l => l.Level == meta.Level);
            if (!exists)
            {
                // 从计算器获取该等级的经验值数据
                var (expRequired, expCumulative) = levelExpData[meta.Level];

                Console.WriteLine($"[Radish.DbMigrate] 创建等级配置 Lv.{meta.Level} ({meta.Name}) - 需要经验: {expRequired}, 累计: {expCumulative}");

                var levelConfig = new LevelConfig
                {
                    Level = meta.Level,
                    LevelName = meta.Name,
                    ExpRequired = expRequired,
                    ExpCumulative = expCumulative,
                    ThemeColor = meta.Color,
                    Description = meta.Desc,
                    IsEnabled = true,
                    SortOrder = meta.Level,
                    CreateTime = DateTime.Now,
                    CreateBy = "System"
                };

                await db.Insertable(levelConfig).ExecuteCommandAsync();
            }
            else
            {
                Console.WriteLine($"[Radish.DbMigrate] 已存在 Lv.{meta.Level} ({meta.Name}) 的等级配置，跳过创建。");
            }
        }
    }
}
