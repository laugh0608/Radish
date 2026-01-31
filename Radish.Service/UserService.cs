using AutoMapper;
using Radish.Common;
using Radish.Common.AttributeTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using SqlSugar;

namespace Radish.Service;

/// <summary>用户服务类</summary>
public class UserService : BaseService<User, UserVo>, IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IBaseRepository<Role> _roleRepository;
    private readonly IBaseRepository<UserRole> _userRoleRepository;
    private readonly IDepartmentService _departmentService;

    public UserService(IDepartmentService departmentService, IMapper mapper,
        IBaseRepository<User> baseRepository, IUserRepository userRepository, IBaseRepository<Role> roleRepository,
        IBaseRepository<UserRole> userRoleRepository) : base(mapper, baseRepository)
    {
        _departmentService = departmentService;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
    }

    /// <summary>
    /// 通过登录用户名和登录密码查询用户的角色名称
    /// </summary>
    /// <param name="loginName">登录用户名</param>
    /// <param name="loginPwd">登陆密码</param>
    /// <returns>string RoleName, 可能为多个</returns>
    public async Task<string> GetUserRoleNameStrAsync(string loginName, string loginPwd)
    {
        string roleName = "";
        var user =
            (await base.QueryAsync(a => a.LoginName == loginName && a.LoginPassword == loginPwd)).FirstOrDefault();
        var roleList = await _roleRepository.QueryAsync(a => a.IsDeleted == false);
        if (user != null)
        {
            var userRoles = await _userRoleRepository.QueryAsync(ur => ur.UserId == user.Uuid);
            if (userRoles.Count > 0)
            {
                var arr = userRoles.Select(ur => ur.RoleId.ObjToString()).ToList();
                var roles = roleList.Where(d => arr.Contains(d.Id.ObjToString()));

                roleName = string.Join(',', roles.Select(r => r.RoleName).ToArray());
            }
        }

        return roleName;
    }
    
    /// <summary>
    /// 获取所有的 角色-API 关系
    /// </summary>
    /// <returns>List RoleModulePermission</returns>
    public async Task<List<RoleModulePermission>> RoleModuleMaps()
    {
        // return await _userRepository.RoleModuleMaps();
        
        return await QueryMuchAsync<RoleModulePermission, ApiModule, Role, RoleModulePermission>(
            (rmp, m, r) => new object[]
            {
                JoinType.Left, rmp.ApiModuleId == m.Id,
                JoinType.Left, rmp.RoleId == r.Id
            },
            (rmp, m, r) => new RoleModulePermission()
            {
                Role = r,
                ApiModule = m,
                IsDeleted = rmp.IsDeleted
            },
            (rmp, m, r) => rmp.IsDeleted == false && m.IsDeleted == false && r.IsDeleted == false
        );
    }

    /// <summary>测试使用同事务</summary>
    /// <remarks>仅为示例，无任何作用</remarks>
    /// <returns></returns>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<bool> TestTranPropagationUser()
    {
        var sysUserInfos = await base.QueryAsync();

        TimeSpan timeSpan = DateTime.Now.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var id = timeSpan.TotalSeconds.ObjToLong();
        var insertSysUserInfo = await base.AddAsync(new User()
        {
            Id = id,
            UserName = $"UserName {id}",
            StatusCode = 0,
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now,
            CriticalModifyTime = DateTime.Now,
            LastErrorTime = DateTime.Now,
            ErrorCount = 0,
            IsEnable = true,
            TenantId = 0,
        });

        await _departmentService.TestTranPropagationDepartment();

        return true;
    }

    /// <summary>
    /// 搜索用户（用于@提及功能）
    /// </summary>
    /// <param name="keyword">搜索关键词（匹配用户名）</param>
    /// <param name="limit">返回结果数量限制（默认10）</param>
    /// <returns>用户提及视图模型列表</returns>
    public async Task<List<UserMentionVo>> SearchUsersForMentionAsync(string keyword, int limit = 10)
    {
        // 参数验证
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return new List<UserMentionVo>();
        }

        // 限制最大查询数量
        if (limit <= 0) limit = 10;
        if (limit > 50) limit = 50;

        // 多查询一些结果用于排序（最多100条）
        var fetchSize = Math.Min(limit * 3, 100);

        // 使用分页查询，取第一页，按用户名排序
        var (data, _) = await base.QueryPageAsync(
            whereExpression: u => u.UserName.Contains(keyword) && u.IsEnable && !u.IsDeleted,
            pageIndex: 1,
            pageSize: fetchSize,
            orderByExpression: u => u.UserName,
            orderByType: OrderByType.Asc
        );

        // 在应用层按匹配度排序：
        // 1. 优先显示以关键词开头的用户（不区分大小写）
        // 2. 然后按字母顺序排序
        // 3. 最后取limit个结果
        var sorted = data
            .OrderBy(u => u.VoUserName.StartsWith(keyword, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(u => u.VoUserName)
            .Take(limit)
            .ToList();

        // 映射到UserMentionVo
        var result = base.Mapper.Map<List<UserMentionVo>>(sorted);
        return result;
    }
}
