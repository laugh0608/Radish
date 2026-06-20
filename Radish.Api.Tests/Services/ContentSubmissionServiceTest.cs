using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.Services;

public class ContentSubmissionServiceTest
{
    [Fact]
    public async Task BeginAsync_Should_Create_Pending_Record_With_Client_Key()
    {
        var records = new List<ContentSubmissionRecord>();
        var (service, repository) = CreateService(records, nextId: 7001);

        var result = await service.BeginAsync(CreateBeginRequest(clientSubmissionId: "forum-post:abc"));

        Assert.Equal(ContentSubmissionBeginStatus.Started, result.Status);
        Assert.Equal(7001, result.RecordId);

        var record = Assert.Single(records);
        Assert.Equal(7001, record.Id);
        Assert.Equal(0, record.TenantId);
        Assert.Equal(42, record.UserId);
        Assert.Equal(ContentSubmissionOperationTypes.ForumPostCreate, record.OperationType);
        Assert.Equal("forum-post:abc", record.ClientSubmissionId);
        Assert.Equal("Category", record.TargetType);
        Assert.Equal(1001, record.TargetId);
        Assert.Equal("digest-a", record.RequestDigest);
        Assert.Equal("summary-a", record.RequestSummary);
        Assert.Equal("fingerprint-a", record.ContentFingerprint);
        Assert.Equal(ContentSubmissionStatuses.Pending, record.Status);
        Assert.Equal("User_42", record.CreateBy);
        Assert.Equal(42, record.CreateId);

        repository.Verify(r => r.AddAsync(It.IsAny<ContentSubmissionRecord>()), Times.Once);
    }

    [Fact]
    public async Task BeginAsync_Should_Return_Succeeded_When_Client_Key_Replayed_With_Same_Digest()
    {
        var existing = CreateRecord(
            id: 8001,
            clientSubmissionId: "forum-post:abc",
            requestDigest: "digest-a",
            status: ContentSubmissionStatuses.Succeeded,
            resultType: ContentSubmissionResultTypes.Post,
            resultId: 9001,
            resultPublicId: "post-public-id");
        var (service, repository) = CreateService([existing]);

        var result = await service.BeginAsync(CreateBeginRequest(clientSubmissionId: "forum-post:abc"));

        Assert.Equal(ContentSubmissionBeginStatus.Succeeded, result.Status);
        Assert.Equal(8001, result.RecordId);
        Assert.Equal(ContentSubmissionResultTypes.Post, result.ResultType);
        Assert.Equal(9001, result.ResultId);
        Assert.Equal("post-public-id", result.ResultPublicId);
        Assert.Equal("刚刚已提交，已返回已有内容", result.Message);

        repository.Verify(r => r.AddAsync(It.IsAny<ContentSubmissionRecord>()), Times.Never);
        repository.Verify(r => r.UpdateAsync(It.IsAny<ContentSubmissionRecord>()), Times.Never);
    }

    [Fact]
    public async Task BeginAsync_Should_Return_Processing_When_Client_Key_Is_Pending()
    {
        var existing = CreateRecord(
            id: 8001,
            clientSubmissionId: "forum-post:abc",
            requestDigest: "digest-a",
            status: ContentSubmissionStatuses.Pending);
        var (service, repository) = CreateService([existing]);

        var result = await service.BeginAsync(CreateBeginRequest(clientSubmissionId: "forum-post:abc"));

        Assert.Equal(ContentSubmissionBeginStatus.Processing, result.Status);
        Assert.Equal(8001, result.RecordId);
        Assert.Equal(3, result.RetryAfterSeconds);
        Assert.Equal("内容正在提交，请稍后确认", result.Message);

        repository.Verify(r => r.AddAsync(It.IsAny<ContentSubmissionRecord>()), Times.Never);
        repository.Verify(r => r.UpdateAsync(It.IsAny<ContentSubmissionRecord>()), Times.Never);
    }

    [Fact]
    public async Task BeginAsync_Should_Return_Conflict_When_Client_Key_Reused_By_Different_Content()
    {
        var existing = CreateRecord(
            id: 8001,
            clientSubmissionId: "forum-post:abc",
            requestDigest: "digest-a",
            status: ContentSubmissionStatuses.Succeeded);
        var (service, repository) = CreateService([existing]);

        var result = await service.BeginAsync(CreateBeginRequest(
            clientSubmissionId: "forum-post:abc",
            requestDigest: "digest-b"));

        Assert.Equal(ContentSubmissionBeginStatus.Conflict, result.Status);
        Assert.Equal("提交意图 ID 已被不同内容使用，请刷新后重新提交", result.Message);

        repository.Verify(r => r.AddAsync(It.IsAny<ContentSubmissionRecord>()), Times.Never);
        repository.Verify(r => r.UpdateAsync(It.IsAny<ContentSubmissionRecord>()), Times.Never);
    }

    [Fact]
    public async Task BeginAsync_Should_Reset_Failed_Record_To_Pending()
    {
        var existing = CreateRecord(
            id: 8001,
            clientSubmissionId: "forum-post:abc",
            requestDigest: "old-digest",
            status: ContentSubmissionStatuses.Failed,
            resultType: ContentSubmissionResultTypes.Post,
            resultId: 9001,
            resultPublicId: "old-public-id",
            errorCode: "PublishFailed",
            errorMessage: "old error");
        var (service, repository) = CreateService([existing]);

        var result = await service.BeginAsync(CreateBeginRequest(
            clientSubmissionId: "forum-post:abc",
            requestDigest: "digest-new",
            requestSummary: "summary-new",
            contentFingerprint: "fingerprint-new"));

        Assert.Equal(ContentSubmissionBeginStatus.Started, result.Status);
        Assert.Equal(8001, result.RecordId);
        Assert.Equal(ContentSubmissionStatuses.Pending, existing.Status);
        Assert.Equal("digest-new", existing.RequestDigest);
        Assert.Equal("summary-new", existing.RequestSummary);
        Assert.Equal("fingerprint-new", existing.ContentFingerprint);
        Assert.Null(existing.ResultType);
        Assert.Null(existing.ResultId);
        Assert.Null(existing.ResultPublicId);
        Assert.Null(existing.ErrorCode);
        Assert.Null(existing.ErrorMessage);
        Assert.Null(existing.CompleteTime);

        repository.Verify(r => r.AddAsync(It.IsAny<ContentSubmissionRecord>()), Times.Never);
        repository.Verify(r => r.UpdateAsync(existing), Times.Once);
    }

    [Fact]
    public async Task BeginAsync_Should_Return_DuplicateContent_When_No_Key_Replays_Recent_Fingerprint()
    {
        var existing = CreateRecord(
            id: 8001,
            operationType: ContentSubmissionOperationTypes.ForumCommentCreate,
            clientSubmissionId: "auto:old",
            requestDigest: "digest-a",
            contentFingerprint: "fingerprint-comment",
            targetType: "Post",
            targetId: 2001,
            status: ContentSubmissionStatuses.Succeeded,
            resultType: ContentSubmissionResultTypes.Comment,
            resultId: 3001,
            resultPublicId: "comment-public-id",
            createTime: DateTime.Now.AddSeconds(-20));
        var (service, repository) = CreateService([existing]);

        var result = await service.BeginAsync(CreateBeginRequest(
            operationType: ContentSubmissionOperationTypes.ForumCommentCreate,
            clientSubmissionId: null,
            contentFingerprint: "fingerprint-comment",
            targetType: "Post",
            targetId: 2001,
            duplicateWindowSeconds: 60));

        Assert.Equal(ContentSubmissionBeginStatus.DuplicateContent, result.Status);
        Assert.Equal(8001, result.RecordId);
        Assert.Equal(ContentSubmissionResultTypes.Comment, result.ResultType);
        Assert.Equal(3001, result.ResultId);
        Assert.Equal("comment-public-id", result.ResultPublicId);
        Assert.InRange(result.RetryAfterSeconds.GetValueOrDefault(), 1, 60);

        repository.Verify(r => r.AddAsync(It.IsAny<ContentSubmissionRecord>()), Times.Never);
    }

    [Fact]
    public async Task BeginAsync_Should_Reject_Invalid_Client_Key_Without_Querying_Repository()
    {
        var (service, repository) = CreateService();

        var result = await service.BeginAsync(CreateBeginRequest(clientSubmissionId: "bad key"));

        Assert.Equal(ContentSubmissionBeginStatus.InvalidKey, result.Status);
        Assert.Equal("提交意图 ID 格式无效", result.Message);

        repository.Verify(r => r.QueryFirstAsync(It.IsAny<Expression<Func<ContentSubmissionRecord, bool>>>()), Times.Never);
        repository.Verify(r => r.AddAsync(It.IsAny<ContentSubmissionRecord>()), Times.Never);
    }

    [Fact]
    public async Task CompleteSuccessAsync_Should_Update_Record_Result()
    {
        var existing = CreateRecord(
            id: 8001,
            clientSubmissionId: "forum-post:abc",
            requestDigest: "digest-a",
            status: ContentSubmissionStatuses.Pending);
        var (service, repository) = CreateService([existing]);

        await service.CompleteSuccessAsync(new ContentSubmissionCompletionRequest
        {
            RecordId = 8001,
            ResultType = ContentSubmissionResultTypes.Post,
            ResultId = 9001,
            ResultPublicId = "post-public-id"
        });

        Assert.Equal(ContentSubmissionStatuses.Succeeded, existing.Status);
        Assert.Equal(ContentSubmissionResultTypes.Post, existing.ResultType);
        Assert.Equal(9001, existing.ResultId);
        Assert.Equal("post-public-id", existing.ResultPublicId);
        Assert.Null(existing.ErrorCode);
        Assert.Null(existing.ErrorMessage);
        Assert.NotNull(existing.CompleteTime);
        Assert.NotNull(existing.ModifyTime);

        repository.Verify(r => r.UpdateAsync(existing), Times.Once);
    }

    [Fact]
    public async Task CompleteFailureAsync_Should_Update_Record_Error()
    {
        var existing = CreateRecord(
            id: 8001,
            clientSubmissionId: "forum-post:abc",
            requestDigest: "digest-a",
            status: ContentSubmissionStatuses.Pending,
            resultType: ContentSubmissionResultTypes.Post,
            resultId: 9001,
            resultPublicId: "post-public-id");
        var (service, repository) = CreateService([existing]);

        await service.CompleteFailureAsync(8001, "PublishFailed", "publish error");

        Assert.Equal(ContentSubmissionStatuses.Failed, existing.Status);
        Assert.Null(existing.ResultType);
        Assert.Null(existing.ResultId);
        Assert.Null(existing.ResultPublicId);
        Assert.Equal("PublishFailed", existing.ErrorCode);
        Assert.Equal("publish error", existing.ErrorMessage);
        Assert.NotNull(existing.CompleteTime);
        Assert.NotNull(existing.ModifyTime);

        repository.Verify(r => r.UpdateAsync(existing), Times.Once);
    }

    [Fact]
    public void CreateRequestSnapshot_Should_Normalize_Order_And_Whitespace()
    {
        var (service, _) = CreateService();

        var first = service.CreateRequestSnapshot(
            new Dictionary<string, object?>
            {
                ["title"] = "  Hello   Radish  ",
                ["content"] = "Line 1\r\n\r\nLine 2"
            },
            new Dictionary<string, object?>
            {
                ["targetId"] = 1001,
                ["content"] = "Line 1\n Line 2"
            });
        var second = service.CreateRequestSnapshot(
            new Dictionary<string, object?>
            {
                ["content"] = "Line 1 Line 2",
                ["title"] = "Hello Radish"
            },
            new Dictionary<string, object?>
            {
                ["content"] = "Line 1 Line 2",
                ["targetId"] = 1001
            });

        Assert.Equal(first.RequestDigest, second.RequestDigest);
        Assert.Equal(first.ContentFingerprint, second.ContentFingerprint);
        Assert.Equal(64, first.RequestDigest.Length);
        Assert.Equal(64, first.ContentFingerprint.Length);
    }

    private static (ContentSubmissionService Service, Mock<IBaseRepository<ContentSubmissionRecord>> Repository) CreateService(
        List<ContentSubmissionRecord>? records = null,
        long nextId = 1001)
    {
        var storedRecords = records ?? [];
        var nextRecordId = nextId;
        var repository = new Mock<IBaseRepository<ContentSubmissionRecord>>(MockBehavior.Strict);

        repository
            .Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<ContentSubmissionRecord, bool>>>()))
            .Returns((Expression<Func<ContentSubmissionRecord, bool>>? whereExpression) =>
            {
                var predicate = whereExpression?.Compile();
                return Task.FromResult(predicate == null
                    ? storedRecords.FirstOrDefault()
                    : storedRecords.FirstOrDefault(predicate));
            });
        repository
            .Setup(r => r.QueryByIdAsync(It.IsAny<long>()))
            .Returns((long id) => Task.FromResult(storedRecords.FirstOrDefault(record => record.Id == id)));
        repository
            .Setup(r => r.AddAsync(It.IsAny<ContentSubmissionRecord>()))
            .Returns((ContentSubmissionRecord record) =>
            {
                var id = nextRecordId++;
                record.Id = id;
                storedRecords.Add(record);
                return Task.FromResult(id);
            });
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<ContentSubmissionRecord>()))
            .ReturnsAsync(true);

        return (new ContentSubmissionService(repository.Object), repository);
    }

    private static ContentSubmissionBeginRequest CreateBeginRequest(
        long tenantId = 0,
        long userId = 42,
        string operationType = ContentSubmissionOperationTypes.ForumPostCreate,
        string? clientSubmissionId = "forum-post:abc",
        string? targetType = "Category",
        long? targetId = 1001,
        string requestDigest = "digest-a",
        string requestSummary = "summary-a",
        string contentFingerprint = "fingerprint-a",
        int duplicateWindowSeconds = 60)
    {
        return new ContentSubmissionBeginRequest
        {
            TenantId = tenantId,
            UserId = userId,
            OperationType = operationType,
            ClientSubmissionId = clientSubmissionId,
            TargetType = targetType,
            TargetId = targetId,
            RequestDigest = requestDigest,
            RequestSummary = requestSummary,
            ContentFingerprint = contentFingerprint,
            DuplicateWindowSeconds = duplicateWindowSeconds
        };
    }

    private static ContentSubmissionRecord CreateRecord(
        long id,
        string operationType = ContentSubmissionOperationTypes.ForumPostCreate,
        string clientSubmissionId = "forum-post:abc",
        string requestDigest = "digest-a",
        string requestSummary = "summary-a",
        string contentFingerprint = "fingerprint-a",
        string? targetType = "Category",
        long? targetId = 1001,
        string status = ContentSubmissionStatuses.Pending,
        string? resultType = null,
        long? resultId = null,
        string? resultPublicId = null,
        string? errorCode = null,
        string? errorMessage = null,
        DateTime? createTime = null)
    {
        return new ContentSubmissionRecord
        {
            Id = id,
            TenantId = 0,
            UserId = 42,
            OperationType = operationType,
            ClientSubmissionId = clientSubmissionId,
            TargetType = targetType,
            TargetId = targetId,
            RequestDigest = requestDigest,
            RequestSummary = requestSummary,
            ContentFingerprint = contentFingerprint,
            Status = status,
            ResultType = resultType,
            ResultId = resultId,
            ResultPublicId = resultPublicId,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ExpiresAt = DateTime.Now.AddHours(1),
            CreateTime = createTime ?? DateTime.Now.AddMinutes(-1),
            CompleteTime = status == ContentSubmissionStatuses.Pending ? null : DateTime.Now.AddSeconds(-30)
        };
    }
}
