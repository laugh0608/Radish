using System;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Radish.Extension.AopExtension;
using Xunit;

namespace Radish.Api.Tests.Extensions;

public sealed class ServiceAopTest
{
    [Fact]
    public async Task TaskMethod_ShouldPreserveOriginalException()
    {
        var expectedException = new InvalidOperationException("service task failed");
        var service = CreateProxy(new ThrowingAsyncService(expectedException));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteAsync());

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public async Task GenericTaskMethod_ShouldPreserveOriginalException()
    {
        var expectedException = new InvalidOperationException("generic service task failed");
        var service = CreateProxy(new ThrowingAsyncService(expectedException));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.QueryAsync());

        Assert.Same(expectedException, exception);
    }

    private static IThrowingAsyncService CreateProxy(IThrowingAsyncService target)
    {
        return new ProxyGenerator().CreateInterfaceProxyWithTarget(
            target,
            new ServiceAop());
    }

    public interface IThrowingAsyncService
    {
        Task ExecuteAsync();

        Task<int> QueryAsync();
    }

    private sealed class ThrowingAsyncService : IThrowingAsyncService
    {
        private readonly Exception _exception;

        public ThrowingAsyncService(Exception exception)
        {
            _exception = exception;
        }

        public Task ExecuteAsync()
        {
            return Task.FromException(_exception);
        }

        public Task<int> QueryAsync()
        {
            return Task.FromException<int>(_exception);
        }
    }
}
