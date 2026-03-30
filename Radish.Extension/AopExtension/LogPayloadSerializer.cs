using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Radish.Extension.AopExtension;

internal static class LogPayloadSerializer
{
    private static readonly JsonSerializerSettings Settings = CreateSettings();

    public static string Serialize(object? value)
    {
        try
        {
            return JsonConvert.SerializeObject(value, Settings);
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new
            {
                kind = "Unserializable",
                type = value?.GetType().FullName ?? typeof(object).FullName,
                error = ex.GetType().FullName,
                ex.Message
            });
        }
    }

    private static JsonSerializerSettings CreateSettings()
    {
        var settings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            MaxDepth = 8
        };

        settings.Error += (_, args) => { args.ErrorContext.Handled = true; };
        settings.Converters.Add(new StreamLogJsonConverter());
        settings.Converters.Add(new FormFileLogJsonConverter());
        settings.Converters.Add(new FileResultLogJsonConverter());
        settings.Converters.Add(new ClaimsPrincipalLogJsonConverter());
        settings.Converters.Add(new HttpContextLogJsonConverter());
        settings.Converters.Add(new HttpRequestLogJsonConverter());
        settings.Converters.Add(new HttpResponseLogJsonConverter());
        settings.Converters.Add(new ByteArrayLogJsonConverter());
        settings.Converters.Add(new DelegateLogJsonConverter());
        settings.Converters.Add(new TypeLogJsonConverter());

        return settings;
    }

    private static long? TryGetStreamLength(Stream stream)
    {
        try
        {
            return stream.CanSeek ? stream.Length : null;
        }
        catch
        {
            return null;
        }
    }

    private static long? TryGetStreamPosition(Stream stream)
    {
        try
        {
            return stream.CanSeek ? stream.Position : null;
        }
        catch
        {
            return null;
        }
    }

    private abstract class SummaryJsonConverter<T> : JsonConverter
    {
        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return typeof(T).IsAssignableFrom(objectType);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is not T typedValue)
            {
                writer.WriteNull();
                return;
            }

            serializer.Serialize(writer, CreateSummary(typedValue));
        }

        protected abstract object CreateSummary(T value);
    }

    private sealed class StreamLogJsonConverter : SummaryJsonConverter<Stream>
    {
        protected override object CreateSummary(Stream value)
        {
            return new
            {
                kind = "Stream",
                type = value.GetType().FullName,
                value.CanRead,
                value.CanWrite,
                value.CanSeek,
                length = TryGetStreamLength(value),
                position = TryGetStreamPosition(value)
            };
        }
    }

    private sealed class FormFileLogJsonConverter : SummaryJsonConverter<IFormFile>
    {
        protected override object CreateSummary(IFormFile value)
        {
            return new
            {
                kind = "FormFile",
                type = value.GetType().FullName,
                value.Name,
                value.FileName,
                value.ContentType,
                value.Length
            };
        }
    }

    private sealed class FileResultLogJsonConverter : SummaryJsonConverter<FileResult>
    {
        protected override object CreateSummary(FileResult value)
        {
            return new
            {
                kind = "FileResult",
                type = value.GetType().FullName,
                value.ContentType,
                value.FileDownloadName,
                value.LastModified,
                value.EntityTag
            };
        }
    }

    private sealed class ClaimsPrincipalLogJsonConverter : SummaryJsonConverter<ClaimsPrincipal>
    {
        protected override object CreateSummary(ClaimsPrincipal value)
        {
            return new
            {
                kind = "ClaimsPrincipal",
                type = value.GetType().FullName,
                isAuthenticated = value.Identity?.IsAuthenticated ?? false,
                authenticationType = value.Identity?.AuthenticationType,
                name = value.Identity?.Name,
                claims = value.Claims
                    .Select(claim => new { claim.Type, claim.Value })
                    .Take(20)
                    .ToList()
            };
        }
    }

    private sealed class HttpContextLogJsonConverter : SummaryJsonConverter<HttpContext>
    {
        protected override object CreateSummary(HttpContext value)
        {
            return new
            {
                kind = "HttpContext",
                type = value.GetType().FullName,
                value.TraceIdentifier,
                request = new
                {
                    value.Request.Method,
                    path = value.Request.Path.Value,
                    value.Request.ContentType,
                    value.Request.ContentLength
                },
                response = new
                {
                    value.Response.StatusCode,
                    value.Response.ContentType,
                    value.Response.ContentLength
                }
            };
        }
    }

    private sealed class HttpRequestLogJsonConverter : SummaryJsonConverter<HttpRequest>
    {
        protected override object CreateSummary(HttpRequest value)
        {
            return new
            {
                kind = "HttpRequest",
                type = value.GetType().FullName,
                value.Method,
                path = value.Path.Value,
                value.ContentType,
                value.ContentLength
            };
        }
    }

    private sealed class HttpResponseLogJsonConverter : SummaryJsonConverter<HttpResponse>
    {
        protected override object CreateSummary(HttpResponse value)
        {
            return new
            {
                kind = "HttpResponse",
                type = value.GetType().FullName,
                value.StatusCode,
                value.ContentType,
                value.ContentLength
            };
        }
    }

    private sealed class ByteArrayLogJsonConverter : SummaryJsonConverter<byte[]>
    {
        protected override object CreateSummary(byte[] value)
        {
            return new
            {
                kind = "ByteArray",
                length = value.Length
            };
        }
    }

    private sealed class DelegateLogJsonConverter : SummaryJsonConverter<Delegate>
    {
        protected override object CreateSummary(Delegate value)
        {
            return new
            {
                kind = "Delegate",
                type = value.GetType().FullName,
                method = value.Method.ToString(),
                targetType = value.Target?.GetType().FullName
            };
        }
    }

    private sealed class TypeLogJsonConverter : SummaryJsonConverter<Type>
    {
        protected override object CreateSummary(Type value)
        {
            return new
            {
                kind = "Type",
                value.FullName,
                assembly = value.Assembly.GetName().Name
            };
        }
    }
}
