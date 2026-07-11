#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public class WikiDocumentAccessServiceTests
{
    [Fact(DisplayName = "公开读取契约只返回已发布公开且未删除文档")]
    public async Task PublicReadMethods_ShouldOnlyExposePublishedPublicDocuments()
    {
        List<WikiDocument> documents =
        [
            CreateDocument(1, "public-root", WikiDocumentStatusEnum.Published, WikiDocumentVisibilityEnum.Public),
            CreateDocument(2, "public-child", WikiDocumentStatusEnum.Published, WikiDocumentVisibilityEnum.Public, parentId: 1),
            CreateDocument(3, "draft-doc", WikiDocumentStatusEnum.Draft, WikiDocumentVisibilityEnum.Public),
            CreateDocument(4, "auth-doc", WikiDocumentStatusEnum.Published, WikiDocumentVisibilityEnum.Authenticated),
            CreateDocument(5, "restricted-doc", WikiDocumentStatusEnum.Published, WikiDocumentVisibilityEnum.Restricted),
            CreateDocument(6, "deleted-doc", WikiDocumentStatusEnum.Published, WikiDocumentVisibilityEnum.Public, isDeleted: true),
        ];
        var service = CreateService(documents);

        var firstPage = await service.GetPublicListAsync(pageIndex: 1, pageSize: 1);
        var tree = await service.GetPublicTreeAsync();

        firstPage.DataCount.ShouldBe(2);
        firstPage.PageCount.ShouldBe(2);
        firstPage.Data.Count.ShouldBe(1);
        tree.Count.ShouldBe(1);
        tree[0].VoSlug.ShouldBe("public-root");
        tree[0].VoChildren.Count.ShouldBe(1);
        tree[0].VoChildren[0].VoSlug.ShouldBe("public-child");

        (await service.GetPublicBySlugAsync(" PUBLIC-ROOT ")).ShouldNotBeNull();
        (await service.GetPublicBySlugAsync("draft-doc")).ShouldBeNull();
        (await service.GetPublicBySlugAsync("auth-doc")).ShouldBeNull();
        (await service.GetPublicBySlugAsync("restricted-doc")).ShouldBeNull();
        (await service.GetPublicBySlugAsync("deleted-doc")).ShouldBeNull();
    }

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
        return CreateService([document], permissionKeys);
    }

    private static WikiDocumentService CreateService(
        IReadOnlyCollection<WikiDocument> documents,
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
        mapper
            .Setup(instance => instance.Map<List<WikiDocumentVo>>(It.IsAny<object>()))
            .Returns<object>(source => ((IEnumerable<WikiDocument>)source).Select(MapDocument).ToList());

        var wikiDocumentRepository = new Mock<IWikiDocumentRepository>();
        wikiDocumentRepository
            .Setup(repository => repository.QueryByIdAsync(It.IsAny<long>()))
            .ReturnsAsync((long id) => documents.FirstOrDefault(document => document.Id == id));
        wikiDocumentRepository
            .Setup(repository => repository.QueryByIdIncludingDeletedAsync(It.IsAny<long>()))
            .ReturnsAsync((long id) => documents.FirstOrDefault(document => document.Id == id));
        wikiDocumentRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<WikiDocument, bool>>>()))
            .ReturnsAsync((Expression<Func<WikiDocument, bool>> expression) => documents.FirstOrDefault(CompilePredicate(expression)));
        wikiDocumentRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<WikiDocument, bool>>?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<WikiDocument, object>>?>(),
                It.IsAny<OrderByType>(),
                It.IsAny<Expression<Func<WikiDocument, object>>?>(),
                It.IsAny<OrderByType>()))
            .ReturnsAsync((
                Expression<Func<WikiDocument, bool>>? expression,
                int pageIndex,
                int pageSize,
                Expression<Func<WikiDocument, object>>? _,
                OrderByType _,
                Expression<Func<WikiDocument, object>>? _,
                OrderByType _) =>
            {
                var filtered = expression == null ? documents.ToList() : documents.Where(CompilePredicate(expression)).ToList();
                return (filtered.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList(), filtered.Count);
            });
        wikiDocumentRepository
            .Setup(repository => repository.QueryWithOrderAsync(
                It.IsAny<Expression<Func<WikiDocument, bool>>?>(),
                It.IsAny<Expression<Func<WikiDocument, object>>>(),
                It.IsAny<OrderByType>(),
                It.IsAny<int>()))
            .ReturnsAsync((
                Expression<Func<WikiDocument, bool>>? expression,
                Expression<Func<WikiDocument, object>> _,
                OrderByType _,
                int _) => expression == null ? documents.ToList() : documents.Where(CompilePredicate(expression)).ToList());

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

    private static WikiDocument CreateDocument(
        long id,
        string slug,
        WikiDocumentStatusEnum status,
        WikiDocumentVisibilityEnum visibility,
        long? parentId = null,
        bool isDeleted = false)
    {
        return new WikiDocument
        {
            Id = id,
            Title = slug,
            Slug = slug,
            MarkdownContent = $"# {slug}",
            ParentId = parentId,
            Status = (int)status,
            Visibility = (int)visibility,
            SourceType = "Custom",
            IsDeleted = isDeleted,
        };
    }

    private static WikiDocumentVo MapDocument(WikiDocument source)
    {
        return new WikiDocumentVo
        {
            VoId = source.Id,
            VoTitle = source.Title,
            VoSlug = source.Slug,
            VoParentId = source.ParentId,
            VoSort = source.Sort,
            VoStatus = source.Status,
            VoVisibility = source.Visibility,
            VoSourceType = source.SourceType,
            VoIsDeleted = source.IsDeleted,
            VoCreateTime = source.CreateTime,
        };
    }

    private static Func<WikiDocument, bool> CompilePredicate(Expression<Func<WikiDocument, bool>> expression)
    {
        var parameter = Expression.Parameter(typeof(WikiDocument), "document");
        var body = new WikiDocumentParameterVisitor(parameter).Visit(expression.Body)!;
        return Expression.Lambda<Func<WikiDocument, bool>>(body, parameter).Compile();
    }

    private sealed class WikiDocumentParameterVisitor(ParameterExpression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node.Type == typeof(WikiDocument) ? replacement : base.VisitParameter(node);
        }
    }
}
