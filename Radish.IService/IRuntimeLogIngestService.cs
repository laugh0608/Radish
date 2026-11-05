using Radish.Model.DtoModels;

namespace Radish.IService;

public interface IRuntimeLogIngestService
{
    Task<RuntimeLogIngestResult> IngestAsync(Stream body, CancellationToken cancellationToken);
}
