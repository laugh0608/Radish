using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>
/// 文件访问令牌服务接口
/// </summary>
public interface IFileAccessTokenService
{
    /// <summary>
    /// 创建文件访问令牌
    /// </summary>
    /// <param name="dto">创建请求</param>
    /// <param name="userId">创建用户ID</param>
    /// <param name="baseUrl">基础URL（用于生成访问链接）</param>
    /// <returns>访问令牌信息</returns>
    Task<FileAccessTokenVo> CreateTokenAsync(CreateFileAccessTokenDto dto, long userId, string baseUrl);

    /// <summary>
    /// 验证并使用令牌
    /// </summary>
    /// <param name="token">令牌</param>
    /// <param name="userId">访问用户ID（可选）</param>
    /// <param name="ipAddress">访问IP地址</param>
    /// <returns>附件ID，如果验证失败返回null</returns>
    Task<long?> ValidateAndUseTokenAsync(string token, long? userId, string ipAddress);

    /// <summary>
    /// 撤销令牌
    /// </summary>
    /// <param name="token">令牌</param>
    /// <param name="userId">用户ID</param>
    Task RevokeTokenAsync(string token, long userId);

    /// <summary>
    /// 获取令牌信息
    /// </summary>
    /// <param name="token">令牌</param>
    /// <param name="userId">用户ID</param>
    /// <returns>令牌信息</returns>
    Task<FileAccessTokenVo?> GetTokenInfoAsync(string token, long userId);

    /// <summary>
    /// 获取附件的所有有效令牌
    /// </summary>
    /// <param name="attachmentId">附件ID</param>
    /// <param name="userId">用户ID</param>
    /// <returns>令牌列表</returns>
    Task<List<FileAccessTokenVo>> GetAttachmentTokensAsync(long attachmentId, long userId);

    /// <summary>
    /// 清理过期令牌（定时任务调用）
    /// </summary>
    Task CleanupExpiredTokensAsync();
}
