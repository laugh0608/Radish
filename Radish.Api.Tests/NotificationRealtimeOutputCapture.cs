using System;
using System.IO;
using Radish.Api.Tests.ErrorHandling;
using Serilog.Formatting.Display;

namespace Radish.Api.Tests;

internal sealed class NotificationRealtimeOutputCapture : IDisposable
{
    private static readonly MessageTemplateTextFormatter LegacyFormatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
    private readonly ApplicationLogScope _global;

    public NotificationRealtimeOutputCapture(bool candidate, string environment, bool audit = false)
    {
        Output = new HttpBoundaryCapture(candidate, environment, audit: audit);
        _global = new ApplicationLogScope(Output.Logger);
    }

    public HttpBoundaryCapture Output { get; }
    public Serilog.Core.Logger Logger => Output.Logger;
    public string Text
    {
        get
        {
            if (Output.Candidate) return Output.Output.ToString() + Output.Emergency;
            using var text = new StringWriter();
            foreach (var entry in Output.Sink.Events) LegacyFormatter.Format(entry, text);
            return text.ToString();
        }
    }

    public void Dispose()
    {
        _global.Dispose();
        Output.Dispose();
    }
}
