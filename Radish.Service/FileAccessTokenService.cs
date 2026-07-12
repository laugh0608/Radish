using System.Net;
using Radish.Common.Exceptions;
using Radish.Common.Security;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Serilog;

namespace Radish.Service;

/// <summary>
/// 文件访问令牌服务实现。原始 token 只在创建响应出现，持久化层仅保存 hash。
/// </summary>
public class FileAccessTokenService : IFileAccessTokenService
{
    private const int MaxValidHours = 168;
    private readonly IFileAccessTokenRepository _tokenRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly TimeProvider _timeProvider;

    public FileAccessTokenService(
        IFileAccessTokenRepository tokenRepository,
        IBaseRepository<Attachment> attachmentRepository,
        TimeProvider timeProvider)
    {
        _tokenRepository = tokenRepository;
        _attachmentRepository = attachmentRepository;
        _timeProvider = timeProvider;
    }

    public async Task<FileAccessTokenCreatedVo> CreateTokenAsync(
        CreateFileAccessTokenDto dto,
        long userId,
        bool canManageAll,
        string publicBaseUrl)
    {
        ValidateCreateTokenRequest(dto);
        await EnsureCanManageAttachmentAsync(dto.AttachmentId, userId, canManageAll);

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
        Log.Information(
            "[FileAccessToken] 创建令牌记录 {TokenId}, 附件: {AttachmentId}, 有效期: {Hours}小时",
            tokenEntity.Id,
            dto.AttachmentId,
            dto.ValidHours);

        return MapCreatedVo(tokenEntity, rawToken, publicBaseUrl, now);
    }

    public async Task<long?> ValidateAndUseTokenAsync(string rawToken, long? userId, string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            Log.Warning("[FileAccessToken] 令牌为空");
            return null;
        }

        var normalizedIp = NormalizeAuthorizedIp(ipAddress);
        var tokenHash = FileAccessTokenHashing.HashToken(rawToken);
        var now = GetUtcNow();
        var consumedToken = await _tokenRepository.TryConsumeAsync(tokenHash, userId, normalizedIp, now);
        if (consumedToken == null)
        {
            Log.Warning("[FileAccessToken] 令牌消费失败: {TokenHashPreview}", MaskHash(tokenHash));
            return null;
        }

        Log.Information(
            "[FileAccessToken] 令牌记录 {TokenId} 消费成功, 附件: {AttachmentId}, 访问次数: {Count}",
            consumedToken.Id,
            consumedToken.AttachmentId,
            consumedToken.AccessCount);
        return consumedToken.AttachmentId;
    }

    public async Task RevokeTokenAsync(long tokenId, long userId, bool canManageAll)
    {
        if (tokenId <= 0)
        {
            throw ValidationError("令牌记录 ID 无效", "FileToken.InvalidId");
        }

        var tokenEntity = await _tokenRepository.QueryByIdAsync(tokenId)
            ?? throw NotFoundError();
        await EnsureCanManageTokenAsync(tokenEntity, userId, canManageAll);

        var revoked = await _tokenRepository.TryRevokeByIdAsync(tokenId, GetUtcNow());
        if (!revoked)
        {
            throw new BusinessException("令牌已撤销", 409, "FileToken.AlreadyRevoked", "error.file_token.already_revoked");
        }

        Log.Information("[FileAccessToken] 撤销令牌记录 {TokenId}", tokenId);
    }

    public async Task RevokeTokenAsync(string rawToken, long userId, bool canManageAll)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw ValidationError("令牌不能为空", "FileToken.Required");
        }

        var tokenHash = FileAccessTokenHashing.HashToken(rawToken);
        var tokenEntity = await _tokenRepository.GetByHashAsync(tokenHash)
            ?? throw NotFoundError();
        await EnsureCanManageTokenAsync(tokenEntity, userId, canManageAll);

        var revoked = await _tokenRepository.TryRevokeByHashAsync(tokenHash, GetUtcNow());
        if (!revoked)
        {
            throw new BusinessException("令牌已撤销", 409, "FileToken.AlreadyRevoked", "error.file_token.already_revoked");
        }

        Log.Information("[FileAccessToken] 通过兼容入口撤销令牌记录 {TokenId}", tokenEntity.Id);
    }

    public async Task<FileAccessTokenSummaryVo?> GetTokenInfoAsync(
        string rawToken,
        long userId,
        bool canManageAll)
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

        await EnsureCanManageTokenAsync(tokenEntity, userId, canManageAll);
        return MapSummaryVo(tokenEntity, GetUtcNow());
    }

    public async Task<List<FileAccessTokenSummaryVo>> GetAttachmentTokensAsync(
        long attachmentId,
        long userId,
        bool canManageAll)
    {
        await EnsureCanManageAttachmentAsync(attachmentId, userId, canManageAll);
        var now = GetUtcNow();
        var tokens = await _tokenRepository.QueryAsync(token =>
            token.AttachmentId == attachmentId &&
            !token.IsRevoked &&
            token.ExpiresAt > now);
        return tokens.Select(token => MapSummaryVo(token, now)).ToList();
    }

    public async Task CleanupExpiredTokensAsync()
    {
        var now = GetUtcNow();
        var expiredTokens = await _tokenRepository.QueryAsync(token =>
            token.ExpiresAt <= now &&
            !token.IsRevoked);

        foreach (var token in expiredTokens)
        {
            await _tokenRepository.TryRevokeByIdAsync(token.Id, now);
        }

        if (expiredTokens.Count > 0)
        {
            Log.Information("[FileAccessToken] 清理过期令牌: {Count} 个", expiredTokens.Count);
        }
    }

    private async Task EnsureCanManageTokenAsync(FileAccessToken token, long userId, bool canManageAll)
    {
        if (canManageAll || token.CreatedBy == userId)
        {
            return;
        }

        var attachment = await _attachmentRepository.QueryByIdAsync(token.AttachmentId);
        if (attachment?.UploaderId != userId)
        {
            throw ForbiddenError();
        }
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }

    private async Task EnsureCanManageAttachmentAsync(long attachmentId, long userId, bool canManageAll)
    {
        var attachment = await _attachmentRepository.QueryByIdAsync(attachmentId);
        if (attachment == null)
        {
            throw new BusinessException("附件不存在", 404, "Attachment.NotFound", "error.attachment.not_found");
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

    private static string MaskHash(string tokenHash)
    {
        return tokenHash.Length <= 10
            ? "<invalid-hash>"
            : $"{tokenHash[..6]}***{tokenHash[^4..]}";
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
