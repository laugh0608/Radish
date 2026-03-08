#nullable enable

using System;
using System.IO;
using Microsoft.Extensions.FileProviders;
using Radish.Common.DocumentTool;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

/// <summary>
/// 固定文档静态资源暴露边界测试。
/// </summary>
public class BuiltInDocumentStaticAssetFileProviderTests
{
    [Fact(DisplayName = "应仅暴露允许目录下的静态资源文件")]
    public void GetFileInfo_ShouldReturnFile_WhenPathAllowed()
    {
        var docsRoot = CreateTempDocsRoot();
        try
        {
            WriteBinaryFile(Path.Combine(docsRoot, "images", "logo.png"), new byte[] { 1, 2, 3, 4 });
            WriteBinaryFile(Path.Combine(docsRoot, "guide", "sample.png"), new byte[] { 5, 6, 7, 8 });
            WriteFile(Path.Combine(docsRoot, "guide", "getting-started.md"), "# 快速开始");

            var provider = new BuiltInDocumentStaticAssetFileProvider(new PhysicalFileProvider(docsRoot));

            provider.GetFileInfo("images/logo.png").Exists.ShouldBeTrue();
            provider.GetFileInfo("guide/sample.png").Exists.ShouldBeFalse();
            provider.GetFileInfo("guide/getting-started.md").Exists.ShouldBeFalse();
        }
        finally
        {
            DeleteDirectory(docsRoot);
        }
    }

    [Theory(DisplayName = "应拒绝目录与路径穿越访问")]
    [InlineData("images")]
    [InlineData("../images/logo.png")]
    [InlineData("/../guide/getting-started.md")]
    public void GetFileInfo_ShouldRejectDirectoryOrTraversalPath(string subpath)
    {
        var docsRoot = CreateTempDocsRoot();
        try
        {
            WriteBinaryFile(Path.Combine(docsRoot, "images", "logo.png"), new byte[] { 1, 2, 3, 4 });

            var provider = new BuiltInDocumentStaticAssetFileProvider(new PhysicalFileProvider(docsRoot));

            provider.GetFileInfo(subpath).Exists.ShouldBeFalse();
            provider.GetDirectoryContents("images").Exists.ShouldBeFalse();
        }
        finally
        {
            DeleteDirectory(docsRoot);
        }
    }

    [Theory(DisplayName = "策略应拒绝非允许目录或非静态资源扩展")]
    [InlineData("guide/getting-started.md")]
    [InlineData("guide/sample.png")]
    [InlineData("README")]
    [InlineData("index.json")]
    public void IsAllowedAssetPath_ShouldRejectDisallowedPath(string path)
    {
        BuiltInDocumentStaticAssetPolicy.IsAllowedAssetPath(path).ShouldBeFalse();
    }

    private static string CreateTempDocsRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "RadishBuiltInAssetTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void WriteBinaryFile(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
