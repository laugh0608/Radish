using System;
using System.Threading.Tasks;
using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

internal static partial class InitialDataSeeder
{
    /// <summary>初始化论坛分类数据</summary>
    private static async Task SeedForumCategoriesAsync(ISqlSugarClient db)
    {
        // 默认论坛分类 ID
        const long techCategoryId = 80000;
        const long lifeCategoryId = 80001;
        const long discussCategoryId = 80002;

        // 技术交流分类
        var techExists = await db.Queryable<Category>().AnyAsync(c => c.Id == techCategoryId);
        if (!techExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认分类 Id={techCategoryId}, Name=技术交流...");

            var techCategory = new Category(new CategoryInitializationOptions("技术交流")
            {
                Slug = "tech",
                Description = "技术相关的讨论和分享",
                OrderSort = 0,
                IsEnabled = true,
                IsDeleted = false
            })
            {
                Id = techCategoryId,
            };

            await db.Insertable(techCategory).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={techCategoryId} 的技术交流分类，跳过创建。");
        }

        // 生活随笔分类
        var lifeExists = await db.Queryable<Category>().AnyAsync(c => c.Id == lifeCategoryId);
        if (!lifeExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认分类 Id={lifeCategoryId}, Name=生活随笔...");

            var lifeCategory = new Category(new CategoryInitializationOptions("生活随笔")
            {
                Slug = "life",
                Description = "记录生活点滴，分享日常感悟",
                OrderSort = 1,
                IsEnabled = true,
                IsDeleted = false
            })
            {
                Id = lifeCategoryId,
            };

            await db.Insertable(lifeCategory).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={lifeCategoryId} 的生活随笔分类，跳过创建。");
        }

        // 问答讨论分类
        var discussExists = await db.Queryable<Category>().AnyAsync(c => c.Id == discussCategoryId);
        if (!discussExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认分类 Id={discussCategoryId}, Name=问答讨论...");

            var discussCategory = new Category(new CategoryInitializationOptions("问答讨论")
            {
                Slug = "discuss",
                Description = "提问解答，经验交流",
                OrderSort = 2,
                IsEnabled = true,
                IsDeleted = false
            })
            {
                Id = discussCategoryId,
            };

            await db.Insertable(discussCategory).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={discussCategoryId} 的问答讨论分类，跳过创建。");
        }
    }

    /// <summary>初始化论坛标签数据</summary>
    private static async Task SeedForumTagsAsync(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<Tag>();
        Console.WriteLine("[Radish.DbMigrate] 已同步 Tag 表结构（自动补齐缺失列）。");

        var fixedTags = new[]
        {
            new { Id = 90100L, Name = "社区新闻", Color = "#1677FF", SortOrder = 1 },
            new { Id = 90101L, Name = "社区活动", Color = "#52C41A", SortOrder = 2 },
            new { Id = 90102L, Name = "精华帖", Color = "#FAAD14", SortOrder = 3 },
            new { Id = 90103L, Name = "碎碎念", Color = "#722ED1", SortOrder = 4 },
            new { Id = 90104L, Name = "公告", Color = "#F5222D", SortOrder = 5 }
        };

        foreach (var meta in fixedTags)
        {
            var existingByName = await db.Queryable<Tag>()
                .FirstAsync(t => t.Name == meta.Name);

            if (existingByName != null)
            {
                existingByName.Color = meta.Color;
                existingByName.SortOrder = meta.SortOrder;
                existingByName.IsEnabled = true;
                existingByName.IsFixed = true;
                existingByName.IsDeleted = false;
                existingByName.DeletedAt = null;
                existingByName.DeletedBy = null;
                existingByName.ModifyTime = DateTime.Now;
                existingByName.ModifyBy = "System";

                await db.Updateable(existingByName).ExecuteCommandAsync();
                Console.WriteLine($"[Radish.DbMigrate] 已更新固定标签 Name={meta.Name} 的配置。");
                continue;
            }

            var existsById = await db.Queryable<Tag>().AnyAsync(t => t.Id == meta.Id);
            if (existsById)
            {
                Console.WriteLine($"[Radish.DbMigrate] Id={meta.Id} 已存在且名称不同，跳过创建 Name={meta.Name}。");
                continue;
            }

            Console.WriteLine($"[Radish.DbMigrate] 创建固定标签 Id={meta.Id}, Name={meta.Name}...");

            var tag = new Tag(new TagInitializationOptions(meta.Name)
            {
                Color = meta.Color,
                SortOrder = meta.SortOrder,
                IsEnabled = true,
                IsFixed = true,
                IsDeleted = false,
                CreateBy = "System",
                CreateId = 0
            })
            {
                Id = meta.Id
            };

            await db.Insertable(tag).ExecuteCommandAsync();
        }
    }
}
