using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Radish.Common.CoreTool;
using Radish.Common.Exceptions;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Radish.Service.Internal;
using Radish.Shared.Constants;
using Serilog;

namespace Radish.Service;

/// <summary>
/// 分片上传服务实现。
/// </summary>
public class ChunkedUploadService : IChunkedUploadService
{
    private const int MaxTotalChunks = 10_000;
    private const int ReconciliationQueryBatchSize = 500;
    private const int SettlementReplayBatchSize = 2_000;
    private static readonly TimeSpan OrphanDirectoryGracePeriod = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan QuotaSettlementReplayWindow = TimeSpan.FromDays(8);

    private readonly IUploadSessionRepository _sessionRepository;
    private readonly IAttachmentService _attachmentService;
    private readonly IUploadRateLimitService _rateLimitService;
    private readonly ChunkedUploadOptions _chunkedOptions;
    private readonly FileStorageOptions _fileStorageOptions;
    private readonly TimeProvider _timeProvider;
    private readonly string _tempChunkPath;

    public ChunkedUploadService(
        IUploadSessionRepository sessionRepository,
        IAttachmentService attachmentService,
        IUploadRateLimitService rateLimitService,
        IOptions<ChunkedUploadOptions> chunkedOptions,
        IOptions<FileStorageOptions> fileStorageOptions,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository;
        _attachmentService = attachmentService;
        _rateLimitService = rateLimitService;
        _chunkedOptions = chunkedOptions.Value;
        _fileStorageOptions = fileStorageOptions.Value;
        _timeProvider = timeProvider;

        if (_chunkedOptions.Enable &&
            (string.IsNullOrWhiteSpace(_chunkedOptions.TempChunkPath) ||
             _chunkedOptions.MinChunkSize <= 0 ||
             _chunkedOptions.MaxChunkSize < _chunkedOptions.MinChunkSize ||
             _chunkedOptions.MaxChunkSize > ChunkedUploadOptions.TransportMaxChunkSize ||
             _chunkedOptions.DefaultChunkSize < _chunkedOptions.MinChunkSize ||
             _chunkedOptions.DefaultChunkSize > _chunkedOptions.MaxChunkSize ||
             _chunkedOptions.SessionExpirationHours is < 1 or > 168))
        {
            throw new InvalidOperationException(
                "分片上传配置无效：临时目录不能为空，分片范围须有效且不超过 10 MiB，会话有效期须为 1-168 小时。");
        }

        var solutionRoot = AppPathTool.GetSolutionRootOrBasePath();
        var configuredTempPath = string.IsNullOrWhiteSpace(_chunkedOptions.TempChunkPath)
            ? "DataBases/Temp/Chunks"
            : _chunkedOptions.TempChunkPath;
        _tempChunkPath = Path.IsPathRooted(configuredTempPath)
            ? Path.GetFullPath(configuredTempPath)
            : Path.GetFullPath(Path.Combine(solutionRoot, configuredTempPath));
        if (_chunkedOptions.Enable)
        {
            Directory.CreateDirectory(_tempChunkPath);
        }
    }

    public async Task<UploadSessionVo> CreateSessionAsync(CreateUploadSessionDto dto, long userId, string userName)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsureChunkedUploadEnabled();
        var validation = ValidateCreateRequest(dto);
        var now = GetUtcNow();
        var sessionId = Guid.NewGuid().ToString("N");
        var sessionLifetime = TimeSpan.FromHours(_chunkedOptions.SessionExpirationHours);
        var reservationLifetime = sessionLifetime + TimeSpan.FromMinutes(30);
        var acquireResult = await _rateLimitService.AcquireUploadAsync(
            userId,
            sessionId,
            dto.TotalSize,
            reservationLifetime);
        if (!acquireResult.IsAllowed)
        {
            throw BuildRateLimitException(acquireResult);
        }

        var session = new UploadSession
        {
            SessionId = sessionId,
            FileName = dto.FileName.Trim(),
            TotalSize = dto.TotalSize,
            MimeType = string.IsNullOrWhiteSpace(dto.MimeType) ? null : dto.MimeType.Trim(),
            ChunkSize = validation.ChunkSize,
            TotalChunks = validation.TotalChunks,
            UploadedChunks = 0,
            UploadedChunkIndexes = "[]",
            BusinessType = validation.BusinessType,
            BusinessId = null,
            UserId = userId,
            UserName = userName,
            Status = "Uploading",
            ExpiresAt = now.Add(sessionLifetime),
            CreateTime = now,
            ModifyTime = now
        };

        try
        {
            Directory.CreateDirectory(GetSessionDirectory(sessionId));
            var sessionRecordId = await _sessionRepository.AddAsync(session);
            if (sessionRecordId <= 0)
            {
                throw new InvalidOperationException("创建分片上传会话记录失败。");
            }
            session.Id = sessionRecordId;
        }
        catch
        {
            CleanupSessionFiles(sessionId);
            await TryReleaseReservationAsync(userId, sessionId);
            throw;
        }

        Log.Information(
            "[ChunkedUpload] 创建上传会话: {SessionId}, 文件: {FileName}, 大小: {Size}, 分片数: {Chunks}",
            sessionId,
            session.FileName,
            session.TotalSize,
            session.TotalChunks);
        return MapToVo(session);
    }

    public async Task<UploadSessionVo> UploadChunkAsync(
        string sessionId,
        int chunkIndex,
        IFormFile chunkData,
        long userId)
    {
        EnsureChunkedUploadEnabled();
        ValidateSessionId(sessionId);
        ArgumentNullException.ThrowIfNull(chunkData);

        using (await AsyncKeyedLock.AcquireAsync(GetSessionLockKey(sessionId)))
        {
            var session = await GetOwnedSessionOrThrowAsync(sessionId, userId);
            await EnsureUploadingSessionAsync(session);
            if (chunkIndex < 0 || chunkIndex >= session.TotalChunks)
            {
                throw UploadError(
                    "分片索引无效",
                    StatusCodes.Status400BadRequest,
                    AttachmentErrorCodes.UploadChunkInvalid);
            }

            var expectedLength = GetExpectedChunkLength(session, chunkIndex);
            if (chunkData.Length != expectedLength || chunkData.Length > _chunkedOptions.MaxChunkSize)
            {
                throw UploadError(
                    "分片数据长度与会话契约不一致",
                    StatusCodes.Status400BadRequest,
                    AttachmentErrorCodes.UploadChunkInvalid);
            }

            var uploadedIndexes = DeserializeUploadedIndexes(session);
            var chunkFilePath = GetChunkFilePath(sessionId, chunkIndex);
            if (uploadedIndexes.Contains(chunkIndex))
            {
                if (!File.Exists(chunkFilePath) || new FileInfo(chunkFilePath).Length != expectedLength)
                {
                    throw UploadError(
                        "上传会话数据不一致",
                        StatusCodes.Status409Conflict,
                        AttachmentErrorCodes.UploadSessionStateConflict);
                }

                return MapToVo(session);
            }

            var temporaryPath = $"{chunkFilePath}.part";
            var previousUploadedIndexes = session.UploadedChunkIndexes;
            var previousUploadedChunks = session.UploadedChunks;
            var previousModifyTime = session.ModifyTime;
            try
            {
                await using (var stream = new FileStream(
                                 temporaryPath,
                                 FileMode.Create,
                                 FileAccess.Write,
                                 FileShare.None,
                                 81920,
                                 FileOptions.Asynchronous))
                {
                    await chunkData.CopyToAsync(stream);
                }

                if (new FileInfo(temporaryPath).Length != expectedLength)
                {
                    throw UploadError(
                        "分片数据长度与会话契约不一致",
                        StatusCodes.Status400BadRequest,
                        AttachmentErrorCodes.UploadChunkInvalid);
                }

                File.Move(temporaryPath, chunkFilePath, overwrite: true);
                uploadedIndexes.Add(chunkIndex);
                uploadedIndexes.Sort();
                session.UploadedChunkIndexes = JsonConvert.SerializeObject(uploadedIndexes);
                session.UploadedChunks = uploadedIndexes.Count;
                session.ModifyTime = GetUtcNow();
                await UpdateSessionOrThrowAsync(session);
            }
            catch (SessionUpdateRejectedException)
            {
                DeleteFileIfExists(temporaryPath);
                DeleteFileIfExists(chunkFilePath);
                session.UploadedChunkIndexes = previousUploadedIndexes;
                session.UploadedChunks = previousUploadedChunks;
                session.ModifyTime = previousModifyTime;
                throw;
            }
            catch
            {
                DeleteFileIfExists(temporaryPath);
                throw;
            }

            Log.Information(
                "[ChunkedUpload] 上传分片: {SessionId}, 分片: {ChunkIndex}/{TotalChunks}",
                sessionId,
                chunkIndex,
                session.TotalChunks);
            return MapToVo(session);
        }
    }

    public async Task<AttachmentVo?> MergeChunksAsync(MergeChunksDto dto, long userId, string userName)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsureChunkedUploadEnabled();
        ValidateSessionId(dto.SessionId);

        using (await AsyncKeyedLock.AcquireAsync(GetSessionLockKey(dto.SessionId)))
        {
            var session = await GetOwnedSessionOrThrowAsync(dto.SessionId, userId);
            if (session.Status == "Completed")
            {
                if (!session.AttachmentId.HasValue)
                {
                    throw UploadError(
                        "已完成的上传会话缺少附件关联",
                        StatusCodes.Status409Conflict,
                        AttachmentErrorCodes.UploadSessionStateConflict);
                }

                var existingAttachment = await _attachmentService.QueryByIdAsync(session.AttachmentId.Value);
                if (existingAttachment == null)
                {
                    throw UploadError(
                        "已完成上传对应的附件不存在",
                        StatusCodes.Status409Conflict,
                        AttachmentErrorCodes.UploadSessionStateConflict);
                }

                await TryCompleteReservationAsync(userId, session.SessionId, existingAttachment.VoId);
                CleanupSessionFiles(session.SessionId);
                return existingAttachment;
            }

            await EnsureUploadingSessionAsync(session);
            var uploadedIndexes = DeserializeUploadedIndexes(session);
            if (session.UploadedChunks != session.TotalChunks ||
                uploadedIndexes.Count != session.TotalChunks ||
                !Enumerable.Range(0, session.TotalChunks).SequenceEqual(uploadedIndexes))
            {
                throw UploadError(
                    "分片尚未全部上传",
                    StatusCodes.Status409Conflict,
                    AttachmentErrorCodes.UploadChunksIncomplete);
            }

            AttachmentVo? persistedAttachment = null;
            try
            {
                var mergedFilePath = await MergeChunkFilesAsync(session);
                await using var fileStream = new FileStream(
                    mergedFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    FileOptions.Asynchronous);
                var formFile = new FormFile(fileStream, 0, fileStream.Length, "file", session.FileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = session.MimeType ?? "application/octet-stream"
                };
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

                persistedAttachment = await _attachmentService.UploadFileAsync(
                    formFile,
                    uploadOptions,
                    userId,
                    userName);
                if (persistedAttachment == null)
                {
                    throw UploadError(
                        "创建附件失败",
                        StatusCodes.Status500InternalServerError,
                        AttachmentErrorCodes.ProcessingFailed);
                }

                session.Status = "Completed";
                session.AttachmentId = persistedAttachment.VoId;
                session.ErrorMessage = null;
                session.ModifyTime = GetUtcNow();
                try
                {
                    await UpdateSessionOrThrowAsync(session);
                }
                catch (Exception exception)
                {
                    // 附件已经持久化，不能把非幂等操作伪装成失败并诱导客户端重新合并。
                    // 清理分片后，即使会话状态回写暂时失败，也不会再次生成重复附件。
                    Log.Error(
                        exception,
                        "[ChunkedUpload] 附件 {AttachmentId} 已持久化，但完成会话回写失败: {SessionId}",
                        persistedAttachment.VoId,
                        session.SessionId);
                    await TryUpdateSessionAsync(session);
                }
            }
            catch (Exception exception)
            {
                session.Status = "Failed";
                session.ErrorMessage = "分片合并或附件处理失败";
                session.ModifyTime = GetUtcNow();
                await TryUpdateSessionAsync(session);
                CleanupSessionFiles(session.SessionId);

                if (persistedAttachment == null)
                {
                    await TryReleaseReservationAsync(userId, session.SessionId);
                }
                else
                {
                    await TryCompleteReservationAsync(userId, session.SessionId, persistedAttachment.VoId);
                }

                Log.Error(exception, "[ChunkedUpload] 合并失败: {SessionId}", session.SessionId);
                throw;
            }

            await TryCompleteReservationAsync(userId, session.SessionId, persistedAttachment.VoId);
            CleanupSessionFiles(session.SessionId);
            Log.Information(
                "[ChunkedUpload] 合并完成: {SessionId}, 附件ID: {AttachmentId}",
                session.SessionId,
                persistedAttachment.VoId);
            return persistedAttachment;
        }
    }

    public async Task<UploadSessionVo?> GetSessionAsync(string sessionId, long userId)
    {
        EnsureChunkedUploadEnabled();
        ValidateSessionId(sessionId);
        var session = await _sessionRepository.QueryFirstAsync(
            candidate => candidate.SessionId == sessionId && candidate.UserId == userId);
        return session == null ? null : MapToVo(session);
    }

    public async Task CancelSessionAsync(string sessionId, long userId)
    {
        EnsureChunkedUploadEnabled();
        ValidateSessionId(sessionId);
        using (await AsyncKeyedLock.AcquireAsync(GetSessionLockKey(sessionId)))
        {
            var session = await GetOwnedSessionOrThrowAsync(sessionId, userId);
            if (session.Status == "Cancelled")
            {
                CleanupSessionFiles(sessionId);
                await TryReleaseReservationAsync(userId, sessionId);
                return;
            }

            if (session.Status != "Uploading" && session.Status != "Failed")
            {
                throw UploadError(
                    "上传会话状态不允许取消",
                    StatusCodes.Status409Conflict,
                    AttachmentErrorCodes.UploadSessionStateConflict);
            }

            session.Status = "Cancelled";
            session.ErrorMessage = null;
            session.ModifyTime = GetUtcNow();
            await UpdateSessionOrThrowAsync(session);
            CleanupSessionFiles(sessionId);
            await TryReleaseReservationAsync(userId, sessionId);
            Log.Information("[ChunkedUpload] 取消会话: {SessionId}", sessionId);
        }
    }

    public async Task CleanupExpiredSessionsAsync()
    {
        var now = GetUtcNow();
        var expiredSessions = await _sessionRepository.QueryExpiredAcrossTenantsAsync(now);
        var cleanedCount = 0;
        var settlementAttemptedSessionIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var session in expiredSessions)
        {
            try
            {
                using (await AsyncKeyedLock.AcquireAsync(GetSessionLockKey(session.SessionId)))
                {
                    var expiredBeforeUtc = GetUtcNow();
                    var markedExpired = await _sessionRepository.TryMarkExpiredAcrossTenantsAsync(
                        session.SessionId,
                        session.TenantId,
                        session.UserId,
                        expiredBeforeUtc,
                        expiredBeforeUtc);
                    if (!markedExpired)
                    {
                        continue;
                    }

                    CleanupSessionFiles(session.SessionId);
                    await TryReleaseReservationAsync(session.UserId, session.SessionId);
                    settlementAttemptedSessionIds.Add(session.SessionId);
                    cleanedCount++;
                }
            }
            catch (Exception exception)
            {
                Log.Error(
                    exception,
                    "[ChunkedUpload] 单个过期会话清理失败，后续批次将重试: {SessionId}",
                    session.SessionId);
            }
        }

        await ReconcileSessionDirectoriesAsync(now);
        await ReplayTerminalQuotaSettlementsAsync(now, settlementAttemptedSessionIds);

        if (cleanedCount > 0)
        {
            Log.Information("[ChunkedUpload] 清理过期会话: {Count} 个", cleanedCount);
        }
    }

    private async Task ReconcileSessionDirectoriesAsync(DateTime now)
    {
        if (!Directory.Exists(_tempChunkPath))
        {
            return;
        }

        List<string> sessionDirectories;
        try
        {
            sessionDirectories = Directory.EnumerateDirectories(_tempChunkPath)
                .Where(path => Guid.TryParseExact(Path.GetFileName(path), "N", out _))
                .ToList();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "[ChunkedUpload] 枚举分片临时目录失败");
            return;
        }

        if (sessionDirectories.Count == 0)
        {
            return;
        }

        var sessionsById = new Dictionary<string, UploadSession>(StringComparer.Ordinal);
        var sessionIds = sessionDirectories.Select(path => Path.GetFileName(path)!).ToList();
        foreach (var batch in sessionIds.Chunk(ReconciliationQueryBatchSize))
        {
            var sessions = await _sessionRepository.QueryBySessionIdsAcrossTenantsAsync(batch.ToList());
            foreach (var session in sessions)
            {
                sessionsById[session.SessionId] = session;
            }
        }

        foreach (var sessionDirectory in sessionDirectories)
        {
            var sessionId = Path.GetFileName(sessionDirectory);
            try
            {
                if (!sessionsById.TryGetValue(sessionId, out var session))
                {
                    var lastWriteTimeUtc = Directory.GetLastWriteTimeUtc(sessionDirectory);
                    if (lastWriteTimeUtc <= now.Subtract(OrphanDirectoryGracePeriod))
                    {
                        CleanupSessionFiles(sessionId);
                    }

                    continue;
                }

                if (!IsTerminalStatus(session.Status))
                {
                    continue;
                }

                using (await AsyncKeyedLock.AcquireAsync(GetSessionLockKey(sessionId)))
                {
                    CleanupSessionFiles(sessionId);
                }
            }
            catch (Exception exception)
            {
                Log.Error(
                    exception,
                    "[ChunkedUpload] 临时目录对账失败，后续批次将重试: {SessionId}",
                    sessionId);
            }
        }
    }

    private async Task ReplayTerminalQuotaSettlementsAsync(
        DateTime now,
        ISet<string> settlementAttemptedSessionIds)
    {
        var sessions = await _sessionRepository.QueryTerminalForSettlementAcrossTenantsAsync(
            now.Subtract(QuotaSettlementReplayWindow),
            SettlementReplayBatchSize);
        foreach (var session in sessions)
        {
            if (!settlementAttemptedSessionIds.Add(session.SessionId))
            {
                continue;
            }

            if (session.Status == "Completed" && session.AttachmentId.HasValue)
            {
                await TryCompleteReservationAsync(
                    session.UserId,
                    session.SessionId,
                    session.AttachmentId.Value);
            }
            else
            {
                await TryReleaseReservationAsync(session.UserId, session.SessionId);
            }
        }
    }

    private static bool IsTerminalStatus(string status)
    {
        return status is "Completed" or "Cancelled" or "Expired" or "Failed";
    }

    private CreateRequestValidation ValidateCreateRequest(CreateUploadSessionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FileName) ||
            dto.FileName.Length > 255 ||
            dto.FileName.IndexOfAny(['/', '\\']) >= 0 ||
            dto.FileName.Any(char.IsControl) ||
            dto.MimeType?.Length > 100 ||
            dto.BusinessId.HasValue)
        {
            throw UploadError(
                "上传会话请求无效",
                StatusCodes.Status400BadRequest,
                AttachmentErrorCodes.UploadSessionInvalid);
        }

        if (!AttachmentBusinessTypes.TryNormalize(dto.BusinessType, out var businessType))
        {
            throw UploadError(
                "不支持该附件业务类型",
                StatusCodes.Status400BadRequest,
                AttachmentErrorCodes.BusinessTypeUnsupported);
        }

        var extension = Path.GetExtension(dto.FileName).ToLowerInvariant();
        if (!FileStoragePolicy.IsAllowedForBusinessType(
                _fileStorageOptions,
                extension,
                AttachmentBusinessTypes.RequiresImage(businessType)))
        {
            throw UploadError(
                "不支持的文件类型",
                StatusCodes.Status415UnsupportedMediaType,
                AttachmentErrorCodes.UnsupportedMediaType);
        }

        var maxFileSize = FileStoragePolicy.GetMaxFileSize(_fileStorageOptions, businessType, extension);
        if (dto.TotalSize <= 0)
        {
            throw UploadError(
                "文件不能为空",
                StatusCodes.Status400BadRequest,
                AttachmentErrorCodes.FileEmpty);
        }

        if (dto.TotalSize > maxFileSize)
        {
            throw UploadError(
                "文件大小超过限制",
                StatusCodes.Status413PayloadTooLarge,
                AttachmentErrorCodes.FileTooLarge,
                FormatFileSize(maxFileSize));
        }

        var chunkSize = dto.ChunkSize == 0 ? _chunkedOptions.DefaultChunkSize : dto.ChunkSize;
        if (chunkSize < _chunkedOptions.MinChunkSize || chunkSize > _chunkedOptions.MaxChunkSize)
        {
            throw UploadError(
                "分片大小无效",
                StatusCodes.Status400BadRequest,
                AttachmentErrorCodes.UploadSessionInvalid);
        }

        var totalChunks = 1 + ((dto.TotalSize - 1) / chunkSize);
        if (totalChunks <= 0 || totalChunks > MaxTotalChunks || totalChunks > int.MaxValue)
        {
            throw UploadError(
                "分片总数无效",
                StatusCodes.Status400BadRequest,
                AttachmentErrorCodes.UploadSessionInvalid);
        }

        return new CreateRequestValidation(businessType, chunkSize, (int)totalChunks);
    }

    private async Task<UploadSession> GetOwnedSessionOrThrowAsync(string sessionId, long userId)
    {
        var session = await _sessionRepository.QueryFirstAsync(
            candidate => candidate.SessionId == sessionId && candidate.UserId == userId);
        return session ?? throw UploadError(
            "上传会话不存在或无权访问",
            StatusCodes.Status404NotFound,
            AttachmentErrorCodes.UploadSessionNotFound);
    }

    private async Task EnsureUploadingSessionAsync(UploadSession session)
    {
        if (GetUtcNow() >= session.ExpiresAt)
        {
            session.Status = "Expired";
            session.ErrorMessage = "上传会话已过期";
            session.ModifyTime = GetUtcNow();
            await UpdateSessionOrThrowAsync(session);
            CleanupSessionFiles(session.SessionId);
            await TryReleaseReservationAsync(session.UserId, session.SessionId);
            throw UploadError(
                "上传会话已过期",
                StatusCodes.Status410Gone,
                AttachmentErrorCodes.UploadSessionExpired);
        }

        if (session.Status != "Uploading")
        {
            throw UploadError(
                "上传会话状态不允许执行该操作",
                StatusCodes.Status409Conflict,
                AttachmentErrorCodes.UploadSessionStateConflict);
        }
    }

    private async Task<string> MergeChunkFilesAsync(UploadSession session)
    {
        var mergedFilePath = GetContainedSessionPath(session.SessionId, "merged_file.part");
        DeleteFileIfExists(mergedFilePath);
        await using (var outputStream = new FileStream(
                         mergedFilePath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         81920,
                         FileOptions.Asynchronous))
        {
            for (var index = 0; index < session.TotalChunks; index++)
            {
                var chunkFilePath = GetChunkFilePath(session.SessionId, index);
                var expectedLength = GetExpectedChunkLength(session, index);
                if (!File.Exists(chunkFilePath) || new FileInfo(chunkFilePath).Length != expectedLength)
                {
                    throw UploadError(
                        "上传分片文件不完整",
                        StatusCodes.Status409Conflict,
                        AttachmentErrorCodes.UploadSessionStateConflict);
                }

                await using var inputStream = new FileStream(
                    chunkFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    FileOptions.Asynchronous);
                await inputStream.CopyToAsync(outputStream);
            }
        }

        if (new FileInfo(mergedFilePath).Length != session.TotalSize)
        {
            throw UploadError(
                "合并后文件大小不匹配",
                StatusCodes.Status409Conflict,
                AttachmentErrorCodes.UploadSessionStateConflict);
        }

        return mergedFilePath;
    }

    private string GetSessionDirectory(string sessionId)
    {
        return GetContainedSessionPath(sessionId);
    }

    private string GetChunkFilePath(string sessionId, int chunkIndex)
    {
        return GetContainedSessionPath(sessionId, $"chunk_{chunkIndex}");
    }

    private string GetContainedSessionPath(string sessionId, params string[] childSegments)
    {
        ValidateSessionId(sessionId);
        var combinedSegments = new[] { _tempChunkPath, sessionId }.Concat(childSegments).ToArray();
        var candidate = Path.GetFullPath(Path.Combine(combinedSegments));
        var relative = Path.GetRelativePath(_tempChunkPath, candidate);
        if (relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException("分片上传临时路径超出配置根目录。");
        }

        return candidate;
    }

    private void CleanupSessionFiles(string sessionId)
    {
        try
        {
            var sessionDirectory = GetSessionDirectory(sessionId);
            if (Directory.Exists(sessionDirectory))
            {
                Directory.Delete(sessionDirectory, recursive: true);
                Log.Information("[ChunkedUpload] 清理会话文件: {SessionId}", sessionId);
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "[ChunkedUpload] 清理会话文件失败: {SessionId}", sessionId);
        }
    }

    private async Task TryReleaseReservationAsync(long userId, string sessionId)
    {
        try
        {
            await _rateLimitService.FailUploadAsync(userId, sessionId);
        }
        catch (Exception exception)
        {
            Log.Error(
                exception,
                "[ChunkedUpload] 释放上传预留失败: {SessionId}, 用户: {UserId}",
                sessionId,
                userId);
        }
    }

    private async Task TryCompleteReservationAsync(long userId, string sessionId, long attachmentId)
    {
        try
        {
            await _rateLimitService.CompleteUploadAsync(userId, sessionId);
        }
        catch (Exception exception)
        {
            Log.Error(
                exception,
                "[ChunkedUpload] 附件 {AttachmentId} 已持久化，但上传配额结算失败: {SessionId}, 用户: {UserId}",
                attachmentId,
                sessionId,
                userId);
        }
    }

    private async Task TryUpdateSessionAsync(UploadSession session)
    {
        try
        {
            await UpdateSessionOrThrowAsync(session);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "[ChunkedUpload] 更新失败会话状态失败: {SessionId}", session.SessionId);
        }
    }

    private async Task UpdateSessionOrThrowAsync(UploadSession session)
    {
        if (!await _sessionRepository.UpdateAsync(session))
        {
            throw new SessionUpdateRejectedException();
        }
    }

    private UploadSessionVo MapToVo(UploadSession session)
    {
        var uploadedIndexes = DeserializeUploadedIndexes(session);
        var progress = session.TotalChunks > 0
            ? (decimal)session.UploadedChunks / session.TotalChunks * 100
            : 0;
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

    private static List<int> DeserializeUploadedIndexes(UploadSession session)
    {
        try
        {
            return JsonConvert.DeserializeObject<List<int>>(session.UploadedChunkIndexes ?? "[]") ?? [];
        }
        catch (JsonException exception)
        {
            throw UploadError(
                "上传会话数据不一致",
                exception,
                StatusCodes.Status409Conflict,
                AttachmentErrorCodes.UploadSessionStateConflict);
        }
    }

    private static long GetExpectedChunkLength(UploadSession session, int chunkIndex)
    {
        return chunkIndex == session.TotalChunks - 1
            ? session.TotalSize - ((long)chunkIndex * session.ChunkSize)
            : session.ChunkSize;
    }

    private static void ValidateSessionId(string sessionId)
    {
        if (!Guid.TryParseExact(sessionId, "N", out _))
        {
            throw UploadError(
                "上传会话标识无效",
                StatusCodes.Status400BadRequest,
                AttachmentErrorCodes.UploadSessionInvalid);
        }
    }

    private void EnsureChunkedUploadEnabled()
    {
        if (!_chunkedOptions.Enable)
        {
            throw UploadError(
                "分片上传当前未启用",
                StatusCodes.Status503ServiceUnavailable,
                AttachmentErrorCodes.ChunkedUploadDisabled);
        }
    }

    private BusinessException BuildRateLimitException(UploadRateLimitCheckResult result)
    {
        var errorCode = result.FailureKind switch
        {
            UploadRateLimitFailureKind.ConcurrentUploads => AttachmentErrorCodes.ConcurrentUploadLimitReached,
            UploadRateLimitFailureKind.UploadFrequency => AttachmentErrorCodes.UploadFrequencyLimitReached,
            UploadRateLimitFailureKind.DailyUploadSize => AttachmentErrorCodes.DailyUploadSizeLimitReached,
            _ => AttachmentErrorCodes.RateLimited
        };
        return UploadError(
            result.ErrorMessage ?? "上传请求过于频繁，请稍后再试",
            StatusCodes.Status429TooManyRequests,
            errorCode,
            result.MessageArguments);
    }

    private DateTime GetUtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static string GetSessionLockKey(string sessionId) => $"chunk-session:{sessionId}";

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static BusinessException UploadError(
        string message,
        int statusCode,
        string errorCode,
        params object[] messageArguments)
    {
        return new BusinessException(
            message,
            statusCode,
            errorCode,
            AttachmentErrorCodes.ResolveMessageKey(errorCode),
            messageArguments);
    }

    private static BusinessException UploadError(
        string message,
        Exception innerException,
        int statusCode,
        string errorCode,
        params object[] messageArguments)
    {
        return new BusinessException(
            message,
            innerException,
            statusCode,
            errorCode,
            AttachmentErrorCodes.ResolveMessageKey(errorCode),
            messageArguments);
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double length = bytes;
        var order = 0;
        while (length >= 1024 && order < sizes.Length - 1)
        {
            order++;
            length /= 1024;
        }

        return $"{length:0.##} {sizes[order]}";
    }

    private sealed record CreateRequestValidation(string BusinessType, int ChunkSize, int TotalChunks);

    private sealed class SessionUpdateRejectedException()
        : InvalidOperationException("分片上传会话状态未能持久化。")
    {
    }
}
