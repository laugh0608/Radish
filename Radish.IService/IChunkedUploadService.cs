using Microsoft.AspNetCore.Http;
using Radish.Model.Models;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>
/// 分片上传服务接口
/// </summary>
public interface IChunkedUploadService
{
    /// <summary>
    /// 创建上传会话
    /// </summary>
    /// <param name="dto">创建请求</param>
    /// <param name="userId">用户ID</param>
    /// <param name="userName">用户名</param>
    /// <returns>上传会话信息</returns>
    Task<UploadSessionVo> CreateSessionAsync(CreateUploadSessionDto dto, long userId, string userName);

    /// <summary>
    /// 上传分片
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="chunkIndex">分片索引</param>
    /// <param name="chunkData">分片数据</param>
    /// <param name="userId">用户ID</param>
    /// <returns>上传会话信息</returns>
    Task<UploadSessionVo> UploadChunkAsync(string sessionId, int chunkIndex, IFormFile chunkData, long userId);

    /// <summary>
    /// 合并分片并创建附件
    /// </summary>
    /// <param name="dto">合并请求</param>
    /// <param name="userId">用户ID</param>
    /// <param name="userName">用户名</param>
    /// <returns>附件信息</returns>
    Task<AttachmentVo?> MergeChunksAsync(MergeChunksDto dto, long userId, string userName);

    /// <summary>
    /// 获取上传会话信息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="userId">用户ID</param>
    /// <returns>上传会话信息</returns>
    Task<UploadSessionVo?> GetSessionAsync(string sessionId, long userId);

    /// <summary>
    /// 取消上传会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="userId">用户ID</param>
    Task CancelSessionAsync(string sessionId, long userId);

    /// <summary>
    /// 清理过期会话（定时任务调用）
    /// </summary>
    Task CleanupExpiredSessionsAsync();
}
