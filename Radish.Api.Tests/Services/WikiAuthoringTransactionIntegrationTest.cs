using System;
using System.Threading.Tasks;
using AutoMapper;
using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.CoreTool;
using Radish.Common.OptionTool;
using Radish.Extension.AopExtension;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Repository;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Service;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class WikiAuthoringTransactionIntegrationTest
{
    [Fact]
    public async Task AdminApply_ThroughAop_ShouldRollbackDocumentDraftAndRevisionWhenEvidenceWriteFails()
    {
        new ServiceCollection().ConfigureApplication();
        using var db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main",
            DbType = DbType.Sqlite,
            ConnectionString = "Data Source=:memory:",
            IsAutoCloseConnection = false,
            InitKeyType = InitKeyType.Attribute
        });
        db.CodeFirst.InitTables<WikiDocument>();
        db.CodeFirst.InitTables<WikiDocumentDraft>();
        db.CodeFirst.InitTables<WikiDocumentRevision>();
        db.CodeFirst.InitTables<WikiDocumentCollaborator>();
        db.CodeFirst.InitTables<WikiDocumentReviewEvent>();
        db.CodeFirst.InitTables<Attachment>();
        db.CodeFirst.InitTables<User>();
        SeedSubmittedDraft(db);

        var unitOfWork = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
        var documentRepository = new WikiDocumentRepository(unitOfWork);
        IBaseRepository<WikiDocumentRevision> revisionRepository =
            new BaseRepository<WikiDocumentRevision>(unitOfWork);
        IBaseRepository<Attachment> attachmentRepository =
            new BaseRepository<Attachment>(unitOfWork);
        IBaseRepository<WikiDocumentDraft> draftRepository =
            new BaseRepository<WikiDocumentDraft>(unitOfWork);
        IBaseRepository<WikiDocumentCollaborator> collaboratorRepository =
            new BaseRepository<WikiDocumentCollaborator>(unitOfWork);
        IBaseRepository<WikiDocumentReviewEvent> reviewEventRepository =
            new BaseRepository<WikiDocumentReviewEvent>(unitOfWork);
        IBaseRepository<User> userRepository =
            new BaseRepository<User>(unitOfWork);
        var attachmentReferences = new Mock<IWikiAttachmentReferenceRepository>(MockBehavior.Strict);
        attachmentReferences
            .Setup(repository => repository.SyncSourceAsync(
                It.IsAny<WikiAttachmentReferenceSyncCommand>()))
            .Returns(Task.CompletedTask);
        attachmentReferences
            .Setup(repository => repository.AppendRevisionAsync(
                It.IsAny<WikiAttachmentReferenceSyncCommand>()))
            .ThrowsAsync(new InvalidOperationException("revision evidence unavailable"));
        var target = new WikiDocumentService(
            Mock.Of<IMapper>(),
            documentRepository,
            revisionRepository,
            attachmentRepository,
            attachmentReferences.Object,
            Mock.Of<IConsoleAuthorizationService>(),
            Options.Create(new DocumentOptions()),
            draftRepository,
            collaboratorRepository,
            reviewEventRepository,
            userRepository);
        var proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget<IWikiDocumentService>(
            target,
            new TranAop(unitOfWork, NullLogger<TranAop>.Instance));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            proxy.AdminReviewDraftAsync(
                30001,
                new ReviewWikiDraftDto
                {
                    Action = WikiDocumentReviewActions.Apply,
                    ExpectedDraftVersion = 1,
                    ExpectedDocumentVersion = 0
                },
                90001,
                "Reviewer",
                0));

        Assert.Equal("revision evidence unavailable", exception.Message);
        Assert.Equal(0, unitOfWork.TranCount);
        var document = db.Queryable<WikiDocument>().InSingle(20001);
        var draft = db.Queryable<WikiDocumentDraft>().InSingle(30001);
        Assert.Equal(0, document.Version);
        Assert.Equal("formal", document.MarkdownContent);
        Assert.Equal(30001, document.ActiveDraftId);
        Assert.Equal((int)WikiDocumentDraftState.Submitted, draft.ReviewState);
        Assert.Equal(1, draft.DraftVersion);
        Assert.Equal(0, db.Queryable<WikiDocumentRevision>().Count());
        Assert.Equal(0, db.Queryable<WikiDocumentReviewEvent>().Count());
    }

    private static void SeedSubmittedDraft(SqlSugarScope db)
    {
        db.Insertable(new WikiDocument
        {
            Id = 20001,
            TenantId = 0,
            Title = "Formal",
            Slug = "formal",
            MarkdownContent = "formal",
            Status = (int)WikiDocumentStatusEnum.Draft,
            SourceType = "Custom",
            Version = 0,
            OwnerUserId = 10001,
            ActiveDraftId = 30001,
            CreateId = 10001,
            CreateBy = "Owner"
        }).ExecuteCommand();
        db.Insertable(new WikiDocumentDraft
        {
            Id = 30001,
            TenantId = 0,
            DocumentId = 20001,
            BaseDocumentVersion = 0,
            DraftVersion = 1,
            Title = "Draft",
            Slug = "draft",
            MarkdownContent = "draft body",
            ReviewState = (int)WikiDocumentDraftState.Submitted,
            CreateId = 10001,
            CreateBy = "Owner"
        }).ExecuteCommand();
    }
}
