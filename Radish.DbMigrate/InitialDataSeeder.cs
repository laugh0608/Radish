using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common.HelpTool;
using Radish.Common.TenantTool;
using Radish.IService;
using Radish.Model;
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
        await SeedUserRolesAsync(db);
        await SeedPermissionsAsync(db);
        await SeedForumCategoriesAsync(db);
        await SeedForumTagsAsync(db);
        await SeedLevelConfigsAsync(db, services);

        Console.WriteLine("[Radish.DbMigrate] ✓ Seed 完成（默认角色/租户/部门/用户/用户角色/角色-API 权限/论坛分类/标签/等级配置）。");
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
        // 默认标签 ID
        const long csharpTagId = 90000;
        const long dotnetTagId = 90001;
        const long webTagId = 90002;
        const long databaseTagId = 90003;
        const long generalTagId = 90004;

        // C# 标签
        var csharpExists = await db.Queryable<Tag>().AnyAsync(t => t.Id == csharpTagId);
        if (!csharpExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认标签 Id={csharpTagId}, Name=C#...");

            var csharpTag = new Tag("C#")
            {
                Id = csharpTagId,
                Color = "#68217A",
                IsEnabled = true,
                IsDeleted = false,
            };

            await db.Insertable(csharpTag).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={csharpTagId} 的 C# 标签，跳过创建。");
        }

        // .NET 标签
        var dotnetExists = await db.Queryable<Tag>().AnyAsync(t => t.Id == dotnetTagId);
        if (!dotnetExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认标签 Id={dotnetTagId}, Name=.NET...");

            var dotnetTag = new Tag(".NET")
            {
                Id = dotnetTagId,
                Color = "#512BD4",
                IsEnabled = true,
                IsDeleted = false,
            };

            await db.Insertable(dotnetTag).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={dotnetTagId} 的 .NET 标签，跳过创建。");
        }

        // Web开发 标签
        var webExists = await db.Queryable<Tag>().AnyAsync(t => t.Id == webTagId);
        if (!webExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认标签 Id={webTagId}, Name=Web开发...");

            var webTag = new Tag("Web开发")
            {
                Id = webTagId,
                Color = "#0078D4",
                IsEnabled = true,
                IsDeleted = false,
            };

            await db.Insertable(webTag).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={webTagId} 的 Web开发 标签，跳过创建。");
        }

        // 数据库 标签
        var databaseExists = await db.Queryable<Tag>().AnyAsync(t => t.Id == databaseTagId);
        if (!databaseExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认标签 Id={databaseTagId}, Name=数据库...");

            var databaseTag = new Tag("数据库")
            {
                Id = databaseTagId,
                Color = "#107C10",
                IsEnabled = true,
                IsDeleted = false,
            };

            await db.Insertable(databaseTag).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={databaseTagId} 的数据库标签，跳过创建。");
        }

        // 综合 标签
        var generalExists = await db.Queryable<Tag>().AnyAsync(t => t.Id == generalTagId);
        if (!generalExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认标签 Id={generalTagId}, Name=综合...");

            var generalTag = new Tag("综合")
            {
                Id = generalTagId,
                Color = "#8A8A8A",
                IsEnabled = true,
                IsDeleted = false,
            };

            await db.Insertable(generalTag).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={generalTagId} 的综合标签，跳过创建。");
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
}
