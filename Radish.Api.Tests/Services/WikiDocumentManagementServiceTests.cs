#nullable enable

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.Exceptions;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

/// <summary>
/// 文档管理行为回归测试。
/// </summary>
public class WikiDocumentManagementServiceTests
{
    [Fact(DisplayName = "删除文档前应阻止仍有子文档的节点")]
    public async Task DeleteDocumentAsync_ShouldThrow_WhenChildrenExist()
    {
        var repository = new Mock<IWikiDocumentRepository>();
        repository.Setup(r => r.QueryByIdAsync(10)).ReturnsAsync(new WikiDocument
        {
            Id = 10,
            Title = "父文档",
            Slug = "parent",
            SourceType = "Custom",
            Status = 0,
            IsDeleted = false
        });
        repository.Setup(r => r.QueryExistsAsync(It.IsAny<Expression<Func<WikiDocument, bool>>>())).ReturnsAsync(true);

        var service = CreateService(repository);

        var exception = await Should.ThrowAsync<BusinessException>(() => service.DeleteDocumentAsync(
            10,
            new WikiDocumentGovernanceActionDto
            {
                ExpectedGovernanceVersion = 0,
                Reason = "清理父文档"
            },
            1,
            "Tester"));
        exception.Message.ShouldBe("请先处理子文档后再删除当前文档");
    }

    [Fact(DisplayName = "恢复固定文档应被拒绝")]
    public async Task RestoreDocumentAsync_ShouldThrow_WhenDocumentIsBuiltIn()
    {
        var repository = new Mock<IWikiDocumentRepository>();
        repository.Setup(r => r.QueryByIdIncludingDeletedAsync(11)).ReturnsAsync(new WikiDocument
        {
            Id = 11,
            Title = "固定文档",
            Slug = "built-in-doc",
            SourceType = "BuiltIn",
            Status = 1,
            IsDeleted = true
        });

        var service = CreateService(repository);

        var exception = await Should.ThrowAsync<BusinessException>(() => service.RestoreDocumentAsync(
            11,
            new WikiDocumentGovernanceActionDto
            {
                ExpectedGovernanceVersion = 0,
                Reason = "恢复文档"
            },
            1,
            "Tester"));
        exception.Message.ShouldBe("固定文档为只读内容，请修改 Docs 目录中的源文件");
    }

    [Fact(DisplayName = "更新文档时父级不能设置为自身子孙节点")]
    public async Task UpdateDocumentAsync_ShouldThrow_WhenParentIsDescendant()
    {
        var repository = new Mock<IWikiDocumentRepository>();
        repository.Setup(r => r.QueryByIdAsync(1)).ReturnsAsync(new WikiDocument
        {
            Id = 1,
            Title = "根文档",
            Slug = "root",
            MarkdownContent = "old",
            SourceType = "Custom",
            ParentId = null,
            Sort = 0,
            Status = 0,
            Version = 1,
            IsDeleted = false
        });
        repository.Setup(r => r.QueryByIdAsync(3)).ReturnsAsync(new WikiDocument
        {
            Id = 3,
            Title = "孙节点",
            Slug = "child",
            MarkdownContent = "child",
            SourceType = "Custom",
            ParentId = 2,
            Sort = 0,
            Status = 0,
            Version = 1,
            IsDeleted = false
        });
        repository.Setup(r => r.QueryByIdAsync(2)).ReturnsAsync(new WikiDocument
        {
            Id = 2,
            Title = "子节点",
            Slug = "middle",
            MarkdownContent = "middle",
            SourceType = "Custom",
            ParentId = 1,
            Sort = 0,
            Status = 0,
            Version = 1,
            IsDeleted = false
        });
        repository.Setup(r => r.QueryExistsAsync(It.IsAny<Expression<Func<WikiDocument, bool>>>())).ReturnsAsync(false);

        var service = CreateService(repository);
        var dto = new UpdateWikiDocumentDto
        {
            Title = "根文档",
            MarkdownContent = "new",
            ParentId = 3,
            Sort = 10
        };

        var exception = await Should.ThrowAsync<BusinessException>(() => service.UpdateDocumentAsync(1, dto, 1, "Tester"));
        exception.Message.ShouldBe("父级文档不能设置为当前文档的子孙节点");
    }

    [Fact(DisplayName = "更新访问策略只应修改可见性与允许列表")]
    public async Task UpdateAccessPolicyAsync_ShouldOnlyUpdateAccessPolicy()
    {
        var document = new WikiDocument
        {
            Id = 12,
            Title = "治理文档",
            Slug = "governance-doc",
            MarkdownContent = "# old",
            SourceType = "Custom",
            Status = (int)WikiDocumentStatusEnum.Published,
            Visibility = (int)WikiDocumentVisibilityEnum.Authenticated,
            Version = 5,
            GovernanceVersion = 2,
            IsDeleted = false
        };

        var repository = new Mock<IWikiDocumentRepository>();
        repository.Setup(r => r.QueryByIdAsync(12)).ReturnsAsync(document);
        WikiDocumentGovernanceMutationCommand? capturedCommand = null;
        repository
            .Setup(r => r.ApplyGovernanceMutationAsync(It.IsAny<WikiDocumentGovernanceMutationCommand>()))
            .Callback<WikiDocumentGovernanceMutationCommand>(command => capturedCommand = command)
            .ReturnsAsync((WikiDocumentGovernanceMutationCommand command) => CreateGovernanceWriteResult(document, command));

        var service = CreateService(repository);
        var dto = new UpdateWikiDocumentAccessPolicyDto
        {
            Visibility = (int)WikiDocumentVisibilityEnum.Restricted,
            AllowedRoles = ["Admin", "editor"],
            AllowedPermissions = ["console.docs.view"],
            ExpectedGovernanceVersion = 2,
            Reason = "仅允许维护角色访问"
        };

        var result = await service.UpdateAccessPolicyAsync(12, dto, 9, "Governance");

        capturedCommand.ShouldNotBeNull();
        capturedCommand.Action.ShouldBe(WikiDocumentGovernanceActions.UpdateAccessPolicy);
        capturedCommand.ExpectedGovernanceVersion.ShouldBe(2);
        capturedCommand.ExpectedDocumentVersion.ShouldBeNull();
        capturedCommand.TargetVisibility.ShouldBe((int)WikiDocumentVisibilityEnum.Restricted);
        capturedCommand.TargetAllowedRoles.ShouldBe("|admin|editor|");
        capturedCommand.TargetAllowedPermissions.ShouldBe("|console.docs.view|");
        capturedCommand.ContentMutation.ShouldBeNull();
        capturedCommand.Reason.ShouldBe("仅允许维护角色访问");
        result.VoEvent.VoResultGovernanceVersion.ShouldBe(3);
    }

    [Fact(DisplayName = "更新固定文档访问策略应被拒绝")]
    public async Task UpdateAccessPolicyAsync_ShouldThrow_WhenDocumentIsBuiltIn()
    {
        var repository = new Mock<IWikiDocumentRepository>();
        repository.Setup(r => r.QueryByIdAsync(13)).ReturnsAsync(new WikiDocument
        {
            Id = 13,
            Title = "固定文档",
            Slug = "built-in-doc",
            SourceType = "BuiltIn",
            Status = (int)WikiDocumentStatusEnum.Published,
            Visibility = (int)WikiDocumentVisibilityEnum.Public,
            IsDeleted = false
        });

        var service = CreateService(repository);
        var dto = new UpdateWikiDocumentAccessPolicyDto
        {
            Visibility = (int)WikiDocumentVisibilityEnum.Authenticated,
            ExpectedGovernanceVersion = 0,
            Reason = "更新访问策略"
        };

        var exception = await Should.ThrowAsync<BusinessException>(() => service.UpdateAccessPolicyAsync(13, dto, 1, "Tester"));
        exception.Message.ShouldBe("固定文档为只读内容，请修改 Docs 目录中的源文件");
    }

    [Fact(DisplayName = "Revision 附件引用冲突应返回稳定 WikiAttachment 错误")]
    public async Task CreateDocumentAsync_ShouldMapRevisionReferenceConflict()
    {
        var documentRepository = new Mock<IWikiDocumentRepository>();
        documentRepository
            .Setup(repository => repository.QueryExistsAsync(
                It.IsAny<Expression<Func<WikiDocument, bool>>>()))
            .ReturnsAsync(false);
        documentRepository
            .Setup(repository => repository.AddAsync(It.IsAny<WikiDocument>()))
            .ReturnsAsync(20001);
        var revisionRepository = new Mock<IBaseRepository<WikiDocumentRevision>>();
        revisionRepository
            .Setup(repository => repository.AddAsync(It.IsAny<WikiDocumentRevision>()))
            .ReturnsAsync(30001);
        var attachmentReferences = new Mock<IWikiAttachmentReferenceRepository>();
        attachmentReferences
            .Setup(repository => repository.SyncSourceAsync(
                It.IsAny<WikiAttachmentReferenceSyncCommand>()))
            .Returns(Task.CompletedTask);
        attachmentReferences
            .Setup(repository => repository.AppendRevisionAsync(
                It.IsAny<WikiAttachmentReferenceSyncCommand>()))
            .ThrowsAsync(new WikiAttachmentReferenceConflictException());
        var service = CreateService(
            documentRepository,
            revisionRepository,
            attachmentReferences);

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            service.CreateDocumentAsync(
                new CreateWikiDocumentDto
                {
                    Title = "Conflict",
                    MarkdownContent = "body"
                },
                1001,
                "Editor",
                0));

        exception.ErrorCode.ShouldBe(WikiAttachmentErrorCodes.ReferenceConflict);
        exception.MessageKey.ShouldBe("error.wiki_attachment.reference_conflict");
    }

    [Fact(DisplayName = "回滚 Revision 前应重新校验附件是否仍可引用")]
    public async Task RollbackAsync_ShouldRejectUnavailableRevisionAttachment()
    {
        var documentRepository = new Mock<IWikiDocumentRepository>();
        documentRepository
            .Setup(repository => repository.QueryByIdAsync(20001))
            .ReturnsAsync(new WikiDocument
            {
                Id = 20001,
                TenantId = 9,
                Title = "Current",
                Slug = "current",
                MarkdownContent = "current body",
                SourceType = "Custom",
                Version = 2
            });
        var revisionRepository = new Mock<IBaseRepository<WikiDocumentRevision>>();
        revisionRepository
            .Setup(repository => repository.QueryByIdAsync(30001))
            .ReturnsAsync(new WikiDocumentRevision
            {
                Id = 30001,
                TenantId = 9,
                DocumentId = 20001,
                Version = 1,
                Title = "Previous",
                MarkdownContent = "![removed](attachment://40001)"
            });
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>();
        attachmentRepository
            .Setup(repository => repository.QueryAsync(
                It.IsAny<Expression<Func<Attachment, bool>>>()))
            .ReturnsAsync([]);
        var service = CreateService(
            documentRepository,
            revisionRepository,
            new Mock<IWikiAttachmentReferenceRepository>(),
            attachmentRepository);

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            service.RollbackAsync(
                30001,
                new WikiDocumentContentGovernanceActionDto
                {
                    ExpectedGovernanceVersion = 0,
                    ExpectedDocumentVersion = 2,
                    Reason = "恢复上一版本"
                },
                1001,
                "Editor"));

        exception.ErrorCode.ShouldBe(WikiAttachmentErrorCodes.InvalidReference);
        documentRepository.Verify(
            repository => repository.ApplyGovernanceMutationAsync(
                It.IsAny<WikiDocumentGovernanceMutationCommand>()),
            Times.Never);
    }

    [Fact(DisplayName = "发布竞争写入应映射为稳定治理版本冲突")]
    public async Task PublishAsync_ShouldMapRepositoryGovernanceVersionConflict()
    {
        var repository = new Mock<IWikiDocumentRepository>();
        repository.Setup(candidate => candidate.QueryByIdAsync(20001)).ReturnsAsync(new WikiDocument
        {
            Id = 20001,
            TenantId = 0,
            Title = "Draft",
            Slug = "draft",
            MarkdownContent = "body",
            SourceType = "Custom",
            Status = (int)WikiDocumentStatusEnum.Draft,
            Visibility = (int)WikiDocumentVisibilityEnum.Authenticated,
            Version = 3,
            GovernanceVersion = 4
        });
        repository
            .Setup(candidate => candidate.ApplyGovernanceMutationAsync(
                It.IsAny<WikiDocumentGovernanceMutationCommand>()))
            .ThrowsAsync(new WikiDocumentGovernanceVersionConflictException());
        var service = CreateService(repository);

        var exception = await Should.ThrowAsync<BusinessException>(() => service.PublishAsync(
            20001,
            new WikiDocumentContentGovernanceActionDto
            {
                ExpectedGovernanceVersion = 4,
                ExpectedDocumentVersion = 3,
                Reason = "准备发布"
            },
            1001,
            "Editor"));

        exception.StatusCode.ShouldBe(409);
        exception.ErrorCode.ShouldBe("Wiki.GovernanceVersionConflict");
        exception.MessageKey.ShouldBe("error.wiki.governance_version_conflict");
    }

    [Fact(DisplayName = "治理历史应限制分页并映射追加式事件")]
    public async Task GetGovernanceHistoryAsync_ShouldClampPagingAndMapEvents()
    {
        var repository = new Mock<IWikiDocumentRepository>();
        repository.Setup(candidate => candidate.QueryByIdIncludingDeletedAsync(20001))
            .ReturnsAsync(new WikiDocument { Id = 20001, TenantId = 9 });
        WikiDocumentGovernanceHistoryQuery? capturedQuery = null;
        repository
            .Setup(candidate => candidate.QueryGovernanceHistoryAsync(
                It.IsAny<WikiDocumentGovernanceHistoryQuery>()))
            .Callback<WikiDocumentGovernanceHistoryQuery>(query => capturedQuery = query)
            .ReturnsAsync((
                (IReadOnlyList<WikiDocumentGovernanceEvent>)
                [new WikiDocumentGovernanceEvent
                {
                    Id = 30001,
                    TenantId = 9,
                    DocumentId = 20001,
                    Action = WikiDocumentGovernanceActions.Archive,
                    FromDocumentVersion = 3,
                    ToDocumentVersion = 3,
                    ExpectedGovernanceVersion = 4,
                    ResultGovernanceVersion = 5,
                    Reason = "阶段归档",
                    ActorUserId = 1001,
                    ActorName = "Editor"
                }],
                121));
        var service = CreateService(repository);

        var result = await service.GetGovernanceHistoryAsync(20001, 0, 999);

        capturedQuery.ShouldNotBeNull();
        capturedQuery.TenantId.ShouldBe(9);
        capturedQuery.PageIndex.ShouldBe(1);
        capturedQuery.PageSize.ShouldBe(100);
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(100);
        result.DataCount.ShouldBe(121);
        result.PageCount.ShouldBe(2);
        Assert.Single(result.Data).VoResultGovernanceVersion.ShouldBe(5);
    }

    private static WikiDocumentService CreateService(Mock<IWikiDocumentRepository> wikiDocumentRepository)
    {
        return CreateService(
            wikiDocumentRepository,
            new Mock<IBaseRepository<WikiDocumentRevision>>(),
            new Mock<IWikiAttachmentReferenceRepository>());
    }

    private static WikiDocumentService CreateService(
        Mock<IWikiDocumentRepository> wikiDocumentRepository,
        Mock<IBaseRepository<WikiDocumentRevision>> revisionRepository,
        Mock<IWikiAttachmentReferenceRepository> attachmentReferences,
        Mock<IBaseRepository<Attachment>>? attachmentRepository = null)
    {
        var mapper = new Mock<IMapper>();
        mapper
            .Setup(candidate => candidate.Map<WikiDocumentDetailVo>(It.IsAny<WikiDocument>()))
            .Returns((WikiDocument document) => new WikiDocumentDetailVo
            {
                VoId = document.Id,
                VoTitle = document.Title,
                VoMarkdownContent = document.MarkdownContent,
                VoVersion = document.Version,
                VoGovernanceVersion = document.GovernanceVersion
            });
        var consoleAuthorizationService = new Mock<IConsoleAuthorizationService>();
        consoleAuthorizationService
            .Setup(service => service.GetPermissionKeysByRolesAsync(It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync([]);
        return new WikiDocumentService(
            mapper.Object,
            wikiDocumentRepository.Object,
            revisionRepository.Object,
            attachmentRepository?.Object ?? Mock.Of<IBaseRepository<Attachment>>(),
            attachmentReferences.Object,
            consoleAuthorizationService.Object,
            Options.Create(new DocumentOptions()));
    }

    private static WikiDocumentGovernanceWriteResult CreateGovernanceWriteResult(
        WikiDocument document,
        WikiDocumentGovernanceMutationCommand command)
    {
        var resultVersion = command.ExpectedGovernanceVersion + 1;
        var resultDocument = new WikiDocument
        {
            Id = document.Id,
            TenantId = document.TenantId,
            Title = command.ContentMutation?.Title ?? document.Title,
            MarkdownContent = command.ContentMutation?.MarkdownContent ?? document.MarkdownContent,
            Version = command.ContentMutation?.ResultDocumentVersion ?? document.Version,
            GovernanceVersion = resultVersion,
            Status = command.TargetStatus,
            Visibility = command.TargetVisibility,
            AllowedRoles = command.TargetAllowedRoles,
            AllowedPermissions = command.TargetAllowedPermissions,
            IsDeleted = command.TargetIsDeleted,
            SourceType = document.SourceType
        };
        return new WikiDocumentGovernanceWriteResult(
            resultDocument,
            new WikiDocumentGovernanceEvent
            {
                Id = 90001,
                TenantId = document.TenantId,
                DocumentId = document.Id,
                Action = command.Action,
                FromStatus = document.Status,
                ToStatus = command.TargetStatus,
                FromVisibility = document.Visibility,
                ToVisibility = command.TargetVisibility,
                FromDocumentVersion = document.Version,
                ToDocumentVersion = resultDocument.Version,
                ExpectedGovernanceVersion = command.ExpectedGovernanceVersion,
                ResultGovernanceVersion = resultVersion,
                Reason = command.Reason,
                ActorUserId = command.ActorUserId,
                ActorName = command.ActorName,
                CreateTime = command.NowUtc
            });
    }
}
