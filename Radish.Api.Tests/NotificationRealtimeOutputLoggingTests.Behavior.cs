using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Radish.Api.Services;
using Radish.Api.Tests.ErrorHandling;
using Radish.IRepository;
using Radish.Model;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests;

using static NotificationRealtimeOutputLoggingTests;

public sealed partial class UserFollowLoggingTests
{
    internal static async Task VerifyFollowAsync(HttpBoundaryCapture output, AssertEvents assertEvents)
    {
        using var fixture = new Fixture(output.Logger);
        fixture.FailNotification("outbox", new ArgumentException(Secret));
        var response = await fixture.Controller.Follow(new() { TargetUserId = TargetId });
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("UserFollow.FollowRejected", response.Code);
        Assert.Equal(Secret, response.MessageInfo);
        Assert.True(fixture.Followed);
        assertEvents(new Expected(typeof(UserFollowService),
            "user_follow.notification_enqueue_failed", "argument", "Error"));
        Assert.True((await fixture.Controller.Follow(new() { TargetUserId = TargetId })).IsSuccess);
        Assert.Empty(output.Events);
    }

    internal static async Task VerifyFollowOutputFaultAsync(HttpBoundaryCapture output, Func<Func<Task>, Task>? whenAuditFails)
    {
        using var fixture = new Fixture(output.Logger);
        fixture.FailNotification("outbox", new InvalidOperationException(Secret));
        if (whenAuditFails is null)
            Assert.Equal(404, (await fixture.Controller.Follow(new() { TargetUserId = TargetId })).StatusCode);
        else
            await whenAuditFails(() => fixture.Controller.Follow(new() { TargetUserId = TargetId }));
        Assert.True(fixture.Followed);
        fixture.Outbox.Verify(r => r.AddAsync(It.IsAny<ReliableOutboxDraft>()), Times.Once);
    }
}

public sealed partial class NotificationCreationLoggingTests
{
    internal static async Task VerifyNotificationAsync(HttpBoundaryCapture output, AssertEvents assertEvents)
    {
        foreach (var stage in new[] { "group", "NotificationInboxChanged", "UnreadCountChanged" })
        {
            using var fixture = new Fixture(output.Logger);
            if (stage == "group")
                fixture.Clients.Setup(c => c.Group(It.IsAny<string>())).Throws(new IOException(Secret));
            else
                fixture.Client.Setup(c => c.SendCoreAsync(stage, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new IOException(Secret));
            await fixture.EnqueueAsync(CreateDto());
            await fixture.ExecuteAsync();
            var state = (await fixture.SnapshotAsync())!;
            Assert.Equal(ReliableOutboxStatuses.Succeeded, state.Status);
            Assert.Equal(0, state.AttemptCount);
            fixture.Client.Verify(c => c.SendCoreAsync("UnreadCountChanged", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                stage == "UnreadCountChanged" ? Times.Once() : Times.Never());
            assertEvents(new Expected(typeof(NotificationPushService), "notification.push_failed", "io"));
        }
    }

    internal static async Task VerifyNotificationOutputFaultAsync(HttpBoundaryCapture output, Func<Func<Task>, Task>? whenAuditFails)
    {
        using var fixture = new Fixture(output.Logger);
        fixture.Client.Setup(c => c.SendCoreAsync("NotificationInboxChanged", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException(Secret));
        if (whenAuditFails is null)
            Assert.Equal(NotificationId, await fixture.Service.CreateNotificationAsync(CreateDto()));
        else
            await whenAuditFails(() => fixture.Service.CreateNotificationAsync(CreateDto()));
        fixture.Inbox.Verify(r => r.PersistAsync(It.IsAny<Notification>(), It.IsAny<IReadOnlyList<NotificationInboxRecipient>>(), Now), Times.Once);
        fixture.Client.Verify(c => c.SendCoreAsync("UnreadCountChanged", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public sealed partial class UserInteractionLoggingTests
{
    internal static async Task VerifyInteractionAsync(HttpBoundaryCapture output, AssertEvents assertEvents)
    {
        using var fixture = new Fixture(output.Logger) { FailAll = true };
        await fixture.Notifier.NotifyRelationshipChangedAsync(FirstUser, SecondUser, Version);
        Assert.Equal(new[] { $"chat:user:{FirstUser}", $"notification:user:{FirstUser}",
            $"chat:user:{SecondUser}", $"notification:user:{SecondUser}" }, fixture.Attempts);
        var chat = new Expected(typeof(UserInteractionRealtimeNotifier), "user_interaction.chat_push_failed", "io");
        var notification = new Expected(typeof(UserInteractionRealtimeNotifier),
            "user_interaction.notification_push_failed", "io");
        assertEvents(chat, notification, chat, notification);
    }

    internal static async Task VerifyInteractionOutputFaultAsync(HttpBoundaryCapture output, Func<Func<Task>, Task>? whenAuditFails)
    {
        using var fixture = new Fixture(output.Logger) { FailAll = true };
        if (whenAuditFails is null)
            await fixture.Notifier.NotifyRelationshipChangedAsync(FirstUser, SecondUser, Version);
        else
            await whenAuditFails(() => fixture.Notifier.NotifyRelationshipChangedAsync(FirstUser, SecondUser, Version));
        Assert.Equal(whenAuditFails is null ? 4 : 1, fixture.Attempts.Count);
    }
}

public sealed partial class CommentRealtimeLoggingTests
{
    internal static async Task VerifyCommentAsync(HttpBoundaryCapture output, AssertEvents assertEvents)
    {
        using var fixture = new Fixture(output.Logger) { FailedEvent = "all" };
        Assert.Equal(200, (await fixture.CallControllerAsync("create")).StatusCode);
        Assert.Equal(new[] { "CommentCreated", "CommentHighlightsChanged" }, fixture.Sends.Select(s => s.Event));
        var warning = new Expected(typeof(CommentRealtimePushService), "comment.push_failed", "io");
        assertEvents(warning, warning);
    }

    internal static async Task VerifyCommentOutputFaultAsync(HttpBoundaryCapture output, Func<Func<Task>, Task>? whenAuditFails)
    {
        using var fixture = new Fixture(output.Logger) { FailedEvent = "all" };
        if (whenAuditFails is null)
            Assert.Equal(200, (await fixture.CallControllerAsync("create")).StatusCode);
        else
            await whenAuditFails(() => fixture.CallControllerAsync("create"));
        Assert.Equal(whenAuditFails is null ? 2 : 1, fixture.Sends.Count);
        Assert.Contains("create", fixture.Steps);
    }
}
