#nullable enable
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Radish.Extension.AopExtension;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests;

public class LogPayloadSerializerTests
{
    [Fact(DisplayName = "安全日志序列化应对复合对象中的 Stream 做摘要化")]
    public void Serialize_Should_Summarize_Stream_In_CompositePayload()
    {
        var payload = new
        {
            Stream = new MemoryStream(new byte[] { 1, 2, 3 }),
            Name = "avatar"
        };

        var json = InvokeSerialize(payload);

        json.ShouldContain("\"kind\":\"Stream\"");
        json.ShouldContain("\"type\":\"System.IO.MemoryStream\"");
        json.ShouldContain("\"length\":3");
        json.ShouldContain("\"Name\":\"avatar\"");
    }

    [Fact(DisplayName = "安全日志序列化应对 FileResult 做摘要化")]
    public void Serialize_Should_Summarize_FileResult()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var payload = new FileStreamResult(stream, "image/png")
        {
            FileDownloadName = "avatar.png"
        };

        var json = InvokeSerialize(payload);

        json.ShouldContain("\"kind\":\"FileResult\"");
        json.ShouldContain("\"ContentType\":\"image/png\"");
        json.ShouldContain("\"FileDownloadName\":\"avatar.png\"");
    }

    [Fact(DisplayName = "安全日志序列化应对 IFormFile 做摘要化")]
    public void Serialize_Should_Summarize_FormFile()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        IFormFile file = new FormFile(stream, 0, stream.Length, "file", "demo.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var json = InvokeSerialize(new { File = file });

        json.ShouldContain("\"kind\":\"FormFile\"");
        json.ShouldContain("\"FileName\":\"demo.png\"");
        json.ShouldContain("\"ContentType\":\"image/png\"");
        json.ShouldContain("\"Length\":5");
    }

    private static string InvokeSerialize(object? value)
    {
        var serializerType = typeof(ServiceAop).Assembly.GetType("Radish.Extension.AopExtension.LogPayloadSerializer");
        serializerType.ShouldNotBeNull();

        var method = serializerType.GetMethod("Serialize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        method.ShouldNotBeNull();

        var result = method.Invoke(null, new[] { value });
        result.ShouldBeOfType<string>();
        return (string)result;
    }
}
