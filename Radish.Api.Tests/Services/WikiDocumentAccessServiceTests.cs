#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

public class WikiDocumentAccessServiceTests
{
    [Fact(DisplayName = "匿名用户可读取公开文档")]
    public async Task GetDetailAsync_ShouldAllowAnonymous_WhenDocumentIsPublic()
    {
        var service = CreateService(
            new WikiDocument
            {
                Id = 1,
                Title = "公开文档",
                Slug = "public-doc",
                MarkdownContent = "# Public",
                Status = (int)WikiDocumentStatusEnum.Published,
                Visibility = (int)WikiDocumentVisibilityEnum.Public,
                SourceType = "Custom",
                IsDeleted = false
            });

        var result = await service.GetDetailAsync(1, isAuthenticated: false);

        result.ShouldNotBeNull();
        result.VoTitle.ShouldBe("公开文档");
    }

    [Fact(DisplayName = "匿名用户不可读取登录可见文档")]
    public async Task GetDetailAsync_ShouldBlockAnonymous_WhenDocumentRequiresLogin()
    {
        var service = CreateService(
            new WikiDocument
            {
                Id = 2,
                Title = "登录可见",
                Slug = "auth-doc",
                MarkdownContent = "# Auth",
                Status = (int)WikiDocumentStatusEnum.Published,
                Visibility = (int)WikiDocumentVisibilityEnum.Authenticated,
                SourceType = "Custom",
                IsDeleted = false
            });

        var result = await service.GetDetailAsync(2, isAuthenticated: false);

        result.ShouldBeNull();
    }

    [Fact(DisplayName = "受限文档应允许匹配角色访问")]
    public async Task GetDetailAsync_ShouldAllowRole_WhenDocumentIsRestricted()
    {
        var service = CreateService(
            new WikiDocument
            {
                Id = 3,
                Title = "管理员文档",
                Slug = "admin-doc",
                MarkdownContent = "# Admin",
                Status = (int)WikiDocumentStatusEnum.Published,
                Visibility = (int)WikiDocumentVisibilityEnum.Restricted,
                AllowedRoles = "|admin|",
                SourceType = "Custom",
                IsDeleted = false
            });

        var result = await service.GetDetailAsync(3, isAuthenticated: true, roleNames: ["Admin"]);

        result.ShouldNotBeNull();
        result.VoTitle.ShouldBe("管理员文档");
    }

    [Fact(DisplayName = "受限文档应允许匹配权限访问")]
    public async Task GetDetailAsync_ShouldAllowPermission_WhenDocumentIsRestricted()
    {
        var service = CreateService(
            new WikiDocument
            {
                Id = 4,
                Title = "权限文档",
                Slug = "permission-doc",
                MarkdownContent = "# Permission",
                Status = (int)WikiDocumentStatusEnum.Published,
                Visibility = (int)WikiDocumentVisibilityEnum.Restricted,
                AllowedPermissions = "|wiki.private.read|",
                SourceType = "Custom",
                IsDeleted = false
            },
            permissionKeys: ["wiki.private.read"]);

        var result = await service.GetDetailAsync(4, isAuthenticated: true, roleNames: ["User"]);

        result.ShouldNotBeNull();
        result.VoTitle.ShouldBe("权限文档");
    }

    private static WikiDocumentService CreateService(
        WikiDocument document,
        IReadOnlyCollection<string>? permissionKeys = null)
    {
        var mapper = new Mock<IMapper>();
        mapper
            .Setup(instance => instance.Map<WikiDocumentDetailVo>(It.IsAny<WikiDocument>()))
            .Returns<WikiDocument>(source => new WikiDocumentDetailVo
            {
                VoId = source.Id,
                VoTitle = source.Title,
                VoSlug = source.Slug,
                VoMarkdownContent = source.MarkdownContent,
                VoStatus = source.Status,
                VoVisibility = source.Visibility,
                VoSourceType = source.SourceType,
                VoCreateTime = source.CreateTime,
            });

        var wikiDocumentRepository = new Mock<IWikiDocumentRepository>();
        wikiDocumentRepository
            .Setup(repository => repository.QueryByIdAsync(document.Id))
            .ReturnsAsync(document);
        wikiDocumentRepository
            .Setup(repository => repository.QueryByIdIncludingDeletedAsync(document.Id))
            .ReturnsAsync(document);

        var revisionRepository = new Mock<IBaseRepository<WikiDocumentRevision>>();
        var consoleAuthorizationService = new Mock<IConsoleAuthorizationService>();
        consoleAuthorizationService
            .Setup(service => service.GetPermissionKeysByRolesAsync(It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync(permissionKeys?.ToList() ?? []);

        return new WikiDocumentService(
            mapper.Object,
            wikiDocumentRepository.Object,
            revisionRepository.Object,
            consoleAuthorizationService.Object,
            Options.Create(new DocumentOptions()));
    }
}
