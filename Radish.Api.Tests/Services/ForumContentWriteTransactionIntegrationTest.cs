using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Radish.Common.CoreTool;
using Radish.Extension.AopExtension;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Service;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ForumContentWriteTransactionIntegrationTest
{
    [Fact]
    public async Task PublishPostAsync_Through_Aop_Should_Rollback_Ledger_Business_And_Outbox_When_CompletionFails()
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
        db.CodeFirst.InitTables<ContentSubmissionRecord>();
        await db.Ado.ExecuteCommandAsync(
            "CREATE TABLE forum_business_fact (id INTEGER PRIMARY KEY)");
        await db.Ado.ExecuteCommandAsync(
            "CREATE TABLE forum_outbox_fact (id INTEGER PRIMARY KEY, business_id INTEGER NOT NULL)");

        var unitOfWork = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
        IBaseRepository<ContentSubmissionRecord> recordRepository =
            new BaseRepository<ContentSubmissionRecord>(unitOfWork);
        var realSubmissionService = new ContentSubmissionService(recordRepository, unitOfWork);
        var submissionService = new Mock<IContentSubmissionService>(MockBehavior.Strict);
        submissionService
            .Setup(service => service.CreateRequestSnapshot(
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>()))
            .Returns((
                IReadOnlyDictionary<string, object?> requestValues,
                IReadOnlyDictionary<string, object?> fingerprintValues) =>
                realSubmissionService.CreateRequestSnapshot(requestValues, fingerprintValues));
        submissionService
            .Setup(service => service.BeginAsync(It.IsAny<ContentSubmissionBeginRequest>()))
            .Returns((ContentSubmissionBeginRequest request) =>
                realSubmissionService.BeginAsync(request));
        submissionService
            .Setup(service => service.CompleteSuccessAsync(
                It.IsAny<ContentSubmissionCompletionRequest>()))
            .ThrowsAsync(new InvalidOperationException("completion unavailable"));

        var postService = new Mock<IPostService>(MockBehavior.Strict);
        postService
            .Setup(service => service.PublishPostAsync(
                It.IsAny<Post>(),
                It.IsAny<CreatePollDto?>(),
                It.IsAny<CreateLotteryDto?>(),
                It.IsAny<bool>(),
                It.IsAny<List<string>?>(),
                It.IsAny<bool>()))
            .Returns(async () =>
            {
                await db.Ado.ExecuteCommandAsync(
                    "INSERT INTO forum_business_fact (id) VALUES (9001)");
                await db.Ado.ExecuteCommandAsync(
                    "INSERT INTO forum_outbox_fact (id, business_id) VALUES (9101, 9001)");
                return 9001;
            });

        var target = new ForumContentWriteService(
            submissionService.Object,
            postService.Object,
            new Mock<ICommentService>(MockBehavior.Strict).Object);
        var proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget<IForumContentWriteService>(
            target,
            new TranAop(unitOfWork, NullLogger<TranAop>.Instance));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            proxy.PublishPostAsync(
                CreatePost(),
                poll: null,
                lottery: null,
                isQuestion: false,
                tagNames: ["Radish"],
                allowCreateTag: true,
                clientSubmissionId: "forum-post:transaction"));

        Assert.Equal("completion unavailable", exception.Message);
        Assert.Equal(0, unitOfWork.TranCount);
        Assert.Equal(0, db.Queryable<ContentSubmissionRecord>().Count());
        Assert.Equal(
            0,
            Convert.ToInt32(
                await db.Ado.GetScalarAsync("SELECT COUNT(*) FROM forum_business_fact")));
        Assert.Equal(
            0,
            Convert.ToInt32(
                await db.Ado.GetScalarAsync("SELECT COUNT(*) FROM forum_outbox_fact")));
        submissionService.Verify(
            service => service.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>()),
            Times.Once);
    }

    private static Post CreatePost()
    {
        return new Post
        {
            TenantId = 0,
            AuthorId = 42,
            AuthorName = "萝卜",
            CategoryId = 1001,
            Title = "事务一致性测试",
            Content = "完成记录失败时全部回滚",
            ContentType = "Markdown",
            PublicId = "pst_transaction"
        };
    }
}
