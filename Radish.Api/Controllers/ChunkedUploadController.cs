using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Authorization;
using Radish.Api.Filters;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Radish.Shared.Constants;

namespace Radish.Api.Controllers;

/// <summary>
/// 分片上传控制器
/// </summary>
[ApiController]
[ApiErrorContract]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("分片上传")]
public class ChunkedUploadController : ControllerBase
{
    private readonly IChunkedUploadService _chunkedUploadService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUserService _userService;

    public ChunkedUploadController(
        IChunkedUploadService chunkedUploadService,
        ICurrentUserAccessor currentUserAccessor,
        IUserService userService)
    {
        _chunkedUploadService = chunkedUploadService;
        _currentUserAccessor = currentUserAccessor;
        _userService = userService;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>
    /// 创建上传会话
    /// </summary>
    /// <param name="dto">创建请求</param>
    /// <returns>上传会话信息</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status503ServiceUnavailable)]
    public async Task<MessageModel> CreateSession([FromBody] CreateUploadSessionDto dto)
    {
        try
        {
            var userId = Current.UserId;
            var userName = Current.UserName;
            var accessResult = await AttachmentUploadAuthorization.EvaluateAsync(
                Current,
                _userService,
                dto.BusinessType);
            if (!accessResult.IsSupported)
            {
                throw UploadBusinessError(
                    "不支持该附件业务类型",
                    StatusCodes.Status400BadRequest,
                    AttachmentErrorCodes.BusinessTypeUnsupported);
            }

            if (!accessResult.IsAuthorized)
            {
                throw UploadBusinessError(
                    "当前账号暂无该附件上传权限",
                    StatusCodes.Status403Forbidden,
                    AttachmentErrorCodes.UploadForbidden);
            }

            dto.BusinessType = accessResult.NormalizedBusinessType!;

            var session = await _chunkedUploadService.CreateSessionAsync(dto, userId, userName);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "创建上传会话成功",
                ResponseData = session
            };
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw BuildUnexpectedError("创建上传会话失败，请稍后重试", ex);
        }
    }

    /// <summary>
    /// 上传分片
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="chunkIndex">分片索引</param>
    /// <param name="chunkData">分片数据</param>
    /// <returns>上传会话信息</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status503ServiceUnavailable)]
    [RequestSizeLimit(ChunkedUploadOptions.MultipartRequestSizeLimit)]
    public async Task<MessageModel> UploadChunk(
        [FromForm] string sessionId,
        [FromForm] int chunkIndex,
        [FromForm] IFormFile chunkData)
    {
        try
        {
            if (chunkData == null || chunkData.Length == 0)
            {
                throw UploadBusinessError(
                    "分片数据不能为空",
                    StatusCodes.Status400BadRequest,
                    AttachmentErrorCodes.UploadChunkInvalid);
            }

            var userId = Current.UserId;
            var session = await _chunkedUploadService.UploadChunkAsync(sessionId, chunkIndex, chunkData, userId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "上传分片成功",
                ResponseData = session
            };
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw BuildUnexpectedError("上传分片失败，请稍后重试", ex);
        }
    }

    /// <summary>
    /// 合并分片
    /// </summary>
    /// <param name="dto">合并请求</param>
    /// <returns>附件信息</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status503ServiceUnavailable)]
    public async Task<MessageModel> MergeChunks([FromBody] MergeChunksDto dto)
    {
        try
        {
            var userId = Current.UserId;
            var userName = Current.UserName;

            var attachment = await _chunkedUploadService.MergeChunksAsync(dto, userId, userName);

            if (attachment == null)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                    MessageInfo = "合并分片失败"
                };
            }

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "合并分片成功",
                ResponseData = attachment
            };
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw BuildUnexpectedError("合并上传分片失败，请稍后重试", ex);
        }
    }

    /// <summary>
    /// 获取上传会话信息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>上传会话信息</returns>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status503ServiceUnavailable)]
    public async Task<MessageModel> GetSession([FromQuery] string sessionId)
    {
        try
        {
            var userId = Current.UserId;
            var session = await _chunkedUploadService.GetSessionAsync(sessionId, userId);

            if (session == null)
            {
                throw UploadBusinessError(
                    "上传会话不存在或无权访问",
                    StatusCodes.Status404NotFound,
                    AttachmentErrorCodes.UploadSessionNotFound);
            }

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = session
            };
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw BuildUnexpectedError("获取上传会话失败，请稍后重试", ex);
        }
    }

    /// <summary>
    /// 取消上传会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>操作结果</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status503ServiceUnavailable)]
    public async Task<MessageModel> CancelSession([FromBody] string sessionId)
    {
        try
        {
            var userId = Current.UserId;
            await _chunkedUploadService.CancelSessionAsync(sessionId, userId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "取消上传成功"
            };
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw BuildUnexpectedError("取消上传失败，请稍后重试", ex);
        }
    }

    private static BusinessException BuildUnexpectedError(string message, Exception exception)
    {
        return new BusinessException(
            message,
            exception,
            StatusCodes.Status500InternalServerError,
            "System.UnexpectedError",
            "error.system.unexpected_error");
    }

    private static BusinessException UploadBusinessError(string message, int statusCode, string errorCode)
    {
        return new BusinessException(
            message,
            statusCode,
            errorCode,
            AttachmentErrorCodes.ResolveMessageKey(errorCode));
    }
}
