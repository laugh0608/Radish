using Radish.IRepository.Base;
using Radish.Model.Models;

namespace Radish.IRepository;

public interface IFileAccessTokenRepository : IBaseRepository<FileAccessToken>
{
    Task<FileAccessToken?> TryConsumeAsync(
        string tokenHash,
        long? userId,
        string? normalizedIp,
        DateTime now);

    Task<bool> TryRevokeByIdAsync(long tokenId, DateTime now);

    Task<bool> TryRevokeByHashAsync(string tokenHash, DateTime now);

    Task<FileAccessToken?> GetByHashAsync(string tokenHash);
}
