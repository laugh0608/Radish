using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>内容治理服务接口</summary>
public interface IContentModerationService : IBaseService<ContentReport, ContentReportVo>
{
    /// <summary>提交举报</summary>
    Task<long> SubmitReportAsync(SubmitContentReportDto dto, long reporterUserId, string reporterUserName, long tenantId);

    /// <summary>分页获取审核队列</summary>
    Task<VoPagedResult<ContentReportQueueItemVo>> GetReportQueueAsync(int? status, int pageIndex, int pageSize);

    /// <summary>审核举报单</summary>
    Task<ContentReportQueueItemVo> ReviewReportAsync(
        ReviewContentReportDto dto,
        long reviewerUserId,
        string reviewerUserName,
        long tenantId);

    /// <summary>手动执行治理动作</summary>
    Task<UserModerationActionVo> ApplyUserActionAsync(
        ApplyUserModerationActionDto dto,
        long operatorUserId,
        string operatorUserName,
        long tenantId);

    /// <summary>获取用户治理状态</summary>
    Task<UserModerationStatusVo> GetUserModerationStatusAsync(long userId);

    /// <summary>获取用户发布权限</summary>
    Task<ContentModerationPermissionVo> GetPublishPermissionAsync(long userId);

    /// <summary>分页获取治理动作记录</summary>
    Task<VoPagedResult<UserModerationActionVo>> GetActionLogsAsync(int pageIndex, int pageSize, long? targetUserId = null);
}
