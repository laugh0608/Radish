using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common;
using Radish.Common.HelpTool;
using Radish.Common.TenantTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.Models;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>
/// 初始化基础数据（角色/用户/租户/部门）的 Seed 入口。
/// 后续若需要扩展更多种子数据，可在此类中继续拆分方法。
/// </summary>
internal static class InitialDataSeeder
{
    public static async Task SeedAsync(ISqlSugarClient db, IServiceProvider services)
    {
        await SeedRolesAsync(db);
        await SeedTenantsAsync(db);
        await SeedDepartmentsAsync(db);
        await SeedUsersAsync(db);
        await SeedUserTimePreferencesAsync(db);
        await SeedUserRolesAsync(db);
        await SeedPermissionsAsync(db);
        await SeedForumCategoriesAsync(db);
        await SeedForumTagsAsync(db);
        await SeedLevelConfigsAsync(db, services);
        await SeedShopCategoriesAsync(db);
        await SeedShopProductsAsync(db);

        Console.WriteLine("[Radish.DbMigrate] ✓ Seed 完成:");
        Console.WriteLine("  - 角色/租户/部门/用户/用户角色");
        Console.WriteLine("  - 用户时区偏好");
        Console.WriteLine("  - 角色-API 权限/论坛分类/标签");
        Console.WriteLine("  - 等级配置/商城分类/商品");
    }

    /// <summary>初始化用户时区偏好（默认值来自 Time:DefaultTimeZoneId）</summary>
    private static async Task SeedUserTimePreferencesAsync(ISqlSugarClient db)
    {
        // 兼容旧库直接执行 seed：先确保 UserTimePreference 表结构存在
        db.CodeFirst.InitTables<UserTimePreference>();
        Console.WriteLine("[Radish.DbMigrate] 已同步 UserTimePreference 表结构（自动补齐缺失表/列）。");

        const long systemUserId = 20000;
        const long adminUserId = 20001;
        const long testUserId = 20002;
        const long radishTenantId = 30000;

        var configuredDefaultTimeZoneId = AppSettingsTool.RadishApp("Time", "DefaultTimeZoneId");
        var defaultTimeZoneId = string.IsNullOrWhiteSpace(configuredDefaultTimeZoneId)
            ? "Asia/Shanghai"
            : configuredDefaultTimeZoneId.Trim();

        var seedItems = new[]
        {
            new { Id = 71000L, UserId = systemUserId, UserName = "system" },
            new { Id = 71001L, UserId = adminUserId, UserName = "admin" },
            new { Id = 71002L, UserId = testUserId, UserName = "test" }
        };

        foreach (var item in seedItems)
        {
            var exists = await db.Queryable<UserTimePreference>().AnyAsync(p => p.UserId == item.UserId);
            if (exists)
            {
                Console.WriteLine($"[Radish.DbMigrate] 用户 Id={item.UserId} 的时区偏好已存在，跳过创建。");
                continue;
            }

            Console.WriteLine($"[Radish.DbMigrate] 创建用户 Id={item.UserId} ({item.UserName}) 的时区偏好：{defaultTimeZoneId}...");
            await db.Insertable(new UserTimePreference
            {
                Id = item.Id,
                UserId = item.UserId,
                TenantId = radishTenantId,
                TimeZoneId = defaultTimeZoneId,
                CreateBy = "System",
                ModifyBy = "System",
                ModifyTime = DateTime.UtcNow
            }).ExecuteCommandAsync();
        }
    }

    /// <summary>初始化用户-角色关系</summary>
    private static async Task SeedUserRolesAsync(ISqlSugarClient db)
    {
        // 与用户/角色种子中的固定 Id 对齐
        const long systemUserId = 20000;
        const long adminUserId = 20001;
        const long testUserId = 20002;

        const long systemRoleId = 10000;
        const long adminRoleId = 10001;
        const long testRoleId = 10002;

        // UserRole 关联记录的固定 ID
        const long userRoleId1 = 70000; // system -> System
        const long userRoleId2 = 70001; // admin -> Admin
        const long userRoleId3 = 70002; // test -> Test

        // system 用户 -> System 角色
        var exists = await db.Queryable<UserRole>().AnyAsync(ur => ur.UserId == systemUserId && ur.RoleId == systemRoleId);
        if (!exists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 绑定用户 Id={systemUserId} (system) 到角色 Id={systemRoleId} (System)...");
            await db.Insertable(new UserRole
            {
                Id = userRoleId1,
                UserId = systemUserId,
                RoleId = systemRoleId,
                IsDeleted = false,
                CreateBy = "System",
            }).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在用户 Id={systemUserId} 与角色 Id={systemRoleId} 的绑定，跳过创建。");
        }

        // admin 用户 -> Admin 角色
        exists = await db.Queryable<UserRole>().AnyAsync(ur => ur.UserId == adminUserId && ur.RoleId == adminRoleId);
        if (!exists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 绑定用户 Id={adminUserId} (admin) 到角色 Id={adminRoleId} (Admin)...");
            await db.Insertable(new UserRole
            {
                Id = userRoleId2,
                UserId = adminUserId,
                RoleId = adminRoleId,
                IsDeleted = false,
                CreateBy = "System",
            }).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在用户 Id={adminUserId} 与角色 Id={adminRoleId} 的绑定，跳过创建。");
        }

        // test 用户 -> Test 角色
        exists = await db.Queryable<UserRole>().AnyAsync(ur => ur.UserId == testUserId && ur.RoleId == testRoleId);
        if (!exists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 绑定用户 Id={testUserId} (test) 到角色 Id={testRoleId} (Test)...");
            await db.Insertable(new UserRole
            {
                Id = userRoleId3,
                UserId = testUserId,
                RoleId = testRoleId,
                IsDeleted = false,
                CreateBy = "System",
            }).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在用户 Id={testUserId} 与角色 Id={testRoleId} 的绑定，跳过创建。");
        }
    }

    /// <summary>初始化角色-API 权限（示例：允许 System/Admin 访问用户基本信息接口）</summary>
    private static async Task SeedPermissionsAsync(ISqlSugarClient db)
    {
        // 为 UserController.GetUserByHttpContext 建立 ApiModule 与 RoleModulePermission
        // 便于通过 RadishAuthPolicy 或客户端权限检查进行验证。

        const long userByHttpContextApiId = 50000;
        const string linkUrl = "/api/v1/User/GetUserByHttpContext";

        var apiExists = await db.Queryable<ApiModule>().AnyAsync(m => m.Id == userByHttpContextApiId);
        if (!apiExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建 ApiModule Id={userByHttpContextApiId}, LinkUrl={linkUrl}...");

            var options = new ApiModuleInitializationOptions("Get current user by HttpContext", linkUrl)
            {
                ControllerName = "User",
                ActionName = "GetUserByHttpContext",
                IsEnabled = true,
                IsDeleted = false,
                IsMenu = false,
                OrderSort = 0,
            };

            var module = new ApiModule(options)
            {
                Id = userByHttpContextApiId,
            };

            await db.Insertable(module).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={userByHttpContextApiId} 的 ApiModule，跳过创建。");
        }

        // System + Admin + Test 默认都可以访问该接口
        const long systemRoleId = 10000;
        const long adminRoleId = 10001;
        const long testRoleId = 10002;

        await EnsureRoleApiPermissionAsync(db, systemRoleId, userByHttpContextApiId, "System");
        await EnsureRoleApiPermissionAsync(db, adminRoleId, userByHttpContextApiId, "Admin");
        await EnsureRoleApiPermissionAsync(db, testRoleId, userByHttpContextApiId, "Test");
    }

    private static async Task EnsureRoleApiPermissionAsync(ISqlSugarClient db, long roleId, long apiModuleId,
        string roleName)
    {
        var exists = await db.Queryable<RoleModulePermission>()
            .AnyAsync(p => p.RoleId == roleId && p.ApiModuleId == apiModuleId);
        if (exists)
        {
            Console.WriteLine(
                $"[Radish.DbMigrate] 已存在角色 Id={roleId} 与 ApiModule Id={apiModuleId} 的权限记录，跳过创建。");
            return;
        }

        Console.WriteLine(
            $"[Radish.DbMigrate] 创建角色 Id={roleId} ({roleName}) 对 ApiModule Id={apiModuleId} 的访问权限...");

        // 为种子权限使用固定、靠后的 Id 段，避免与历史数据的主键冲突
        var permId = roleId switch
        {
            10000 => 60000, // System 对 GetUserByHttpContext
            10001 => 60001, // Admin 对 GetUserByHttpContext
            10002 => 60002, // Test 对 GetUserByHttpContext
            _ => 0          // 其它角色走默认雪花/自增配置
        };

        var perm = new RoleModulePermission
        {
            Id = permId,
            RoleId = roleId,
            ApiModuleId = apiModuleId,
            IsDeleted = false,
            CreateBy = "System",
        };

        await db.Insertable(perm).ExecuteCommandAsync();
    }

    /// <summary>初始化角色相关数据</summary>
    private static async Task SeedRolesAsync(ISqlSugarClient db)
    {
        // 固定 Id 的系统默认角色，避免雪花 ID 随机值带来的难以记忆
        const long systemRoleId = 10000;
        const long adminRoleId = 10001;
        const long testRoleId = 10002;

        // System 角色
        var systemExists = await db.Queryable<Role>().AnyAsync(r => r.Id == systemRoleId);
        if (!systemExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认角色 Id={systemRoleId}, RoleName=System...");

            var systemRole = new Role("System")
            {
                Id = systemRoleId,
                RoleDescription = "System built-in role (超级管理员，拥有系统级权限)",
                IsDeleted = false,
                IsEnabled = true,
                OrderSort = 0,
                DepartmentIds = string.Empty,
            };

            await db.Insertable(systemRole).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={systemRoleId} 的 System 角色，跳过创建。");
        }

        // Admin 角色
        var adminExists = await db.Queryable<Role>().AnyAsync(r => r.Id == adminRoleId);
        if (!adminExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认角色 Id={adminRoleId}, RoleName=Admin...");

            var adminRole = new Role("Admin")
            {
                Id = adminRoleId,
                RoleDescription = "Admin built-in role (管理员，拥有常规管理权限)",
                IsDeleted = false,
                IsEnabled = true,
                OrderSort = 1,
                DepartmentIds = string.Empty,
            };

            await db.Insertable(adminRole).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={adminRoleId} 的 Admin 角色，跳过创建。");
        }

        // Test 角色
        var testExists = await db.Queryable<Role>().AnyAsync(r => r.Id == testRoleId);
        if (!testExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认角色 Id={testRoleId}, RoleName=Test...");

            var testRole = new Role("Test")
            {
                Id = testRoleId,
                RoleDescription = "Test built-in role (测试角色，用于测试普通用户权限)",
                IsDeleted = false,
                IsEnabled = true,
                OrderSort = 2,
                DepartmentIds = string.Empty,
            };

            await db.Insertable(testRole).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={testRoleId} 的 Test 角色，跳过创建。");
        }
    }

    /// <summary>初始化租户相关数据</summary>
    private static async Task SeedTenantsAsync(ISqlSugarClient db)
    {
        // 默认租户（30000 Radish, 30001 Test）
        const long radishTenantId = 30000;
        const long testTenantId = 30001;

        var radishTenantExists = await db.Queryable<Tenant>().AnyAsync(t => t.Id == radishTenantId);
        if (!radishTenantExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认租户 Id={radishTenantId}, TenantName=Radish...");

            var radishTenant = new Tenant("Radish", TenantTypeEnum.None)
            {
                Id = radishTenantId,
                IsEnable = true,
                TenantConfigId = "Main",
                TenantRemark = "Default Radish tenant",
            };

            await db.Insertable(radishTenant).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={radishTenantId} 的 Radish 租户，跳过创建。");
        }

        var testTenantExists = await db.Queryable<Tenant>().AnyAsync(t => t.Id == testTenantId);
        if (!testTenantExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认租户 Id={testTenantId}, TenantName=Test...");

            var testTenant = new Tenant("Test", TenantTypeEnum.None)
            {
                Id = testTenantId,
                IsEnable = true,
                TenantConfigId = "Test",
                TenantRemark = "Test tenant",
            };

            await db.Insertable(testTenant).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={testTenantId} 的 Test 租户，跳过创建。");
        }
    }

    /// <summary>初始化部门相关数据</summary>
    private static async Task SeedDepartmentsAsync(ISqlSugarClient db)
    {
        // 默认部门（40000 Development, 40001 Test）
        const long devDeptId = 40000;
        const long testDeptId = 40001;

        var devDeptExists = await db.Queryable<Department>().AnyAsync(d => d.Id == devDeptId);
        if (!devDeptExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认部门 Id={devDeptId}, DepartmentName=Development...");

            var devDept = new Department("Development")
            {
                Id = devDeptId,
                Pid = 0,
                OrderSort = 0,
                StatusCode = (int)DepartmentStatusCodeEnum.Normal,
                IsDeleted = false,
                CreateBy = "System",
            };

            await db.Insertable(devDept).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={devDeptId} 的 Development 部门，跳过创建。");
        }

        var testDeptExists = await db.Queryable<Department>().AnyAsync(d => d.Id == testDeptId);
        if (!testDeptExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认部门 Id={testDeptId}, DepartmentName=Test...");

            var testDept = new Department("Test")
            {
                Id = testDeptId,
                Pid = 0,
                OrderSort = 1,
                StatusCode = (int)DepartmentStatusCodeEnum.Normal,
                IsDeleted = false,
                CreateBy = "System",
            };

            await db.Insertable(testDept).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={testDeptId} 的 Test 部门，跳过创建。");
        }
    }

    /// <summary>初始化用户相关数据</summary>
    private static async Task SeedUsersAsync(ISqlSugarClient db)
    {
        // 默认用户 ID
        const long systemUserId = 20000;
        const long adminUserId = 20001;
        const long testUserId = 20002;

        // 与租户、部门保持固定 Id 对齐
        const long radishTenantId = 30000;
        const long devDeptId = 40000;

        // 创建 system 用户
        var systemUserExists = await db.Queryable<User>().AnyAsync(u => u.Id == systemUserId);
        if (!systemUserExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认用户 Id={systemUserId}, LoginName=system...");

            var systemUserOptions = new UserInitializationOptions("system", PasswordHasher.HashPassword("system123456"))
            {
                UserName = "system",
                UserRealName = "System User",
                UserSex = (int)UserSexEnum.Unknown,
                UserAge = 30,
                UserBirth = DateTime.Today.AddYears(-30),
                TenantId = radishTenantId,
                DepartmentId = devDeptId,
                IsEnable = true,
                IsDeleted = false,
                StatusCode = (int)UserStatusCodeEnum.Normal,
                Remark = "System administrator user",
            };

            var systemUser = new User(systemUserOptions)
            {
                Id = systemUserId,
            };

            await db.Insertable(systemUser).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={systemUserId} 的 system 用户，跳过创建。");
        }

        // 创建 admin 用户
        var adminUserExists = await db.Queryable<User>().AnyAsync(u => u.Id == adminUserId);
        if (!adminUserExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认用户 Id={adminUserId}, LoginName=admin...");

            var adminUserOptions = new UserInitializationOptions("admin", PasswordHasher.HashPassword("admin123456"))
            {
                UserName = "admin",
                UserRealName = "Admin User",
                UserSex = (int)UserSexEnum.Unknown,
                UserAge = 25,
                UserBirth = DateTime.Today.AddYears(-25),
                TenantId = radishTenantId,
                DepartmentId = devDeptId,
                IsEnable = true,
                IsDeleted = false,
                StatusCode = (int)UserStatusCodeEnum.Normal,
                Remark = "Administrator user",
            };

            var adminUser = new User(adminUserOptions)
            {
                Id = adminUserId,
            };

            await db.Insertable(adminUser).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={adminUserId} 的 admin 用户，跳过创建。");
        }

        // 创建 test 用户
        var testUserExists = await db.Queryable<User>().AnyAsync(u => u.Id == testUserId);
        if (!testUserExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认用户 Id={testUserId}, LoginName=test...");

            var testUserOptions = new UserInitializationOptions("test", PasswordHasher.HashPassword("test123456"))
            {
                UserName = "test",
                UserRealName = "Test User",
                UserSex = (int)UserSexEnum.Unknown,
                UserAge = 18,
                UserBirth = DateTime.Today.AddYears(-18),
                TenantId = radishTenantId,
                DepartmentId = devDeptId,
                IsEnable = true,
                IsDeleted = false,
                StatusCode = (int)UserStatusCodeEnum.Normal,
                Remark = "Test user",
            };

            var testUser = new User(testUserOptions)
            {
                Id = testUserId,
            };

            await db.Insertable(testUser).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={testUserId} 的 test 用户，跳过创建。");
        }
    }

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

    /// <summary>初始化商城分类数据</summary>
    private static async Task SeedShopCategoriesAsync(ISqlSugarClient db)
    {
        var categories = new[]
        {
            new ProductCategory
            {
                Id = "badge",
                Name = "徽章收藏",
                Icon = "badge",
                Description = "专属徽章，展示你的独特身份",
                SortOrder = 0,
                IsEnabled = true,
                CreateTime = DateTime.Now
            },
            new ProductCategory
            {
                Id = "frame",
                Name = "头像框",
                Icon = "frame",
                Description = "精美头像框，装点个人形象",
                SortOrder = 1,
                IsEnabled = true,
                CreateTime = DateTime.Now
            },
            new ProductCategory
            {
                Id = "title",
                Name = "称号",
                Icon = "title",
                Description = "独特称号，彰显个性",
                SortOrder = 2,
                IsEnabled = true,
                CreateTime = DateTime.Now
            },
            new ProductCategory
            {
                Id = "signature",
                Name = "签名档",
                Icon = "signature",
                Description = "个性签名，留下你的专属印记",
                SortOrder = 3,
                IsEnabled = true,
                CreateTime = DateTime.Now
            },
            new ProductCategory
            {
                Id = "effect",
                Name = "特效装扮",
                Icon = "sparkles",
                Description = "点赞特效、用户名特效等",
                SortOrder = 4,
                IsEnabled = true,
                CreateTime = DateTime.Now
            },
            new ProductCategory
            {
                Id = "theme",
                Name = "主题皮肤",
                Icon = "palette",
                Description = "个性化界面主题",
                SortOrder = 5,
                IsEnabled = true,
                CreateTime = DateTime.Now
            }
        };

        foreach (var category in categories)
        {
            var exists = await db.Queryable<ProductCategory>().AnyAsync(c => c.Id == category.Id);
            if (!exists)
            {
                Console.WriteLine($"[Radish.DbMigrate] 创建商品分类 Id={category.Id}, Name={category.Name}...");
                await db.Insertable(category).ExecuteCommandAsync();
            }
            else
            {
                Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={category.Id} 的商品分类，跳过创建。");
            }
        }
    }

    /// <summary>初始化商城商品数据</summary>
    private static async Task SeedShopProductsAsync(ISqlSugarClient db)
    {
        // 商品 ID 从 100000 开始
        var products = new[]
        {
            // ========== 徽章类 ==========
            new Product
            {
                Id = 100001,
                Name = "元老徽章",
                Description = "社区元老专属徽章，见证社区成长",
                Icon = "badge-veteran",
                CategoryId = "badge",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Badge,
                BenefitValue = "badge-veteran",
                Price = 500,
                StockType = StockType.Limited,
                Stock = 100,
                LimitPerUser = 1,
                DurationType = DurationType.Permanent,
                SortOrder = 0,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100002,
                Name = "萝卜爱好者",
                Description = "热爱萝卜的小伙伴专属徽章",
                Icon = "badge-carrot-lover",
                CategoryId = "badge",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Badge,
                BenefitValue = "badge-carrot-lover",
                Price = 200,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 1,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100003,
                Name = "夜猫子",
                Description = "深夜活跃用户专属徽章",
                Icon = "badge-night-owl",
                CategoryId = "badge",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Badge,
                BenefitValue = "badge-night-owl",
                Price = 150,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 2,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },

            // ========== 头像框类 ==========
            new Product
            {
                Id = 100011,
                Name = "星光头像框",
                Description = "闪烁的星光环绕，璀璨夺目",
                Icon = "frame-star",
                CategoryId = "frame",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.AvatarFrame,
                BenefitValue = "frame-star",
                Price = 300,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Days,
                DurationDays = 30,
                SortOrder = 0,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100012,
                Name = "萝卜花环",
                Description = "可爱的萝卜花环头像框",
                Icon = "frame-carrot-wreath",
                CategoryId = "frame",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.AvatarFrame,
                BenefitValue = "frame-carrot-wreath",
                Price = 250,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Days,
                DurationDays = 30,
                SortOrder = 1,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100013,
                Name = "极简线条框",
                Description = "简约而不简单的线条设计",
                Icon = "frame-minimal",
                CategoryId = "frame",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.AvatarFrame,
                BenefitValue = "frame-minimal",
                Price = 100,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 2,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },

            // ========== 称号类 ==========
            new Product
            {
                Id = 100021,
                Name = "萝卜达人",
                Description = "专属称号「萝卜达人」，显示在用户名旁",
                Icon = "title-expert",
                CategoryId = "title",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Title,
                BenefitValue = "萝卜达人",
                Price = 200,
                StockType = StockType.Limited,
                Stock = 50,
                LimitPerUser = 1,
                DurationType = DurationType.Permanent,
                SortOrder = 0,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100022,
                Name = "用爱发电",
                Description = "专属称号「用爱发电」，致敬每一位贡献者",
                Icon = "title-love-power",
                CategoryId = "title",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Title,
                BenefitValue = "用爱发电",
                Price = 150,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 1,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100023,
                Name = "快乐水源",
                Description = "专属称号「快乐水源」，传递快乐的人",
                Icon = "title-joy",
                CategoryId = "title",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Title,
                BenefitValue = "快乐水源",
                Price = 150,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 2,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },

            // ========== 签名档类 ==========
            new Product
            {
                Id = 100031,
                Name = "来自萝卜星球",
                Description = "评论签名「来自萝卜星球」",
                Icon = "signature-planet",
                CategoryId = "signature",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Signature,
                BenefitValue = "来自萝卜星球 🥕",
                Price = 100,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 0,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100032,
                Name = "深夜食堂",
                Description = "评论签名「来自深夜食堂」",
                Icon = "signature-midnight",
                CategoryId = "signature",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Signature,
                BenefitValue = "来自深夜食堂 🌙",
                Price = 100,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 1,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100033,
                Name = "代码世界",
                Description = "评论签名「来自代码世界」",
                Icon = "signature-code",
                CategoryId = "signature",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Signature,
                BenefitValue = "来自代码世界 💻",
                Price = 100,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 2,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },

            // ========== 特效类 ==========
            new Product
            {
                Id = 100041,
                Name = "彩虹用户名",
                Description = "用户名显示为彩虹渐变色",
                Icon = "effect-rainbow",
                CategoryId = "effect",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.NameColor,
                BenefitValue = "rainbow",
                Price = 400,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Days,
                DurationDays = 30,
                SortOrder = 0,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100042,
                Name = "金色用户名",
                Description = "用户名显示为尊贵金色",
                Icon = "effect-gold",
                CategoryId = "effect",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.NameColor,
                BenefitValue = "gold",
                Price = 300,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Days,
                DurationDays = 30,
                SortOrder = 1,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100043,
                Name = "爱心点赞特效",
                Description = "点赞时显示爱心飘散动画",
                Icon = "effect-heart-like",
                CategoryId = "effect",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.LikeEffect,
                BenefitValue = "heart-burst",
                Price = 200,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Days,
                DurationDays = 30,
                SortOrder = 2,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },

            // ========== 主题类 ==========
            new Product
            {
                Id = 100051,
                Name = "暗夜主题",
                Description = "深邃的暗夜配色主题",
                Icon = "theme-dark",
                CategoryId = "theme",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Theme,
                BenefitValue = "theme-dark-night",
                Price = 200,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 0,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100052,
                Name = "樱花主题",
                Description = "浪漫的樱花粉配色主题",
                Icon = "theme-sakura",
                CategoryId = "theme",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Theme,
                BenefitValue = "theme-sakura",
                Price = 200,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 1,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },

            // ========== 实用类 ==========
            new Product
            {
                Id = 100061,
                Name = "改名卡",
                Description = "使用后可修改一次用户名",
                Icon = "card-rename",
                CategoryId = "effect",
                ProductType = ProductType.Consumable,
                ConsumableType = ConsumableType.RenameCard,
                Price = 100,
                StockType = StockType.Unlimited,
                SortOrder = 10,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            }
        };

        foreach (var product in products)
        {
            var exists = await db.Queryable<Product>().AnyAsync(p => p.Id == product.Id);
            if (!exists)
            {
                Console.WriteLine($"[Radish.DbMigrate] 创建商品 Id={product.Id}, Name={product.Name}...");
                await db.Insertable(product).ExecuteCommandAsync();
            }
            else
            {
                Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={product.Id} 的商品，跳过创建。");
            }
        }
    }
}
