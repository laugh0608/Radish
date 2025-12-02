using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenIddict.Abstractions;
using Radish.Model.OpenIddict;
using SqlSugar;

namespace Radish.Auth.OpenIddict.Stores;

/// <summary>
/// 使用 SqlSugar 实现的 OpenIddict 授权记录存储。
/// 为简化实现，部分「查询 + 组合条件」逻辑采用内存 LINQ 处理，满足开发环境和中小规模生产使用。
/// </summary>
public class RadishAuthorizationStore : IOpenIddictAuthorizationStore<RadishAuthorization>
{
    private readonly SqlSugarScope _db;

    public RadishAuthorizationStore(SqlSugarScope db)
    {
        _db = db;
    }

    #region 帮助方法

    private static bool TryParseId(string? identifier, out long id)
    {
        return long.TryParse(identifier, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
    }

    private static string? ConvertIdToString(long id)
    {
        return id.ToString(CultureInfo.InvariantCulture);
    }

    private static ImmutableArray<string> DeserializeStringArray(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return ImmutableArray<string>.Empty;
        }

        try
        {
            var array = JsonSerializer.Deserialize<string[]>(json);
            return array is { Length: > 0 }
                ? ImmutableArray.Create(array)
                : ImmutableArray<string>.Empty;
        }
        catch
        {
            return ImmutableArray<string>.Empty;
        }
    }

    private static string SerializeStringArray(ImmutableArray<string> values)
    {
        if (values.IsDefaultOrEmpty)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(values.ToArray());
    }

    private static ImmutableDictionary<string, JsonElement> DeserializeProperties(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return ImmutableDictionary<string, JsonElement>.Empty;
        }

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (dict is null || dict.Count == 0)
            {
                return ImmutableDictionary<string, JsonElement>.Empty;
            }

            return dict.ToImmutableDictionary();
        }
        catch
        {
            return ImmutableDictionary<string, JsonElement>.Empty;
        }
    }

    private static string SerializeProperties(ImmutableDictionary<string, JsonElement> properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(properties);
    }

    private static bool HasScopes(string scopesJson, ImmutableArray<string> required)
    {
        if (required.IsDefaultOrEmpty)
        {
            return true;
        }

        var scopes = DeserializeStringArray(scopesJson);
        if (scopes.IsDefaultOrEmpty)
        {
            return false;
        }

        var set = scopes.ToHashSet(StringComparer.Ordinal);
        return required.All(set.Contains);
    }

    #endregion

    #region 计数与查询

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _db.Queryable<RadishAuthorization>().CountAsync();
    }

    public async ValueTask<long> CountAsync<TResult>(Func<IQueryable<RadishAuthorization>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var list = await _db.Queryable<RadishAuthorization>().ToListAsync();
        var result = query(list.AsQueryable());
        return result.LongCount();
    }

    public async ValueTask<RadishAuthorization?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryParseId(identifier, out var id))
        {
            return null;
        }

        return await _db.Queryable<RadishAuthorization>().InSingleAsync(id);
    }

    public async IAsyncEnumerable<RadishAuthorization> FindAsync(
        string? subject, string? client,
        string? status, string? type,
        ImmutableArray<string>? scopes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var authorizations = await _db.Queryable<RadishAuthorization>().ToListAsync();
        List<RadishApplication>? apps = null;

        if (!string.IsNullOrEmpty(client))
        {
            apps = await _db.Queryable<RadishApplication>().ToListAsync();
        }

        var requiredScopes = scopes ?? ImmutableArray<string>.Empty;

        foreach (var authorization in authorizations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrEmpty(subject) && !string.Equals(authorization.Subject, subject, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(status) && !string.Equals(authorization.Status, status, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(type) && !string.Equals(authorization.Type, type, StringComparison.Ordinal))
            {
                continue;
            }

            if (!requiredScopes.IsDefaultOrEmpty && !HasScopes(authorization.Scopes, requiredScopes))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(client))
            {
                if (apps is null)
                {
                    continue;
                }

                var app = apps.FirstOrDefault(x => x.Id == authorization.ApplicationId);
                if (app is null || !string.Equals(app.ClientId, client, StringComparison.Ordinal))
                {
                    continue;
                }
            }

            yield return authorization;
        }
    }

    public async IAsyncEnumerable<RadishAuthorization> FindByApplicationIdAsync(string identifier,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryParseId(identifier, out var appId))
        {
            yield break;
        }

        var list = await _db.Queryable<RadishAuthorization>()
            .Where(x => x.ApplicationId == appId)
            .ToListAsync();

        foreach (var item in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    public async IAsyncEnumerable<RadishAuthorization> FindBySubjectAsync(string subject,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(subject))
        {
            yield break;
        }

        var list = await _db.Queryable<RadishAuthorization>()
            .Where(x => x.Subject == subject)
            .ToListAsync();

        foreach (var item in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<RadishAuthorization>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var list = await _db.Queryable<RadishAuthorization>().ToListAsync();
        var resultQuery = query(list.AsQueryable(), state);
        return resultQuery.FirstOrDefault();
    }

    public async IAsyncEnumerable<RadishAuthorization> ListAsync(int? count, int? offset,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = _db.Queryable<RadishAuthorization>();

        if (offset.HasValue && offset.Value > 0)
        {
            query = query.Skip(offset.Value);
        }

        if (count.HasValue && count.Value > 0)
        {
            query = query.Take(count.Value);
        }

        var list = await query.ToListAsync();
        foreach (var item in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    public async IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<RadishAuthorization>, TState, IQueryable<TResult>> query,
        TState state,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var list = await _db.Queryable<RadishAuthorization>().ToListAsync();
        var resultQuery = query(list.AsQueryable(), state);

        foreach (var item in resultQuery)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    #endregion

    #region 创建、更新、删除

    public async ValueTask CreateAsync(RadishAuthorization authorization, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        await _db.Insertable(authorization).ExecuteCommandAsync();
    }

    public async ValueTask UpdateAsync(RadishAuthorization authorization, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        await _db.Updateable(authorization).ExecuteCommandAsync();
    }

    public async ValueTask DeleteAsync(RadishAuthorization authorization, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        await _db.Deleteable(authorization).ExecuteCommandAsync();
    }

    public ValueTask<RadishAuthorization> InstantiateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<RadishAuthorization>(new RadishAuthorization());
    }

    #endregion

    #region 字段读取

    public ValueTask<string?> GetApplicationIdAsync(RadishAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        return new ValueTask<string?>(ConvertIdToString(authorization.ApplicationId));
    }

    public ValueTask<DateTimeOffset?> GetCreationDateAsync(RadishAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        // RadishAuthorization.CreateTime 是 DateTime，这里视为本地时间
        var dto = new DateTimeOffset(authorization.CreateTime);
        return new ValueTask<DateTimeOffset?>(dto);
    }

    public ValueTask<string?> GetIdAsync(RadishAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        return new ValueTask<string?>(ConvertIdToString(authorization.Id));
    }

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(RadishAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        return new ValueTask<ImmutableDictionary<string, JsonElement>>(DeserializeProperties(authorization.Properties));
    }

    public ValueTask<ImmutableArray<string>> GetScopesAsync(RadishAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        return new ValueTask<ImmutableArray<string>>(DeserializeStringArray(authorization.Scopes));
    }

    public ValueTask<string?> GetStatusAsync(RadishAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        return new ValueTask<string?>(authorization.Status);
    }

    public ValueTask<string?> GetSubjectAsync(RadishAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        return new ValueTask<string?>(authorization.Subject);
    }

    public ValueTask<string?> GetTypeAsync(RadishAuthorization authorization, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        return new ValueTask<string?>(authorization.Type);
    }

    #endregion

    #region 字段写入

    public ValueTask SetApplicationIdAsync(RadishAuthorization authorization, string? identifier, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        if (TryParseId(identifier, out var id))
        {
            authorization.ApplicationId = id;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask SetCreationDateAsync(RadishAuthorization authorization, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        if (date.HasValue)
        {
            authorization.CreateTime = date.Value.LocalDateTime;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask SetPropertiesAsync(RadishAuthorization authorization,
        ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        authorization.Properties = SerializeProperties(properties);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetScopesAsync(RadishAuthorization authorization,
        ImmutableArray<string> scopes, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        authorization.Scopes = SerializeStringArray(scopes);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetStatusAsync(RadishAuthorization authorization, string? status, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        authorization.Status = status ?? string.Empty;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetSubjectAsync(RadishAuthorization authorization, string? subject, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        authorization.Subject = subject ?? string.Empty;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetTypeAsync(RadishAuthorization authorization, string? type, CancellationToken cancellationToken)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        authorization.Type = type ?? string.Empty;
        return ValueTask.CompletedTask;
    }

    #endregion

    #region 清理与撤销

    public async ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 简化实现：删除创建时间早于 threshold 且 Status 非 "Valid" 的授权记录。
        var list = await _db.Queryable<RadishAuthorization>()
            .Where(x => x.CreateTime < threshold.LocalDateTime && x.Status != "Valid")
            .ToListAsync();

        if (list.Count == 0)
        {
            return 0;
        }

        var count = list.Count;
        await _db.Deleteable(list).ExecuteCommandAsync();
        return count;
    }

    public async ValueTask<long> RevokeAsync(string? subject, string? client, string? status, string? type, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = _db.Queryable<RadishAuthorization>();

        if (!string.IsNullOrEmpty(subject))
        {
            query = query.Where(x => x.Subject == subject);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(x => x.Type == type);
        }

        var list = await query.ToListAsync();

        if (!string.IsNullOrEmpty(client))
        {
            var apps = await _db.Queryable<RadishApplication>().ToListAsync();
            list = list
                .Where(a => apps.Any(app => app.Id == a.ApplicationId && app.ClientId == client))
                .ToList();
        }

        if (list.Count == 0)
        {
            return 0;
        }

        foreach (var auth in list)
        {
            auth.Status = "Revoked";
        }

        await _db.Updateable(list).UpdateColumns(x => new { x.Status }).ExecuteCommandAsync();
        return list.Count;
    }

    public async ValueTask<long> RevokeByApplicationIdAsync(string identifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryParseId(identifier, out var appId))
        {
            return 0;
        }

        var list = await _db.Queryable<RadishAuthorization>()
            .Where(x => x.ApplicationId == appId)
            .ToListAsync();

        if (list.Count == 0)
        {
            return 0;
        }

        foreach (var auth in list)
        {
            auth.Status = "Revoked";
        }

        await _db.Updateable(list).UpdateColumns(x => new { x.Status }).ExecuteCommandAsync();
        return list.Count;
    }

    public async ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(subject))
        {
            return 0;
        }

        var list = await _db.Queryable<RadishAuthorization>()
            .Where(x => x.Subject == subject)
            .ToListAsync();

        if (list.Count == 0)
        {
            return 0;
        }

        foreach (var auth in list)
        {
            auth.Status = "Revoked";
        }

        await _db.Updateable(list).UpdateColumns(x => new { x.Status }).ExecuteCommandAsync();
        return list.Count;
    }

    #endregion
}
