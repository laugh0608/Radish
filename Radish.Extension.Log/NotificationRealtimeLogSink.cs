using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>按精确生成身份重建关注通知、实时推送与 Hub 自有事件。</summary>
internal sealed class NotificationRealtimeLogSink(ILogEventSink next) : ILogEventSink
{
    private const string Follow = "Radish.Service.UserFollowService";
    private const string Notification = "Radish.Api.Services.NotificationPushService";
    private const string Interaction = "Radish.Api.Services.UserInteractionRealtimeNotifier";
    private const string Comment = "Radish.Api.Services.CommentRealtimePushService";
    private const string ChatHub = "Radish.Api.Hubs.ChatHub";
    private const string NotificationHub = "Radish.Api.Hubs.NotificationHub";
    private const string Unknown = "runtime.unclassified";
    private static readonly MessageTemplate UnknownHubTemplate = new MessageTemplateParser().Parse("Unrecognized Hub lifecycle event.");

    private static readonly Dictionary<string, MessageTemplate> Templates = new(StringComparer.Ordinal)
    {
        ["user_follow.notification_enqueue_failed"] = new MessageTemplateParser().Parse("Follow notification enqueue failed before a handled response."),
        ["notification.push_failed"] = new MessageTemplateParser().Parse("Notification realtime push is unavailable."),
        ["user_interaction.chat_push_failed"] = new MessageTemplateParser().Parse("Relationship invalidation chat push is unavailable."),
        ["user_interaction.notification_push_failed"] = new MessageTemplateParser().Parse("Relationship invalidation notification push is unavailable."),
        ["comment.push_failed"] = new MessageTemplateParser().Parse("Comment realtime push is unavailable."),
        ["chat.connection_closed"] = new MessageTemplateParser().Parse("Chat connection closed with a failure."),
        ["notification.connection_closed"] = new MessageTemplateParser().Parse("Notification connection closed with a failure."),
        [Unknown] = new MessageTemplateParser().Parse("Unrecognized notification or realtime push event.")
    };

    public void Emit(LogEvent value)
    {
        if (!value.Properties.TryGetValue("SourceContext", out var owner) ||
            owner is not ScalarValue { Value: string source } ||
            source is not (Follow or Notification or Interaction or Comment or ChatHub or NotificationHub))
        {
            next.Emit(value);
            return;
        }

        var code = (source, ReadEventId(value), value.Level) switch
        {
            (Follow, (1, "FollowNotificationEnqueueFailed"), LogEventLevel.Error) => "user_follow.notification_enqueue_failed",
            (Notification, (1, "NotificationPushFailed"), LogEventLevel.Warning) => "notification.push_failed",
            (Interaction, (1, "ChatRelationshipPushFailed"), LogEventLevel.Warning) => "user_interaction.chat_push_failed",
            (Interaction, (2, "NotificationRelationshipPushFailed"), LogEventLevel.Warning) => "user_interaction.notification_push_failed",
            (Comment, (1, "CommentPushFailed"), LogEventLevel.Warning) => "comment.push_failed",
            (ChatHub, (1, "ChatConnectionClosed"), LogEventLevel.Warning) => "chat.connection_closed",
            (NotificationHub, (1, "NotificationConnectionClosed"), LogEventLevel.Warning) => "notification.connection_closed",
            _ => Unknown
        };
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(source)),
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("application")),
            new("Diagnostic", new ScalarValue(code == Unknown && value.Level < LogEventLevel.Warning))
        };
        if (code != Unknown && value.Properties.TryGetValue("failureKind", out var failure) &&
            failure is ScalarValue
                { Value: "aggregate" or "database" or "cancelled" or "timeout" or "io" or "access-denied" or "argument" or "invalid-operation" or "other" })
            properties.Add(new("failureKind", failure));

        var activity = Activity.Current is { IdFormat: ActivityIdFormat.W3C } current ? current : null;
        var template = code == Unknown && source is ChatHub or NotificationHub ? UnknownHubTemplate : Templates[code];
        next.Emit(new LogEvent(value.Timestamp, value.Level, null, template, properties,
            value.TraceId ?? activity?.TraceId ?? default, value.SpanId ?? activity?.SpanId ?? default));
    }

    private static (int?, string?) ReadEventId(LogEvent value)
    {
        if (!value.Properties.TryGetValue("EventId", out var identity) || identity is not StructureValue details)
            return (null, null);
        var ids = details.Properties.Where(p => p.Name == "Id").ToArray();
        var names = details.Properties.Where(p => p.Name == "Name").ToArray();
        return ids.Length == 1 && ids[0].Value is ScalarValue { Value: int id } &&
            names.Length == 1 && names[0].Value is ScalarValue { Value: string name } ? (id, name) : (null, null);
    }
}
