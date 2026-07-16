using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Security;

public class AttachmentStaticExposureContractTests
{
    [Fact]
    public void Api_Should_Expose_Only_Trusted_DefaultIcon_Under_Uploads_Path()
    {
        var programSource = File.ReadAllText(GetRepositoryPath("Radish.Api", "Program.cs"));

        programSource.ShouldContain("Path.Combine(uploadsFullPath, \"DefaultIco\")");
        programSource.ShouldContain("PhysicalFileProvider(trustedDefaultIconPath)");
        programSource.ShouldNotContain("PhysicalFileProvider(uploadsFullPath)");
    }

    [Fact]
    public void Gateway_Should_Not_Proxy_Entire_UserUpload_Root()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(GetRepositoryPath("Radish.Gateway", "appsettings.json")),
            new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        var routePaths = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Routes")
            .EnumerateObject()
            .Select(route => route.Value.GetProperty("Match").GetProperty("Path").GetString())
            .ToList();

        routePaths.ShouldContain("/uploads/DefaultIco/{**catch-all}");
        routePaths.ShouldNotContain("/uploads/{**catch-all}");
        routePaths.ShouldContain("/_assets/attachments/{**catch-all}");
    }

    private static string GetRepositoryPath(params string[] segments)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Radish.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("无法定位 Radish 仓库根目录");
        return Path.Combine([directory.FullName, .. segments]);
    }
}
