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
    private readonly IBaseService<BusinessTable,BusinessTableVo> _businessTableService;

    public TenantController(IBaseService<BusinessTable, BusinessTableVo> businessTableService)
    {
        _businessTableService = businessTableService;
    }
    
    /// <summary>
    /// 获取租户下全部业务数据
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<object> GetAll()
    {
        return await _businessTableService.QueryAsync();
    }
}