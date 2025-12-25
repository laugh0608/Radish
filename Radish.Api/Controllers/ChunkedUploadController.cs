using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>
/// 分片上传控制器
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("分片上传")]
public class ChunkedUploadController : ControllerBase
{
    private readonly IChunkedUploadService _chunkedUploadService;
    private readonly IHttpContextUser _httpContextUser;

    public ChunkedUploadController(
        IChunkedUploadService chunkedUploadService,
        IHttpContextUser httpContextUser)
    {
        _chunkedUploadService = chunkedUploadService;
        _httpContextUser = httpContextUser;
    }

    /// <summary>
    /// 创建上传会话
    /// </summary>
    /// <param name="dto">创建请求</param>
    /// <returns>上传会话信息</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> CreateSession([FromBody] CreateUploadSessionDto dto)
    {
        try
        {
            var userId = _httpContextUser.UserId;
            var userName = _httpContextUser.UserName;

            var session = await _chunkedUploadService.CreateSessionAsync(dto, userId, userName);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "创建上传会话成功",
                ResponseData = session
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
    [RequestSizeLimit(10_485_760)] // 10MB 单个分片限制
    public async Task<MessageModel> UploadChunk(
        [FromForm] string sessionId,
        [FromForm] int chunkIndex,
        [FromForm] IFormFile chunkData)
    {
        try
        {
            if (chunkData == null || chunkData.Length == 0)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                    MessageInfo = "分片数据不能为空"
                };
            }

            var userId = _httpContextUser.UserId;
            var session = await _chunkedUploadService.UploadChunkAsync(sessionId, chunkIndex, chunkData, userId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "上传分片成功",
                ResponseData = session
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
    /// 合并分片
    /// </summary>
    /// <param name="dto">合并请求</param>
    /// <returns>附件信息</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> MergeChunks([FromBody] MergeChunksDto dto)
    {
        try
        {
            var userId = _httpContextUser.UserId;
            var userName = _httpContextUser.UserName;

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
    /// 获取上传会话信息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>上传会话信息</returns>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> GetSession([FromQuery] string sessionId)
    {
        try
        {
            var userId = _httpContextUser.UserId;
            var session = await _chunkedUploadService.GetSessionAsync(sessionId, userId);

            if (session == null)
            {
                return new MessageModel
                {
                    IsSuccess = false,
                    StatusCode = (int)HttpStatusCodeEnum.NotFound,
                    MessageInfo = "上传会话不存在"
                };
            }

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = session
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
    /// 取消上传会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>操作结果</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> CancelSession([FromBody] string sessionId)
    {
        try
        {
            var userId = _httpContextUser.UserId;
            await _chunkedUploadService.CancelSessionAsync(sessionId, userId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "取消上传成功"
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
}
