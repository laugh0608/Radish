using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Radish.Model.OpenIddict;
using SqlSugar;

namespace Radish.Auth.OpenIddict.Stores;

/// <summary>
/// 使用 SqlSugar 实现的 OpenIddict 应用存储（客户端）。
/// 目前实现的是「够用优先」版本：
/// - 必要字段（ClientId/Secret/Type/ConsentType/RedirectUris/...）完整映射到 RadishApplication
/// - Properties 使用 JSON 存储 <string, JsonElement>
/// - Settings/DisplayNames/JsonWebKeySet 暂时只做最小实现，不影响常规授权码流程
/// </summary>
public class RadishApplicationStore : IOpenIddictApplicationStore<RadishApplication>
{
    private readonly SqlSugarScope _db;

    public RadishApplicationStore(SqlSugarScope db)
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

    private static ImmutableDictionary<string, string> DeserializeSettings(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return ImmutableDictionary<string, string>.Empty;
        }

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict is null || dict.Count == 0)
            {
                return ImmutableDictionary<string, string>.Empty;
            }

            return dict.ToImmutableDictionary();
        }
        catch
        {
            return ImmutableDictionary<string, string>.Empty;
        }
    }

    private static string SerializeSettings(ImmutableDictionary<string, string> settings)
    {
        if (settings is null || settings.Count == 0)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(settings);
    }

    #endregion

    #region 计数与查询

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _db.Queryable<RadishApplication>().CountAsync();
    }

    public async ValueTask<long> CountAsync<TResult>(Func<IQueryable<RadishApplication>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var list = await _db.Queryable<RadishApplication>().ToListAsync();
        var result = query(list.AsQueryable());
        return result.LongCount();
    }

    public async ValueTask<RadishApplication?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryParseId(identifier, out var id))
        {
            return null;
        }

        return await _db.Queryable<RadishApplication>().InSingleAsync(id);
    }

    public async ValueTask<RadishApplication?> FindByClientIdAsync(string identifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(identifier))
        {
            return null;
        }

        return await _db.Queryable<RadishApplication>()
            .FirstAsync(x => x.ClientId == identifier);
    }

    public async IAsyncEnumerable<RadishApplication> FindByPostLogoutRedirectUriAsync(string uri,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(uri))
        {
            yield break;
        }

        var apps = await _db.Queryable<RadishApplication>().ToListAsync();
        foreach (var app in apps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var uris = DeserializeStringArray(app.PostLogoutRedirectUris);
            if (!uris.IsDefaultOrEmpty && uris.Contains(uri, StringComparer.Ordinal))
            {
                yield return app;
            }
        }
    }

    public async IAsyncEnumerable<RadishApplication> FindByRedirectUriAsync(string uri,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(uri))
        {
            yield break;
        }

        var apps = await _db.Queryable<RadishApplication>().ToListAsync();
        foreach (var app in apps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var uris = DeserializeStringArray(app.RedirectUris);
            if (!uris.IsDefaultOrEmpty && uris.Contains(uri, StringComparer.Ordinal))
            {
                yield return app;
            }
        }
    }

    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<RadishApplication>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var list = await _db.Queryable<RadishApplication>().ToListAsync();
        var resultQuery = query(list.AsQueryable(), state);
        return resultQuery.FirstOrDefault();
    }

    public async IAsyncEnumerable<RadishApplication> ListAsync(int? count, int? offset,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = _db.Queryable<RadishApplication>();

        if (offset.HasValue && offset.Value > 0)
        {
            query = query.Skip(offset.Value);
        }

        if (count.HasValue && count.Value > 0)
        {
            query = query.Take(count.Value);
        }

        var apps = await query.ToListAsync();
        foreach (var app in apps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return app;
        }
    }

    public async IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<RadishApplication>, TState, IQueryable<TResult>> query,
        TState state,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var list = await _db.Queryable<RadishApplication>().ToListAsync();
        var resultQuery = query(list.AsQueryable(), state);

        foreach (var item in resultQuery)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    #endregion

    #region 创建、更新、删除

    public async ValueTask CreateAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        await _db.Insertable(application).ExecuteCommandAsync();
    }

    public async ValueTask UpdateAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        await _db.Updateable(application).ExecuteCommandAsync();
    }

    public async ValueTask DeleteAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        await _db.Deleteable(application).ExecuteCommandAsync();
    }

    public ValueTask<RadishApplication> InstantiateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<RadishApplication>(new RadishApplication());
    }

    #endregion

    #region 字段读取

    public ValueTask<string?> GetApplicationTypeAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new ValueTask<string?>(application.Type);
    }

    public ValueTask<string?> GetClientIdAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new ValueTask<string?>(application.ClientId);
    }

    public ValueTask<string?> GetClientSecretAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new ValueTask<string?>(application.ClientSecret);
    }

    public ValueTask<string?> GetClientTypeAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new ValueTask<string?>(application.Type);
    }

    public ValueTask<string?> GetConsentTypeAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new ValueTask<string?>(application.ConsentType);
    }

    public ValueTask<string?> GetDisplayNameAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new ValueTask<string?>(application.DisplayName);
    }

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        // 当前版本暂不区分多语言显示名称，直接返回空字典。
        return new ValueTask<ImmutableDictionary<CultureInfo, string>>(ImmutableDictionary<CultureInfo, string>.Empty);
    }

    public ValueTask<string?> GetIdAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new ValueTask<string?>(ConvertIdToString(application.Id));
    }

    public ValueTask<JsonWebKeySet?> GetJsonWebKeySetAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        // 当前不为单个客户端存储 JWKS，直接返回 null。
        return new ValueTask<JsonWebKeySet?>((JsonWebKeySet?)null);
    }

    public ValueTask<ImmutableArray<string>> GetPermissionsAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new ValueTask<ImmutableArray<string>>(DeserializeStringArray(application.Permissions));
    }

    public ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new ValueTask<ImmutableArray<string>>(DeserializeStringArray(application.PostLogoutRedirectUris));
    }

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new ValueTask<ImmutableDictionary<string, JsonElement>>(DeserializeProperties(application.Properties));
    }

    public ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new ValueTask<ImmutableArray<string>>(DeserializeStringArray(application.RedirectUris));
    }

    public ValueTask<ImmutableArray<string>> GetRequirementsAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        return new ValueTask<ImmutableArray<string>>(DeserializeStringArray(application.Requirements));
    }

    public ValueTask<ImmutableDictionary<string, string>> GetSettingsAsync(RadishApplication application, CancellationToken cancellationToken)
    {
        // 当前未将 Settings 单独持久化，返回空字典即可。
        return new ValueTask<ImmutableDictionary<string, string>>(ImmutableDictionary<string, string>.Empty);
    }

    #endregion

    #region 字段写入（只修改实体，不直接保存到数据库）

    public ValueTask SetApplicationTypeAsync(RadishApplication application, string? type, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.Type = type ?? string.Empty;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetClientIdAsync(RadishApplication application, string? identifier, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.ClientId = identifier ?? string.Empty;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetClientSecretAsync(RadishApplication application, string? secret, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.ClientSecret = string.IsNullOrEmpty(secret) ? null : secret;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetClientTypeAsync(RadishApplication application, string? type, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.Type = type ?? string.Empty;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetConsentTypeAsync(RadishApplication application, string? type, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.ConsentType = type ?? string.Empty;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetDisplayNameAsync(RadishApplication application, string? name, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.DisplayName = name ?? string.Empty;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetDisplayNamesAsync(RadishApplication application,
        ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken)
    {
        // 当前不单独持久化多语言显示名，直接忽略即可。
        return ValueTask.CompletedTask;
    }

    public ValueTask SetJsonWebKeySetAsync(RadishApplication application, JsonWebKeySet? set, CancellationToken cancellationToken)
    {
        // 当前不为单个客户端持久化 JWKS，忽略即可。
        return ValueTask.CompletedTask;
    }

    public ValueTask SetPermissionsAsync(RadishApplication application,
        ImmutableArray<string> permissions, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.Permissions = SerializeStringArray(permissions);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetPostLogoutRedirectUrisAsync(RadishApplication application,
        ImmutableArray<string> uris, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.PostLogoutRedirectUris = SerializeStringArray(uris);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetPropertiesAsync(RadishApplication application,
        ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.Properties = SerializeProperties(properties);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetRedirectUrisAsync(RadishApplication application,
        ImmutableArray<string> uris, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.RedirectUris = SerializeStringArray(uris);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetRequirementsAsync(RadishApplication application,
        ImmutableArray<string> requirements, CancellationToken cancellationToken)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        application.Requirements = SerializeStringArray(requirements);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetSettingsAsync(RadishApplication application,
        ImmutableDictionary<string, string> settings, CancellationToken cancellationToken)
    {
        // 当前未单独持久化 Settings，忽略即可。
        _ = application; // 避免未使用参数警告
        _ = settings;
        return ValueTask.CompletedTask;
    }

    #endregion
}
