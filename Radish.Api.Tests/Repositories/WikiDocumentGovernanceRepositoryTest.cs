using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Radish.Common.CoreTool;
using Radish.Common.HttpContextTool;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class WikiDocumentGovernanceRepositoryTest
{
    [Fact]
    public async Task ApplyGovernanceMutationAsync_ShouldCommitDocumentAndEventAtomicallyWithCas()
    {
        EnsurePublicTenantAppContext();
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-wiki-governance-repository-{Guid.NewGuid():N}.db");
        using var db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        var repository = new WikiDocumentRepository(
            new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));

        try
        {
            db.CodeFirst.InitTables<WikiDocument, WikiDocumentGovernanceEvent>();
            db.Insertable(CreateDocument()).ExecuteCommand();
            var command = CreatePublishCommand();

            db.Ado.ExecuteCommand(
                "CREATE TRIGGER fail_wiki_governance_event " +
                "BEFORE INSERT ON \"WikiDocumentGovernanceEvent\" " +
                "BEGIN SELECT RAISE(ABORT, 'forced event failure'); END");
            await Assert.ThrowsAnyAsync<Exception>(() =>
                repository.ApplyGovernanceMutationAsync(command));
            Assert.Equal(0, db.Queryable<WikiDocument>().InSingle(20001).GovernanceVersion);
            Assert.Equal(0, db.Queryable<WikiDocumentGovernanceEvent>().Count());

            db.Ado.ExecuteCommand("DROP TRIGGER fail_wiki_governance_event");
            var result = await repository.ApplyGovernanceMutationAsync(command);

            Assert.Equal(1, result.Document.GovernanceVersion);
            Assert.Equal((int)WikiDocumentStatusEnum.Published, result.Document.Status);
            Assert.Equal(0, result.GovernanceEvent.ExpectedGovernanceVersion);
            Assert.Equal(1, result.GovernanceEvent.ResultGovernanceVersion);
            Assert.Equal("首次发布", result.GovernanceEvent.Reason);
            Assert.Equal(1, db.Queryable<WikiDocumentGovernanceEvent>().Count());

            await Assert.ThrowsAsync<WikiDocumentGovernanceVersionConflictException>(() =>
                repository.ApplyGovernanceMutationAsync(command));
            Assert.Equal(1, db.Queryable<WikiDocumentGovernanceEvent>().Count());

            var wrongDocumentVersion = CreatePublishCommand() with
            {
                ExpectedGovernanceVersion = 1,
                ExpectedDocumentVersion = 2
            };
            await Assert.ThrowsAsync<WikiDocumentContentVersionConflictException>(() =>
                repository.ApplyGovernanceMutationAsync(wrongDocumentVersion));

            var history = await repository.QueryGovernanceHistoryAsync(
                new WikiDocumentGovernanceHistoryQuery(0, 20001, 1, 10));
            Assert.Equal(1, history.Total);
            Assert.Equal(result.GovernanceEvent.Id, Assert.Single(history.Items).Id);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static WikiDocument CreateDocument() => new()
    {
        Id = 20001,
        TenantId = 0,
        Title = "Governed",
        Slug = "governed",
        MarkdownContent = "body",
        Status = (int)WikiDocumentStatusEnum.Draft,
        Visibility = (int)WikiDocumentVisibilityEnum.Authenticated,
        SourceType = "Custom",
        Version = 1,
        GovernanceVersion = 0,
        CreateId = 10001,
        CreateBy = "Tester",
        CreateTime = DateTime.UtcNow
    };

    private static WikiDocumentGovernanceMutationCommand CreatePublishCommand() => new(
        TenantId: 0,
        DocumentId: 20001,
        Action: WikiDocumentGovernanceActions.Publish,
        ExpectedGovernanceVersion: 0,
        ExpectedDocumentVersion: 1,
        TargetStatus: (int)WikiDocumentStatusEnum.Published,
        TargetPublishedAt: DateTime.UtcNow,
        TargetVisibility: (int)WikiDocumentVisibilityEnum.Authenticated,
        TargetAllowedRoles: null,
        TargetAllowedPermissions: null,
        TargetIsDeleted: false,
        TargetDeletedAt: null,
        TargetDeletedBy: null,
        ContentMutation: null,
        SourceRevisionId: null,
        Reason: "首次发布",
        ActorUserId: 10001,
        ActorName: "Tester",
        NowUtc: DateTime.UtcNow);

    private static void EnsurePublicTenantAppContext()
    {
        var currentUserAccessor = new Mock<ICurrentUserAccessor>(MockBehavior.Strict);
        currentUserAccessor
            .SetupGet(accessor => accessor.Current)
            .Returns(CurrentUser.Anonymous);
        var services = new ServiceCollection();
        services.AddSingleton(currentUserAccessor.Object);
        services.ConfigureApplication();
        services.BuildServiceProvider().ConfigureApplication();
        App.IsBuild = true;
    }
}
