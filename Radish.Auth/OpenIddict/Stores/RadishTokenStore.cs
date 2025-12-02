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
/// 使用 SqlSugar 实现的 OpenIddict Token 存储。
/// Prune/Revocation 等操作做了简化实现，主要面向实际授权码/刷新令牌场景。
/// </summary>
public class RadishTokenStore : IOpenIddictTokenStore<RadishToken>
{
    private readonly SqlSugarScope _db;

    public RadishTokenStore(SqlSugarScope db)
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

    #endregion

    #region 计数与查询

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _db.Queryable<RadishToken>().CountAsync();
    }

    public async ValueTask<long> CountAsync<TResult>(Func<IQueryable<RadishToken>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var list = await _db.Queryable<RadishToken>().ToListAsync();
        var result = query(list.AsQueryable());
        return result.LongCount();
    }

    public async IAsyncEnumerable<RadishToken> FindAsync(
        string? subject, string? client,
        string? status, string? type,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tokens = await _db.Queryable<RadishToken>().ToListAsync();
        List<RadishApplication>? apps = null;

        if (!string.IsNullOrEmpty(client))
        {
            apps = await _db.Queryable<RadishApplication>().ToListAsync();
        }

        foreach (var token in tokens)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrEmpty(subject) && !string.Equals(token.Subject, subject, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(status) && !string.Equals(token.Status, status, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(type) && !string.Equals(token.Type, type, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(client))
            {
                if (apps is null)
                {
                    continue;
                }

                var app = apps.FirstOrDefault(x => x.Id == token.ApplicationId);
                if (app is null || !string.Equals(app.ClientId, client, StringComparison.Ordinal))
                {
                    continue;
                }
            }

            yield return token;
        }
    }

    public async IAsyncEnumerable<RadishToken> FindByApplicationIdAsync(string identifier,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryParseId(identifier, out var appId))
        {
            yield break;
        }

        var list = await _db.Queryable<RadishToken>()
            .Where(x => x.ApplicationId == appId)
            .ToListAsync();

        foreach (var token in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return token;
        }
    }

    public async IAsyncEnumerable<RadishToken> FindByAuthorizationIdAsync(string identifier,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryParseId(identifier, out var authId))
        {
            yield break;
        }

        var list = await _db.Queryable<RadishToken>()
            .Where(x => x.AuthorizationId == authId)
            .ToListAsync();

        foreach (var token in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return token;
        }
    }

    public async ValueTask<RadishToken?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryParseId(identifier, out var id))
        {
            return null;
        }

        return await _db.Queryable<RadishToken>().InSingleAsync(id);
    }

    public async ValueTask<RadishToken?> FindByReferenceIdAsync(string identifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(identifier))
        {
            return null;
        }

        return await _db.Queryable<RadishToken>()
            .FirstAsync(x => x.ReferenceId == identifier);
    }

    public async IAsyncEnumerable<RadishToken> FindBySubjectAsync(string subject,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(subject))
        {
            yield break;
        }

        var list = await _db.Queryable<RadishToken>()
            .Where(x => x.Subject == subject)
            .ToListAsync();

        foreach (var token in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return token;
        }
    }

    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<RadishToken>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var list = await _db.Queryable<RadishToken>().ToListAsync();
        var resultQuery = query(list.AsQueryable(), state);
        return resultQuery.FirstOrDefault();
    }

    public ValueTask<string?> GetApplicationIdAsync(RadishToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<string?>(ConvertIdToString(token.ApplicationId));
    }

    public ValueTask<string?> GetAuthorizationIdAsync(RadishToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<string?>(token.AuthorizationId.HasValue
            ? ConvertIdToString(token.AuthorizationId.Value)
            : null);
    }

    public ValueTask<DateTimeOffset?> GetCreationDateAsync(RadishToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        var dto = new DateTimeOffset(token.CreateTime);
        return new ValueTask<DateTimeOffset?>(dto);
    }

    public ValueTask<DateTimeOffset?> GetExpirationDateAsync(RadishToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (!token.ExpirationTime.HasValue)
        {
            return new ValueTask<DateTimeOffset?>((DateTimeOffset?)null);
        }

        return new ValueTask<DateTimeOffset?>(new DateTimeOffset(token.ExpirationTime.Value));
    }

    public ValueTask<string?> GetIdAsync(RadishToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<string?>(ConvertIdToString(token.Id));
    }

    public ValueTask<string?> GetPayloadAsync(RadishToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<string?>(token.Payload);
    }

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(RadishToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<ImmutableDictionary<string, JsonElement>>(DeserializeProperties(token.Properties));
    }

    public ValueTask<DateTimeOffset?> GetRedemptionDateAsync(RadishToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (!token.RedemptionTime.HasValue)
        {
            return new ValueTask<DateTimeOffset?>((DateTimeOffset?)null);
        }

        return new ValueTask<DateTimeOffset?>(new DateTimeOffset(token.RedemptionTime.Value));
    }

    public ValueTask<string?> GetReferenceIdAsync(RadishToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<string?>(token.ReferenceId);
    }

    public ValueTask<string?> GetStatusAsync(RadishToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<string?>(token.Status);
    }

    public ValueTask<string?> GetSubjectAsync(RadishToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<string?>(token.Subject);
    }

    public ValueTask<string?> GetTypeAsync(RadishToken token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return new ValueTask<string?>(token.Type);
    }

    public ValueTask<RadishToken> InstantiateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<RadishToken>(new RadishToken());
    }

    public async IAsyncEnumerable<RadishToken> ListAsync(int? count, int? offset,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = _db.Queryable<RadishToken>();

        if (offset.HasValue && offset.Value > 0)
        {
            query = query.Skip(offset.Value);
        }

        if (count.HasValue && count.Value > 0)
        {
            query = query.Take(count.Value);
        }

        var list = await query.ToListAsync();
        foreach (var token in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return token;
        }
    }

    public async IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<RadishToken>, TState, IQueryable<TResult>> query,
        TState state,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var list = await _db.Queryable<RadishToken>().ToListAsync();
        var resultQuery = query(list.AsQueryable(), state);

        foreach (var item in resultQuery)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    #endregion

    #region 创建、更新、删除

    public async ValueTask CreateAsync(RadishToken token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        await _db.Insertable(token).ExecuteCommandAsync();
    }

    public async ValueTask UpdateAsync(RadishToken token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        await _db.Updateable(token).ExecuteCommandAsync();
    }

    public async ValueTask DeleteAsync(RadishToken token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        await _db.Deleteable(token).ExecuteCommandAsync();
    }

    #endregion

    #region 字段写入

    public ValueTask SetApplicationIdAsync(RadishToken token, string? identifier, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (TryParseId(identifier, out var id))
        {
            token.ApplicationId = id;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask SetAuthorizationIdAsync(RadishToken token, string? identifier, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (TryParseId(identifier, out var id))
        {
            token.AuthorizationId = id;
        }
        else
        {
            token.AuthorizationId = null;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask SetCreationDateAsync(RadishToken token, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        if (date.HasValue)
        {
            token.CreateTime = date.Value.LocalDateTime;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask SetExpirationDateAsync(RadishToken token, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.ExpirationTime = date?.LocalDateTime;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetPayloadAsync(RadishToken token, string? payload, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.Payload = payload ?? string.Empty;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetPropertiesAsync(RadishToken token,
        ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.Properties = SerializeProperties(properties);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetRedemptionDateAsync(RadishToken token, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.RedemptionTime = date?.LocalDateTime;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetReferenceIdAsync(RadishToken token, string? identifier, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.ReferenceId = identifier;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetStatusAsync(RadishToken token, string? status, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.Status = status ?? string.Empty;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetSubjectAsync(RadishToken token, string? subject, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.Subject = subject ?? string.Empty;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetTypeAsync(RadishToken token, string? type, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        token.Type = type ?? string.Empty;
        return ValueTask.CompletedTask;
    }

    #endregion

    #region 清理与撤销

    public async ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 简化实现：删除 ExpirationTime 早于 threshold 且 Status != "Valid" 的 Token
        var list = await _db.Queryable<RadishToken>()
            .Where(x => x.ExpirationTime != null && x.ExpirationTime < threshold.LocalDateTime && x.Status != "Valid")
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

        var query = _db.Queryable<RadishToken>();

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
                .Where(t => apps.Any(app => app.Id == t.ApplicationId && app.ClientId == client))
                .ToList();
        }

        if (list.Count == 0)
        {
            return 0;
        }

        foreach (var token in list)
        {
            token.Status = "Revoked";
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

        var list = await _db.Queryable<RadishToken>()
            .Where(x => x.ApplicationId == appId)
            .ToListAsync();

        if (list.Count == 0)
        {
            return 0;
        }

        foreach (var token in list)
        {
            token.Status = "Revoked";
        }

        await _db.Updateable(list).UpdateColumns(x => new { x.Status }).ExecuteCommandAsync();
        return list.Count;
    }

    public async ValueTask<long> RevokeByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryParseId(identifier, out var authId))
        {
            return 0;
        }

        var list = await _db.Queryable<RadishToken>()
            .Where(x => x.AuthorizationId == authId)
            .ToListAsync();

        if (list.Count == 0)
        {
            return 0;
        }

        foreach (var token in list)
        {
            token.Status = "Revoked";
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

        var list = await _db.Queryable<RadishToken>()
            .Where(x => x.Subject == subject)
            .ToListAsync();

        if (list.Count == 0)
        {
            return 0;
        }

        foreach (var token in list)
        {
            token.Status = "Revoked";
        }

        await _db.Updateable(list).UpdateColumns(x => new { x.Status }).ExecuteCommandAsync();
        return list.Count;
    }

    #endregion
}
