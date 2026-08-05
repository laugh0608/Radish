using Radish.Model.ViewModels;

namespace Radish.IService;

public interface IPublicDiscoverService
{
    Task<PublicDiscoverFeedVo> GetFeedAsync(string? cursor, int pageSize);
}
