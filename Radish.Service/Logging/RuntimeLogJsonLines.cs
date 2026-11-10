using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;

namespace Radish.Service.Logging;

/// <summary>按字节预算读取，不持有信封；每次 yield 的 document 由消费者立即释放。</summary>
internal static class RuntimeLogJsonLines
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal static async IAsyncEnumerable<JsonDocument> ReadAsync(Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var line = new byte[RuntimeLogPolicy.MaxEventBytes];
        var length = 0;
        long total = 0;
        var records = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            total += read;
            if (total > RuntimeLogIngestOptions.MaxEnvelopeBytes) throw new IngestInputException(413);
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == (byte)'\n')
                {
                    if (++records > RuntimeLogIngestOptions.MaxRecords) throw new IngestInputException(413);
                    if (length > 0) yield return Parse(line.AsSpan(0, length));
                    length = 0;
                }
                else
                {
                    if (length == line.Length) throw new IngestInputException(413);
                    line[length++] = buffer[i];
                }
            }
        }
        if (length > 0)
        {
            if (++records > RuntimeLogIngestOptions.MaxRecords) throw new IngestInputException(413);
            yield return Parse(line.AsSpan(0, length));
        }
    }

    private static JsonDocument Parse(ReadOnlySpan<byte> bytes)
    {
        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(Utf8.GetString(bytes), new JsonDocumentOptions { MaxDepth = 4 });
            EnsureUniqueNames(document.RootElement);
            return document;
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException or IngestInputException or InvalidOperationException)
        {
            document?.Dispose();
            throw new IngestInputException(400);
        }
    }

    private static void EnsureUniqueNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new IngestInputException(400);
                EnsureUniqueNames(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.String) _ = element.GetString();
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var value in element.EnumerateArray()) EnsureUniqueNames(value);
    }
}

internal sealed class IngestInputException(int statusCode) : Exception("Invalid runtime log envelope.")
{
    public int StatusCode { get; } = statusCode;
}
