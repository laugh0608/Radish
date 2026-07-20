using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.Localization;

public class AuthResourceParityTest
{
    [Fact]
    public void AuthResources_ShouldKeepDefaultChineseAndEnglishKeysAligned()
    {
        var resourcesDirectory = Path.Combine(FindRepositoryRoot(), "Radish.Auth", "Resources");
        var defaultKeys = ReadResourceKeys(Path.Combine(resourcesDirectory, "Errors.resx"));
        var chineseKeys = ReadResourceKeys(Path.Combine(resourcesDirectory, "Errors.zh.resx"));
        var englishKeys = ReadResourceKeys(Path.Combine(resourcesDirectory, "Errors.en.resx"));

        Assert.Equal(defaultKeys.Length, defaultKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(chineseKeys.Length, chineseKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(englishKeys.Length, englishKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(defaultKeys, chineseKeys);
        Assert.Equal(defaultKeys, englishKeys);
    }

    [Fact]
    public void ApiErrorResources_ShouldKeepChineseAndEnglishKeysAligned()
    {
        var resourcesDirectory = Path.Combine(FindRepositoryRoot(), "Radish.Api", "Resources");
        var chineseKeys = ReadResourceKeys(Path.Combine(resourcesDirectory, "Errors.zh.resx"));
        var englishKeys = ReadResourceKeys(Path.Combine(resourcesDirectory, "Errors.en.resx"));

        Assert.Equal(chineseKeys.Length, chineseKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(englishKeys.Length, englishKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(chineseKeys, englishKeys);
        Assert.Contains("error.order.retry_rejected", chineseKeys);
        Assert.Contains("error.moderation.concurrent_review_conflict", chineseKeys);
        Assert.Contains("error.product.version_conflict", chineseKeys);
        Assert.Contains("error.wiki.document_not_found", chineseKeys);
        Assert.Contains("error.wiki.parent_cycle_conflict", chineseKeys);
        Assert.Contains("error.pet.care_cooldown", chineseKeys);
        Assert.Contains("error.pet.daily_limit_reached", chineseKeys);
        Assert.Contains("error.experience.not_found", chineseKeys);
        Assert.Contains("error.experience.other_user_forbidden", chineseKeys);
        Assert.Contains("error.bootstrap.display_name_length_invalid", chineseKeys);
        Assert.Contains("error.bootstrap.password_weak", chineseKeys);
        Assert.Contains("error.bootstrap.concurrent_initialization", chineseKeys);
        Assert.Contains("error.bootstrap.initialization_failed", chineseKeys);
    }

    [Fact]
    public void ForumPublishErrorCodes_ShouldResolveToChineseAndEnglishApiResources()
    {
        var resourcesDirectory = Path.Combine(FindRepositoryRoot(), "Radish.Api", "Resources");
        var chineseKeys = ReadResourceKeys(Path.Combine(resourcesDirectory, "Errors.zh.resx")).ToHashSet(StringComparer.Ordinal);
        var englishKeys = ReadResourceKeys(Path.Combine(resourcesDirectory, "Errors.en.resx")).ToHashSet(StringComparer.Ordinal);
        var errorCodes = typeof(ForumPublishErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => Assert.IsType<string>(field.GetRawConstantValue()))
            .ToArray();

        Assert.NotEmpty(errorCodes);
        Assert.Equal(errorCodes.Length, errorCodes.Distinct(StringComparer.Ordinal).Count());
        foreach (var errorCode in errorCodes)
        {
            var messageKey = ForumPublishErrorCodes.ResolveMessageKey(errorCode);
            Assert.Contains(messageKey, chineseKeys);
            Assert.Contains(messageKey, englishKeys);
        }
    }

    [Fact]
    public void AttachmentErrorCodes_ShouldResolveToChineseAndEnglishApiResources()
    {
        var resourcesDirectory = Path.Combine(FindRepositoryRoot(), "Radish.Api", "Resources");
        var chineseKeys = ReadResourceKeys(Path.Combine(resourcesDirectory, "Errors.zh.resx")).ToHashSet(StringComparer.Ordinal);
        var englishKeys = ReadResourceKeys(Path.Combine(resourcesDirectory, "Errors.en.resx")).ToHashSet(StringComparer.Ordinal);
        var errorCodes = typeof(AttachmentErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => Assert.IsType<string>(field.GetRawConstantValue()))
            .ToArray();

        Assert.NotEmpty(errorCodes);
        Assert.Equal(errorCodes.Length, errorCodes.Distinct(StringComparer.Ordinal).Count());
        foreach (var errorCode in errorCodes)
        {
            var messageKey = AttachmentErrorCodes.ResolveMessageKey(errorCode);
            Assert.Contains(messageKey, chineseKeys);
            Assert.Contains(messageKey, englishKeys);
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Radish.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("无法定位 Radish 仓库根目录。");
    }

    private static string[] ReadResourceKeys(string filePath)
    {
        return XDocument.Load(filePath)
            .Root!
            .Elements("data")
            .Select(element => element.Attribute("name")?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
