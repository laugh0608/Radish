using Radish.Common;
using Radish.Common.HttpContextTool;
using Radish.IRepository;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.Models;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Repository;

public class BootstrapRepository : IBootstrapRepository
{
    private const long AdminRoleId = 10001;
    private readonly SqlSugarScope _dbScope;

    public BootstrapRepository(IUnitOfWorkManage unitOfWorkManage)
    {
        _dbScope = unitOfWorkManage.GetDbClient();
    }

    public async Task<bool> AdministratorExistsAsync()
    {
        var db = GetMainDb();
        return await AdministratorExistsAsync(db);
    }

    public async Task<BootstrapAdminCreationResult> TryCreateFirstAdministratorAsync(
        string displayName,
        string passwordHash,
        string email,
        PublicIndexReservationPolicy publicIndexReservationPolicy)
    {
        var db = GetMainDb();
        publicIndexReservationPolicy ??= PublicIndexReservationPolicy.Empty;

        try
        {
            db.Ado.BeginTran();

            if (await AdministratorExistsAsync(db))
            {
                db.Ado.CommitTran();
                return BootstrapAdminCreationResult.Failed(
                    BootstrapAdminCreationStatus.AlreadyInitialized,
                    "系统已存在管理员，初始化入口已关闭");
            }

            var emailExists = await db.Queryable<User>()
                .AnyAsync(u => !u.IsDeleted && u.UserEmail == email);
            if (emailExists)
            {
                db.Ado.CommitTran();
                return BootstrapAdminCreationResult.Failed(
                    BootstrapAdminCreationStatus.EmailTaken,
                    "邮箱已被注册");
            }

            await db.Insertable(new SystemBootstrapState
            {
                Id = SystemBootstrapState.FirstAdminBootstrapId,
                BootstrapKey = "FirstAdministrator",
                IsCompleted = false,
                CompletedUserId = 0,
                CompletedTime = DateTime.UtcNow,
                CreateTime = DateTime.UtcNow
            }).ExecuteCommandAsync();

            var adminRoleId = await EnsureAdminRoleAsync(db);
            var initializedAt = DateTime.UtcNow;
            var administrator = new User(new UserInitializationOptions(email, passwordHash)
            {
                UserName = displayName,
                UserEmail = email,
                UserRealName = string.Empty,
                UserSex = (int)UserSexEnum.Unknown,
                TenantId = 0,
                DepartmentId = 0,
                IsEnable = true,
                IsDeleted = false,
                StatusCode = (int)UserStatusCodeEnum.Normal,
                Remark = "First administrator initialized by bootstrap"
            })
            {
                LoginName = string.Empty,
                CreateTime = initializedAt,
                UpdateTime = initializedAt,
                CriticalModifyTime = initializedAt,
                LastErrorTime = initializedAt,
                PublicIndex = await AllocateNextPublicIndexAsync(db, publicIndexReservationPolicy)
            };

            var userId = await db.Insertable(administrator).ExecuteReturnSnowflakeIdAsync();

            await db.Insertable(new UserRole
            {
                UserId = userId,
                RoleId = adminRoleId,
                IsDeleted = false,
                CreateBy = "Bootstrap",
                CreateTime = DateTime.UtcNow
            }).ExecuteReturnSnowflakeIdAsync();

            await db.Updateable<SystemBootstrapState>()
                .SetColumns(state => new SystemBootstrapState
                {
                    IsCompleted = true,
                    CompletedUserId = userId,
                    CompletedLoginName = email,
                    CompletedTime = DateTime.UtcNow
                })
                .Where(state => state.Id == SystemBootstrapState.FirstAdminBootstrapId)
                .ExecuteCommandAsync();

            db.Ado.CommitTran();
            return BootstrapAdminCreationResult.Created(userId, displayName, email);
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            db.Ado.RollbackTran();

            if (await AdministratorExistsAsync(db))
            {
                return BootstrapAdminCreationResult.Failed(
                    BootstrapAdminCreationStatus.AlreadyInitialized,
                    "系统已存在管理员，初始化入口已关闭");
            }

            return BootstrapAdminCreationResult.Failed(
                BootstrapAdminCreationStatus.ConcurrentInitialization,
                "管理员初始化正在进行，请刷新后重试");
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }

    private ISqlSugarClient GetMainDb()
    {
        var mainDbConnId = AppSettingsTool.RadishApp("MainDb");
        var normalizedMainDbConnId = string.IsNullOrWhiteSpace(mainDbConnId)
            ? "Main"
            : mainDbConnId.Trim();

        return _dbScope.GetConnectionScope(normalizedMainDbConnId.ToLowerInvariant());
    }

    private static async Task<bool> AdministratorExistsAsync(ISqlSugarClient db)
    {
        return await db.Queryable<UserRole, Role, User>((userRole, role, user) => new object[]
            {
                JoinType.Inner, userRole.RoleId == role.Id,
                JoinType.Inner, userRole.UserId == user.Id
            })
            .Where((userRole, role, user) =>
                !userRole.IsDeleted &&
                !role.IsDeleted &&
                role.IsEnabled &&
                !user.IsDeleted &&
                user.IsEnable &&
                (role.RoleName == UserRoles.System || role.RoleName == UserRoles.Admin))
            .AnyAsync();
    }

    private static async Task<long> EnsureAdminRoleAsync(ISqlSugarClient db)
    {
        var role = await db.Queryable<Role>()
            .FirstAsync(r => !r.IsDeleted && r.RoleName == UserRoles.Admin);
        if (role != null)
        {
            return role.Id;
        }

        await db.Insertable(new Role(UserRoles.Admin)
        {
            Id = AdminRoleId,
            RoleDescription = "Admin built-in role (管理员，拥有常规管理权限)",
            IsDeleted = false,
            IsEnabled = true,
            OrderSort = 1,
            DepartmentIds = string.Empty,
            CreateBy = "Bootstrap",
            CreateTime = DateTime.UtcNow
        }).ExecuteCommandAsync();

        return AdminRoleId;
    }

    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            var message = current.Message;
            if (!string.IsNullOrWhiteSpace(message) &&
                (message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("duplicate key value", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("23505", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            current = current.InnerException!;
        }

        return false;
    }

    private static async Task<long> AllocateNextPublicIndexAsync(
        ISqlSugarClient db,
        PublicIndexReservationPolicy publicIndexReservationPolicy)
    {
        var maxPublicIndex = await db.Queryable<User>()
            .Where(user => user.PublicIndex >= User.PublicIndexStart)
            .MaxAsync(user => user.PublicIndex);

        return publicIndexReservationPolicy.FindNextAvailableAfter(
            maxPublicIndex.GetValueOrDefault(User.PublicIndexStart - 1));
    }
}
