using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Radish.Api.Hubs;
using Radish.Api.Tests.ErrorHandling;
using Radish.Common.HttpContextTool;
using Xunit;

namespace Radish.Api.Tests;

public sealed partial class ChatHubLoggingTests
{
    internal static async Task VerifyLifecycleAsync(HttpBoundaryCapture output, Action<HubLifecycleOutputLoggingTests.Expected> observe)
    {
        using (var normal = new Fixture(output.Logger))
        {
            await normal.Hub.OnConnectedAsync();
            await normal.Hub.OnDisconnectedAsync(null);
            Assert.Equal(new[] { "add-user", "presence-remove", "remove-user" }, normal.Steps);
            Assert.Empty(output.Events);
        }
        using (var fixture = new Fixture(output.Logger))
        {
            var finishRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Groups.Setup(g => g.RemoveFromGroupAsync(fixture.Connection, $"user:{UserId}", default))
                .Callback(() => Assert.Equal(new[] { "presence-remove" }, fixture.Steps)).Returns(finishRemoval.Task);
            var disconnect = fixture.Hub.OnDisconnectedAsync(new IOException(Secret));
            try
            {
                Assert.False(disconnect.IsCompleted);
                Assert.Empty(output.Events);
            }
            finally { finishRemoval.TrySetResult(); }
            await disconnect;
            fixture.Groups.Verify(g => g.RemoveFromGroupAsync(fixture.Connection, $"user:{UserId}", default), Times.Once);
            observe(new(typeof(ChatHub), "chat.connection_closed", "io"));
        }
        foreach (var presence in new[] { true, false })
        {
            using var fixture = new Fixture(output.Logger);
            var failure = new IOException(Secret);
            if (presence) fixture.Presence.Setup(p => p.RemoveConnection(fixture.Connection, UserId)).Throws(failure);
            else fixture.Groups.Setup(g => g.RemoveFromGroupAsync(fixture.Connection, $"user:{UserId}", default)).ThrowsAsync(failure);
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Hub.OnDisconnectedAsync(new TimeoutException(Secret))));
            if (presence) fixture.Groups.VerifyNoOtherCalls();
            else Assert.Equal(new[] { "presence-remove" }, fixture.Steps);
            Assert.Empty(output.Events);
        }
    }

    internal static async Task VerifyOutputFailureAsync(HttpBoundaryCapture output, Func<Func<Task>, Task> invoke)
    {
        using var fixture = new Fixture(output.Logger);
        await invoke(() => fixture.Hub.OnDisconnectedAsync(new IOException(Secret)));
        Assert.Equal(new[] { "presence-remove", "remove-user" }, fixture.Steps);
    }
}

public sealed partial class NotificationHubLoggingTests
{
    internal static async Task VerifyLifecycleAsync(HttpBoundaryCapture output, Action<HubLifecycleOutputLoggingTests.Expected> observe)
    {
        using (var normal = new Fixture(output.Logger))
        {
            await normal.Hub.OnConnectedAsync();
            await normal.Hub.OnDisconnectedAsync(null);
            Assert.Equal(new[] { "add", "summary", "NotificationInboxChanged", "UnreadCountChanged", "remove" }, normal.Steps);
            Assert.Empty(output.Events);
        }
        using (var fixture = new Fixture(output.Logger))
        {
            var finishRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Groups.Setup(g => g.RemoveFromGroupAsync(Secret, $"user:{UserId}", default)).Returns(finishRemoval.Task);
            var disconnect = fixture.Hub.OnDisconnectedAsync(new TimeoutException(Secret));
            try
            {
                Assert.False(disconnect.IsCompleted);
                Assert.Empty(output.Events);
            }
            finally { finishRemoval.TrySetResult(); }
            await disconnect;
            fixture.Groups.Verify(g => g.RemoveFromGroupAsync(Secret, $"user:{UserId}", default), Times.Once);
            fixture.Notifications.VerifyNoOtherCalls();
            fixture.Caller.VerifyNoOtherCalls();
            observe(new(typeof(NotificationHub), "notification.connection_closed", "timeout"));
        }
        using (var invalid = new Fixture(output.Logger))
        {
            invalid.Normalizer.Setup(n => n.Normalize(invalid.Principal, It.IsAny<string?>())).Returns(new CurrentUser { UserId = 0 });
            await invalid.Hub.OnDisconnectedAsync(new OperationCanceledException(Secret));
            invalid.Groups.VerifyNoOtherCalls();
            observe(new(typeof(NotificationHub), "notification.connection_closed", "cancelled"));
        }
        using var failed = new Fixture(output.Logger);
        var failure = new IOException(Secret);
        failed.Groups.Setup(g => g.RemoveFromGroupAsync(Secret, $"user:{UserId}", default)).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => failed.Hub.OnDisconnectedAsync(new TimeoutException(Secret))));
        Assert.Empty(output.Events);
    }

    internal static async Task VerifyOutputFailureAsync(HttpBoundaryCapture output, Func<Func<Task>, Task> invoke)
    {
        using var fixture = new Fixture(output.Logger);
        await invoke(() => fixture.Hub.OnDisconnectedAsync(new IOException(Secret)));
        Assert.Equal(new[] { "remove" }, fixture.Steps);
    }
}
