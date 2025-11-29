using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common;
using Radish.IService;
using Radish.Model;
using Radish.Shared;
using Radish.Model.ViewModels;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>测试事务-订单交易接口控制器</summary>
[ApiController]
[Route("api/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = "RadishAuthPolicy")]
public class TransactionController : ControllerBase
{
    private readonly IBaseService<Role, RoleVo> _roleService;
    private readonly IUserService _userService;
    private readonly IUnitOfWorkManage _unitOfWorkManage;
    private readonly IDepartmentService  _departmentService;

    public TransactionController(IBaseService<Role, RoleVo> roleService, IUserService userService, IUnitOfWorkManage unitOfWorkManage, IDepartmentService departmentService)
    {
        _roleService = roleService;
        _userService = userService;
        _unitOfWorkManage = unitOfWorkManage;
        _departmentService = departmentService;
    }

    [HttpGet]
    public async Task<MessageModel> Get()
    {
        try
        {
            Console.WriteLine($"Begin Transaction");
            // _unitOfWorkManage.BeginTran(); // 事务开始
            using var uow = _unitOfWorkManage.CreateUnitOfWork(); // 新事务写法
            // 先查询一下
            var roles = await _roleService.QueryAsync();
            // 输出当前角色表中的数据条数
            Console.WriteLine($"1 first time : the count of role is :{roles.Count}");
            // 插入一条数据
            Console.WriteLine($"insert a data into the table role now.");
            TimeSpan timeSpan = DateTime.Now.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var insertPassword = await _roleService.AddAsync(new Role()
            {
                Id = timeSpan.TotalSeconds.ObjToLong(),
                IsDeleted = false,
                RoleName = "role name",
            });
            // 再查询一下，插入成功后多了一条数据
            var roles2 = await _roleService.QueryAsync();
            Console.WriteLine($"2 second time : the count of role is :{roles2.Count}");
            // 认为制造一个异常
            int ex = 0;
            Console.WriteLine($"There's an exception!!");
            Console.WriteLine($" ");
            int throwEx = 1 / ex;
            // 事务结束
            uow.Commit(); // 新事务写法会自动回滚，不需要下面的手动回滚
            // _unitOfWorkManage.CommitTran();
        }
        catch (Exception)
        {
            // 抛出异常时，事务回滚
            // _unitOfWorkManage.RollbackTran(); // 新事务写法 uow.Commit(); 会自动回滚，不需要手动回滚
            // 再查询一次
            var roles3 = await _roleService.QueryAsync();
            Console.WriteLine($"3 third time : the count of role is :{roles3.Count}");
        }

        return new MessageModel()
        {
            StatusCode = (int)HttpStatusCodeEnum.Success,
            IsSuccess = true,
            MessageInfo = "Success",
        };
    }

    /// <summary>测试使用同事务</summary>
    /// <remarks>仅为示例，无任何作用</remarks>
    /// <returns></returns>
    [HttpGet]
    public async Task<object> TestTranPropagation()
    {
        return await _userService.TestTranPropagationUser();
    }
}
