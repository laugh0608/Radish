using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Radish.Controllers.ConventionalControllers.V2;

/// <summary>
/// 示例 Todo 应用服务（V2）
/// </summary>
public interface ITodoAppService : IApplicationService
{
    /// <summary>
    /// 获取示例字符串（返回当前请求的 API 版本）。
    /// </summary>
    Task<string> GetAsync();
}
