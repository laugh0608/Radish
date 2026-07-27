using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.Constants;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ForumQuestionRepositoryTest : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        $"radish-forum-question-{Guid.NewGuid():N}.db");
    private readonly SqlSugarScope _db;
    private readonly ForumQuestionRepository _repository;

    public ForumQuestionRepositoryTest()
    {
        _db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={_path};Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        _db.CodeFirst.InitTables<
            Post,
            PostQuestion,
            PostAnswer,
            PostAnswerContentRevision>();
        _db.CodeFirst.InitTables<PostAnswerAcceptanceEvent, Attachment>();
        _repository = new ForumQuestionRepository(
            new UnitOfWorkManage(_db, NullLogger<UnitOfWorkManage>.Instance));
        SeedQuestion();
    }

    [Fact]
    public async Task Repository_ShouldPageAnswersAndEnforceBothCasVersions()
    {
        var context = await _repository.QueryQuestionAsync(9, "pst_question_repository");
        Assert.NotNull(context);

        var answer = new PostAnswer
        {
            PublicId = "ans_0123456789abcdef0123456789abcdef",
            PostId = 2001,
            AuthorId = 3001,
            AuthorName = "Answerer",
            Content = "初始回答",
            ContentRevision = 1,
            TenantId = 9,
            CreateBy = "Answerer",
            CreateId = 3001
        };
        await _repository.InsertAnswerAsync(answer, new PostAnswerContentRevision
        {
            TenantId = 9,
            PostId = 2001,
            RevisionNumber = 1,
            SourceType = ForumContentRevisionSourceTypes.Baseline,
            IntegrityStatus = ForumContentRevisionIntegrityStatuses.Complete,
            Content = "初始回答",
            EditorId = 3001,
            EditorName = "Answerer",
            CreateBy = "Answerer",
            CreateId = 3001
        });

        var page = await _repository.QueryAnswerPageAsync(9, 2001, 1, 20, "default");
        Assert.Equal(1, page.Total);
        Assert.Equal(answer.PublicId, Assert.Single(page.Items).PublicId);

        answer.Content = "第二版";
        answer.ContentRevision = 2;
        answer.EditCount = 1;
        Assert.True(await _repository.UpdateAnswerContentAsync(
            answer,
            1,
            new PostAnswerContentRevision
            {
                TenantId = 9,
                AnswerId = answer.Id,
                PostId = 2001,
                RevisionNumber = 2,
                SourceType = ForumContentRevisionSourceTypes.Edit,
                IntegrityStatus = ForumContentRevisionIntegrityStatuses.Complete,
                Content = "第二版",
                EditorId = 3001,
                EditorName = "Answerer",
                CreateBy = "Answerer",
                CreateId = 3001
            }));
        Assert.False(await _repository.UpdateAnswerContentAsync(
            answer,
            1,
            new PostAnswerContentRevision()));

        context = await _repository.QueryQuestionAsync(9, "2001");
        Assert.NotNull(context);
        var acceptanceEvent = new PostAnswerAcceptanceEvent
        {
            TenantId = 9,
            PostId = 2001,
            PostQuestionId = context.Question.Id,
            EventType = PostAnswerAcceptanceEventTypes.Accepted,
            CurrentAnswerId = answer.Id,
            CurrentAnswerContentRevision = 2,
            OperatorId = 1001,
            OperatorName = "Owner"
        };
        Assert.True(await _repository.ChangeAcceptanceAsync(
            context,
            null,
            answer,
            0,
            acceptanceEvent,
            "Owner",
            1001,
            DateTime.UtcNow));
        Assert.False(await _repository.ChangeAcceptanceAsync(
            context,
            null,
            answer,
            0,
            new PostAnswerAcceptanceEvent(),
            "Owner",
            1001,
            DateTime.UtcNow));
    }

    private void SeedQuestion()
    {
        _db.Insertable(new Post
        {
            Id = 2001,
            PublicId = "pst_question_repository",
            TenantId = 9,
            Title = "问题",
            Content = "问题正文",
            AuthorId = 1001,
            AuthorName = "Owner",
            IsPublished = true,
            PublishTime = DateTime.UtcNow,
            IsEnabled = true,
            CreateBy = "Owner",
            CreateId = 1001
        }).ExecuteCommand();
        _db.Insertable(new PostQuestion
        {
            Id = 2101,
            PostId = 2001,
            TenantId = 9,
            CreateBy = "Owner",
            CreateId = 1001
        }).ExecuteCommand();
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
