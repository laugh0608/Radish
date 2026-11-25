using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

public interface IRuntimeLogConsoleService
{
    bool Enabled { get; }
    Task<RuntimeLogPageVo> QueryAsync(RuntimeLogQueryDto query, CancellationToken token);
    Task<RuntimeLogDetailVo?> GetAsync(string eventId, CancellationToken token);
    Task<RuntimeLogAlertsVo> GetAlertsAsync(CancellationToken token);
    Task<bool> TransitionAsync(string id, RuntimeLogAlertActionDto action, CancellationToken token);
    Task<RuntimeLogPipelineVo> GetPipelineAsync(CancellationToken token);
    Task<IReadOnlyList<RuntimeLogHistoryVo>> GetHistoryAsync(DateTimeOffset? from, DateTimeOffset? to, bool audit, CancellationToken token);
    Task<RuntimeLogCsvVo> ExportAsync(RuntimeLogQueryDto query, CancellationToken token);
}
