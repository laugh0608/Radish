using Radish.Model.DtoModels;
using Radish.Model;

namespace Radish.IRepository;

public interface IBootstrapRepository
{
    Task<bool> AdministratorExistsAsync();

    Task<BootstrapAdminCreationResult> TryCreateFirstAdministratorAsync(
        string displayName,
        string passwordHash,
        string email,
        PublicIndexReservationPolicy publicIndexReservationPolicy);
}
