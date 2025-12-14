using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.TenantTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.Tenants;
using Radish.Model.ViewModels;
using SqlSugar;

namespace Radish.Api.Controllers;

/// <summary>
/// 多租户- 租户 Id 方案
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = "RadishAuthPolicy")]
public class TenantController : ControllerBase
{
    private readonly IBaseService<BusinessTable, BusinessTableVo> _businessTableService;
    private readonly IBaseService<MultiBusinessTable, MultiBusinessTableVo> _multiBusinessTableService;
    private readonly IBaseService<SubLibBusinessTable, SubLibBusinessTableVo> _subLibBusinessTableService;
    private readonly IBaseService<Tenant, TenantVo> _tenantService;

    /// <summary>
    /// 构造函数，注入依赖
    /// </summary>
    /// <param name="businessTableService"></param>
    /// <param name="multiBusinessTableService"></param>
    /// <param name="subLibBusinessTableService"></param>
    /// <param name="tenantService"></param>
    public TenantController(IBaseService<BusinessTable, BusinessTableVo> businessTableService,
        IBaseService<MultiBusinessTable, MultiBusinessTableVo> multiBusinessTableService,
        IBaseService<SubLibBusinessTable, SubLibBusinessTableVo> subLibBusinessTableService, IBaseService<Tenant, TenantVo> tenantService)
    {
        _businessTableService = businessTableService;
        _multiBusinessTableService = multiBusinessTableService;
        _subLibBusinessTableService = subLibBusinessTableService;
        _tenantService = tenantService;
    }

    /// <summary>
    /// 使用缓存获取全部租户列表
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<MessageModel<object>> GetTenantWithCache()
    {
        var res = await _tenantService.QueryWithCacheAsync();
        return MessageModel<object>.Success("获取成功", res);
    }

    /// <summary>
    /// 测试添加一个租户
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public async Task<MessageModel<object>> AddTest()
    {
        var tenant = new Tenant()
        {
            Id = SnowFlakeSingle.instance.getID(),
            TenantName = "TestName",
            TenantType = TenantTypeEnum.DataBases,
            TenantConfigId = "TestConfig",
            IsEnable = false
        };
        var res = await _tenantService.AddAsync(tenant);
        return MessageModel<object>.Success("添加成功", res);
    }

    /// <summary>
    /// 获取租户下全部业务数据-字段隔离
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<object> BusinessTable()
    {
        return await _businessTableService.QueryAsync();
    }

    /// <summary>
    /// 获取租户下全部业务数据-表隔离
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<object> MultiBusinessTable()
    {
        return await _multiBusinessTableService.QueryAsync();
    }

    /// <summary>
    /// 获取租户下全部业务数据-库隔离
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<object> SubLibBusinessTable()
    {
        return await _subLibBusinessTableService.QueryAsync();
    }
}