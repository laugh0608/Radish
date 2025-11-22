using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Radish.Api.Controllers;
using Radish.Common.CacheTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.LogModels;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(WeatherForecastController))]
public class WeatherForecastControllerTest
{
    [Fact]
    public void Get_ShouldReturnFiveForecasts()
    {
        var controller = new WeatherForecastController(new FakeScopeFactory(),
            new FakeBaseService<Role, RoleVo>(),
            new FakeBaseService<AuditSqlLog, AuditSqlLogVo>(),
            new FakeCaching());

        var result = controller.Get().ToArray();

        Assert.Equal(5, result.Length);
        Assert.All(result, forecast => Assert.False(string.IsNullOrEmpty(forecast.Summary)));
    }

    private sealed class FakeBaseService<TEntity, TVo> : IBaseService<TEntity, TVo> where TEntity : class
    {
        public Task<long> AddAsync(TEntity entity) => Task.FromResult(0L);

        public Task<List<long>> AddSplitAsync(TEntity entity) => Task.FromResult(new List<long>());

        public Task<List<TVo>> QueryAsync(Expression<Func<TEntity, bool>>? whereExpression = null) =>
            Task.FromResult(new List<TVo>());

        public Task<List<TEntity>> QuerySplitAsync(Expression<Func<TEntity, bool>>? whereExpression,
            string orderByFields = "Id") =>
            Task.FromResult(new List<TEntity>());
    }

    private sealed class FakeCaching : ICaching
    {
        public IDistributedCache Cache { get; } = new NoopDistributedCache();

        public void AddCacheKey(string cacheKey) => throw new NotImplementedException();

        public Task AddCacheKeyAsync(string cacheKey) => throw new NotImplementedException();

        public void DelByPattern(string key) => throw new NotImplementedException();

        public Task DelByPatternAsync(string key) => throw new NotImplementedException();

        public void DelCacheKey(string cacheKey) => throw new NotImplementedException();

        public Task DelCacheKeyAsync(string cacheKey) => throw new NotImplementedException();

        public bool Exists(string cacheKey) => throw new NotImplementedException();

        public Task<bool> ExistsAsync(string cacheKey) => throw new NotImplementedException();

        public List<string> GetAllCacheKeys() => throw new NotImplementedException();

        public Task<List<string>> GetAllCacheKeysAsync() => throw new NotImplementedException();

        public T Get<T>(string cacheKey) => throw new NotImplementedException();

        public Task<T> GetAsync<T>(string cacheKey) => throw new NotImplementedException();

        public object Get(Type type, string cacheKey) => throw new NotImplementedException();

        public Task<object> GetAsync(Type type, string cacheKey) => throw new NotImplementedException();

        public string GetString(string cacheKey) => throw new NotImplementedException();

        public Task<string> GetStringAsync(string cacheKey) => throw new NotImplementedException();

        public void Remove(string key) => throw new NotImplementedException();

        public Task RemoveAsync(string key) => throw new NotImplementedException();

        public void RemoveAll() => throw new NotImplementedException();

        public Task RemoveAllAsync() => throw new NotImplementedException();

        public void Set<T>(string cacheKey, T value, TimeSpan? expire = null) =>
            throw new NotImplementedException();

        public Task SetAsync<T>(string cacheKey, T value) => throw new NotImplementedException();

        public Task SetAsync<T>(string cacheKey, T value, TimeSpan expire) => throw new NotImplementedException();

        public void SetPermanent<T>(string cacheKey, T value) => throw new NotImplementedException();

        public Task SetPermanentAsync<T>(string cacheKey, T value) => throw new NotImplementedException();

        public void SetString(string cacheKey, string value, TimeSpan? expire = null) =>
            throw new NotImplementedException();

        public Task SetStringAsync(string cacheKey, string value) => throw new NotImplementedException();

        public Task SetStringAsync(string cacheKey, string value, TimeSpan expire) =>
            throw new NotImplementedException();

        public Task DelByParentKeyAsync(string key) => throw new NotImplementedException();
    }

    private sealed class NoopDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromResult<byte[]?>(null);

        public void Refresh(string key) { }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key) { }

        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default) => Task.CompletedTask;
    }

    private sealed class FakeScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeScope();

        private sealed class FakeScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new FakeServiceProvider();

            public void Dispose() { }
        }
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
