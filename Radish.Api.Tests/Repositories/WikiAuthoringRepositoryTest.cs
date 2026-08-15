using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.CustomEnum;
using Radish.Api.Tests.TestCollections;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

[Collection(PostgreSqlIntegrationCollection.CollectionName)]
public sealed class WikiAuthoringRepositoryTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable =
        "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public async Task QueryLatestTerminalDraftsAsync_ShouldReturnOnlyHighestDraftIdPerDocument()
    {
        var (path, db, repository) = CreateRepository();
        using (db)
        {
            try
            {
                var firstDraft = Seed(db);
                firstDraft.ReviewState = (int)WikiDocumentDraftState.Applied;
                db.Updateable(firstDraft).ExecuteCommand();
                db.Insertable(new WikiDocumentDraft
                {
                    Id = 30002,
                    TenantId = 0,
                    DocumentId = 20001,
                    BaseDocumentVersion = 1,
                    DraftVersion = 3,
                    Title = "Latest",
                    Slug = "latest",
                    MarkdownContent = "latest body",
                    ReviewState = (int)WikiDocumentDraftState.Withdrawn,
                    CreateId = 10001,
                    CreateBy = "Author"
                }).ExecuteCommand();
                db.Insertable(new WikiDocument
                {
                    Id = 20002,
                    TenantId = 0,
                    Title = "Second",
                    Slug = "second",
                    MarkdownContent = "formal",
                    SourceType = "Custom",
                    Version = 1,
                    OwnerUserId = 10001,
                    CreateId = 10001,
                    CreateBy = "Author"
                }).ExecuteCommand();
                db.Insertable(new WikiDocumentDraft
                {
                    Id = 31001,
                    TenantId = 0,
                    DocumentId = 20002,
                    BaseDocumentVersion = 1,
                    DraftVersion = 2,
                    Title = "Second terminal",
                    Slug = "second-terminal",
                    MarkdownContent = "body",
                    ReviewState = (int)WikiDocumentDraftState.Rejected,
                    CreateId = 10001,
                    CreateBy = "Author"
                }).ExecuteCommand();

                var single = await repository.QueryLatestTerminalDraftAsync(20001);
                var batch = await repository.QueryLatestTerminalDraftEvidenceAsync([20001, 20002]);

                Assert.NotNull(single);
                Assert.Equal(30002, single.Id);
                Assert.Equal([30002L, 31001L], batch.Select(draft => draft.DraftId).Order());
                Assert.DoesNotContain(
                    typeof(WikiTerminalDraftEvidence).GetProperties(),
                    property => property.Name == nameof(WikiDocumentDraft.MarkdownContent));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task QueryAuthorPageAsync_ShouldFilterScopeAndDraftStageWithStablePaging()
    {
        var (path, db, repository) = CreateRepository();
        using (db)
        {
            try
            {
                db.CodeFirst.InitTables<WikiDocument>();
                db.CodeFirst.InitTables<WikiDocumentDraft>();
                db.CodeFirst.InitTables<WikiDocumentCollaborator>();
                var now = DateTime.UtcNow;
                db.Insertable(new[]
                {
                    new WikiDocument
                    {
                        Id = 21001, TenantId = 0, Title = "Owned editable", Slug = "owned-editable",
                        MarkdownContent = "", SourceType = "Custom", OwnerUserId = 10001,
                        ActiveDraftId = 31001, ModifyTime = now.AddMinutes(-4), CreateId = 10001, CreateBy = "Owner"
                    },
                    new WikiDocument
                    {
                        Id = 21002, TenantId = 0, Title = "Owned submitted", Slug = "owned-submitted",
                        MarkdownContent = "", SourceType = "Custom", OwnerUserId = 10001,
                        ActiveDraftId = 31002, ModifyTime = now.AddMinutes(-2), CreateId = 10001, CreateBy = "Owner"
                    },
                    new WikiDocument
                    {
                        Id = 21003, TenantId = 0, Title = "Collaborating terminal", Slug = "collaborating-terminal",
                        MarkdownContent = "", SourceType = "Custom", OwnerUserId = 10002,
                        ModifyTime = now.AddMinutes(-3), CreateId = 10002, CreateBy = "Owner"
                    },
                    new WikiDocument
                    {
                        Id = 21004, TenantId = 0, Title = "Collaborating none", Slug = "collaborating-none",
                        MarkdownContent = "", SourceType = "Custom", OwnerUserId = 10002,
                        ModifyTime = now.AddMinutes(-1), CreateId = 10002, CreateBy = "Owner"
                    },
                    new WikiDocument
                    {
                        Id = 21005, TenantId = 0, Title = "Unrelated", Slug = "unrelated",
                        MarkdownContent = "", SourceType = "Custom", OwnerUserId = 10003,
                        ModifyTime = now, CreateId = 10003, CreateBy = "Owner"
                    }
                }).ExecuteCommand();
                db.Insertable(new[]
                {
                    new WikiDocumentDraft
                    {
                        Id = 31001, TenantId = 0, DocumentId = 21001, BaseDocumentVersion = 1,
                        Title = "Owned editable", Slug = "owned-editable", MarkdownContent = "body",
                        ReviewState = (int)WikiDocumentDraftState.Editing, CreateId = 10001, CreateBy = "Owner"
                    },
                    new WikiDocumentDraft
                    {
                        Id = 31002, TenantId = 0, DocumentId = 21002, BaseDocumentVersion = 1,
                        Title = "Owned submitted", Slug = "owned-submitted", MarkdownContent = "body",
                        ReviewState = (int)WikiDocumentDraftState.Submitted, CreateId = 10001, CreateBy = "Owner"
                    },
                    new WikiDocumentDraft
                    {
                        Id = 31003, TenantId = 0, DocumentId = 21003, BaseDocumentVersion = 1,
                        Title = "Collaborating terminal", Slug = "collaborating-terminal", MarkdownContent = "body",
                        ReviewState = (int)WikiDocumentDraftState.Applied, CreateId = 10002, CreateBy = "Owner"
                    }
                }).ExecuteCommand();
                db.Insertable(new[]
                {
                    new WikiDocumentCollaborator
                    {
                        Id = 41003, TenantId = 0, DocumentId = 21003, UserId = 10001,
                        InviteState = (int)WikiDocumentCollaboratorState.Accepted,
                        InvitedBy = 10002, CreateId = 10002, CreateBy = "Owner"
                    },
                    new WikiDocumentCollaborator
                    {
                        Id = 41004, TenantId = 0, DocumentId = 21004, UserId = 10001,
                        InviteState = (int)WikiDocumentCollaboratorState.Pending,
                        InvitedBy = 10002, CreateId = 10002, CreateBy = "Owner"
                    }
                }).ExecuteCommand();

                var firstPage = await repository.QueryAuthorPageAsync(new WikiAuthorDocumentPageQuery(
                    10001, WikiAuthorDocumentScope.All, WikiAuthorDraftStage.All, 1, 2));
                var secondPage = await repository.QueryAuthorPageAsync(new WikiAuthorDocumentPageQuery(
                    10001, WikiAuthorDocumentScope.All, WikiAuthorDraftStage.All, 2, 2));
                var ownedEditable = await repository.QueryAuthorPageAsync(new WikiAuthorDocumentPageQuery(
                    10001, WikiAuthorDocumentScope.Owned, WikiAuthorDraftStage.Editable, 1, 20));
                var collaboratingTerminal = await repository.QueryAuthorPageAsync(new WikiAuthorDocumentPageQuery(
                    10001, WikiAuthorDocumentScope.Collaborating, WikiAuthorDraftStage.Terminal, 1, 20));
                var collaboratingNone = await repository.QueryAuthorPageAsync(new WikiAuthorDocumentPageQuery(
                    10001, WikiAuthorDocumentScope.Collaborating, WikiAuthorDraftStage.None, 1, 20));

                Assert.Equal(4, firstPage.totalCount);
                Assert.Equal([21004L, 21002L], firstPage.data.Select(document => document.Id));
                Assert.Equal([21003L, 21001L], secondPage.data.Select(document => document.Id));
                Assert.Equal(21001, Assert.Single(ownedEditable.data).Id);
                Assert.Equal(21003, Assert.Single(collaboratingTerminal.data).Id);
                Assert.Equal(21004, Assert.Single(collaboratingNone.data).Id);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task QueryLatestTerminalDraftsAsync_ShouldTranslateOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 Wiki 终态证据 PostgreSQL 测试");

        var schema = $"wiki_terminal_evidence_{Guid.NewGuid():N}";
        using var adminDb = PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = adminConnectionString!,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString =
                $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = PostgreSqlIntegrationSqlSugarFactory.CreateScope(new ConnectionConfig
            {
                ConfigId = "main",
                ConnectionString = connectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
            var firstDraft = Seed(db);
            firstDraft.ReviewState = (int)WikiDocumentDraftState.Applied;
            db.Updateable(firstDraft).ExecuteCommand();
            db.Insertable(new WikiDocumentDraft
            {
                Id = 30002,
                TenantId = 0,
                DocumentId = 20001,
                BaseDocumentVersion = 1,
                DraftVersion = 2,
                Title = "Latest",
                Slug = "latest",
                MarkdownContent = "latest body",
                ReviewState = (int)WikiDocumentDraftState.Withdrawn,
                CreateId = 10001,
                CreateBy = "Author"
            }).ExecuteCommand();
            var repository = new WikiDocumentRepository(
                new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));

            var single = await repository.QueryLatestTerminalDraftAsync(20001);
            var batch = await repository.QueryLatestTerminalDraftEvidenceAsync([20001]);

            Assert.Equal(30002, single!.Id);
            Assert.Equal(30002, Assert.Single(batch).DraftId);
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync(
                $"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    [Fact]
    public async Task SaveDraftAsync_ShouldUseDraftVersionCompareAndSet()
    {
        var (path, db, repository) = CreateRepository();
        using (db)
        {
            try
            {
                Seed(db);
                var command = new WikiDraftSaveCommand(
                    30001, 0, 1, "Updated", "updated", null, "new body", null, null, "summary",
                    10001, "Author", DateTime.UtcNow);

                Assert.Equal(1, await repository.SaveDraftAsync(command));
                Assert.Equal(0, await repository.SaveDraftAsync(command));
                var stored = db.Queryable<WikiDocumentDraft>().Single();
                Assert.Equal(2, stored.DraftVersion);
                Assert.Equal("new body", stored.MarkdownContent);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ApplyDraftToDocumentAsync_ShouldUseFormalVersionAndActiveDraftCompareAndSet()
    {
        var (path, db, repository) = CreateRepository();
        using (db)
        {
            try
            {
                var draft = Seed(db);
                var command = new WikiDraftApplyCommand(
                    20001, 0, draft, null, 90001, "Reviewer", DateTime.UtcNow);

                Assert.Equal(1, await repository.ApplyDraftToDocumentAsync(command));
                Assert.Equal(0, await repository.ApplyDraftToDocumentAsync(command));
                var stored = db.Queryable<WikiDocument>().Single();
                Assert.Equal(1, stored.Version);
                Assert.Equal(draft.MarkdownContent, stored.MarkdownContent);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ApplyDraftToDocumentAsync_ShouldRejectFormalVersionDriftFromDraftBase()
    {
        var (path, db, repository) = CreateRepository();
        using (db)
        {
            try
            {
                var draft = Seed(db);
                db.Updateable<WikiDocument>()
                    .SetColumns(document => new WikiDocument { Version = 1 })
                    .Where(document => document.Id == 20001)
                    .ExecuteCommand();
                var command = new WikiDraftApplyCommand(
                    20001, 0, draft, null,
                    90001, "Reviewer", DateTime.UtcNow);

                Assert.Equal(0, await repository.ApplyDraftToDocumentAsync(command));
                var stored = db.Queryable<WikiDocument>().Single();
                Assert.Equal(1, stored.Version);
                Assert.Equal(string.Empty, stored.MarkdownContent);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task PurgeTerminalDraftPayloadsAsync_ShouldOnlyClearExpiredTerminalPayloads()
    {
        var (path, db, repository) = CreateRepository();
        using (db)
        {
            try
            {
                var draft = Seed(db);
                draft.ReviewState = (int)WikiDocumentDraftState.Applied;
                draft.ReviewedAt = DateTime.UtcNow.AddDays(-91);
                draft.ModifyTime = draft.ReviewedAt;
                db.Updateable(draft).ExecuteCommand();
                db.Insertable(new WikiAttachmentReference
                {
                    Id = 50001,
                    TenantId = 0,
                    DocumentId = draft.DocumentId,
                    AttachmentId = 60001,
                    ReferenceKind = (int)WikiAttachmentReferenceKind.DraftContent,
                    ReferenceSourceId = draft.Id,
                    CreateTime = DateTime.UtcNow,
                    CreateBy = "Author",
                    CreateId = 10001
                }).ExecuteCommand();

                var now = DateTime.UtcNow;
                Assert.Equal(1, await repository.PurgeTerminalDraftPayloadsAsync(now.AddDays(-90), 100, now));
                Assert.Equal(0, await repository.PurgeTerminalDraftPayloadsAsync(now.AddDays(-90), 100, now));

                var stored = db.Queryable<WikiDocumentDraft>().Single();
                Assert.Equal(string.Empty, stored.MarkdownContent);
                Assert.Equal(now, stored.PayloadPurgedAt);
                Assert.True(db.Queryable<WikiAttachmentReference>().Single().IsDeleted);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task PurgeTerminalDraftPayloadsAsync_ShouldUseRecentWithdrawTransitionTime()
    {
        var (path, db, repository) = CreateRepository();
        using (db)
        {
            try
            {
                var draft = Seed(db);
                var oldReviewTime = DateTime.UtcNow.AddDays(-100);
                draft.ReviewState = (int)WikiDocumentDraftState.ChangesRequested;
                draft.DraftVersion = 2;
                draft.ReviewedAt = oldReviewTime;
                draft.ModifyTime = oldReviewTime;
                db.Updateable(draft).ExecuteCommand();
                var withdrawnAt = DateTime.UtcNow;
                Assert.Equal(1, await repository.TransitionDraftAsync(new WikiDraftTransitionCommand(
                    draft.Id,
                    draft.TenantId,
                    2,
                    [(int)WikiDocumentDraftState.ChangesRequested],
                    (int)WikiDocumentDraftState.Withdrawn,
                    null,
                    null,
                    10001,
                    "Author",
                    withdrawnAt)));

                Assert.Equal(0, await repository.PurgeTerminalDraftPayloadsAsync(
                    withdrawnAt.AddDays(-90),
                    100,
                    withdrawnAt));
                var stored = db.Queryable<WikiDocumentDraft>().Single();
                Assert.Null(stored.PayloadPurgedAt);
                Assert.Equal("body", stored.MarkdownContent);
                Assert.Equal(oldReviewTime, stored.ReviewedAt);
                Assert.Equal(withdrawnAt, stored.ModifyTime);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task TransitionCollaboratorAsync_ShouldAllowOnlyOnePendingResponse()
    {
        var (path, db, repository) = CreateRepository();
        using (db)
        {
            try
            {
                db.CodeFirst.InitTables<WikiDocumentCollaborator>();
                db.Insertable(new WikiDocumentCollaborator
                {
                    Id = 40001,
                    TenantId = 0,
                    DocumentId = 20001,
                    UserId = 10002,
                    InviteState = (int)WikiDocumentCollaboratorState.Pending,
                    InvitedBy = 10001,
                    InvitedAt = DateTime.UtcNow,
                    CreateId = 10001,
                    CreateBy = "Owner"
                }).ExecuteCommand();
                var now = DateTime.UtcNow;
                var accept = new WikiCollaboratorTransitionCommand(
                    40001, 0,
                    (int)WikiDocumentCollaboratorState.Pending,
                    (int)WikiDocumentCollaboratorState.Accepted,
                    10002, "Editor", now);
                var decline = accept with { TargetState = (int)WikiDocumentCollaboratorState.Declined };

                Assert.Equal(1, await repository.TransitionCollaboratorAsync(accept));
                Assert.Equal(0, await repository.TransitionCollaboratorAsync(decline));

                var stored = db.Queryable<WikiDocumentCollaborator>().Single();
                Assert.Equal((int)WikiDocumentCollaboratorState.Accepted, stored.InviteState);
                Assert.Equal(now, stored.RespondedAt);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task TryAddCollaboratorAsync_ShouldConvergeDuplicateRelation()
    {
        var (path, db, repository) = CreateRepository();
        using (db)
        {
            try
            {
                db.CodeFirst.InitTables<WikiDocumentCollaborator>();
                var collaborator = new WikiDocumentCollaborator
                {
                    Id = 40001,
                    TenantId = 0,
                    DocumentId = 20001,
                    UserId = 10002,
                    InviteState = (int)WikiDocumentCollaboratorState.Pending,
                    InvitedBy = 10001,
                    InvitedAt = DateTime.UtcNow,
                    CreateId = 10001,
                    CreateBy = "Owner"
                };

                Assert.True(await repository.TryAddCollaboratorAsync(collaborator));
                collaborator.Id = 40002;
                Assert.False(await repository.TryAddCollaboratorAsync(collaborator));
                Assert.Single(db.Queryable<WikiDocumentCollaborator>().ToList());
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    private static WikiDocumentDraft Seed(SqlSugarScope db)
    {
        db.CodeFirst.InitTables<WikiDocument>();
        db.CodeFirst.InitTables<WikiDocumentDraft>();
        db.CodeFirst.InitTables<WikiAttachmentReference>();
        var document = new WikiDocument
        {
            Id = 20001,
            TenantId = 0,
            Title = "Draft",
            Slug = "draft",
            MarkdownContent = string.Empty,
            Status = (int)WikiDocumentStatusEnum.Draft,
            SourceType = "Custom",
            Version = 0,
            OwnerUserId = 10001,
            ActiveDraftId = 30001,
            CreateId = 10001,
            CreateBy = "Author"
        };
        var draft = new WikiDocumentDraft
        {
            Id = 30001,
            TenantId = 0,
            DocumentId = document.Id,
            BaseDocumentVersion = 0,
            DraftVersion = 1,
            Title = "Draft",
            Slug = "draft",
            MarkdownContent = "body",
            ReviewState = (int)WikiDocumentDraftState.Editing,
            CreateId = 10001,
            CreateBy = "Author"
        };
        db.Insertable(document).ExecuteCommand();
        db.Insertable(draft).ExecuteCommand();
        return draft;
    }

    private static (string path, SqlSugarScope db, WikiDocumentRepository repository) CreateRepository()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-wiki-author-repository-{Guid.NewGuid():N}.db");
        var db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        var repository = new WikiDocumentRepository(
            new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));
        return (path, db, repository);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
