using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model.Tenants;
using Radish.Model.ViewModels;

namespace Radish.Api.Controllers;

/// <summary>
/// 多租户- 租户 Id 方案
/// </summary>
[ApiController]
[Route("api/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = "RadishAuthPolicy")]
public class TenantController : ControllerBase
{
    private readonly IBaseService<BusinessTable, BusinessTableVo> _businessTableService;
    private readonly IBaseService<MultiBusinessTable, MultiBusinessTableVo> _multiBusinessTableService;

    public TenantController(IBaseService<BusinessTable, BusinessTableVo> businessTableService,
        IBaseService<MultiBusinessTable, MultiBusinessTableVo> multiBusinessTableService)
    {
        _businessTableService = businessTableService;
        _multiBusinessTableService = multiBusinessTableService;
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
}