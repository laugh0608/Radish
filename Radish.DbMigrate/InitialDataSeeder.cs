using System;
using System.Threading.Tasks;
using Radish.Common.TenantTool;
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
    public static async Task SeedAsync(ISqlSugarClient db)
    {
        Console.WriteLine("[Radish.DbMigrate] 开始执行初始数据 Seed...");

        await SeedRolesAsync(db);
        await SeedTenantsAsync(db);
        await SeedDepartmentsAsync(db);
        await SeedUsersAsync(db);
        await SeedUserRolesAsync(db);
        await SeedPermissionsAsync(db);

        Console.WriteLine("[Radish.DbMigrate] Seed 完成（默认角色/租户/部门/用户/用户角色/角色-API 权限）。");
    }

    /// <summary>初始化用户-角色关系</summary>
    private static async Task SeedUserRolesAsync(ISqlSugarClient db)
    {
        // 与用户/角色种子中的固定 Id 对齐
        const long testUserId = 20002;
        const long systemRoleId = 10000;
        const long adminRoleId = 10001;

        // test 用户作为 System + Admin 角色
        var exists = await db.Queryable<UserRole>().AnyAsync(ur => ur.UserId == testUserId && ur.RoleId == systemRoleId);
        if (!exists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 绑定用户 Id={testUserId} 到角色 Id={systemRoleId} (System)...");
            await db.Insertable(new UserRole
            {
                UserId = testUserId,
                RoleId = systemRoleId,
                IsDeleted = false,
                CreateBy = "System",
            }).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在用户 Id={testUserId} 与角色 Id={systemRoleId} 的绑定，跳过创建。");
        }

        exists = await db.Queryable<UserRole>().AnyAsync(ur => ur.UserId == testUserId && ur.RoleId == adminRoleId);
        if (!exists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 绑定用户 Id={testUserId} 到角色 Id={adminRoleId} (Admin)...");
            await db.Insertable(new UserRole
            {
                UserId = testUserId,
                RoleId = adminRoleId,
                IsDeleted = false,
                CreateBy = "System",
            }).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在用户 Id={testUserId} 与角色 Id={adminRoleId} 的绑定，跳过创建。");
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

        // System + Admin 默认都可以访问该接口
        const long systemRoleId = 10000;
        const long adminRoleId = 10001;

        await EnsureRoleApiPermissionAsync(db, systemRoleId, userByHttpContextApiId, "System");
        await EnsureRoleApiPermissionAsync(db, adminRoleId, userByHttpContextApiId, "Admin");
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

        // 额外内置角色（20000,20001）
        const long systemRoleId2 = 20000;
        const long adminRoleId2 = 20001;

        var system2Exists = await db.Queryable<Role>().AnyAsync(r => r.Id == systemRoleId2);
        if (!system2Exists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认角色 Id={systemRoleId2}, RoleName=system...");

            var systemRole2 = new Role("system")
            {
                Id = systemRoleId2,
                RoleDescription = "System role for default tenant",
                IsDeleted = false,
                IsEnabled = true,
                OrderSort = 10,
                DepartmentIds = string.Empty,
            };

            await db.Insertable(systemRole2).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={systemRoleId2} 的 system 角色，跳过创建。");
        }

        var admin2Exists = await db.Queryable<Role>().AnyAsync(r => r.Id == adminRoleId2);
        if (!admin2Exists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认角色 Id={adminRoleId2}, RoleName=admin...");

            var adminRole2 = new Role("admin")
            {
                Id = adminRoleId2,
                RoleDescription = "Admin role for default tenant",
                IsDeleted = false,
                IsEnabled = true,
                OrderSort = 11,
                DepartmentIds = string.Empty,
            };

            await db.Insertable(adminRole2).ExecuteCommandAsync();
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={adminRoleId2} 的 admin 角色，跳过创建。");
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
        // 默认测试用户（20002 test）
        const long testUserId = 20002;

        // 与租户、部门保持固定 Id 对齐
        const long radishTenantId = 30000;
        const long devDeptId = 40000;

        var testUserExists = await db.Queryable<User>().AnyAsync(u => u.Id == testUserId);
        if (!testUserExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认用户 Id={testUserId}, LoginName=test...");

            var testUserOptions = new UserInitializationOptions("test", "test123456")
            {
                UserName = "test",
                UserRealName = "Test User",
                UserSex = (int)UserSexEnum.Unknown,
                UserAge = 18,
                UserBirth = DateTime.Today,
                TenantId = radishTenantId,
                DepartmentId = devDeptId,
                IsEnable = true,
                IsDeleted = false,
                StatusCode = (int)UserStatusCodeEnum.Normal,
                Remark = "Default test user",
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
}
