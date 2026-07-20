using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class WikiAuthoringRepositoryTest
{
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
                    20001, 0, 0, draft, null, 90001, "Reviewer", DateTime.UtcNow);

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

                var now = DateTime.UtcNow;
                Assert.Equal(1, await repository.PurgeTerminalDraftPayloadsAsync(now.AddDays(-90), 100, now));
                Assert.Equal(0, await repository.PurgeTerminalDraftPayloadsAsync(now.AddDays(-90), 100, now));

                var stored = db.Queryable<WikiDocumentDraft>().Single();
                Assert.Equal(string.Empty, stored.MarkdownContent);
                Assert.Equal(now, stored.PayloadPurgedAt);
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
}
