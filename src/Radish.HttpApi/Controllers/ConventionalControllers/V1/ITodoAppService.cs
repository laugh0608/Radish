using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Radish.Controllers.ConventionalControllers.V1;

public interface ITodoAppService : IApplicationService
{
    Task<string> GetAsync();
}
