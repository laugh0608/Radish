using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;
using Radish.Auth.OpenIddict;

namespace Radish.Api.Services;

/// <summary>
/// OpenIddict 客户端列表的数据库权威查询结果。
/// </summary>
public sealed record ClientApplicationQueryPage(
    int Total,
    IReadOnlyList<string> ApplicationIds);

/// <summary>
/// 为客户端管理接口提供稳定、可分页的只读查询边界。
/// </summary>
public interface IClientApplicationQueryService
{
    Task<ClientApplicationQueryPage> QueryAsync(
        int page,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 基于 OpenIddict EF Core 存储执行客户端权威查询。
/// </summary>
public sealed class ClientApplicationQueryService : IClientApplicationQueryService
{
    private const string DeletedMetadataMarker = "\"IsDeleted\":\"true\"";
    private readonly AuthOpenIddictDbContext _dbContext;

    public ClientApplicationQueryService(AuthOpenIddictDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ClientApplicationQueryPage> QueryAsync(
        int page,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<OpenIddictEntityFrameworkCoreApplication>()
            .AsNoTracking()
            .Where(application =>
                application.Properties == null ||
                !application.Properties.Contains(DeletedMetadataMarker));

        var normalizedKeyword = keyword?.Trim().ToLower();
        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            query = query.Where(application =>
                (application.ClientId != null && application.ClientId.ToLower().Contains(normalizedKeyword)) ||
                (application.DisplayName != null && application.DisplayName.ToLower().Contains(normalizedKeyword)));
        }

        var total = await query.CountAsync(cancellationToken);
        var applicationIds = await query
            .OrderBy(application => application.ClientId)
            .ThenBy(application => application.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(application => application.Id!)
            .ToListAsync(cancellationToken);

        return new ClientApplicationQueryPage(total, applicationIds);
    }
}
