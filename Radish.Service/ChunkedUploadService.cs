using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Radish.IRepository;
using Radish.IService;
using Radish.Model.DTOs;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Serilog;

namespace Radish.Service;

/// <summary>
/// 分片上传服务实现
/// </summary>
public class ChunkedUploadService : IChunkedUploadService
{
    private readonly IBaseRepository<UploadSession> _sessionRepository;
    private readonly IAttachmentService _attachmentService;
    private readonly string _tempChunkPath;

    public ChunkedUploadService(
        IBaseRepository<UploadSession> sessionRepository,
        IAttachmentService attachmentService)
    {
        _sessionRepository = sessionRepository;
        _attachmentService = attachmentService;
        _tempChunkPath = Path.Combine("DataBases", "Temp", "Chunks");

        // 确保临时目录存在
        if (!Directory.Exists(_tempChunkPath))
        {
            Directory.CreateDirectory(_tempChunkPath);
        }
    }

    /// <summary>
    /// 创建上传会话
    /// </summary>
    public async Task<UploadSessionVo> CreateSessionAsync(CreateUploadSessionDto dto, long userId, string userName)
    {
        // 计算总分片数
        var totalChunks = (int)Math.Ceiling((double)dto.TotalSize / dto.ChunkSize);

        // 生成会话ID
        var sessionId = Guid.NewGuid().ToString("N");

        // 创建会话记录
        var session = new UploadSession
        {
            SessionId = sessionId,
            FileName = dto.FileName,
            TotalSize = dto.TotalSize,
            MimeType = dto.MimeType,
            ChunkSize = dto.ChunkSize,
            TotalChunks = totalChunks,
            UploadedChunks = 0,
            UploadedChunkIndexes = "[]",
            BusinessType = dto.BusinessType,
            BusinessId = dto.BusinessId,
            UserId = userId,
            UserName = userName,
            Status = "Uploading",
            ExpiresAt = DateTime.Now.AddHours(24), // 24小时过期
            CreateTime = DateTime.Now,
            ModifyTime = DateTime.Now
        };

        await _sessionRepository.AddAsync(session);

        // 创建会话专用目录
        var sessionDir = GetSessionDirectory(sessionId);
        if (!Directory.Exists(sessionDir))
        {
            Directory.CreateDirectory(sessionDir);
        }

        Log.Information("[ChunkedUpload] 创建上传会话: {SessionId}, 文件: {FileName}, 大小: {Size}, 分片数: {Chunks}",
            sessionId, dto.FileName, dto.TotalSize, totalChunks);

        return MapToVo(session);
    }

    /// <summary>
    /// 上传分片
    /// </summary>
    public async Task<UploadSessionVo> UploadChunkAsync(string sessionId, int chunkIndex, IFormFile chunkData, long userId)
    {
        // 获取会话
        var session = await _sessionRepository.QueryFirstAsync(s => s.SessionId == sessionId && s.UserId == userId);
        if (session == null)
        {
            throw new Exception($"上传会话不存在或无权访问: {sessionId}");
        }

        // 检查会话状态
        if (session.Status != "Uploading")
        {
            throw new Exception($"会话状态不允许上传: {session.Status}");
        }

        // 检查是否过期
        if (DateTime.Now > session.ExpiresAt)
        {
            session.Status = "Failed";
            session.ErrorMessage = "会话已过期";
            await _sessionRepository.UpdateAsync(session);
            throw new Exception("上传会话已过期");
        }

        // 检查分片索引
        if (chunkIndex < 0 || chunkIndex >= session.TotalChunks)
        {
            throw new Exception($"分片索引无效: {chunkIndex}, 总分片数: {session.TotalChunks}");
        }

        // 检查分片是否已上传
        var uploadedIndexes = JsonConvert.DeserializeObject<List<int>>(session.UploadedChunkIndexes ?? "[]") ?? new List<int>();
        if (uploadedIndexes.Contains(chunkIndex))
        {
            Log.Warning("[ChunkedUpload] 分片已存在: {SessionId}, 分片: {ChunkIndex}", sessionId, chunkIndex);
            return MapToVo(session);
        }

        // 保存分片文件
        var chunkFilePath = GetChunkFilePath(sessionId, chunkIndex);
        using (var stream = new FileStream(chunkFilePath, FileMode.Create))
        {
            await chunkData.CopyToAsync(stream);
        }

        // 更新会话记录
        uploadedIndexes.Add(chunkIndex);
        uploadedIndexes.Sort();
        session.UploadedChunkIndexes = JsonConvert.SerializeObject(uploadedIndexes);
        session.UploadedChunks = uploadedIndexes.Count;
        session.ModifyTime = DateTime.Now;

        await _sessionRepository.UpdateAsync(session);

        Log.Information("[ChunkedUpload] 上传分片: {SessionId}, 分片: {ChunkIndex}/{TotalChunks}",
            sessionId, chunkIndex, session.TotalChunks);

        return MapToVo(session);
    }

    /// <summary>
    /// 合并分片并创建附件
    /// </summary>
    public async Task<AttachmentVo?> MergeChunksAsync(MergeChunksDto dto, long userId, string userName)
    {
        // 获取会话
        var session = await _sessionRepository.QueryFirstAsync(s => s.SessionId == dto.SessionId && s.UserId == userId);
        if (session == null)
        {
            throw new Exception($"上传会话不存在或无权访问: {dto.SessionId}");
        }

        // 检查会话状态
        if (session.Status != "Uploading")
        {
            throw new Exception($"会话状态不允许合并: {session.Status}");
        }

        // 检查是否所有分片都已上传
        if (session.UploadedChunks != session.TotalChunks)
        {
            throw new Exception($"分片未全部上传: {session.UploadedChunks}/{session.TotalChunks}");
        }

        try
        {
            // 合并分片
            var mergedFilePath = await MergeChunkFilesAsync(session);

            // 创建 IFormFile
            using var fileStream = new FileStream(mergedFilePath, FileMode.Open, FileAccess.Read);
            var formFile = new FormFile(fileStream, 0, fileStream.Length, "file", session.FileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = session.MimeType ?? "application/octet-stream"
            };

            // 上传到附件服务
            var uploadOptions = new FileUploadOptionsDto
            {
                OriginalFileName = session.FileName,
                BusinessType = session.BusinessType,
                GenerateThumbnail = dto.GenerateThumbnail,
                GenerateMultipleSizes = dto.GenerateMultipleSizes,
                AddWatermark = dto.AddWatermark,
                WatermarkText = dto.WatermarkText,
                CalculateHash = true,
                RemoveExif = dto.RemoveExif
            };

            var attachment = await _attachmentService.UploadFileAsync(formFile, uploadOptions, userId, userName);

            if (attachment != null)
            {
                // 更新会话状态
                session.Status = "Completed";
                session.AttachmentId = attachment.VoId;
                session.ModifyTime = DateTime.Now;
                await _sessionRepository.UpdateAsync(session);

                // 清理临时文件
                CleanupSessionFiles(session.SessionId);

                Log.Information("[ChunkedUpload] 合并完成: {SessionId}, 附件ID: {AttachmentId}",
                    session.SessionId, attachment.VoId);

                return attachment;
            }
            else
            {
                session.Status = "Failed";
                session.ErrorMessage = "创建附件失败";
                await _sessionRepository.UpdateAsync(session);
                throw new Exception("创建附件失败");
            }
        }
        catch (Exception ex)
        {
            session.Status = "Failed";
            session.ErrorMessage = ex.Message;
            await _sessionRepository.UpdateAsync(session);
            Log.Error(ex, "[ChunkedUpload] 合并失败: {SessionId}", session.SessionId);
            throw;
        }
    }

    /// <summary>
    /// 获取上传会话信息
    /// </summary>
    public async Task<UploadSessionVo?> GetSessionAsync(string sessionId, long userId)
    {
        var session = await _sessionRepository.QueryFirstAsync(s => s.SessionId == sessionId && s.UserId == userId);
        return session == null ? null : MapToVo(session);
    }

    /// <summary>
    /// 取消上传会话
    /// </summary>
    public async Task CancelSessionAsync(string sessionId, long userId)
    {
        var session = await _sessionRepository.QueryFirstAsync(s => s.SessionId == sessionId && s.UserId == userId);
        if (session == null)
        {
            throw new Exception($"上传会话不存在或无权访问: {sessionId}");
        }

        session.Status = "Cancelled";
        session.ModifyTime = DateTime.Now;
        await _sessionRepository.UpdateAsync(session);

        // 清理临时文件
        CleanupSessionFiles(sessionId);

        Log.Information("[ChunkedUpload] 取消会话: {SessionId}", sessionId);
    }

    /// <summary>
    /// 清理过期会话
    /// </summary>
    public async Task CleanupExpiredSessionsAsync()
    {
        var expiredSessions = await _sessionRepository.QueryAsync(s =>
            s.ExpiresAt < DateTime.Now &&
            (s.Status == "Uploading" || s.Status == "Failed"));

        foreach (var session in expiredSessions)
        {
            // 清理临时文件
            CleanupSessionFiles(session.SessionId);

            // 更新状态
            session.Status = "Expired";
            session.ModifyTime = DateTime.Now;
            await _sessionRepository.UpdateAsync(session);
        }

        if (expiredSessions.Any())
        {
            Log.Information("[ChunkedUpload] 清理过期会话: {Count} 个", expiredSessions.Count());
        }
    }

    #region 私有方法

    /// <summary>
    /// 获取会话目录
    /// </summary>
    private string GetSessionDirectory(string sessionId)
    {
        return Path.Combine(_tempChunkPath, sessionId);
    }

    /// <summary>
    /// 获取分片文件路径
    /// </summary>
    private string GetChunkFilePath(string sessionId, int chunkIndex)
    {
        var sessionDir = GetSessionDirectory(sessionId);
        return Path.Combine(sessionDir, $"chunk_{chunkIndex}");
    }

    /// <summary>
    /// 合并分片文件
    /// </summary>
    private async Task<string> MergeChunkFilesAsync(UploadSession session)
    {
        var sessionDir = GetSessionDirectory(session.SessionId);
        var mergedFilePath = Path.Combine(sessionDir, "merged_file");

        using (var outputStream = new FileStream(mergedFilePath, FileMode.Create))
        {
            for (int i = 0; i < session.TotalChunks; i++)
            {
                var chunkFilePath = GetChunkFilePath(session.SessionId, i);
                if (!File.Exists(chunkFilePath))
                {
                    throw new Exception($"分片文件不存在: {i}");
                }

                using (var inputStream = new FileStream(chunkFilePath, FileMode.Open))
                {
                    await inputStream.CopyToAsync(outputStream);
                }
            }
        }

        // 验证文件大小
        var fileInfo = new FileInfo(mergedFilePath);
        if (fileInfo.Length != session.TotalSize)
        {
            throw new Exception($"合并后文件大小不匹配: 预期 {session.TotalSize}, 实际 {fileInfo.Length}");
        }

        return mergedFilePath;
    }

    /// <summary>
    /// 清理会话文件
    /// </summary>
    private void CleanupSessionFiles(string sessionId)
    {
        try
        {
            var sessionDir = GetSessionDirectory(sessionId);
            if (Directory.Exists(sessionDir))
            {
                Directory.Delete(sessionDir, true);
                Log.Information("[ChunkedUpload] 清理会话文件: {SessionId}", sessionId);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ChunkedUpload] 清理会话文件失败: {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// 映射到 ViewModel
    /// </summary>
    private UploadSessionVo MapToVo(UploadSession session)
    {
        var uploadedIndexes = JsonConvert.DeserializeObject<List<int>>(session.UploadedChunkIndexes ?? "[]") ?? new List<int>();
        var progress = session.TotalChunks > 0 ? (decimal)session.UploadedChunks / session.TotalChunks * 100 : 0;

        return new UploadSessionVo
        {
            VoSessionId = session.SessionId,
            VoFileName = session.FileName,
            VoTotalSize = session.TotalSize,
            VoChunkSize = session.ChunkSize,
            VoTotalChunks = session.TotalChunks,
            VoUploadedChunks = session.UploadedChunks,
            VoUploadedChunkIndexes = uploadedIndexes,
            VoProgress = Math.Round(progress, 2),
            VoStatus = session.Status,
            VoAttachmentId = session.AttachmentId,
            VoExpiresAt = session.ExpiresAt,
            VoCreateTime = session.CreateTime
        };
    }

    #endregion
}
