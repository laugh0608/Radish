using Microsoft.AspNetCore.Http;
using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Service;

/// <summary>问答回答与采纳状态的业务权威服务。</summary>
public sealed class ForumQuestionService : IForumQuestionService
{
    private const int CreateDuplicateWindowSeconds = 120;
    private const int CreateFrequencyWindowSeconds = 30;
    private const int MutationDuplicateWindowSeconds = 60;
    private readonly IForumQuestionRepository _repository;
    private readonly IContentSubmissionService _contentSubmissionService;
    private readonly IUserInteractionPolicyService _interactionPolicyService;
    private readonly IReliableOutboxService _reliableOutboxService;

    public ForumQuestionService(
        IForumQuestionRepository repository,
        IContentSubmissionService contentSubmissionService,
        IUserInteractionPolicyService interactionPolicyService,
        IReliableOutboxService reliableOutboxService)
    {
        _repository = repository;
        _contentSubmissionService = contentSubmissionService;
        _interactionPolicyService = interactionPolicyService;
        _reliableOutboxService = reliableOutboxService;
    }

    public async Task<PostAnswerPageVo> GetAnswerPageAsync(
        long tenantId,
        string postIdentifier,
        int pageIndex,
        int pageSize,
        string sort,
        long currentUserId)
    {
        var context = await RequireQuestionAsync(tenantId, postIdentifier);
        var page = await _repository.QueryAnswerPageAsync(
            tenantId,
            context.Post.Id,
            pageIndex,
            pageSize,
            sort);
        var acceptedPublicId = page.Items
            .FirstOrDefault(item => item.Id == context.Question.AcceptedAnswerId)?.PublicId;
        if (acceptedPublicId == null && context.Question.AcceptedAnswerId.HasValue)
        {
            var accepted = await _repository.QueryAnswerByIdAsync(
                tenantId,
                context.Question.AcceptedAnswerId.Value);
            acceptedPublicId = accepted?.PublicId;
        }

        return new PostAnswerPageVo
        {
            VoPostPublicId = context.Post.PublicId ?? string.Empty,
            VoIsSolved = context.Question.IsSolved,
            VoAcceptedAnswerPublicId = acceptedPublicId,
            VoAcceptanceRevision = context.Question.AcceptanceRevision,
            VoTotal = page.Total,
            VoPageIndex = Math.Max(1, pageIndex),
            VoPageSize = Math.Clamp(pageSize, 1, 100),
            VoItems = page.Items.Select(item => MapAnswer(item, currentUserId)).ToList()
        };
    }

    [UseTran]
    public async Task<PostAnswerMutationVo> CreateAnswerAsync(
        long tenantId,
        long postId,
        string content,
        long authorId,
        string authorName,
        string? clientSubmissionId)
    {
        var normalizedContent = NormalizeContent(content);
        var context = await RequireQuestionAsync(tenantId, postId.ToString());
        if (context.Post.AuthorId != authorId)
        {
            await _interactionPolicyService.EnsureCanInteractAsync(tenantId, authorId, context.Post.AuthorId);
        }

        var begin = await BeginMutationAsync(
            tenantId,
            authorId,
            ContentSubmissionOperationTypes.ForumAnswerCreate,
            clientSubmissionId,
            "Post",
            postId,
            new Dictionary<string, object?>
            {
                ["postId"] = postId,
                ["content"] = normalizedContent
            },
            CreateDuplicateWindowSeconds,
            CreateFrequencyWindowSeconds);
        if (begin.Status != ContentSubmissionBeginStatus.Started)
        {
            if (begin.ResultPublicId is { Length: > 0 })
            {
                var replay = await _repository.QueryAnswerAsync(tenantId, begin.ResultPublicId);
                if (replay != null)
                {
                    return await BuildMutationAsync(context, replay, authorId);
                }
            }
            EnsureStarted(begin);
        }

        var now = DateTime.UtcNow;
        var safeAuthorName = NormalizeOperatorName(authorName);
        var answer = new PostAnswer
        {
            PublicId = PostAnswer.GeneratePublicId(),
            PostId = context.Post.Id,
            AuthorId = authorId,
            AuthorName = safeAuthorName,
            Content = normalizedContent,
            TenantId = tenantId,
            CreateTime = now,
            CreateBy = safeAuthorName,
            CreateId = authorId
        };
        var revision = BuildRevision(answer, 1, ForumContentRevisionSourceTypes.Baseline, null, normalizedContent, authorId, safeAuthorName, now);
        await _repository.InsertAnswerAsync(answer, revision);
        await BindAnswerAttachmentsAsync(
            tenantId,
            answer.Id,
            revision.Id,
            authorId,
            safeAuthorName,
            AttachmentReferenceHelper.ExtractAttachmentIds(normalizedContent),
            now);
        await CompleteAsync(begin, ContentSubmissionResultTypes.PostAnswer, answer.Id, answer.PublicId);
        await EnqueueQuestionAnsweredNotificationAsync(context, answer, now);
        context.Question.AnswerCount++;
        return await BuildMutationAsync(context, answer, authorId);
    }

    [UseTran]
    public async Task<PostAnswerMutationVo> UpdateAnswerAsync(
        long tenantId,
        string answerPublicId,
        string content,
        int expectedContentRevision,
        long operatorId,
        string operatorName,
        string clientSubmissionId)
    {
        return await UpdateAnswerCoreAsync(
            tenantId,
            answerPublicId,
            NormalizeContent(content),
            expectedContentRevision,
            operatorId,
            operatorName,
            clientSubmissionId,
            ForumContentRevisionSourceTypes.Edit,
            null);
    }

    [UseTran]
    public async Task<PostAnswerMutationVo> DeleteAnswerAsync(
        long tenantId,
        string answerPublicId,
        int expectedContentRevision,
        long operatorId,
        string operatorName,
        string clientSubmissionId)
    {
        var answer = await RequireAnswerAsync(tenantId, answerPublicId, includeDeleted: true);
        if (answer.AuthorId != operatorId)
        {
            throw Error("只有回答作者可以删除该回答", StatusCodes.Status403Forbidden, ForumQuestionErrorCodes.AccessDenied);
        }
        var context = await RequireQuestionAsync(tenantId, answer.PostId.ToString());
        var begin = await BeginMutationAsync(
            tenantId,
            operatorId,
            ContentSubmissionOperationTypes.ForumAnswerDelete,
            clientSubmissionId,
            "PostAnswer",
            answer.Id,
            new Dictionary<string, object?>
            {
                ["answerPublicId"] = answer.PublicId,
                ["expectedContentRevision"] = expectedContentRevision
            },
            MutationDuplicateWindowSeconds);
        if (begin.Status == ContentSubmissionBeginStatus.Succeeded)
        {
            return await BuildMutationAsync(context, answer, operatorId);
        }
        if (begin.Status != ContentSubmissionBeginStatus.Started)
        {
            EnsureStarted(begin);
        }
        EnsureAuthorAndMutable(answer, operatorId);
        if (answer.IsDeleted)
        {
            throw Conflict();
        }

        var now = DateTime.UtcNow;
        var safeName = NormalizeOperatorName(operatorName);
        if (!await _repository.SoftDeleteAnswerAsync(answer, expectedContentRevision, safeName, operatorId, now))
        {
            throw Conflict();
        }
        await CompleteAsync(begin, ContentSubmissionResultTypes.PostAnswer, answer.Id, answer.PublicId);
        answer.IsDeleted = true;
        context.Question.AnswerCount = Math.Max(0, context.Question.AnswerCount - 1);
        return await BuildMutationAsync(context, answer, operatorId);
    }

    public async Task<PostAnswerRevisionListVo> GetAnswerRevisionsAsync(
        long tenantId,
        string answerPublicId,
        long currentUserId)
    {
        var answer = await RequireAnswerAsync(tenantId, answerPublicId, includeDeleted: true);
        EnsureRevisionAccess(answer, currentUserId);
        var revisions = await _repository.QueryAnswerRevisionsAsync(tenantId, answer.Id);
        var revisionNumberById = revisions.ToDictionary(item => item.Id, item => item.RevisionNumber);
        return new PostAnswerRevisionListVo
        {
            VoAnswerPublicId = answer.PublicId,
            VoCurrentContentRevision = answer.ContentRevision,
            VoItems = revisions.Select(item => new PostAnswerRevisionSummaryVo
            {
                VoRevisionNumber = item.RevisionNumber,
                VoSourceType = item.SourceType,
                VoIntegrityStatus = item.IntegrityStatus,
                VoIsCurrent = item.RevisionNumber == answer.ContentRevision,
                VoCanRestore = !answer.IsAccepted &&
                               !answer.IsDeleted &&
                               item.IntegrityStatus == ForumContentRevisionIntegrityStatuses.Complete &&
                               item.RevisionNumber != answer.ContentRevision,
                VoRestoredFromRevisionNumber = item.RestoredFromRevisionId.HasValue &&
                                               revisionNumberById.TryGetValue(item.RestoredFromRevisionId.Value, out var sourceNumber)
                    ? sourceNumber
                    : null,
                VoCreateTime = item.CreateTime,
                VoEditorName = item.EditorName
            }).ToList()
        };
    }

    public async Task<PostAnswerRevisionDetailVo> GetAnswerRevisionAsync(
        long tenantId,
        string answerPublicId,
        int revisionNumber,
        long currentUserId)
    {
        var answer = await RequireAnswerAsync(tenantId, answerPublicId, includeDeleted: true);
        EnsureRevisionAccess(answer, currentUserId);
        var revision = await _repository.QueryAnswerRevisionAsync(tenantId, answer.Id, revisionNumber)
                       ?? throw AnswerNotFound("回答版本不存在");
        return new PostAnswerRevisionDetailVo
        {
            VoAnswerPublicId = answer.PublicId,
            VoRevisionNumber = revision.RevisionNumber,
            VoSourceType = revision.SourceType,
            VoIntegrityStatus = revision.IntegrityStatus,
            VoContent = revision.Content,
            VoExpectedContentRevision = answer.ContentRevision,
            VoCreateTime = revision.CreateTime,
            VoEditorName = revision.EditorName
        };
    }

    [UseTran]
    public async Task<PostAnswerMutationVo> RestoreAnswerRevisionAsync(
        long tenantId,
        string answerPublicId,
        int revisionNumber,
        int expectedContentRevision,
        long operatorId,
        string operatorName,
        string clientSubmissionId)
    {
        var answer = await RequireAnswerAsync(tenantId, answerPublicId);
        EnsureAuthorAndMutable(answer, operatorId);
        var source = await _repository.QueryAnswerRevisionAsync(tenantId, answer.Id, revisionNumber)
                     ?? throw AnswerNotFound("回答版本不存在");
        if (source.IntegrityStatus != ForumContentRevisionIntegrityStatuses.Complete)
        {
            throw Error(
                "该历史版本不完整，不能恢复",
                StatusCodes.Status409Conflict,
                ForumQuestionErrorCodes.RevisionIncomplete);
        }
        return await UpdateAnswerCoreAsync(
            tenantId,
            answerPublicId,
            source.Content,
            expectedContentRevision,
            operatorId,
            operatorName,
            clientSubmissionId,
            ForumContentRevisionSourceTypes.Restore,
            source.Id);
    }

    [UseTran]
    public async Task<PostAnswerAcceptanceMutationVo> AcceptAnswerAsync(
        long tenantId,
        string postIdentifier,
        string answerPublicId,
        int expectedAcceptanceRevision,
        long operatorId,
        string operatorName,
        string clientSubmissionId)
    {
        var context = await RequireQuestionAsync(tenantId, postIdentifier);
        EnsureQuestionOwner(context, operatorId);
        var next = await RequireAnswerAsync(tenantId, answerPublicId);
        if (next.PostId != context.Post.Id)
        {
            throw AnswerNotFound();
        }
        if (next.AuthorId == operatorId)
        {
            throw Error("不能采纳自己的回答", StatusCodes.Status400BadRequest, ForumQuestionErrorCodes.AccessDenied);
        }
        await _interactionPolicyService.EnsureCanInteractAsync(tenantId, operatorId, next.AuthorId);
        var begin = await BeginAcceptanceAsync(
            context,
            next.PublicId,
            expectedAcceptanceRevision,
            operatorId,
            clientSubmissionId,
            ContentSubmissionOperationTypes.ForumAnswerAcceptanceChange);
        if (begin.Status == ContentSubmissionBeginStatus.Succeeded)
        {
            return MapAcceptance(context, next.PublicId);
        }
        if (begin.Status != ContentSubmissionBeginStatus.Started)
        {
            EnsureStarted(begin);
        }
        var previous = await ResolveAcceptedAnswerAsync(context);
        if (previous?.Id == next.Id)
        {
            if (context.Question.AcceptanceRevision != expectedAcceptanceRevision)
            {
                throw AcceptanceConflict();
            }
            return MapAcceptance(context, next.PublicId);
        }
        var now = DateTime.UtcNow;
        var safeName = NormalizeOperatorName(operatorName);
        var acceptanceEvent = BuildAcceptanceEvent(
            context,
            previous,
            next,
            previous == null ? PostAnswerAcceptanceEventTypes.Accepted : PostAnswerAcceptanceEventTypes.Replaced,
            operatorId,
            safeName,
            now);
        if (!await _repository.ChangeAcceptanceAsync(
                context,
                previous,
                next,
                expectedAcceptanceRevision,
                acceptanceEvent,
                safeName,
                operatorId,
                now))
        {
            throw AcceptanceConflict();
        }
        await CompleteAsync(begin, ContentSubmissionResultTypes.PostAnswerAcceptanceEvent, acceptanceEvent.Id, next.PublicId);
        await EnqueueAcceptanceNotificationsAsync(context, previous, next, safeName, operatorId, now);
        context.Question.AcceptanceRevision = expectedAcceptanceRevision + 1;
        context.Question.AcceptedAnswerId = next.Id;
        context.Question.AcceptedAnswerContentRevision = next.ContentRevision;
        context.Question.IsSolved = true;
        return MapAcceptance(context, next.PublicId);
    }

    [UseTran]
    public async Task<PostAnswerAcceptanceMutationVo> RevokeAcceptanceAsync(
        long tenantId,
        string postIdentifier,
        int expectedAcceptanceRevision,
        long operatorId,
        string operatorName,
        string clientSubmissionId)
    {
        var context = await RequireQuestionAsync(tenantId, postIdentifier);
        EnsureQuestionOwner(context, operatorId);
        var begin = await BeginAcceptanceAsync(
            context,
            null,
            expectedAcceptanceRevision,
            operatorId,
            clientSubmissionId,
            ContentSubmissionOperationTypes.ForumAnswerAcceptanceChange);
        if (begin.Status == ContentSubmissionBeginStatus.Succeeded)
        {
            return MapAcceptance(context, null);
        }
        if (begin.Status != ContentSubmissionBeginStatus.Started)
        {
            EnsureStarted(begin);
        }
        var previous = await ResolveAcceptedAnswerAsync(context);
        if (previous == null)
        {
            if (context.Question.AcceptanceRevision != expectedAcceptanceRevision)
            {
                throw AcceptanceConflict();
            }
            return MapAcceptance(context, null);
        }
        var now = DateTime.UtcNow;
        var safeName = NormalizeOperatorName(operatorName);
        var acceptanceEvent = BuildAcceptanceEvent(
            context,
            previous,
            null,
            PostAnswerAcceptanceEventTypes.Revoked,
            operatorId,
            safeName,
            now);
        if (!await _repository.ChangeAcceptanceAsync(
                context,
                previous,
                null,
                expectedAcceptanceRevision,
                acceptanceEvent,
                safeName,
                operatorId,
                now))
        {
            throw AcceptanceConflict();
        }
        await CompleteAsync(begin, ContentSubmissionResultTypes.PostAnswerAcceptanceEvent, acceptanceEvent.Id, previous.PublicId);
        await EnqueueAcceptanceRevokedNotificationAsync(context, previous, safeName, operatorId, now);
        context.Question.AcceptanceRevision = expectedAcceptanceRevision + 1;
        context.Question.AcceptedAnswerId = null;
        context.Question.AcceptedAnswerContentRevision = null;
        context.Question.IsSolved = false;
        return MapAcceptance(context, null);
    }

    private async Task<PostAnswerMutationVo> UpdateAnswerCoreAsync(
        long tenantId,
        string answerPublicId,
        string content,
        int expectedContentRevision,
        long operatorId,
        string operatorName,
        string clientSubmissionId,
        string sourceType,
        long? restoredFromRevisionId)
    {
        var answer = await RequireAnswerAsync(tenantId, answerPublicId);
        EnsureAuthorAndMutable(answer, operatorId);
        var context = await RequireQuestionAsync(tenantId, answer.PostId.ToString());
        if (string.Equals(answer.Content, content, StringComparison.Ordinal))
        {
            return await BuildMutationAsync(context, answer, operatorId);
        }
        var operationType = sourceType == ForumContentRevisionSourceTypes.Restore
            ? ContentSubmissionOperationTypes.ForumAnswerRevisionRestore
            : ContentSubmissionOperationTypes.ForumAnswerEdit;
        var begin = await BeginMutationAsync(
            tenantId,
            operatorId,
            operationType,
            clientSubmissionId,
            "PostAnswer",
            answer.Id,
            new Dictionary<string, object?>
            {
                ["answerPublicId"] = answer.PublicId,
                ["content"] = content,
                ["expectedContentRevision"] = expectedContentRevision,
                ["sourceType"] = sourceType,
                ["restoredFromRevisionId"] = restoredFromRevisionId
            },
            MutationDuplicateWindowSeconds);
        if (begin.Status != ContentSubmissionBeginStatus.Started)
        {
            var current = await _repository.QueryAnswerAsync(tenantId, answerPublicId);
            if (begin.Status == ContentSubmissionBeginStatus.Succeeded && current != null)
            {
                return await BuildMutationAsync(context, current, operatorId);
            }
            EnsureStarted(begin);
        }

        var now = DateTime.UtcNow;
        var safeName = NormalizeOperatorName(operatorName);
        answer.Content = content;
        answer.ContentRevision = expectedContentRevision + 1;
        answer.EditCount++;
        answer.ModifyTime = now;
        answer.ModifyBy = safeName;
        answer.ModifyId = operatorId;
        var revision = BuildRevision(
            answer,
            answer.ContentRevision,
            sourceType,
            restoredFromRevisionId,
            content,
            operatorId,
            safeName,
            now);
        if (!await _repository.UpdateAnswerContentAsync(answer, expectedContentRevision, revision))
        {
            throw Conflict();
        }
        await BindAnswerAttachmentsAsync(
            tenantId,
            answer.Id,
            revision.Id,
            operatorId,
            safeName,
            AttachmentReferenceHelper.ExtractAttachmentIds(content),
            now);
        await CompleteAsync(begin, ContentSubmissionResultTypes.PostAnswerContentRevision, revision.Id, answer.PublicId);
        return await BuildMutationAsync(context, answer, operatorId);
    }

    private async Task BindAnswerAttachmentsAsync(
        long tenantId,
        long answerId,
        long revisionId,
        long authorId,
        string operatorName,
        IReadOnlySet<long> attachmentIds,
        DateTime now)
    {
        try
        {
            await _repository.BindAnswerAttachmentsAsync(
                tenantId,
                answerId,
                revisionId,
                authorId,
                operatorName,
                attachmentIds,
                now);
        }
        catch (ForumAnswerAttachmentUnavailableException)
        {
            throw Error(
                "回答引用的附件不存在、不可用或归属无效",
                StatusCodes.Status409Conflict,
                ForumQuestionErrorCodes.AttachmentUnavailable);
        }
    }

    private async Task<ContentSubmissionBeginResult> BeginMutationAsync(
        long tenantId,
        long userId,
        string operationType,
        string? clientSubmissionId,
        string targetType,
        long targetId,
        IReadOnlyDictionary<string, object?> values,
        int duplicateWindowSeconds,
        int frequencyWindowSeconds = 0)
    {
        var snapshot = _contentSubmissionService.CreateRequestSnapshot(values, values);
        return await _contentSubmissionService.BeginAsync(new ContentSubmissionBeginRequest
        {
            TenantId = tenantId,
            UserId = userId,
            OperationType = operationType,
            ClientSubmissionId = clientSubmissionId,
            TargetType = targetType,
            TargetId = targetId,
            RequestDigest = snapshot.RequestDigest,
            RequestSummary = snapshot.RequestSummary,
            ContentFingerprint = snapshot.ContentFingerprint,
            DuplicateWindowSeconds = duplicateWindowSeconds,
            FrequencyWindowSeconds = frequencyWindowSeconds,
            FrequencyTargetType = frequencyWindowSeconds > 0 ? targetType : null,
            FrequencyTargetId = frequencyWindowSeconds > 0 ? targetId : null
        });
    }

    private Task<ContentSubmissionBeginResult> BeginAcceptanceAsync(
        ForumQuestionContext context,
        string? answerPublicId,
        int expectedAcceptanceRevision,
        long operatorId,
        string clientSubmissionId,
        string operationType)
    {
        return BeginMutationAsync(
            context.Post.TenantId,
            operatorId,
            operationType,
            clientSubmissionId,
            "PostQuestion",
            context.Question.Id,
            new Dictionary<string, object?>
            {
                ["postPublicId"] = context.Post.PublicId,
                ["answerPublicId"] = answerPublicId,
                ["expectedAcceptanceRevision"] = expectedAcceptanceRevision
            },
            MutationDuplicateWindowSeconds);
    }

    private async Task CompleteAsync(
        ContentSubmissionBeginResult begin,
        string resultType,
        long resultId,
        string? publicId)
    {
        if (begin.RecordId.HasValue)
        {
            await _contentSubmissionService.CompleteSuccessAsync(new ContentSubmissionCompletionRequest
            {
                RecordId = begin.RecordId.Value,
                ResultType = resultType,
                ResultId = resultId,
                ResultPublicId = publicId
            });
        }
    }

    private async Task<ForumQuestionContext> RequireQuestionAsync(long tenantId, string postIdentifier)
    {
        return await _repository.QueryQuestionAsync(tenantId, postIdentifier)
               ?? throw Error("问答帖不存在", StatusCodes.Status404NotFound, ForumQuestionErrorCodes.NotFound);
    }

    private async Task<PostAnswer> RequireAnswerAsync(long tenantId, string answerPublicId, bool includeDeleted = false)
    {
        if (!PostAnswer.HasPublicIdFormat(answerPublicId))
        {
            throw AnswerNotFound();
        }
        return await _repository.QueryAnswerAsync(tenantId, answerPublicId, includeDeleted)
               ?? throw AnswerNotFound();
    }

    private async Task<PostAnswer?> ResolveAcceptedAnswerAsync(ForumQuestionContext context)
    {
        if (!context.Question.AcceptedAnswerId.HasValue)
        {
            return null;
        }
        return await _repository.QueryAnswerByIdAsync(
            context.Question.TenantId,
            context.Question.AcceptedAnswerId.Value);
    }

    private async Task<PostAnswerMutationVo> BuildMutationAsync(
        ForumQuestionContext context,
        PostAnswer answer,
        long currentUserId)
    {
        var latestContext = await _repository.QueryQuestionAsync(context.Post.TenantId, context.Post.Id.ToString())
                            ?? context;
        return new PostAnswerMutationVo
        {
            VoPostPublicId = context.Post.PublicId ?? string.Empty,
            VoAnswer = MapAnswer(answer, currentUserId),
            VoAnswerCount = latestContext.Question.AnswerCount
        };
    }

    private static PostAnswerVo MapAnswer(PostAnswer answer, long currentUserId)
    {
        var isAuthor = currentUserId > 0 && answer.AuthorId == currentUserId;
        var mutable = isAuthor && !answer.IsAccepted && !answer.IsDeleted && answer.IsEnabled;
        return new PostAnswerVo
        {
            VoAnswerId = answer.Id,
            VoPublicId = answer.PublicId,
            VoPostId = answer.PostId,
            VoAuthorId = answer.AuthorId,
            VoAuthorName = answer.AuthorName,
            VoContent = answer.Content,
            VoIsAccepted = answer.IsAccepted,
            VoContentRevision = answer.ContentRevision,
            VoEditCount = answer.EditCount,
            VoIsEnabled = answer.IsEnabled,
            VoCanEdit = mutable,
            VoCanDelete = mutable,
            VoCanReport = currentUserId > 0 && currentUserId != answer.AuthorId && !answer.IsDeleted && answer.IsEnabled,
            VoCreateTime = answer.CreateTime,
            VoModifyTime = answer.ModifyTime
        };
    }

    private static PostAnswerContentRevision BuildRevision(
        PostAnswer answer,
        int revisionNumber,
        string sourceType,
        long? restoredFromRevisionId,
        string content,
        long editorId,
        string editorName,
        DateTime now)
    {
        return new PostAnswerContentRevision
        {
            TenantId = answer.TenantId,
            AnswerId = answer.Id,
            PostId = answer.PostId,
            RevisionNumber = revisionNumber,
            SourceType = sourceType,
            RestoredFromRevisionId = restoredFromRevisionId,
            IntegrityStatus = ForumContentRevisionIntegrityStatuses.Complete,
            Content = content,
            EditorId = editorId,
            EditorName = editorName,
            CreateTime = now,
            CreateBy = editorName,
            CreateId = editorId
        };
    }

    private static PostAnswerAcceptanceEvent BuildAcceptanceEvent(
        ForumQuestionContext context,
        PostAnswer? previous,
        PostAnswer? current,
        string eventType,
        long operatorId,
        string operatorName,
        DateTime now)
    {
        return new PostAnswerAcceptanceEvent
        {
            TenantId = context.Question.TenantId,
            PostId = context.Post.Id,
            PostQuestionId = context.Question.Id,
            EventType = eventType,
            PreviousAnswerId = previous?.Id,
            PreviousAnswerContentRevision = previous?.ContentRevision,
            CurrentAnswerId = current?.Id,
            CurrentAnswerContentRevision = current?.ContentRevision,
            OperatorId = operatorId,
            OperatorName = operatorName,
            CreateTime = now
        };
    }

    private async Task EnqueueQuestionAnsweredNotificationAsync(
        ForumQuestionContext context,
        PostAnswer answer,
        DateTime occurredAtUtc)
    {
        if (context.Post.AuthorId == answer.AuthorId)
        {
            return;
        }
        var dto = BuildNotification(
            NotificationType.QuestionAnswered,
            "问题收到新回答",
            context,
            answer,
            answer.AuthorId,
            answer.AuthorName,
            [context.Post.AuthorId],
            occurredAtUtc);
        await EnqueueNotificationAsync(
            $"question:{context.Question.Id}:answer:{answer.Id}:created",
            answer.Id,
            dto,
            occurredAtUtc);
    }

    private async Task EnqueueAcceptanceNotificationsAsync(
        ForumQuestionContext context,
        PostAnswer? previous,
        PostAnswer current,
        string operatorName,
        long operatorId,
        DateTime occurredAtUtc)
    {
        var accepted = BuildNotification(
            NotificationType.AnswerAccepted,
            "回答已被采纳",
            context,
            current,
            operatorId,
            operatorName,
            [current.AuthorId],
            occurredAtUtc);
        await EnqueueNotificationAsync(
            $"question:{context.Question.Id}:acceptance:{context.Question.AcceptanceRevision + 1}:accepted",
            current.Id,
            accepted,
            occurredAtUtc);
        if (previous != null && previous.AuthorId != current.AuthorId)
        {
            await EnqueueAcceptanceRevokedNotificationAsync(context, previous, operatorName, operatorId, occurredAtUtc);
        }
    }

    private async Task EnqueueAcceptanceRevokedNotificationAsync(
        ForumQuestionContext context,
        PostAnswer previous,
        string operatorName,
        long operatorId,
        DateTime occurredAtUtc)
    {
        var revoked = BuildNotification(
            NotificationType.AnswerAcceptanceRevoked,
            "回答采纳状态已撤销",
            context,
            previous,
            operatorId,
            operatorName,
            [previous.AuthorId],
            occurredAtUtc);
        await EnqueueNotificationAsync(
            $"question:{context.Question.Id}:acceptance:{context.Question.AcceptanceRevision + 1}:revoked:{previous.Id}",
            previous.Id,
            revoked,
            occurredAtUtc);
    }

    private static CreateNotificationDto BuildNotification(
        string type,
        string title,
        ForumQuestionContext context,
        PostAnswer answer,
        long triggerId,
        string triggerName,
        List<long> receivers,
        DateTime occurredAtUtc)
    {
        return new CreateNotificationDto
        {
            Type = type,
            Title = title,
            Content = context.Post.Title,
            Priority = (int)NotificationPriority.Normal,
            BusinessType = BusinessType.Post,
            BusinessId = context.Post.Id,
            TriggerId = triggerId,
            TriggerName = triggerName,
            ReceiverUserIds = receivers,
            TenantId = context.Post.TenantId,
            TemplateArguments = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["actorName"] = triggerName,
                ["targetTitle"] = context.Post.Title
            },
            TargetKind = NotificationTargetKind.ForumPost,
            Target = new NotificationTargetData
            {
                PostId = context.Post.Id,
                PostPublicId = context.Post.PublicId,
                AnswerId = answer.Id,
                AnswerPublicId = answer.PublicId
            },
            OccurredAtUtc = occurredAtUtc
        };
    }

    private Task EnqueueNotificationAsync(
        string eventKey,
        long answerId,
        CreateNotificationDto notification,
        DateTime occurredAtUtc)
    {
        notification.NotificationId = SnowFlakeSingle.Instance.NextId();
        notification.BusinessKey = $"notification:{eventKey}";
        return _reliableOutboxService.AddAsync(
            ReliableOutboxSources.Main,
            notification.TenantId ?? 0,
            ReliableTaskTypes.NotificationRequested,
            $"task:notification:{eventKey}",
            "PostAnswer",
            answerId.ToString(),
            new NotificationRequestedTaskPayload(notification),
            occurredAtUtc);
    }

    private static PostAnswerAcceptanceMutationVo MapAcceptance(
        ForumQuestionContext context,
        string? acceptedAnswerPublicId)
    {
        return new PostAnswerAcceptanceMutationVo
        {
            VoPostPublicId = context.Post.PublicId ?? string.Empty,
            VoAcceptedAnswerPublicId = acceptedAnswerPublicId,
            VoAcceptanceRevision = context.Question.AcceptanceRevision,
            VoIsSolved = context.Question.IsSolved
        };
    }

    private static void EnsureQuestionOwner(ForumQuestionContext context, long operatorId)
    {
        if (context.Post.AuthorId != operatorId)
        {
            throw Error("只有提问者可以变更采纳状态", StatusCodes.Status403Forbidden, ForumQuestionErrorCodes.AccessDenied);
        }
    }

    private static void EnsureAuthorAndMutable(PostAnswer answer, long operatorId)
    {
        if (answer.AuthorId != operatorId)
        {
            throw Error("只有回答作者可以修改该回答", StatusCodes.Status403Forbidden, ForumQuestionErrorCodes.AccessDenied);
        }
        if (answer.IsAccepted)
        {
            throw Error("已采纳回答不能编辑或删除", StatusCodes.Status409Conflict, ForumQuestionErrorCodes.AcceptedAnswerLocked);
        }
    }

    private static void EnsureRevisionAccess(PostAnswer answer, long currentUserId)
    {
        if (answer.AuthorId != currentUserId)
        {
            throw Error("仅回答作者可以查看历史版本", StatusCodes.Status403Forbidden, ForumQuestionErrorCodes.AccessDenied);
        }
    }

    private static void EnsureStarted(ContentSubmissionBeginResult result)
    {
        var (message, status) = result.Status switch
        {
            ContentSubmissionBeginStatus.Processing => ("请求正在处理中", StatusCodes.Status409Conflict),
            ContentSubmissionBeginStatus.Conflict => ("幂等键与已有请求不一致", StatusCodes.Status409Conflict),
            ContentSubmissionBeginStatus.InvalidKey => ("客户端提交标识无效", StatusCodes.Status400BadRequest),
            ContentSubmissionBeginStatus.DuplicateContent => ("短时间内不能重复提交相同内容", StatusCodes.Status409Conflict),
            ContentSubmissionBeginStatus.FrequencyLimited => ("操作过于频繁，请稍后重试", StatusCodes.Status429TooManyRequests),
            ContentSubmissionBeginStatus.Succeeded => ("请求已完成但结果不可恢复", StatusCodes.Status409Conflict),
            _ => ("内容提交状态异常", StatusCodes.Status409Conflict)
        };
        throw Error(message, status, ForumQuestionErrorCodes.Conflict);
    }

    private static string NormalizeContent(string content)
    {
        var normalized = content?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("回答内容不能为空", nameof(content));
        }
        if (normalized.Length > 20000)
        {
            throw new ArgumentException("回答内容不能超过 20000 个字符", nameof(content));
        }
        return normalized;
    }

    private static string NormalizeOperatorName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "System" : value.Trim();

    private static BusinessException AnswerNotFound(string message = "回答不存在") =>
        Error(message, StatusCodes.Status404NotFound, ForumQuestionErrorCodes.AnswerNotFound);

    private static BusinessException Conflict() =>
        Error("回答已被其他请求修改，请刷新后重试", StatusCodes.Status409Conflict, ForumQuestionErrorCodes.Conflict);

    private static BusinessException AcceptanceConflict() =>
        Error("采纳状态已被其他请求修改，请刷新后重试", StatusCodes.Status409Conflict, ForumQuestionErrorCodes.AcceptanceConflict);

    private static BusinessException Error(string message, int statusCode, string errorCode) =>
        new(message, statusCode, errorCode, ForumQuestionErrorCodes.ResolveMessageKey(errorCode));
}
