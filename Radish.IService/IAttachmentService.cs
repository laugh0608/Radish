using Microsoft.AspNetCore.Http;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>附件服务接口</summary>
public interface IAttachmentService : IBaseService<Attachment, AttachmentVo>
{
    /// <summary>
    /// 上传文件
    /// </summary>
    /// <param name="file">上传的文件</param>
    /// <param name="optionsDto">上传选项</param>
    /// <param name="uploaderId">上传者 ID</param>
    /// <param name="uploaderName">上传者名称</param>
    /// <returns>附件信息</returns>
    Task<AttachmentVo?> UploadFileAsync(
        IFormFile file,
        FileUploadOptionsDto optionsDto,
        long uploaderId,
        string uploaderName);

    /// <summary>
    /// 删除文件（同时删除物理文件和数据库记录）
    /// </summary>
    /// <param name="attachmentId">附件 ID</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteFileAsync(long attachmentId);

    /// <summary>
    /// 批量删除文件
    /// </summary>
    /// <param name="attachmentIds">附件 ID 列表</param>
    /// <returns>成功删除的数量</returns>
    Task<int> DeleteFilesAsync(List<long> attachmentIds);

    /// <summary>
    /// 根据业务类型和业务 ID 获取附件列表
    /// </summary>
    /// <param name="businessType">业务类型</param>
    /// <param name="businessId">业务 ID</param>
    /// <returns>附件列表</returns>
    Task<List<AttachmentVo>> GetByBusinessAsync(string businessType, long businessId);

    /// <summary>
    /// 根据文件哈希查找已存在的附件（去重）
    /// </summary>
    /// <param name="fileHash">文件哈希值</param>
    /// <returns>已存在的附件信息</returns>
    Task<AttachmentVo?> FindByHashAsync(string fileHash);

    /// <summary>
    /// 更新附件的业务关联
    /// </summary>
    /// <param name="attachmentId">附件 ID</param>
    /// <param name="businessType">业务类型</param>
    /// <param name="businessId">业务 ID</param>
    /// <returns>是否更新成功</returns>
    Task<bool> UpdateBusinessAssociationAsync(long attachmentId, string businessType, long businessId);

    /// <summary>
    /// 增加下载次数
    /// </summary>
    /// <param name="attachmentId">附件 ID</param>
    Task IncrementDownloadCountAsync(long attachmentId);

    /// <summary>
    /// 获取附件下载流
    /// </summary>
    /// <param name="attachmentId">附件 ID</param>
    /// <param name="requestUserId">请求用户 ID（可选，用于权限检查）</param>
    /// <param name="requestUserRoles">请求用户角色（可选，用于权限检查）</param>
    /// <returns>文件流和文件信息</returns>
    Task<(Stream? stream, AttachmentVo? attachment)> GetDownloadStreamAsync(long attachmentId, long? requestUserId = null, List<string>? requestUserRoles = null);
}
