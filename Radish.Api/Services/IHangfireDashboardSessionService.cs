using Radish.Model.ViewModels;

namespace Radish.Api.Services;

/// <summary>将已验证的 Console 身份兑换为仅限任务看板的短期会话。</summary>
public interface IHangfireDashboardSessionService
{
    Task<HangfireSessionVo?> CreateAsync();
}
