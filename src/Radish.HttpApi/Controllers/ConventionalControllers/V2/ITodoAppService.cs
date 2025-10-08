using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Radish.Controllers.ConventionalControllers.V2;

public interface ITodoAppService : IApplicationService
{
    Task<string> GetAsync();
}
