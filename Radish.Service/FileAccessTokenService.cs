using System.Net;
using Radish.Common.Exceptions;
using Radish.Common.Security;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Radish.Service.Internal;

namespace Radish.Service;

/// <summary>
/// 文件访问令牌服务实现。原始 token 只在创建响应出现，持久化层仅保存 hash。
/// </summary>
public class FileAccessTokenService : IFileAccessTokenService
{
    private const int MaxValidHours = 168;
    private readonly IFileAccessTokenRepository _tokenRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IWikiAttachmentAccessService _wikiAttachmentAccessService;
    private readonly TimeProvider _timeProvider;

    public FileAccessTokenService(
        IFileAccessTokenRepository tokenRepository,
        IBaseRepository<Attachment> attachmentRepository,
        IWikiAttachmentAccessService wikiAttachmentAccessService,
        TimeProvider timeProvider)
    {
        _tokenRepository = tokenRepository;
        _attachmentRepository = attachmentRepository;
        _wikiAttachmentAccessService = wikiAttachmentAccessService;
        _timeProvider = timeProvider;
    }

    public async Task<FileAccessTokenCreatedVo> CreateTokenAsync(
        CreateFileAccessTokenDto dto,
        long userId,
        bool canManageAll,
        string publicBaseUrl,
        long tenantId = 0,
        IReadOnlyCollection<string>? roleNames = null)
    {
        ValidateCreateTokenRequest(dto);
        await EnsureCanManageAttachmentAsync(
            dto.AttachmentId,
            userId,
            canManageAll,
            tenantId,
            roleNames);

        var rawToken = FileAccessTokenHashing.GenerateRawToken();
        var now = GetUtcNow();
        var tokenEntity = new FileAccessToken
        {
            TokenHash = FileAccessTokenHashing.HashToken(rawToken),
            AttachmentId = dto.AttachmentId,
            AuthorizedUserId = dto.AuthorizedUserId,
            AuthorizedIp = NormalizeAuthorizedIp(dto.AuthorizedIp),
            MaxAccessCount = dto.MaxAccessCount,
            AccessCount = 0,
            ExpiresAt = now.AddHours(dto.ValidHours),
            CreatedBy = userId,
            IsRevoked = false,
            CreateTime = now,
            ModifyTime = now
        };

        tokenEntity.Id = await _tokenRepository.AddAsync(tokenEntity);

        return MapCreatedVo(tokenEntity, rawToken, publicBaseUrl, now);
    }

    public async Task<long?> ValidateAndUseTokenAsync(
        string rawToken,
        long? userId,
        string ipAddress,
        long tenantId = 0,
        IReadOnlyCollection<string>? roleNames = null)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var normalizedIp = NormalizeAuthorizedIp(ipAddress);
        var tokenHash = FileAccessTokenHashing.HashToken(rawToken);
        var now = GetUtcNow();
        var candidate = await _tokenRepository.GetByHashAsync(tokenHash);
        if (candidate == null ||
            candidate.IsRevoked ||
            candidate.ExpiresAt <= now ||
            (candidate.AuthorizedUserId.HasValue && candidate.AuthorizedUserId != userId) ||
            (!string.IsNullOrWhiteSpace(candidate.AuthorizedIp) && candidate.AuthorizedIp != normalizedIp) ||
            (candidate.MaxAccessCount > 0 && candidate.AccessCount >= candidate.MaxAccessCount))
        {
            return null;
        }

        var attachment = await _attachmentRepository.QueryByIdAsync(candidate.AttachmentId);
        if (attachment == null || attachment.IsDeleted || !attachment.IsEnabled)
        {
            return null;
        }
        if (await _wikiAttachmentAccessService.IsWikiControlledAsync(attachment) &&
            !await _wikiAttachmentAccessService.CanReadAsync(
                attachment,
                tenantId,
                userId,
                roleNames))
        {
            return null;
        }

        var consumedToken = await _tokenRepository.TryConsumeAsync(tokenHash, userId, normalizedIp, now);
        if (consumedToken == null)
        {
            return null;
        }

        return consumedToken.AttachmentId;
    }

    public async Task RevokeTokenAsync(
        long tokenId,
        long userId,
        bool canManageAll,
        long tenantId = 0,
        IReadOnlyCollection<string>? roleNames = null)
    {
        if (tokenId <= 0)
        {
            throw ValidationError("令牌记录 ID 无效", "FileToken.InvalidId");
        }

        var tokenEntity = await _tokenRepository.QueryByIdAsync(tokenId)
            ?? throw NotFoundError();
        await EnsureCanManageTokenAsync(
            tokenEntity,
            userId,
            canManageAll,
            tenantId,
            roleNames);

        var revoked = await _tokenRepository.TryRevokeByIdAsync(tokenId, GetUtcNow());
        if (!revoked)
        {
            throw new BusinessException("令牌已撤销", 409, "FileToken.AlreadyRevoked", "error.file_token.already_revoked");
        }
    }

    public async Task RevokeTokenAsync(
        string rawToken,
        long userId,
        bool canManageAll,
        long tenantId = 0,
        IReadOnlyCollection<string>? roleNames = null)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw ValidationError("令牌不能为空", "FileToken.Required");
        }

        var tokenHash = FileAccessTokenHashing.HashToken(rawToken);
        var tokenEntity = await _tokenRepository.GetByHashAsync(tokenHash)
            ?? throw NotFoundError();
        await EnsureCanManageTokenAsync(
            tokenEntity,
            userId,
            canManageAll,
            tenantId,
            roleNames);

        var revoked = await _tokenRepository.TryRevokeByHashAsync(tokenHash, GetUtcNow());
        if (!revoked)
        {
            throw new BusinessException("令牌已撤销", 409, "FileToken.AlreadyRevoked", "error.file_token.already_revoked");
        }
    }

    public async Task<FileAccessTokenSummaryVo?> GetTokenInfoAsync(
        string rawToken,
        long userId,
        bool canManageAll,
        long tenantId = 0,
        IReadOnlyCollection<string>? roleNames = null)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var tokenEntity = await _tokenRepository.GetByHashAsync(FileAccessTokenHashing.HashToken(rawToken));
        if (tokenEntity == null)
        {
            return null;
        }

        await EnsureCanManageTokenAsync(
            tokenEntity,
            userId,
            canManageAll,
            tenantId,
            roleNames);
        return MapSummaryVo(tokenEntity, GetUtcNow());
    }

    public async Task<List<FileAccessTokenSummaryVo>> GetAttachmentTokensAsync(
        long attachmentId,
        long userId,
        bool canManageAll,
        long tenantId = 0,
        IReadOnlyCollection<string>? roleNames = null)
    {
        await EnsureCanManageAttachmentAsync(
            attachmentId,
            userId,
            canManageAll,
            tenantId,
            roleNames);
        var now = GetUtcNow();
        var tokens = await _tokenRepository.QueryAsync(token =>
            token.AttachmentId == attachmentId &&
            !token.IsRevoked &&
            token.ExpiresAt > now);
        return tokens.Select(token => MapSummaryVo(token, now)).ToList();
    }

    public async Task CleanupExpiredTokensAsync()
    {
        using var summary = new ServiceCleanupSummary("file-tokens");
        var now = GetUtcNow();
        var expiredTokens = await _tokenRepository.QueryAsync(token =>
            token.ExpiresAt <= now &&
            !token.IsRevoked);

        foreach (var token in expiredTokens)
        {
            var revoked = await _tokenRepository.TryRevokeByIdAsync(token.Id, now);
            summary.ProcessedCount++;
            if (revoked) summary.UpdatedCount++;
            else summary.SkippedCount++;
        }

        summary.Completed = true;
    }

    private async Task EnsureCanManageTokenAsync(
        FileAccessToken token,
        long userId,
        bool canManageAll,
        long tenantId,
        IReadOnlyCollection<string>? roleNames)
    {
        var attachment = await _attachmentRepository.QueryByIdAsync(token.AttachmentId);
        if (attachment == null)
        {
            throw ForbiddenError();
        }

        if (await _wikiAttachmentAccessService.IsWikiControlledAsync(attachment))
        {
            if (!await _wikiAttachmentAccessService.CanManageAsync(
                    attachment,
                    tenantId,
                    userId,
                    roleNames))
            {
                throw ForbiddenError();
            }
            return;
        }

        if (!canManageAll && token.CreatedBy != userId && attachment.UploaderId != userId)
        {
            throw ForbiddenError();
        }
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }

    private async Task EnsureCanManageAttachmentAsync(
        long attachmentId,
        long userId,
        bool canManageAll,
        long tenantId,
        IReadOnlyCollection<string>? roleNames)
    {
        var attachment = await _attachmentRepository.QueryByIdAsync(attachmentId);
        if (attachment == null)
        {
            throw new BusinessException("附件不存在", 404, "Attachment.NotFound", "error.attachment.not_found");
        }

        if (await _wikiAttachmentAccessService.IsWikiControlledAsync(attachment))
        {
            if (!await _wikiAttachmentAccessService.CanManageAsync(
                    attachment,
                    tenantId,
                    userId,
                    roleNames))
            {
                throw ForbiddenError();
            }
            return;
        }

        if (!canManageAll && attachment.UploaderId != userId)
        {
            throw ForbiddenError();
        }
    }

    private static void ValidateCreateTokenRequest(CreateFileAccessTokenDto dto)
    {
        if (dto.AttachmentId <= 0)
        {
            throw ValidationError("附件 ID 无效", "FileToken.InvalidAttachmentId");
        }

        if (dto.MaxAccessCount < 0)
        {
            throw ValidationError("最大访问次数不能小于 0", "FileToken.InvalidMaxAccessCount");
        }

        if (dto.ValidHours <= 0 || dto.ValidHours > MaxValidHours)
        {
            throw ValidationError($"有效期必须在 1 到 {MaxValidHours} 小时之间", "FileToken.InvalidValidity");
        }

        if (dto.AuthorizedUserId is <= 0)
        {
            throw ValidationError("授权用户 ID 无效", "FileToken.InvalidAuthorizedUser");
        }

        if (!string.IsNullOrWhiteSpace(dto.AuthorizedIp) && NormalizeAuthorizedIp(dto.AuthorizedIp) == null)
        {
            throw ValidationError("授权 IP 地址无效", "FileToken.InvalidAuthorizedIp");
        }
    }

    private static string? NormalizeAuthorizedIp(string? authorizedIp)
    {
        if (string.IsNullOrWhiteSpace(authorizedIp))
        {
            return null;
        }

        var trimmedIp = authorizedIp.Trim();
        return trimmedIp.Length <= 50 && IPAddress.TryParse(trimmedIp, out var parsedIp)
            ? parsedIp.ToString()
            : null;
    }

    private static FileAccessTokenSummaryVo MapSummaryVo(FileAccessToken token, DateTime now)
    {
        return new FileAccessTokenSummaryVo
        {
            VoId = token.Id,
            VoTokenPreview = $"token-{token.Id}",
            VoAttachmentId = token.AttachmentId,
            VoCreatedBy = token.CreatedBy,
            VoMaxAccessCount = token.MaxAccessCount,
            VoAccessCount = token.AccessCount,
            VoExpiresAt = token.ExpiresAt,
            VoIsExpired = now >= token.ExpiresAt,
            VoIsRevoked = token.IsRevoked,
            VoCreateTime = token.CreateTime
        };
    }

    private static FileAccessTokenCreatedVo MapCreatedVo(
        FileAccessToken token,
        string rawToken,
        string publicBaseUrl,
        DateTime now)
    {
        var baseUrl = publicBaseUrl.Trim().TrimEnd('/');
        return new FileAccessTokenCreatedVo
        {
            VoId = token.Id,
            VoTokenPreview = $"token-{token.Id}",
            VoAttachmentId = token.AttachmentId,
            VoCreatedBy = token.CreatedBy,
            VoMaxAccessCount = token.MaxAccessCount,
            VoAccessCount = token.AccessCount,
            VoExpiresAt = token.ExpiresAt,
            VoIsExpired = now >= token.ExpiresAt,
            VoIsRevoked = token.IsRevoked,
            VoCreateTime = token.CreateTime,
            VoToken = rawToken,
            VoAccessUrl = $"{baseUrl}/api/v1/Attachment/DownloadByToken?token={Uri.EscapeDataString(rawToken)}"
        };
    }

    private static BusinessException ValidationError(string message, string code)
    {
        return new BusinessException(message, 400, code, "error.file_token.validation_failed");
    }

    private static BusinessException ForbiddenError()
    {
        return new BusinessException("无权管理此文件访问令牌", 403, "FileToken.Forbidden", "error.file_token.forbidden");
    }

    private static BusinessException NotFoundError()
    {
        return new BusinessException("令牌不存在", 404, "FileToken.NotFound", "error.file_token.not_found");
    }
}
