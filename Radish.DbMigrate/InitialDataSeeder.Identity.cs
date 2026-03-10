using Radish.Common;
using Radish.Common.HelpTool;
using Radish.Common.TenantTool;
using Radish.Model;
using Radish.Model.Models;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.DbMigrate;

internal static partial class InitialDataSeeder
{
    /// <summary>初始化用户时区偏好（默认值来自 Time:DefaultTimeZoneId）</summary>
    private static async Task SeedUserTimePreferencesAsync(ISqlSugarClient db)
    {
        // 兼容旧库直接执行 seed：先确保 UserTimePreference 表结构存在
        db.CodeFirst.InitTables<UserTimePreference>();
        Console.WriteLine("[Radish.DbMigrate] 已同步 UserTimePreference 表结构（自动补齐缺失表/列）。");

        const long systemUserId = 20000;
        const long adminUserId = 20001;
        const long testUserId = 20002;
        const long publicTenantId = 0;

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
            var existingPreference = await db.Queryable<UserTimePreference>()
                .FirstAsync(p => p.UserId == item.UserId);

            if (existingPreference != null)
            {
                var updated = await db.Updateable<UserTimePreference>()
                    .SetColumns(p => new UserTimePreference
                    {
                        TenantId = publicTenantId,
                        TimeZoneId = defaultTimeZoneId,
                        ModifyBy = "System",
                        ModifyTime = DateTime.UtcNow
                    })
                    .Where(p => p.UserId == item.UserId)
                    .ExecuteCommandAsync();

                Console.WriteLine(updated > 0
                    ? $"[Radish.DbMigrate] 用户 Id={item.UserId} 的时区偏好已存在，已纠正为 {defaultTimeZoneId}（TenantId={publicTenantId}）。"
                    : $"[Radish.DbMigrate] 用户 Id={item.UserId} 的时区偏好已存在，跳过创建。");
                continue;
            }

            Console.WriteLine($"[Radish.DbMigrate] 创建用户 Id={item.UserId} ({item.UserName}) 的时区偏好：{defaultTimeZoneId}...");

            try
            {
                await db.Insertable(new UserTimePreference
                {
                    Id = item.Id,
                    UserId = item.UserId,
                    TenantId = publicTenantId,
                    TimeZoneId = defaultTimeZoneId,
                    CreateBy = "System",
                    ModifyBy = "System",
                    ModifyTime = DateTime.UtcNow
                }).ExecuteCommandAsync();
            }
            catch (Exception ex) when (IsUniqueConstraintViolation(ex, "UserTimePreference.UserId"))
            {
                var updated = await db.Updateable<UserTimePreference>()
                    .SetColumns(p => new UserTimePreference
                    {
                        TenantId = publicTenantId,
                        TimeZoneId = defaultTimeZoneId,
                        ModifyBy = "System",
                        ModifyTime = DateTime.UtcNow
                    })
                    .Where(p => p.UserId == item.UserId)
                    .ExecuteCommandAsync();

                Console.WriteLine(updated > 0
                    ? $"[Radish.DbMigrate] 检测到用户 Id={item.UserId} 的旧时区偏好记录，已自动纠正为 {defaultTimeZoneId}（TenantId={publicTenantId}）。"
                    : $"[Radish.DbMigrate] 用户 Id={item.UserId} 的时区偏好命中唯一键，但未能完成自动纠正，请检查现有数据。" );
            }
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
        // 为当前用户与角色管理主链路建立 ApiModule 与 RoleModulePermission
        // 便于通过 RadishAuthPolicy 与 Console 权限快照进行验证。

        var apiModules = new[]
        {
            new
            {
                ApiModuleId = 50000L,
                ApiModuleName = "Get current user by HttpContext",
                LinkUrl = "/api/v1/User/GetUserByHttpContext",
                ControllerName = "User",
                ActionName = "GetUserByHttpContext",
                Roles = new[] { 10000L, 10001L, 10002L }
            },
            new
            {
                ApiModuleId = 50010L,
                ApiModuleName = "Get role list",
                LinkUrl = "/api/v1/Role/GetRoleList",
                ControllerName = "Role",
                ActionName = "GetRoleList",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50011L,
                ApiModuleName = "Get role by id",
                LinkUrl = "/api/v1/Role/GetRoleById",
                ControllerName = "Role",
                ActionName = "GetRoleById",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50012L,
                ApiModuleName = "Create role",
                LinkUrl = "/api/v1/Role/CreateRole",
                ControllerName = "Role",
                ActionName = "CreateRole",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50013L,
                ApiModuleName = "Update role",
                LinkUrl = "/api/v1/Role/UpdateRole",
                ControllerName = "Role",
                ActionName = "UpdateRole",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50014L,
                ApiModuleName = "Delete role",
                LinkUrl = "/api/v1/Role/DeleteRole",
                ControllerName = "Role",
                ActionName = "DeleteRole",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50015L,
                ApiModuleName = "Toggle role status",
                LinkUrl = "/api/v1/Role/ToggleRoleStatus",
                ControllerName = "Role",
                ActionName = "ToggleRoleStatus",
                Roles = new[] { 10000L, 10001L }
            }
        };

        foreach (var item in apiModules)
        {
            var apiExists = await db.Queryable<ApiModule>().AnyAsync(m => m.Id == item.ApiModuleId);
            if (!apiExists)
            {
                Console.WriteLine($"[Radish.DbMigrate] 创建 ApiModule Id={item.ApiModuleId}, LinkUrl={item.LinkUrl}...");

                var options = new ApiModuleInitializationOptions(item.ApiModuleName, item.LinkUrl)
                {
                    ControllerName = item.ControllerName,
                    ActionName = item.ActionName,
                    IsEnabled = true,
                    IsDeleted = false,
                    IsMenu = false,
                    OrderSort = 0,
                };

                var module = new ApiModule(options)
                {
                    Id = item.ApiModuleId,
                };

                await db.Insertable(module).ExecuteCommandAsync();
            }
            else
            {
                Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={item.ApiModuleId} 的 ApiModule，跳过创建。");
            }

            foreach (var roleId in item.Roles)
            {
                await EnsureRoleApiPermissionAsync(db, roleId, item.ApiModuleId, roleId switch
                {
                    10000L => "System",
                    10001L => "Admin",
                    10002L => "Test",
                    _ => roleId.ToString()
                });
            }
        }

        const long systemRoleId = 10000;
        const long adminRoleId = 10001;
        const long testRoleId = 10002;

        await EnsureRoleApiPermissionAsync(db, systemRoleId, 50000, "System");
        await EnsureRoleApiPermissionAsync(db, adminRoleId, 50000, "Admin");
        await EnsureRoleApiPermissionAsync(db, testRoleId, 50000, "Test");
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
        var roleOffset = roleId switch
        {
            10000 => 0L,
            10001 => 1L,
            10002 => 2L,
            _ => 9L
        };

        var permId = 60000L + ((apiModuleId - 50000L) * 10L) + roleOffset;

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

        // 当前阶段统一按公共租户运行（TenantId = 0）
        const long publicTenantId = 0;
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
                TenantId = publicTenantId,
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

            try
            {
                await db.Insertable(systemUser).ExecuteCommandAsync();
            }
            catch (Exception ex) when (IsUniqueConstraintViolation(ex, "User.Id"))
            {
                var updated = await db.Updateable<User>()
                    .SetColumns(u => new User
                    {
                        TenantId = publicTenantId,
                        DepartmentId = devDeptId,
                        UpdateTime = DateTime.Now
                    })
                    .Where(u => u.Id == systemUserId)
                    .ExecuteCommandAsync();

                if (updated <= 0)
                {
                    throw;
                }

                Console.WriteLine($"[Radish.DbMigrate] 检测到 system 用户旧记录，已自动纠正租户与部门信息。");
            }
        }
        else
        {
            var updated = await db.Updateable<User>()
                .SetColumns(u => new User
                {
                    TenantId = publicTenantId,
                    UpdateTime = DateTime.Now
                })
                .Where(u => u.Id == systemUserId && u.TenantId != publicTenantId)
                .ExecuteCommandAsync();

            Console.WriteLine(updated > 0
                ? $"[Radish.DbMigrate] 已将 system 用户租户纠正为 {publicTenantId}。"
                : $"[Radish.DbMigrate] 已存在 Id={systemUserId} 的 system 用户，且租户已是 {publicTenantId}，跳过。");
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
                TenantId = publicTenantId,
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

            try
            {
                await db.Insertable(adminUser).ExecuteCommandAsync();
            }
            catch (Exception ex) when (IsUniqueConstraintViolation(ex, "User.Id"))
            {
                var updated = await db.Updateable<User>()
                    .SetColumns(u => new User
                    {
                        TenantId = publicTenantId,
                        DepartmentId = devDeptId,
                        UpdateTime = DateTime.Now
                    })
                    .Where(u => u.Id == adminUserId)
                    .ExecuteCommandAsync();

                if (updated <= 0)
                {
                    throw;
                }

                Console.WriteLine($"[Radish.DbMigrate] 检测到 admin 用户旧记录，已自动纠正租户与部门信息。");
            }
        }
        else
        {
            var updated = await db.Updateable<User>()
                .SetColumns(u => new User
                {
                    TenantId = publicTenantId,
                    UpdateTime = DateTime.Now
                })
                .Where(u => u.Id == adminUserId && u.TenantId != publicTenantId)
                .ExecuteCommandAsync();

            Console.WriteLine(updated > 0
                ? $"[Radish.DbMigrate] 已将 admin 用户租户纠正为 {publicTenantId}。"
                : $"[Radish.DbMigrate] 已存在 Id={adminUserId} 的 admin 用户，且租户已是 {publicTenantId}，跳过。");
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
                TenantId = publicTenantId,
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

            try
            {
                await db.Insertable(testUser).ExecuteCommandAsync();
            }
            catch (Exception ex) when (IsUniqueConstraintViolation(ex, "User.Id"))
            {
                var updated = await db.Updateable<User>()
                    .SetColumns(u => new User
                    {
                        TenantId = publicTenantId,
                        DepartmentId = devDeptId,
                        UpdateTime = DateTime.Now
                    })
                    .Where(u => u.Id == testUserId)
                    .ExecuteCommandAsync();

                if (updated <= 0)
                {
                    throw;
                }

                Console.WriteLine($"[Radish.DbMigrate] 检测到 test 用户旧记录，已自动纠正租户与部门信息。");
            }
        }
        else
        {
            var updated = await db.Updateable<User>()
                .SetColumns(u => new User
                {
                    TenantId = publicTenantId,
                    UpdateTime = DateTime.Now
                })
                .Where(u => u.Id == testUserId && u.TenantId != publicTenantId)
                .ExecuteCommandAsync();

            Console.WriteLine(updated > 0
                ? $"[Radish.DbMigrate] 已将 test 用户租户纠正为 {publicTenantId}。"
                : $"[Radish.DbMigrate] 已存在 Id={testUserId} 的 test 用户，且租户已是 {publicTenantId}，跳过。");
        }
    }
}
