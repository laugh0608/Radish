using AutoMapper;
using Radish.Common;
using Radish.Common.AttributeTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Service;

public class DepartmentService : BaseService<Department, UserVo>, IDepartmentService
{
    private readonly IBaseRepository<Department> _dal;

    public DepartmentService(IMapper mapper, IBaseRepository<Department> baseRepository) : base(mapper, baseRepository)
    {
        _dal = baseRepository;
    }


    /// <summary>测试使用同事务</summary>
    /// <returns></returns>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<bool> TestTranPropagationDepartment()
    {
        TimeSpan timeSpan = DateTime.Now.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var id = timeSpan.TotalSeconds.ObjToLong();
        var insertDepartment = await _dal.AddAsync(new Department()
        {
            Id = id,
            DepartmentName = $"department name {id}",
            CodeRelationship = "",
            OrderSort = 0,
            StatusCode = 0,
            IsDeleted = false,
            Pid = 0
        });

        // 仅测试使用，在 Service 层直接读取 Db 实例，正常业务工况请使用 BaseRepository 或业务自己的特殊仓储中的 CURD 方法
        await Console.Out.WriteLineAsync($"db context id : {base.Db.ContextID}");

        return true;
    }
}