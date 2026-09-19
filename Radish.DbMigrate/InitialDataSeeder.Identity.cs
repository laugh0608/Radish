using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Common.HelpTool;
using Radish.Common.TenantTool;
using Radish.Model;
using Radish.Model.Models;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.DbMigrate;

internal static partial class InitialDataSeeder
{
    private const long SystemPublicIndex = 1;
    private const long AdminPublicIndex = 2;
    private const long TestPublicIndex = 3;

    internal sealed record DeveloperDefaultUserSeed(
        long Id,
        string Key,
        string DisplayName,
        string Email,
        string Password,
        long PublicIndex,
        int Age,
        int BirthYearsAgo,
        string Remark);

    internal static IReadOnlyList<DeveloperDefaultUserSeed> DeveloperDefaultUserSeeds { get; } =
    [
        new(20000, "system", "System", "system@radishx.com", "system123456", SystemPublicIndex, 30, 30, "System administrator user"),
        new(20001, "admin", "Admin", "admin@radishx.com", "admin123456", AdminPublicIndex, 25, 25, "Administrator user"),
        new(20002, "test", "TestUser", "test@radishx.com", "test123456", TestPublicIndex, 18, 18, "Test user")
    ];

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
            new { Id = 71000L, UserId = systemUserId, UserName = "System" },
            new { Id = 71001L, UserId = adminUserId, UserName = "Admin" },
            new { Id = 71002L, UserId = testUserId, UserName = "TestUser" }
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

    private static async Task RetireObsoleteContentModerationApiModulesAsync(ISqlSugarClient db)
    {
        long[] obsoleteApiModuleIds = [50110L, 50111L, 50112L, 50113L];
        var retiredAt = DateTime.UtcNow;

        await db.Updateable<ConsoleResourceApiModule>()
            .SetColumns(item => new ConsoleResourceApiModule
            {
                IsDeleted = true,
                DeletedAt = retiredAt,
                DeletedBy = "System",
                ModifyBy = "System",
                ModifyId = 0,
                ModifyTime = retiredAt
            })
            .Where(item => obsoleteApiModuleIds.Contains(item.ApiModuleId) && !item.IsDeleted)
            .ExecuteCommandAsync();

        await db.Updateable<RoleModulePermission>()
            .SetColumns(item => new RoleModulePermission
            {
                IsDeleted = true,
                ModifyBy = "System",
                ModifyId = 0,
                ModifyTime = retiredAt
            })
            .Where(item => obsoleteApiModuleIds.Contains(item.ApiModuleId) && !item.IsDeleted)
            .ExecuteCommandAsync();

        await db.Updateable<ApiModule>()
            .SetColumns(item => new ApiModule
            {
                IsEnabled = false,
                IsDeleted = true,
                ModifyBy = "System",
                ModifyId = 0,
                ModifyTime = retiredAt
            })
            .Where(item => obsoleteApiModuleIds.Contains(item.Id) && !item.IsDeleted)
            .ExecuteCommandAsync();
    }

    private static async Task EnsureRoleApiPermissionAsync(ISqlSugarClient db, long roleId, long apiModuleId,
        string roleName)
    {
        var existing = await db.Queryable<RoleModulePermission>()
            .FirstAsync(p => p.RoleId == roleId && p.ApiModuleId == apiModuleId);
        if (existing != null)
        {
            if (existing.IsDeleted)
            {
                await db.Updateable<RoleModulePermission>()
                    .SetColumns(p => new RoleModulePermission
                    {
                        IsDeleted = false,
                        ModifyBy = "System",
                        ModifyId = 0,
                        ModifyTime = DateTime.UtcNow
                    })
                    .Where(p => p.Id == existing.Id)
                    .ExecuteCommandAsync();

                Console.WriteLine(
                    $"[Radish.DbMigrate] 已恢复角色 Id={roleId} 与 ApiModule Id={apiModuleId} 的权限记录。");
                return;
            }

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

    private static async Task RestrictTestRoleApiPermissionsAsync(ISqlSugarClient db)
    {
        const long testRoleId = 10002L;
        const long allowedApiModuleId = 50000L;

        var redundantPermissions = await db.Queryable<RoleModulePermission>()
            .Where(permission =>
                permission.RoleId == testRoleId &&
                permission.ApiModuleId != allowedApiModuleId &&
                !permission.IsDeleted)
            .ToListAsync();

        if (redundantPermissions.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var permission in redundantPermissions)
        {
            permission.IsDeleted = true;
            permission.ModifyBy = "System";
            permission.ModifyId = 0;
            permission.ModifyTime = now;
        }

        await db.Updateable(redundantPermissions).ExecuteCommandAsync();
        Console.WriteLine($"[Radish.DbMigrate] 已回收 Test 角色的多余 API 权限，共 {redundantPermissions.Count} 条。");
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
        // 当前阶段统一按公共租户运行（TenantId = 0）
        const long publicTenantId = 0;
        const long devDeptId = 40000;

        foreach (var seed in DeveloperDefaultUserSeeds)
        {
            await SeedDefaultUserAsync(db, seed, publicTenantId, devDeptId);
        }

        await SeedDefaultUserAvatarsAsync(db, publicTenantId);
    }

    private static async Task SeedDefaultUserAsync(
        ISqlSugarClient db,
        DeveloperDefaultUserSeed seed,
        long publicTenantId,
        long devDeptId)
    {
        var userExists = await db.Queryable<User>().AnyAsync(u => u.Id == seed.Id);
        if (!userExists)
        {
            Console.WriteLine($"[Radish.DbMigrate] 创建默认用户 Id={seed.Id}, Email={seed.Email}...");

            var userOptions = new UserInitializationOptions(seed.Email, PasswordHasher.HashPassword(seed.Password))
            {
                UserName = seed.DisplayName,
                UserSex = (int)UserSexEnum.Unknown,
                UserAge = seed.Age,
                UserBirth = DateTime.Today.AddYears(-seed.BirthYearsAgo),
                TenantId = publicTenantId,
                DepartmentId = devDeptId,
                IsEnable = true,
                IsDeleted = false,
                StatusCode = (int)UserStatusCodeEnum.Normal,
                Remark = seed.Remark,
            };

            var user = new User(userOptions)
            {
                Id = seed.Id,
                PublicIndex = seed.PublicIndex,
            };

            try
            {
                await db.Insertable(user).ExecuteCommandAsync();
            }
            catch (Exception ex) when (IsUniqueConstraintViolation(ex, "User.Id"))
            {
                var updated = await CorrectDefaultUserIdentityAsync(db, seed, publicTenantId, devDeptId);
                if (updated <= 0)
                {
                    throw;
                }

                Console.WriteLine($"[Radish.DbMigrate] 检测到 {seed.Key} 用户旧记录，已自动纠正开发默认身份信息。");
            }

            return;
        }

        var corrected = await CorrectDefaultUserIdentityAsync(db, seed, publicTenantId, devDeptId);

        Console.WriteLine(corrected > 0
            ? $"[Radish.DbMigrate] 已纠正 {seed.Key} 用户身份信息：Email={seed.Email}, DisplayName={seed.DisplayName}, PublicIndex={seed.PublicIndex}。"
            : $"[Radish.DbMigrate] 已存在 Id={seed.Id} 的 {seed.Key} 用户，且身份信息正确，跳过。");
    }

    private static Task<int> CorrectDefaultUserIdentityAsync(
        ISqlSugarClient db,
        DeveloperDefaultUserSeed seed,
        long publicTenantId,
        long devDeptId)
    {
        return db.Updateable<User>()
            .SetColumns(u => new User
            {
                TenantId = publicTenantId,
                DepartmentId = devDeptId,
                UserName = seed.DisplayName,
                UserEmail = seed.Email,
                PublicIndex = seed.PublicIndex,
                UpdateTime = DateTime.Now
            })
            .Where(u => u.Id == seed.Id &&
                        (u.TenantId != publicTenantId ||
                         u.DepartmentId != devDeptId ||
                         u.UserName != seed.DisplayName ||
                         u.UserEmail != seed.Email ||
                         u.PublicIndex != seed.PublicIndex))
            .ExecuteCommandAsync();
    }

    private static async Task SeedDefaultUserAvatarsAsync(ISqlSugarClient db, long publicTenantId)
    {
        var defaultAvatarsPath = Path.Combine(AppPathTool.GetDataBasesPath(), "Uploads", "DefaultAvatars");
        if (!Directory.Exists(defaultAvatarsPath))
        {
            Console.WriteLine($"[Radish.DbMigrate] 默认头像目录不存在，跳过：{defaultAvatarsPath}");
            return;
        }

        var avatarSeeds = new[]
        {
            new { AttachmentId = 72000L, UserId = 20000L, UserName = "system", FileName = "radish.png" },
            new { AttachmentId = 72001L, UserId = 20001L, UserName = "admin", FileName = "touxiang1.jpg" },
            new { AttachmentId = 72002L, UserId = 20002L, UserName = "test", FileName = "touxiang2.jpg" }
        };

        foreach (var seed in avatarSeeds)
        {
            var existingAvatar = await db.Queryable<Attachment>()
                .FirstAsync(a => !a.IsDeleted &&
                                 a.BusinessType == "Avatar" &&
                                 a.BusinessId == seed.UserId);
            if (existingAvatar != null)
            {
                Console.WriteLine($"[Radish.DbMigrate] 用户 {seed.UserName} 已存在头像，保留现状。");
                continue;
            }

            var filePath = Path.Combine(defaultAvatarsPath, seed.FileName);
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[Radish.DbMigrate] 默认头像文件不存在，跳过用户 {seed.UserName}：{filePath}");
                continue;
            }

            var fileInfo = new FileInfo(filePath);
            var relativePath = Path.Combine("DefaultAvatars", seed.FileName).Replace('\\', '/');
            var existingAttachment = await db.Queryable<Attachment>()
                .FirstAsync(a => a.Id == seed.AttachmentId);

            if (existingAttachment != null)
            {
                Console.WriteLine($"[Radish.DbMigrate] 默认头像附件 Id={seed.AttachmentId} 已存在，补齐用户 {seed.UserName} 的头像关联。");
                await db.Updateable<Attachment>()
                    .SetColumns(a => new Attachment
                    {
                        OriginalName = seed.FileName,
                        StoredName = $"default-avatar-{seed.UserName}",
                        Extension = Path.GetExtension(seed.FileName),
                        FileSize = fileInfo.Length,
                        MimeType = GetImageMimeType(seed.FileName),
                        StorageType = "Local",
                        StoragePath = relativePath,
                        ThumbnailPath = relativePath,
                        UploaderId = seed.UserId,
                        UploaderName = seed.UserName,
                        BusinessType = "Avatar",
                        BusinessId = seed.UserId,
                        IsPublic = true,
                        IsEnabled = true,
                        IsDeleted = false,
                        TenantId = publicTenantId,
                        ModifyBy = "System",
                        ModifyId = 0,
                        ModifyTime = DateTime.Now
                    })
                    .Where(a => a.Id == seed.AttachmentId)
                    .ExecuteCommandAsync();

                continue;
            }

            Console.WriteLine($"[Radish.DbMigrate] 为用户 {seed.UserName} 补充默认头像：{seed.FileName}");

            await db.Insertable(new Attachment
            {
                Id = seed.AttachmentId,
                OriginalName = seed.FileName,
                StoredName = $"default-avatar-{seed.UserName}",
                Extension = Path.GetExtension(seed.FileName),
                FileSize = fileInfo.Length,
                MimeType = GetImageMimeType(seed.FileName),
                StorageType = "Local",
                StoragePath = relativePath,
                ThumbnailPath = relativePath,
                UploaderId = seed.UserId,
                UploaderName = seed.UserName,
                BusinessType = "Avatar",
                BusinessId = seed.UserId,
                IsPublic = true,
                IsEnabled = true,
                IsDeleted = false,
                TenantId = publicTenantId,
                CreateBy = "System",
                CreateId = 0
            }).ExecuteCommandAsync();
        }
    }

    private static string GetImageMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
