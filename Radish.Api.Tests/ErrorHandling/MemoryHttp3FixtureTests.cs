using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

public sealed class MemoryHttp3FixtureTests
{
    [Fact]
    public async Task StreamClose_ShouldAllowConcurrentAndLateCallbackRegistration()
    {
        var stream = new MemoryHttp3Stream(2, canRead: true, canWrite: false);
        var calls = 0;
        stream.OnClosed(_ =>
        {
            Interlocked.Increment(ref calls);
            // 模拟 Kestrel 关闭回调与另一线程注册的交错；回调不得持有 transport 锁。
            var registration = new Thread(() =>
                stream.OnClosed(_ => Interlocked.Increment(ref calls), null)) { IsBackground = true };
            registration.Start();
            Assert.True(registration.Join(TimeSpan.FromSeconds(2)), "Close callback held the transport lifetime lock.");
        }, null);

        await stream.DisposeAsync();
        await stream.DisposeAsync();
        stream.OnClosed(_ => Interlocked.Increment(ref calls), null);

        Assert.Equal(3, Volatile.Read(ref calls));
        Assert.True(stream.Disposed.Task.IsCompletedSuccessfully);
        await stream.Input.Writer.CompleteAsync();
        await stream.Output.Reader.CompleteAsync();
    }
}
