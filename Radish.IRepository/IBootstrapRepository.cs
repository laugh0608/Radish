using Radish.Model.DtoModels;

namespace Radish.IRepository;

public interface IBootstrapRepository
{
    Task<bool> AdministratorExistsAsync();

    Task<BootstrapAdminCreationResult> TryCreateFirstAdministratorAsync(
        string displayName,
        string passwordHash,
        string email);
}
