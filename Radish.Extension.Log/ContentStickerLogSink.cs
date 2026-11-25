using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Radish.Extension.Log;

/// <summary>按精确生成身份重建内容、回应与表情服务的运行事件。</summary>
internal sealed class ContentStickerLogSink(ILogEventSink next) : ILogEventSink
{
    private const string Comment = "Radish.Service.CommentService";
    private const string Submission = "Radish.Service.ContentSubmissionService";
    private const string Revision = "Radish.Service.ForumContentRevisionService";
    private const string Reaction = "Radish.Service.ReactionService";
    private const string Sticker = "Radish.Service.StickerService";
    private const string Unknown = "runtime.unclassified";

    private static readonly Dictionary<string, MessageTemplate> Templates = new(StringComparer.Ordinal)
    {
        ["comment.edit_failed"] = new MessageTemplateParser().Parse("Comment edit consumed a failure."),
        ["comment.god_recheck_failed"] = new MessageTemplateParser().Parse("God comment recheck failed."),
        ["comment.sofa_recheck_failed"] = new MessageTemplateParser().Parse("Sofa comment recheck failed."),
        ["comment.highlight_recheck_failed"] = new MessageTemplateParser().Parse("Comment highlight recheck failed."),
        ["comment.highlight_fill_failed"] = new MessageTemplateParser().Parse("Comment highlight status fill failed."),
        ["content_submission.conflict_resolved"] = new MessageTemplateParser().Parse("Content submission conflict resolved from an existing record."),
        ["post.restore_failed"] = new MessageTemplateParser().Parse("Post restore consumed a failure."),
        ["reaction.retrying"] = new MessageTemplateParser().Parse("Reaction toggle selected its single conflict retry."),
        ["sticker.cache_read_failed"] = new MessageTemplateParser().Parse("Sticker group cache read failed."),
        ["sticker.cache_write_failed"] = new MessageTemplateParser().Parse("Sticker group cache write failed."),
        ["sticker.cache_invalidation_failed"] = new MessageTemplateParser().Parse("Sticker group cache invalidation failed."),
        ["sticker.batch_item_failed"] = new MessageTemplateParser().Parse("Sticker batch item construction failed."),
        ["sticker.thumbnail_failed"] = new MessageTemplateParser().Parse("Sticker thumbnail generation degraded."),
        [Unknown] = new MessageTemplateParser().Parse("Unrecognized content, reaction or sticker event.")
    };

    public void Emit(LogEvent value)
    {
        if (Scalar(value, "SourceContext") is not { Value: string source } ||
            source is not (Comment or Submission or Revision or Reaction or Sticker))
        {
            next.Emit(value);
            return;
        }

        var code = source is Reaction or Sticker ? ReadTypedCode(source, value) : ReadStaticCode(source, value);
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue(source)),
            new("EventCode", new ScalarValue(code)),
            new("SourceCategory", new ScalarValue("application")),
            new("Diagnostic", new ScalarValue(code == Unknown && value.Level < LogEventLevel.Warning))
        };
        if (code != Unknown && Scalar(value, "failureKind") is
            { Value: "aggregate" or "database" or "cancelled" or "timeout" or "io" or "access-denied" or "argument" or "invalid-operation" or "other" } failure)
            properties.Add(new("failureKind", failure));

        var activity = Activity.Current is { IdFormat: ActivityIdFormat.W3C } current ? current : null;
        next.Emit(new LogEvent(value.Timestamp, value.Level, null, Templates[code], properties,
            value.TraceId ?? activity?.TraceId ?? default, value.SpanId ?? activity?.SpanId ?? default));
    }

    private static string ReadStaticCode(string source, LogEvent value)
    {
        var code = Scalar(value, "EventCode")?.Value as string;
        return (source, code, value.Level) switch
        {
            (Comment, "comment.edit_failed" or "comment.god_recheck_failed" or "comment.sofa_recheck_failed" or
                "comment.highlight_recheck_failed" or "comment.highlight_fill_failed", LogEventLevel.Error) => code,
            (Submission, "content_submission.conflict_resolved", LogEventLevel.Warning) => code,
            (Revision, "post.restore_failed", LogEventLevel.Error) => code,
            _ => Unknown
        };
    }

    private static string ReadTypedCode(string source, LogEvent value)
    {
        if (!value.Properties.TryGetValue("EventId", out var identity) || identity is not StructureValue details)
            return Unknown;
        var ids = details.Properties.Where(p => p.Name == "Id").ToArray();
        var names = details.Properties.Where(p => p.Name == "Name").ToArray();
        if (ids.Length != 1 || ids[0].Value is not ScalarValue { Value: int id } ||
            names.Length != 1 || names[0].Value is not ScalarValue { Value: string name })
            return Unknown;

        return (source, id, name, value.Level) switch
        {
            (Reaction, 1, "ReactionRetrying", LogEventLevel.Warning) => "reaction.retrying",
            (Sticker, 1, "StickerCacheReadFailed", LogEventLevel.Warning) => "sticker.cache_read_failed",
            (Sticker, 2, "StickerCacheWriteFailed", LogEventLevel.Warning) => "sticker.cache_write_failed",
            (Sticker, 3, "StickerCacheInvalidationFailed", LogEventLevel.Warning) => "sticker.cache_invalidation_failed",
            (Sticker, 4, "StickerBatchItemFailed", LogEventLevel.Error) => "sticker.batch_item_failed",
            (Sticker, 5, "StickerThumbnailFailed", LogEventLevel.Warning) => "sticker.thumbnail_failed",
            _ => Unknown
        };
    }

    private static ScalarValue? Scalar(LogEvent value, string key) =>
        value.Properties.TryGetValue(key, out var property) ? property as ScalarValue : null;
}
