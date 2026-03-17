using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>用户浏览记录服务接口</summary>
public interface IUserBrowseHistoryService
{
    /// <summary>记录浏览行为</summary>
    Task RecordAsync(RecordBrowseHistoryDto request);

    /// <summary>获取当前用户浏览记录分页</summary>
    Task<(List<UserBrowseHistoryVo> items, int total)> GetMyPageAsync(long userId, int pageIndex, int pageSize);
}
