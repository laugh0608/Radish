using AutoMapper;
using Radish.Common;
using Radish.Common.AttributeTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Service;

/// <summary>用户服务类</summary>
public class UserService : BaseService<User, UserVo>, IUserService
{
    private readonly IUserRepository _userRepository;

    private readonly IDepartmentService _departmentService;

    public UserService(IDepartmentService departmentService, IMapper mapper,
        IBaseRepository<User> baseRepository, IUserRepository userRepository) : base(mapper, baseRepository)
    {
        _departmentService = departmentService;
        _userRepository = userRepository;
    }

    /// <summary>测试获取用户服务示例</summary>
    /// <remarks>返回的是视图 Vo 模型，隔离实际实体类</remarks>
    /// <returns></returns>
    public async Task<List<UserVo>> GetUsersAsync()
    {
        // 将仓储层返回的实体映射为外部可用的 UserVo，供示例接口与测试调用。
        var userList = await _userRepository.GetUsersAsync();
        return userList.Select(u => new UserVo { VoUsName = u.UserName }).ToList();
    }

    /// <summary>测试使用同事务</summary>
    /// <returns></returns>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<bool> TestTranPropagationUser()
    {
        await Console.Out.WriteLineAsync($"db context id : {base.Db.ContextID}");
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
}