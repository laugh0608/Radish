using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

public interface IBootstrapService
{
    Task<BootstrapStatusVo> GetStatusAsync();

    Task<BootstrapAdminCreationResult> CreateFirstAdministratorAsync(BootstrapCreateAdminDto dto);
}
