using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Serilog;
using System.Net;

namespace Radish.Service;

/// <summary>
/// 文件访问令牌服务实现
/// </summary>
public class FileAccessTokenService : IFileAccessTokenService
{
    private const int MaxValidHours = 168;
    private readonly IBaseRepository<FileAccessToken> _tokenRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;

    public FileAccessTokenService(
        IBaseRepository<FileAccessToken> tokenRepository,
        IBaseRepository<Attachment> attachmentRepository)
    {
        _tokenRepository = tokenRepository;
        _attachmentRepository = attachmentRepository;
    }

    /// <summary>
    /// 创建文件访问令牌
    /// </summary>
    public async Task<FileAccessTokenVo> CreateTokenAsync(CreateFileAccessTokenDto dto, long userId, string baseUrl)
    {
        ValidateCreateTokenRequest(dto);

        // 验证附件是否存在且用户有权限
        var attachment = await _attachmentRepository.QueryByIdAsync(dto.AttachmentId);
        if (attachment == null)
        {
            throw new Exception($"附件不存在: {dto.AttachmentId}");
        }

        // 检查用户是否有权限创建令牌（附件所有者或管理员）
        if (attachment.UploaderId != userId)
        {
            // TODO: 检查是否是管理员
            throw new Exception("无权为此附件创建访问令牌");
        }

        // 生成令牌
        var token = Guid.NewGuid().ToString("N");

        // 创建令牌记录
        var authorizedIp = NormalizeAuthorizedIp(dto.AuthorizedIp);
        var tokenEntity = new FileAccessToken
        {
            Token = token,
            AttachmentId = dto.AttachmentId,
            AuthorizedUserId = dto.AuthorizedUserId,
            AuthorizedIp = authorizedIp,
            MaxAccessCount = dto.MaxAccessCount,
            AccessCount = 0,
            ExpiresAt = DateTime.Now.AddHours(dto.ValidHours),
            CreatedBy = userId,
            IsRevoked = false,
            CreateTime = DateTime.Now,
            ModifyTime = DateTime.Now
        };

        await _tokenRepository.AddAsync(tokenEntity);

        Log.Information("[FileAccessToken] 创建令牌: {TokenPreview}, 附件: {AttachmentId}, 有效期: {Hours}小时",
            MaskToken(token), dto.AttachmentId, dto.ValidHours);

        return MapToVo(tokenEntity, baseUrl);
    }

    /// <summary>
    /// 验证并使用令牌
    /// </summary>
    public async Task<long?> ValidateAndUseTokenAsync(string token, long? userId, string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            Log.Warning("[FileAccessToken] 令牌为空");
            return null;
        }

        var tokenPreview = MaskToken(token);
        var tokenEntity = await _tokenRepository.QueryFirstAsync(t => t.Token == token);
        if (tokenEntity == null)
        {
            Log.Warning("[FileAccessToken] 令牌不存在: {TokenPreview}", tokenPreview);
            return null;
        }

        // 检查是否已撤销
        if (tokenEntity.IsRevoked)
        {
            Log.Warning("[FileAccessToken] 令牌已撤销: {TokenPreview}", tokenPreview);
            return null;
        }

        // 检查是否过期
        if (DateTime.Now > tokenEntity.ExpiresAt)
        {
            Log.Warning("[FileAccessToken] 令牌已过期: {TokenPreview}", tokenPreview);
            return null;
        }

        // 检查访问次数
        if (tokenEntity.MaxAccessCount > 0 && tokenEntity.AccessCount >= tokenEntity.MaxAccessCount)
        {
            Log.Warning("[FileAccessToken] 令牌访问次数已达上限: {TokenPreview}, {Count}/{Max}",
                tokenPreview, tokenEntity.AccessCount, tokenEntity.MaxAccessCount);
            return null;
        }

        // 检查授权用户
        if (tokenEntity.AuthorizedUserId.HasValue && tokenEntity.AuthorizedUserId != userId)
        {
            Log.Warning("[FileAccessToken] 令牌用户不匹配: {TokenPreview}, 授权用户: {AuthUserId}, 访问用户: {UserId}",
                tokenPreview, tokenEntity.AuthorizedUserId, userId);
            return null;
        }

        // 检查授权IP（如果设置了）
        if (!string.IsNullOrEmpty(tokenEntity.AuthorizedIp) && tokenEntity.AuthorizedIp != ipAddress)
        {
            Log.Warning("[FileAccessToken] 令牌IP不匹配: {TokenPreview}, 授权IP: {AuthIp}, 访问IP: {Ip}",
                tokenPreview, tokenEntity.AuthorizedIp, ipAddress);
            return null;
        }

        // 更新访问记录
        tokenEntity.AccessCount++;
        tokenEntity.LastAccessedAt = DateTime.Now;
        tokenEntity.ModifyTime = DateTime.Now;
        await _tokenRepository.UpdateAsync(tokenEntity);

        Log.Information("[FileAccessToken] 令牌验证成功: {TokenPreview}, 附件: {AttachmentId}, 访问次数: {Count}",
            tokenPreview, tokenEntity.AttachmentId, tokenEntity.AccessCount);

        return tokenEntity.AttachmentId;
    }

    /// <summary>
    /// 撤销令牌
    /// </summary>
    public async Task RevokeTokenAsync(string token, long userId)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("令牌不能为空");
        }

        var tokenEntity = await _tokenRepository.QueryFirstAsync(t => t.Token == token);
        if (tokenEntity == null)
        {
            throw new Exception("令牌不存在");
        }

        // 检查权限（只有创建者可以撤销）
        if (tokenEntity.CreatedBy != userId)
        {
            // TODO: 检查是否是管理员
            throw new Exception("无权撤销此令牌");
        }

        tokenEntity.IsRevoked = true;
        tokenEntity.RevokedAt = DateTime.Now;
        tokenEntity.ModifyTime = DateTime.Now;
        await _tokenRepository.UpdateAsync(tokenEntity);

        Log.Information("[FileAccessToken] 撤销令牌: {TokenPreview}", MaskToken(token));
    }

    /// <summary>
    /// 获取令牌信息
    /// </summary>
    public async Task<FileAccessTokenVo?> GetTokenInfoAsync(string token, long userId)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenEntity = await _tokenRepository.QueryFirstAsync(t => t.Token == token);
        if (tokenEntity == null)
        {
            return null;
        }

        // 检查权限（只有创建者可以查看）
        if (tokenEntity.CreatedBy != userId)
        {
            // TODO: 检查是否是管理员
            throw new Exception("无权查看此令牌");
        }

        return MapToVo(tokenEntity, string.Empty);
    }

    /// <summary>
    /// 获取附件的所有有效令牌
    /// </summary>
    public async Task<List<FileAccessTokenVo>> GetAttachmentTokensAsync(long attachmentId, long userId)
    {
        // 验证附件是否存在且用户有权限
        var attachment = await _attachmentRepository.QueryByIdAsync(attachmentId);
        if (attachment == null)
        {
            throw new Exception($"附件不存在: {attachmentId}");
        }

        if (attachment.UploaderId != userId)
        {
            // TODO: 检查是否是管理员
            throw new Exception("无权查看此附件的令牌");
        }

        // 获取所有令牌
        var tokens = await _tokenRepository.QueryAsync(t =>
            t.AttachmentId == attachmentId &&
            !t.IsRevoked &&
            t.ExpiresAt > DateTime.Now);

        return tokens.Select(t => MapToVo(t, string.Empty)).ToList();
    }

    /// <summary>
    /// 清理过期令牌
    /// </summary>
    public async Task CleanupExpiredTokensAsync()
    {
        var expiredTokens = await _tokenRepository.QueryAsync(t =>
            t.ExpiresAt < DateTime.Now &&
            !t.IsRevoked);

        foreach (var token in expiredTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.Now;
            token.ModifyTime = DateTime.Now;
            await _tokenRepository.UpdateAsync(token);
        }

        if (expiredTokens.Any())
        {
            Log.Information("[FileAccessToken] 清理过期令牌: {Count} 个", expiredTokens.Count());
        }
    }

    #region 私有方法

    private static void ValidateCreateTokenRequest(CreateFileAccessTokenDto dto)
    {
        if (dto.AttachmentId <= 0)
        {
            throw new Exception("附件 ID 无效");
        }

        if (dto.MaxAccessCount < 0)
        {
            throw new Exception("最大访问次数不能小于 0");
        }

        if (dto.ValidHours <= 0 || dto.ValidHours > MaxValidHours)
        {
            throw new Exception($"有效期必须在 1 到 {MaxValidHours} 小时之间");
        }

        if (dto.AuthorizedUserId is <= 0)
        {
            throw new Exception("授权用户 ID 无效");
        }

        if (!string.IsNullOrWhiteSpace(dto.AuthorizedIp) && NormalizeAuthorizedIp(dto.AuthorizedIp) == null)
        {
            throw new Exception("授权 IP 地址无效");
        }
    }

    private static string? NormalizeAuthorizedIp(string? authorizedIp)
    {
        if (string.IsNullOrWhiteSpace(authorizedIp))
        {
            return null;
        }

        var trimmedIp = authorizedIp.Trim();
        if (trimmedIp.Length > 50 || !IPAddress.TryParse(trimmedIp, out var parsedIp))
        {
            return null;
        }

        return parsedIp.ToString();
    }

    private static string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return "<empty>";
        }

        return token.Length <= 10
            ? $"{token[..Math.Min(3, token.Length)]}***"
            : $"{token[..6]}***{token[^4..]}";
    }

    /// <summary>
    /// 映射到 ViewModel
    /// </summary>
    private FileAccessTokenVo MapToVo(FileAccessToken token, string baseUrl)
    {
        var accessUrl = string.IsNullOrEmpty(baseUrl)
            ? string.Empty
            : $"{baseUrl}/api/v1/Attachment/DownloadByToken?token={token.Token}";

        return new FileAccessTokenVo
        {
            VoToken = token.Token,
            VoAttachmentId = token.AttachmentId,
            VoAccessUrl = accessUrl,
            VoMaxAccessCount = token.MaxAccessCount,
            VoAccessCount = token.AccessCount,
            VoExpiresAt = token.ExpiresAt,
            VoIsRevoked = token.IsRevoked,
            VoCreateTime = token.CreateTime
        };
    }

    #endregion
}
