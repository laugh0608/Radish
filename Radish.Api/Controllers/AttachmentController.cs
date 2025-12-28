using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>
/// 附件管理控制器
/// </summary>
/// <remarks>
/// 提供文件上传、下载、删除等接口
/// </remarks>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("附件管理")]
public class AttachmentController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;
    private readonly IHttpContextUser _httpContextUser;
    private readonly IUploadRateLimitService _rateLimitService;
    private readonly UploadRateLimitOptions _rateLimitOptions;
    private readonly IFileAccessTokenService _fileAccessTokenService;

    public AttachmentController(
        IAttachmentService attachmentService,
        IHttpContextUser httpContextUser,
        IUploadRateLimitService rateLimitService,
        IOptions<UploadRateLimitOptions> rateLimitOptions,
        IFileAccessTokenService fileAccessTokenService)
    {
        _attachmentService = attachmentService;
        _httpContextUser = httpContextUser;
        _rateLimitService = rateLimitService;
        _rateLimitOptions = rateLimitOptions.Value;
        _fileAccessTokenService = fileAccessTokenService;
    }

    #region Upload

    /// <summary>
    /// 上传图片
    /// </summary>
    /// <param name="file">图片文件</param>
    /// <param name="businessType">业务类型（Post/Comment/Avatar 等）</param>
    /// <param name="generateThumbnail">是否生成缩略图（默认 true）</param>
    /// <param name="generateMultipleSizes">是否生成多尺寸（默认 false）</param>
    /// <param name="addWatermark">是否添加水印（默认 false）</param>
    /// <param name="watermarkText">水印文本（默认 "Radish"）</param>
    /// <param name="removeExif">是否移除 EXIF 信息（默认 true）</param>
    /// <returns>上传结果（包含文件 URL）</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status429TooManyRequests)]
    public async Task<MessageModel> UploadImage(
        [FromForm] IFormFile file,
        [FromForm] string businessType = "General",
        [FromForm] bool generateThumbnail = true,
        [FromForm] bool generateMultipleSizes = false,
        [FromForm] bool addWatermark = false,
        [FromForm] string? watermarkText = null,
        [FromForm] bool removeExif = true)
    {
        if (file == null || file.Length == 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "文件不能为空"
            };
        }

        // 验证是否为图片
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!imageExtensions.Contains(extension))
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "仅支持图片格式（jpg/jpeg/png/gif/bmp/webp/svg）"
            };
        }

        var userId = _httpContextUser.UserId;
        var userName = _httpContextUser.UserName;

        // 限流检查
        if (_rateLimitOptions.Enable)
        {
            var (isAllowed, errorMessage) = await _rateLimitService.CheckUploadAllowedAsync(userId, file.Length);
            if (!isAllowed)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = 429, // Too Many Requests
                    MessageInfo = errorMessage ?? "上传请求过于频繁，请稍后再试"
                };
            }
        }

        // 生成上传 ID
        var uploadId = Guid.NewGuid().ToString();

        try
        {
            // 记录上传开始
            if (_rateLimitOptions.Enable)
            {
                await _rateLimitService.RecordUploadStartAsync(userId, uploadId);
            }

            var options = new FileUploadOptionsDto
            {
                OriginalFileName = file.FileName,
                BusinessType = businessType,
                GenerateThumbnail = generateThumbnail,
                GenerateMultipleSizes = generateMultipleSizes,
                AddWatermark = addWatermark,
                WatermarkText = watermarkText ?? "Radish",
                CalculateHash = true,
                RemoveExif = removeExif
            };

            var attachment = await _attachmentService.UploadFileAsync(file, options, userId, userName);

            if (attachment == null)
            {
                // 记录上传失败
                if (_rateLimitOptions.Enable)
                {
                    await _rateLimitService.RecordUploadFailedAsync(userId, uploadId);
                }

                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                    MessageInfo = "文件上传失败"
                };
            }

            // 记录上传完成
            if (_rateLimitOptions.Enable)
            {
                await _rateLimitService.RecordUploadCompleteAsync(userId, uploadId, file.Length);
            }

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "上传成功",
                ResponseData = attachment
            };
        }
        catch (Exception)
        {
            // 记录上传失败
            if (_rateLimitOptions.Enable)
            {
                await _rateLimitService.RecordUploadFailedAsync(userId, uploadId);
            }
            throw;
        }
    }

    /// <summary>
    /// 上传文档
    /// </summary>
    /// <param name="file">文档文件</param>
    /// <param name="businessType">业务类型（Document 等）</param>
    /// <returns>上传结果（包含文件 URL）</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status429TooManyRequests)]
    public async Task<MessageModel> UploadDocument(
        [FromForm] IFormFile file,
        [FromForm] string businessType = "Document")
    {
        if (file == null || file.Length == 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "文件不能为空"
            };
        }

        // 验证是否为文档
        var documentExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!documentExtensions.Contains(extension))
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "仅支持文档格式（pdf/doc/docx/xls/xlsx/ppt/pptx/txt/md）"
            };
        }

        var userId = _httpContextUser.UserId;
        var userName = _httpContextUser.UserName;

        // 限流检查
        if (_rateLimitOptions.Enable)
        {
            var (isAllowed, errorMessage) = await _rateLimitService.CheckUploadAllowedAsync(userId, file.Length);
            if (!isAllowed)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = 429, // Too Many Requests
                    MessageInfo = errorMessage ?? "上传请求过于频繁，请稍后再试"
                };
            }
        }

        // 生成上传 ID
        var uploadId = Guid.NewGuid().ToString();

        try
        {
            // 记录上传开始
            if (_rateLimitOptions.Enable)
            {
                await _rateLimitService.RecordUploadStartAsync(userId, uploadId);
            }

            var options = new FileUploadOptionsDto
            {
                OriginalFileName = file.FileName,
                BusinessType = businessType,
                GenerateThumbnail = false,
                GenerateMultipleSizes = false,
                AddWatermark = false,
                CalculateHash = true,
                RemoveExif = false
            };

            var attachment = await _attachmentService.UploadFileAsync(file, options, userId, userName);

            if (attachment == null)
            {
                // 记录上传失败
                if (_rateLimitOptions.Enable)
                {
                    await _rateLimitService.RecordUploadFailedAsync(userId, uploadId);
                }

                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                    MessageInfo = "文件上传失败"
                };
            }

            // 记录上传完成
            if (_rateLimitOptions.Enable)
            {
                await _rateLimitService.RecordUploadCompleteAsync(userId, uploadId, file.Length);
            }

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "上传成功",
                ResponseData = attachment
            };
        }
        catch (Exception)
        {
            // 记录上传失败
            if (_rateLimitOptions.Enable)
            {
                await _rateLimitService.RecordUploadFailedAsync(userId, uploadId);
            }
            throw;
        }
    }

    #endregion

    #region Query

    /// <summary>
    /// 根据 ID 获取附件信息
    /// </summary>
    /// <param name="id">附件 ID</param>
    /// <returns>附件信息</returns>
    [HttpGet("{id:long}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> GetById(long id)
    {
        var attachment = await _attachmentService.QueryByIdAsync(id);

        if (attachment == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "附件不存在"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = attachment
        };
    }

    /// <summary>
    /// 根据业务类型和业务 ID 获取附件列表
    /// </summary>
    /// <param name="businessType">业务类型</param>
    /// <param name="businessId">业务 ID</param>
    /// <returns>附件列表</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetByBusiness(string businessType, long businessId)
    {
        var attachments = await _attachmentService.GetByBusinessAsync(businessType, businessId);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = attachments
        };
    }

    /// <summary>
    /// 获取当前用户的上传统计信息
    /// </summary>
    /// <returns>上传统计信息</returns>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetUploadStatistics()
    {
        var userId = _httpContextUser.UserId;
        var statistics = await _rateLimitService.GetUploadStatisticsAsync(userId);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = statistics
        };
    }

    /// <summary>
    /// 分页获取当前用户上传的附件列表
    /// </summary>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量（默认 20）</param>
    /// <returns>分页附件列表</returns>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetMyAttachments(int pageIndex = 1, int pageSize = 20)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var userId = _httpContextUser.UserId;

        var (data, totalCount) = await _attachmentService.QueryPageAsync(
            a => a.UploaderId == userId && !a.IsDeleted,
            pageIndex,
            pageSize,
            a => a.CreateTime,
            SqlSugar.OrderByType.Desc);

        var pageModel = new PageModel<AttachmentVo>
        {
            Page = pageIndex,
            PageSize = pageSize,
            DataCount = totalCount,
            PageCount = (int)Math.Ceiling(totalCount / (double)pageSize),
            Data = data
        };

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = pageModel
        };
    }

    #endregion

    #region Download

    /// <summary>
    /// 下载附件
    /// </summary>
    /// <param name="id">附件 ID</param>
    /// <returns>文件流</returns>
    [HttpGet("{id:long}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(long id)
    {
        var (stream, attachment) = await _attachmentService.GetDownloadStreamAsync(id);

        if (stream == null || attachment == null)
        {
            return NotFound(new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "文件不存在或无权访问"
            });
        }

        return File(stream, attachment.MimeType, attachment.OriginalName);
    }

    #endregion

    #region Token Access

    /// <summary>
    /// 创建文件访问令牌
    /// </summary>
    /// <param name="dto">创建请求</param>
    /// <returns>令牌信息</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> CreateAccessToken([FromBody] CreateFileAccessTokenDto dto)
    {
        try
        {
            var userId = _httpContextUser.UserId;
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var token = await _fileAccessTokenService.CreateTokenAsync(dto, userId, baseUrl);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "创建访问令牌成功",
                ResponseData = token
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = ex.Message
            };
        }
    }

    /// <summary>
    /// 通过令牌下载文件
    /// </summary>
    /// <param name="token">访问令牌</param>
    /// <returns>文件流</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DownloadByToken([FromQuery] string token)
    {
        try
        {
            var userId = _httpContextUser.UserId;
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // 验证令牌
            var attachmentId = await _fileAccessTokenService.ValidateAndUseTokenAsync(token, userId, ipAddress);
            if (!attachmentId.HasValue)
            {
                return StatusCode(403, new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = 403,
                    MessageInfo = "令牌无效、已过期或已达访问次数上限"
                });
            }

            // 下载文件
            var (stream, attachment) = await _attachmentService.GetDownloadStreamAsync(attachmentId.Value);
            if (stream == null || attachment == null)
            {
                return NotFound(new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.NotFound,
                    MessageInfo = "文件不存在"
                });
            }

            return File(stream, attachment.MimeType, attachment.OriginalName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new MessageModel
            {
                IsSuccess = false,
                StatusCode = 500,
                MessageInfo = ex.Message
            });
        }
    }

    /// <summary>
    /// 撤销访问令牌
    /// </summary>
    /// <param name="token">令牌</param>
    /// <returns>操作结果</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> RevokeAccessToken([FromBody] string token)
    {
        try
        {
            var userId = _httpContextUser.UserId;
            await _fileAccessTokenService.RevokeTokenAsync(token, userId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "撤销令牌成功"
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = ex.Message
            };
        }
    }

    /// <summary>
    /// 获取附件的所有访问令牌
    /// </summary>
    /// <param name="attachmentId">附件ID</param>
    /// <returns>令牌列表</returns>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> GetAttachmentTokens([FromQuery] long attachmentId)
    {
        try
        {
            var userId = _httpContextUser.UserId;
            var tokens = await _fileAccessTokenService.GetAttachmentTokensAsync(attachmentId, userId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = tokens
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = ex.Message
            };
        }
    }

    #endregion

    #region Delete

    /// <summary>
    /// 删除附件
    /// </summary>
    /// <param name="id">附件 ID</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{id:long}")]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    public async Task<MessageModel> Delete(long id)
    {
        // 检查附件是否存在
        var attachment = await _attachmentService.QueryByIdAsync(id);
        if (attachment == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "附件不存在"
            };
        }

        // 权限检查：只有上传者或管理员可以删除
        var userId = _httpContextUser.UserId;
        var roles = _httpContextUser.GetClaimValueByType("role");
        var isAdmin = roles.Contains("Admin") || roles.Contains("System");

        if (attachment.UploaderId != userId && !isAdmin)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Forbidden,
                MessageInfo = "无权删除该附件"
            };
        }

        var deleted = await _attachmentService.DeleteFileAsync(id);

        if (!deleted)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = "删除失败"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "删除成功"
        };
    }

    /// <summary>
    /// 批量删除附件
    /// </summary>
    /// <param name="ids">附件 ID 列表</param>
    /// <returns>删除结果</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> DeleteBatch([FromBody] List<long> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "ID 列表不能为空"
            };
        }

        var userId = _httpContextUser.UserId;
        var roles = _httpContextUser.GetClaimValueByType("role");
        var isAdmin = roles.Contains("Admin") || roles.Contains("System");

        // 权限检查：验证每个附件的权限
        foreach (var id in ids)
        {
            var attachment = await _attachmentService.QueryByIdAsync(id);
            if (attachment != null && attachment.UploaderId != userId && !isAdmin)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.Forbidden,
                    MessageInfo = $"无权删除附件 ID: {id}"
                };
            }
        }

        var deletedCount = await _attachmentService.DeleteFilesAsync(ids);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = $"成功删除 {deletedCount}/{ids.Count} 个附件",
            ResponseData = deletedCount
        };
    }

    #endregion

    #region Update

    /// <summary>
    /// 更新附件的业务关联
    /// </summary>
    /// <param name="id">附件 ID</param>
    /// <param name="businessType">业务类型</param>
    /// <param name="businessId">业务 ID</param>
    /// <returns>更新结果</returns>
    [HttpPut("{id:long}")]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> UpdateBusinessAssociation(
        long id,
        [FromQuery] string businessType,
        [FromQuery] long businessId)
    {
        // 检查附件是否存在
        var attachment = await _attachmentService.QueryByIdAsync(id);
        if (attachment == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "附件不存在"
            };
        }

        // 权限检查：只有上传者或管理员可以修改
        var userId = _httpContextUser.UserId;
        var roles = _httpContextUser.GetClaimValueByType("role");
        var isAdmin = roles.Contains("Admin") || roles.Contains("System");

        if (attachment.UploaderId != userId && !isAdmin)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Forbidden,
                MessageInfo = "无权修改该附件"
            };
        }

        var updated = await _attachmentService.UpdateBusinessAssociationAsync(id, businessType, businessId);

        if (!updated)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = "更新失败"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "更新成功"
        };
    }

    #endregion
}
