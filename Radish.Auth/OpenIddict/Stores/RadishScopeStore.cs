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
/// 使用 SqlSugar 实现的 OpenIddict Scope 存储。
/// 多语言显示名/描述目前不单独持久化，主打一个简单可用。
/// </summary>
public class RadishScopeStore : IOpenIddictScopeStore<RadishScope>
{
    private readonly SqlSugarScope _db;

    public RadishScopeStore(SqlSugarScope db)
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

    #endregion

    #region 计数与查询

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _db.Queryable<RadishScope>().CountAsync();
    }

    public async ValueTask<long> CountAsync<TResult>(Func<IQueryable<RadishScope>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var list = await _db.Queryable<RadishScope>().ToListAsync();
        var result = query(list.AsQueryable());
        return result.LongCount();
    }

    public async ValueTask<RadishScope?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryParseId(identifier, out var id))
        {
            return null;
        }

        return await _db.Queryable<RadishScope>().InSingleAsync(id);
    }

    public async ValueTask<RadishScope?> FindByNameAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return await _db.Queryable<RadishScope>().FirstAsync(x => x.Name == name);
    }

    public async IAsyncEnumerable<RadishScope> FindByNamesAsync(ImmutableArray<string> names,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (names.IsDefaultOrEmpty)
        {
            yield break;
        }

        var nameSet = names.ToHashSet(StringComparer.Ordinal);
        var list = await _db.Queryable<RadishScope>().ToListAsync();
        foreach (var scope in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nameSet.Contains(scope.Name))
            {
                yield return scope;
            }
        }
    }

    public async IAsyncEnumerable<RadishScope> FindByResourceAsync(string resource,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(resource))
        {
            yield break;
        }

        var list = await _db.Queryable<RadishScope>().ToListAsync();
        foreach (var scope in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resources = DeserializeStringArray(scope.Resources);
            if (!resources.IsDefaultOrEmpty && resources.Contains(resource, StringComparer.Ordinal))
            {
                yield return scope;
            }
        }
    }

    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<RadishScope>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var list = await _db.Queryable<RadishScope>().ToListAsync();
        var resultQuery = query(list.AsQueryable(), state);
        return resultQuery.FirstOrDefault();
    }

    public async IAsyncEnumerable<RadishScope> ListAsync(int? count, int? offset,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = _db.Queryable<RadishScope>();

        if (offset.HasValue && offset.Value > 0)
        {
            query = query.Skip(offset.Value);
        }

        if (count.HasValue && count.Value > 0)
        {
            query = query.Take(count.Value);
        }

        var list = await query.ToListAsync();
        foreach (var scope in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return scope;
        }
    }

    public async IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<RadishScope>, TState, IQueryable<TResult>> query,
        TState state,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var list = await _db.Queryable<RadishScope>().ToListAsync();
        var resultQuery = query(list.AsQueryable(), state);

        foreach (var item in resultQuery)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    #endregion

    #region 创建、更新、删除

    public async ValueTask CreateAsync(RadishScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (scope is null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        await _db.Insertable(scope).ExecuteCommandAsync();
    }

    public async ValueTask UpdateAsync(RadishScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (scope is null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        await _db.Updateable(scope).ExecuteCommandAsync();
    }

    public async ValueTask DeleteAsync(RadishScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (scope is null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        await _db.Deleteable(scope).ExecuteCommandAsync();
    }

    public ValueTask<RadishScope> InstantiateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<RadishScope>(new RadishScope());
    }

    #endregion

    #region 字段读取

    public ValueTask<string?> GetDescriptionAsync(RadishScope scope, CancellationToken cancellationToken)
    {
        if (scope is null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        return new ValueTask<string?>(scope.Description);
    }

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDescriptionsAsync(RadishScope scope, CancellationToken cancellationToken)
    {
        // 当前未做多语言描述，返回空字典。
        return new ValueTask<ImmutableDictionary<CultureInfo, string>>(ImmutableDictionary<CultureInfo, string>.Empty);
    }

    public ValueTask<string?> GetDisplayNameAsync(RadishScope scope, CancellationToken cancellationToken)
    {
        if (scope is null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        return new ValueTask<string?>(scope.DisplayName);
    }

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(RadishScope scope, CancellationToken cancellationToken)
    {
        // 当前未做多语言显示名，返回空字典。
        return new ValueTask<ImmutableDictionary<CultureInfo, string>>(ImmutableDictionary<CultureInfo, string>.Empty);
    }

    public ValueTask<string?> GetIdAsync(RadishScope scope, CancellationToken cancellationToken)
    {
        if (scope is null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        return new ValueTask<string?>(ConvertIdToString(scope.Id));
    }

    public ValueTask<string?> GetNameAsync(RadishScope scope, CancellationToken cancellationToken)
    {
        if (scope is null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        return new ValueTask<string?>(scope.Name);
    }

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(RadishScope scope, CancellationToken cancellationToken)
    {
        if (scope is null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        return new ValueTask<ImmutableDictionary<string, JsonElement>>(DeserializeProperties(scope.Properties));
    }

    public ValueTask<ImmutableArray<string>> GetResourcesAsync(RadishScope scope, CancellationToken cancellationToken)
    {
        if (scope is null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        return new ValueTask<ImmutableArray<string>>(DeserializeStringArray(scope.Resources));
    }

    #endregion

    #region 字段写入

    public ValueTask SetDescriptionAsync(RadishScope scope, string? description, CancellationToken cancellationToken)
    {
        if (scope is null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        scope.Description = description;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetDescriptionsAsync(RadishScope scope,
        ImmutableDictionary<CultureInfo, string> descriptions, CancellationToken cancellationToken)
    {
        // 多语言描述暂不持久化，忽略即可。
        _ = scope;
        _ = descriptions;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetDisplayNameAsync(RadishScope scope, string? name, CancellationToken cancellationToken)
    {
        if (scope is null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        scope.DisplayName = name ?? string.Empty;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetDisplayNamesAsync(RadishScope scope,
        ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken)
    {
        // 多语言显示名暂不持久化。
        _ = scope;
        _ = names;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetNameAsync(RadishScope scope, string? name, CancellationToken cancellationToken)
    {
        if (scope is null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        scope.Name = name ?? string.Empty;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetPropertiesAsync(RadishScope scope,
        ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        if (scope is null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        scope.Properties = SerializeProperties(properties);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetResourcesAsync(RadishScope scope, ImmutableArray<string> resources, CancellationToken cancellationToken)
    {
        if (scope is null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        scope.Resources = SerializeStringArray(resources);
        return ValueTask.CompletedTask;
    }

    #endregion
}
