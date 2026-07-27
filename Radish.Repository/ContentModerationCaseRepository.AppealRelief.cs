using System.Text.Json;
using Radish.IRepository;
using Radish.Model;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Repository;

public sealed partial class ContentModerationCaseRepository
{
    public Task<ContentModerationAppealReliefWriteResult> ExecuteAppealReliefAsync(
        ContentModerationAppealReliefCommand command)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync(
                    $"moderation-appeal:{command.TenantId}:{command.AppealPublicId}");
                await AcquireTransactionLockAsync(
                    $"moderation-appeal-relief-operation:{command.TenantId}:{command.OperationKey}");
                var appeal = await QueryAppealForWriteAsync(command.TenantId, command.AppealPublicId);
                var replayEvent = await DbProtectedClient.Queryable<ContentModerationAppealEvent>()
                    .Where(item =>
                        item.TenantId == command.TenantId &&
                        item.AppealId == appeal.Id &&
                        item.OperationKey == command.OperationKey)
                    .FirstAsync();
                if (replayEvent != null)
                {
                    var replay = await QueryAppealReliefResultAsync(appeal, true);
                    DbProtectedClient.Ado.CommitTran();
                    return replay;
                }

                await EnsureAppealOperationAvailableAsync(command.TenantId, command.OperationKey, appeal.Id);
                if (appeal.Version != command.ExpectedAppealVersion ||
                    appeal.Status is not
                        (int)ContentModerationAppealStatus.ReliefPending and not
                        (int)ContentModerationAppealStatus.ReliefFailed)
                {
                    throw new ContentModerationConcurrencyException();
                }

                var moderationCase = await DbProtectedClient.Queryable<ContentModerationCase>()
                    .Where(item =>
                        item.TenantId == command.TenantId &&
                        item.Id == appeal.CaseId &&
                        !item.IsDeleted)
                    .FirstAsync() ?? throw new ContentModerationCaseNotFoundException();
                var targetActions = new List<ContentModerationTargetAction>();
                var userActions = new List<UserModerationAction>();
                var eventSequence = await GetNextAppealEventSequenceAsync(appeal.Id);
                var operationFingerprint = BuildOperationFingerprint(command.OperationKey);
                var hasPending = false;
                var hasFailure = false;
                await DbProtectedClient.Insertable(CreateAppealEvent(
                    appeal,
                    eventSequence++,
                    "ReliefExecutionStarted",
                    command.OperationKey,
                    command.OperatorUserId,
                    command.OperatorName,
                    fromStatus: appeal.Status,
                    toStatus: appeal.Status,
                    resultCode: "Accepted")).ExecuteCommandAsync();

                if ((((ContentModerationReliefScope)appeal.GrantedScope) &
                     ContentModerationReliefScope.TargetContent) != 0)
                {
                    var sourceAction = await DbProtectedClient.Queryable<ContentModerationTargetAction>()
                        .Where(item =>
                            item.TenantId == command.TenantId &&
                            item.CaseId == appeal.CaseId &&
                            item.ActionType == (int)ContentModerationTargetActionType.Restrict &&
                            item.ChangedTargetState)
                        .OrderByDescending(item => item.CompletedAt)
                        .OrderByDescending(item => item.Id)
                        .FirstAsync();
                    if (sourceAction != null)
                    {
                        var restoreOperationKey =
                            $"appeal-relief:{appeal.Id}:target:{operationFingerprint}";
                        var restoreAction = await DbProtectedClient.Queryable<ContentModerationTargetAction>()
                            .Where(item =>
                                item.TenantId == command.TenantId &&
                                item.OperationKey == restoreOperationKey)
                            .FirstAsync();
                        if (restoreAction == null)
                        {
                            restoreAction = CreateTargetAction(
                                moderationCase,
                                appeal.Id,
                                ContentModerationTargetActionType.Restore,
                                sourceAction.Id,
                                restoreOperationKey,
                                sourceAction.ResultTargetVersion,
                                command.OperatorUserId,
                                command.OperatorName,
                                command.NowUtc);
                            await DbProtectedClient.Insertable(restoreAction).ExecuteCommandAsync();
                            if ((ContentReportTargetTypeEnum)moderationCase.TargetType ==
                                ContentReportTargetTypeEnum.ChatMessage)
                            {
                                await EnqueueChatRestoreAsync(
                                    appeal,
                                    moderationCase,
                                    sourceAction,
                                    restoreAction,
                                    command);
                                hasPending = true;
                            }
                            else
                            {
                                var targetResult = await RestoreMainTargetAsync(
                                    moderationCase,
                                    sourceAction,
                                    restoreAction,
                                    command);
                                CompleteTargetAction(
                                    restoreAction,
                                    targetResult.resultCode,
                                    targetResult.changed,
                                    targetResult.resultVersion,
                                    command.NowUtc);
                                if (targetResult.resultCode is "TargetChanged" or "ParentUnavailable")
                                {
                                    restoreAction.Status =
                                        (int)ContentModerationTargetActionStatus.Superseded;
                                }
                                await DbProtectedClient.Updateable(restoreAction).ExecuteCommandAsync();
                                hasFailure |= restoreAction.Status ==
                                              (int)ContentModerationTargetActionStatus.Failed;
                            }
                        }

                        targetActions.Add(restoreAction);
                        await DbProtectedClient.Insertable(CreateAppealEvent(
                            appeal,
                            eventSequence++,
                            hasPending ? "ReliefRequested" : "ReliefApplied",
                            null,
                            command.OperatorUserId,
                            command.OperatorName,
                            relatedTargetActionId: restoreAction.Id,
                            resultCode: hasPending
                                ? "ChatRestoreQueued"
                                : restoreAction.ResultCode)).ExecuteCommandAsync();
                    }
                }

                if ((((ContentModerationReliefScope)appeal.GrantedScope) &
                     ContentModerationReliefScope.Mute) != 0)
                {
                    var result = await RestoreUserPolicyAsync(
                        appeal,
                        moderationCase,
                        UserModerationPolicyType.Mute,
                        ModerationActionTypeEnum.Unmute,
                        $"appeal-relief:{appeal.Id}:mute:{operationFingerprint}",
                        command);
                    if (result != null)
                    {
                        userActions.Add(result);
                    }

                    await DbProtectedClient.Insertable(CreateAppealEvent(
                        appeal,
                        eventSequence++,
                        result == null ? "ReliefNoEffect" : "ReliefApplied",
                        null,
                        command.OperatorUserId,
                        command.OperatorName,
                        relatedUserActionId: result?.Id,
                        resultCode: result?.ResultCode ?? "MuteStateChanged")).ExecuteCommandAsync();
                }

                if ((((ContentModerationReliefScope)appeal.GrantedScope) &
                     ContentModerationReliefScope.Ban) != 0)
                {
                    var result = await RestoreUserPolicyAsync(
                        appeal,
                        moderationCase,
                        UserModerationPolicyType.Ban,
                        ModerationActionTypeEnum.Unban,
                        $"appeal-relief:{appeal.Id}:ban:{operationFingerprint}",
                        command);
                    if (result != null)
                    {
                        userActions.Add(result);
                    }

                    await DbProtectedClient.Insertable(CreateAppealEvent(
                        appeal,
                        eventSequence++,
                        result == null ? "ReliefNoEffect" : "ReliefApplied",
                        null,
                        command.OperatorUserId,
                        command.OperatorName,
                        relatedUserActionId: result?.Id,
                        resultCode: result?.ResultCode ?? "BanStateChanged")).ExecuteCommandAsync();
                }

                var nextStatus = hasPending
                    ? (int)ContentModerationAppealStatus.ReliefPending
                    : hasFailure
                        ? (int)ContentModerationAppealStatus.ReliefFailed
                        : (int)ContentModerationAppealStatus.Resolved;
                var previousStatus = appeal.Status;
                var nextVersion = appeal.Version + 1;
                var affected = await DbProtectedClient.Updateable<ContentModerationAppeal>()
                    .SetColumns(item => new ContentModerationAppeal
                    {
                        Status = nextStatus,
                        Version = nextVersion,
                        ResolvedAt = nextStatus == (int)ContentModerationAppealStatus.Resolved
                            ? command.NowUtc
                            : null,
                        ModifyTime = command.NowUtc,
                        ModifyBy = command.OperatorName,
                        ModifyId = command.OperatorUserId
                    })
                    .Where(item =>
                        item.Id == appeal.Id &&
                        item.Version == command.ExpectedAppealVersion &&
                        (item.Status == (int)ContentModerationAppealStatus.ReliefPending ||
                         item.Status == (int)ContentModerationAppealStatus.ReliefFailed))
                    .ExecuteCommandAsync();
                EnsureSingleRow(affected);
                appeal.Status = nextStatus;
                appeal.Version = nextVersion;
                appeal.ResolvedAt = nextStatus == (int)ContentModerationAppealStatus.Resolved
                    ? command.NowUtc
                    : null;
                appeal.ModifyTime = command.NowUtc;
                appeal.ModifyBy = command.OperatorName;
                appeal.ModifyId = command.OperatorUserId;
                await DbProtectedClient.Insertable(CreateAppealEvent(
                    appeal,
                    eventSequence,
                    nextStatus == (int)ContentModerationAppealStatus.Resolved
                        ? "ReliefCompleted"
                        : nextStatus == (int)ContentModerationAppealStatus.ReliefFailed
                            ? "ReliefFailed"
                            : "ReliefPending",
                    null,
                    command.OperatorUserId,
                    command.OperatorName,
                    fromStatus: previousStatus,
                    toStatus: nextStatus,
                    resultCode: nextStatus == (int)ContentModerationAppealStatus.Resolved
                        ? "ReliefCompleted"
                        : "ReliefPending",
                    resultVersion: nextVersion,
                    expectedVersion: command.ExpectedAppealVersion)).ExecuteCommandAsync();
                if (nextStatus == (int)ContentModerationAppealStatus.Resolved)
                {
                    await AppendCaseAppealResolvedEventAsync(
                        appeal,
                        command.OperatorUserId,
                        command.OperatorName);
                }

                await EnqueueAppealUpdatedNotificationAsync(appeal, command.OperationKey, command.NowUtc);
                DbProtectedClient.Ado.CommitTran();
                return new ContentModerationAppealReliefWriteResult(
                    appeal,
                    targetActions,
                    userActions,
                    false);
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    public Task<ContentModerationAppeal> CompleteChatReliefAsync(
        ContentModerationChatReliefCompletionCommand command)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            DbProtectedClient.Ado.BeginTran();
            try
            {
                await AcquireTransactionLockAsync(
                    $"moderation-appeal-id:{command.TenantId}:{command.AppealId}");
                var appeal = await DbProtectedClient.Queryable<ContentModerationAppeal>()
                    .Where(item => item.TenantId == command.TenantId && item.Id == command.AppealId)
                    .FirstAsync() ?? throw new ContentModerationAppealNotFoundException();
                var action = await DbProtectedClient.Queryable<ContentModerationTargetAction>()
                    .Where(item =>
                        item.TenantId == command.TenantId &&
                        item.Id == command.TargetActionId &&
                        item.AppealId == appeal.Id &&
                        item.ActionType == (int)ContentModerationTargetActionType.Restore)
                    .FirstAsync() ?? throw new ContentModerationTargetActionException("ActionNotFound");
                var actionStatus = (ContentModerationTargetActionStatus)action.Status;
                var completionAlreadyRecorded =
                    actionStatus is ContentModerationTargetActionStatus.Succeeded
                        or ContentModerationTargetActionStatus.Superseded
                        or ContentModerationTargetActionStatus.NoEffect ||
                    actionStatus == ContentModerationTargetActionStatus.Failed && !command.Succeeded;
                if (completionAlreadyRecorded)
                {
                    DbProtectedClient.Ado.CommitTran();
                    return appeal;
                }

                CompleteTargetAction(
                    action,
                    command.ResultCode,
                    command.Succeeded && command.ResultCode == "Restored",
                    null,
                    command.NowUtc,
                    command.Succeeded);
                if (command.Succeeded && command.ResultCode == "TargetChanged")
                {
                    action.Status = (int)ContentModerationTargetActionStatus.Superseded;
                }
                await DbProtectedClient.Updateable(action).ExecuteCommandAsync();
                var nextStatus = command.Succeeded
                    ? (int)ContentModerationAppealStatus.Resolved
                    : (int)ContentModerationAppealStatus.ReliefFailed;
                var previousStatus = appeal.Status;
                var nextVersion = appeal.Version + 1;
                await DbProtectedClient.Updateable<ContentModerationAppeal>()
                    .SetColumns(item => new ContentModerationAppeal
                    {
                        Status = nextStatus,
                        Version = nextVersion,
                        ResolvedAt = command.Succeeded ? command.NowUtc : null,
                        ModifyTime = command.NowUtc,
                        ModifyBy = command.OperatorName,
                        ModifyId = command.OperatorUserId
                    })
                    .Where(item => item.Id == appeal.Id && item.Version == appeal.Version)
                    .ExecuteCommandAsync();
                appeal.Status = nextStatus;
                appeal.Version = nextVersion;
                appeal.ResolvedAt = command.Succeeded ? command.NowUtc : null;
                appeal.ModifyTime = command.NowUtc;
                await DbProtectedClient.Insertable(CreateAppealEvent(
                    appeal,
                    await GetNextAppealEventSequenceAsync(appeal.Id),
                    command.Succeeded ? "ReliefCompleted" : "ReliefFailed",
                    null,
                    command.OperatorUserId,
                    command.OperatorName,
                    relatedTargetActionId: action.Id,
                    fromStatus: previousStatus,
                    toStatus: nextStatus,
                    resultCode: command.ResultCode,
                    resultVersion: nextVersion,
                    expectedVersion: nextVersion - 1)).ExecuteCommandAsync();
                if (command.Succeeded)
                {
                    await AppendCaseAppealResolvedEventAsync(
                        appeal,
                        command.OperatorUserId,
                        command.OperatorName);
                }

                await EnqueueAppealUpdatedNotificationAsync(appeal, command.OperationKey, command.NowUtc);
                DbProtectedClient.Ado.CommitTran();
                return appeal;
            }
            catch
            {
                DbProtectedClient.Ado.RollbackTran();
                throw;
            }
        });
    }

    private async Task<ContentModerationAppealReliefWriteResult> QueryAppealReliefResultAsync(
        ContentModerationAppeal appeal,
        bool replay)
    {
        var targetActions = await DbProtectedClient.Queryable<ContentModerationTargetAction>()
            .Where(item => item.TenantId == appeal.TenantId && item.AppealId == appeal.Id)
            .OrderBy(item => item.CreateTime)
            .ToListAsync();
        var userActions = await DbProtectedClient.Queryable<UserModerationAction>()
            .Where(item => item.TenantId == appeal.TenantId && item.AppealId == appeal.Id)
            .OrderBy(item => item.CreateTime)
            .ToListAsync();
        return new ContentModerationAppealReliefWriteResult(appeal, targetActions, userActions, replay);
    }

    private async Task<(string resultCode, bool changed, int? resultVersion)> RestoreMainTargetAsync(
        ContentModerationCase moderationCase,
        ContentModerationTargetAction sourceAction,
        ContentModerationTargetAction restoreAction,
        ContentModerationAppealReliefCommand command)
    {
        return (ContentReportTargetTypeEnum)moderationCase.TargetType switch
        {
            ContentReportTargetTypeEnum.Post =>
                await RestorePostAsync(moderationCase, sourceAction, command),
            ContentReportTargetTypeEnum.Comment =>
                await RestoreCommentAsync(moderationCase, sourceAction, command),
            ContentReportTargetTypeEnum.PostQuickReply =>
                await RestoreQuickReplyAsync(moderationCase, sourceAction, command),
            ContentReportTargetTypeEnum.PostAnswer =>
                await RestoreAnswerAsync(moderationCase, sourceAction, command),
            ContentReportTargetTypeEnum.Product =>
                await RestoreProductAsync(moderationCase, sourceAction, command),
            _ => throw new ContentModerationTargetActionException("Unsupported")
        };
    }

    private async Task<(string, bool, int?)> RestorePostAsync(
        ContentModerationCase moderationCase,
        ContentModerationTargetAction sourceAction,
        ContentModerationAppealReliefCommand command)
    {
        var post = await DbProtectedClient.Queryable<Post>()
            .Where(item => item.Id == moderationCase.TargetContentId && item.TenantId == command.TenantId)
            .FirstAsync();
        if (post == null || !post.IsDeleted || post.ModerationTargetActionId != sourceAction.Id)
        {
            return ("TargetChanged", false, post?.EditCount);
        }

        var affected = await DbProtectedClient.Updateable<Post>()
            .SetColumns(item => new Post
            {
                IsDeleted = false,
                ModerationTargetActionId = null,
                DeletedAt = null,
                DeletedBy = null,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorUserId
            })
            .Where(item =>
                item.Id == post.Id &&
                item.TenantId == command.TenantId &&
                item.IsDeleted &&
                item.ModerationTargetActionId == sourceAction.Id)
            .ExecuteCommandAsync();
        return affected == 1 ? ("Restored", true, post.EditCount) : ("TargetChanged", false, post.EditCount);
    }

    private async Task<(string, bool, int?)> RestoreCommentAsync(
        ContentModerationCase moderationCase,
        ContentModerationTargetAction sourceAction,
        ContentModerationAppealReliefCommand command)
    {
        var comment = await DbProtectedClient.Queryable<Comment>()
            .Where(item => item.Id == moderationCase.TargetContentId && item.TenantId == command.TenantId)
            .FirstAsync();
        if (comment == null || !comment.IsDeleted || comment.ModerationTargetActionId != sourceAction.Id)
        {
            return ("TargetChanged", false, comment?.EditCount);
        }

        var parentAvailable = await DbProtectedClient.Queryable<Post>()
            .AnyAsync(item =>
                item.Id == comment.PostId &&
                item.TenantId == command.TenantId &&
                item.IsPublished &&
                item.IsEnabled &&
                !item.IsDeleted);
        if (!parentAvailable)
        {
            return ("ParentUnavailable", false, comment.EditCount);
        }

        var affected = await DbProtectedClient.Updateable<Comment>()
            .SetColumns(item => new Comment
            {
                IsDeleted = false,
                ModerationTargetActionId = null,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorUserId
            })
            .Where(item =>
                item.Id == comment.Id &&
                item.TenantId == command.TenantId &&
                item.IsDeleted &&
                item.ModerationTargetActionId == sourceAction.Id)
            .ExecuteCommandAsync();
        return affected == 1
            ? ("Restored", true, comment.EditCount)
            : ("TargetChanged", false, comment.EditCount);
    }

    private async Task<(string, bool, int?)> RestoreQuickReplyAsync(
        ContentModerationCase moderationCase,
        ContentModerationTargetAction sourceAction,
        ContentModerationAppealReliefCommand command)
    {
        var reply = await DbProtectedClient.Queryable<PostQuickReply>()
            .Where(item => item.Id == moderationCase.TargetContentId && item.TenantId == command.TenantId)
            .FirstAsync();
        if (reply == null || !reply.IsDeleted || reply.ModerationTargetActionId != sourceAction.Id)
        {
            return ("TargetChanged", false, null);
        }

        var parentAvailable = await DbProtectedClient.Queryable<Post>()
            .AnyAsync(item =>
                item.Id == reply.PostId &&
                item.TenantId == command.TenantId &&
                item.IsPublished &&
                item.IsEnabled &&
                !item.IsDeleted);
        if (!parentAvailable)
        {
            return ("ParentUnavailable", false, null);
        }

        var affected = await DbProtectedClient.Updateable<PostQuickReply>()
            .SetColumns(item => new PostQuickReply
            {
                IsDeleted = false,
                ModerationTargetActionId = null,
                DeletedAt = null,
                DeletedBy = null,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorUserId
            })
            .Where(item =>
                item.Id == reply.Id &&
                item.TenantId == command.TenantId &&
                item.IsDeleted &&
                item.ModerationTargetActionId == sourceAction.Id)
            .ExecuteCommandAsync();
        return affected == 1 ? ("Restored", true, null) : ("TargetChanged", false, null);
    }

    private async Task<(string, bool, int?)> RestoreAnswerAsync(
        ContentModerationCase moderationCase,
        ContentModerationTargetAction sourceAction,
        ContentModerationAppealReliefCommand command)
    {
        var answer = await DbProtectedClient.Queryable<PostAnswer>()
            .Where(item => item.Id == moderationCase.TargetContentId && item.TenantId == command.TenantId)
            .FirstAsync();
        if (answer == null ||
            answer.IsEnabled ||
            answer.ModerationTargetActionId != sourceAction.Id ||
            answer.IsDeleted)
        {
            return ("TargetChanged", false, answer?.ContentRevision);
        }

        var parentAvailable = await DbProtectedClient.Queryable<Post>()
            .AnyAsync(item =>
                item.Id == answer.PostId &&
                item.TenantId == command.TenantId &&
                item.IsPublished &&
                item.IsEnabled &&
                !item.IsDeleted);
        if (!parentAvailable)
        {
            return ("ParentUnavailable", false, answer.ContentRevision);
        }

        var affected = await DbProtectedClient.Updateable<PostAnswer>()
            .SetColumns(item => new PostAnswer
            {
                IsEnabled = true,
                IsAccepted = false,
                ModerationTargetActionId = null,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorUserId
            })
            .Where(item =>
                item.Id == answer.Id &&
                item.TenantId == command.TenantId &&
                !item.IsEnabled &&
                !item.IsDeleted &&
                item.ModerationTargetActionId == sourceAction.Id)
            .ExecuteCommandAsync();
        if (affected == 1)
        {
            var questionAffected = await DbProtectedClient.Updateable<PostQuestion>()
                .SetColumns(item => new PostQuestion
                {
                    AnswerCount = item.AnswerCount + 1,
                    ModifyTime = command.NowUtc,
                    ModifyBy = command.OperatorName,
                    ModifyId = command.OperatorUserId
                })
                .Where(item =>
                    item.TenantId == command.TenantId &&
                    item.PostId == answer.PostId &&
                    !item.IsDeleted)
                .ExecuteCommandAsync();
            if (questionAffected != 1)
            {
                throw new ContentModerationTargetActionException("ParentUnavailable");
            }
        }
        return affected == 1
            ? ("Restored", true, answer.ContentRevision)
            : ("TargetChanged", false, answer.ContentRevision);
    }

    private async Task<(string, bool, int?)> RestoreProductAsync(
        ContentModerationCase moderationCase,
        ContentModerationTargetAction sourceAction,
        ContentModerationAppealReliefCommand command)
    {
        var product = await DbProtectedClient.Queryable<Product>()
            .Where(item => item.Id == moderationCase.TargetContentId && item.TenantId == command.TenantId)
            .FirstAsync();
        if (product == null ||
            product.IsDeleted ||
            !product.IsEnabled ||
            product.IsOnSale ||
            product.ModerationTargetActionId != sourceAction.Id ||
            !sourceAction.ResultTargetVersion.HasValue ||
            product.Version != sourceAction.ResultTargetVersion.Value)
        {
            return ("TargetChanged", false, product?.Version);
        }

        var affected = await DbProtectedClient.Updateable<Product>()
            .SetColumns(item => new Product
            {
                IsOnSale = true,
                ModerationTargetActionId = null,
                OnSaleTime = command.NowUtc,
                OffSaleTime = null,
                Version = item.Version + 1,
                ModifyTime = command.NowUtc,
                ModifyBy = command.OperatorName,
                ModifyId = command.OperatorUserId
            })
            .Where(item =>
                item.Id == product.Id &&
                item.TenantId == command.TenantId &&
                !item.IsDeleted &&
                item.IsEnabled &&
                !item.IsOnSale &&
                item.ModerationTargetActionId == sourceAction.Id &&
                item.Version == sourceAction.ResultTargetVersion.Value)
            .ExecuteCommandAsync();
        return affected == 1
            ? ("Restored", true, product.Version + 1)
            : ("TargetChanged", false, product.Version);
    }

    private async Task<UserModerationAction?> RestoreUserPolicyAsync(
        ContentModerationAppeal appeal,
        ContentModerationCase moderationCase,
        UserModerationPolicyType policyType,
        ModerationActionTypeEnum correctiveAction,
        string operationKey,
        ContentModerationAppealReliefCommand command)
    {
        await AcquireTransactionLockAsync(
            $"moderation-user:{command.TenantId}:{appeal.AppellantUserId}:{(int)policyType}");
        var state = await DbProtectedClient.Queryable<UserModerationState>()
            .Where(item =>
                item.TenantId == command.TenantId &&
                item.TargetUserId == appeal.AppellantUserId &&
                item.PolicyType == (int)policyType)
            .FirstAsync();
        if (state == null ||
            state.State != (int)UserModerationStateValue.Active ||
            state.SourceCaseId != moderationCase.Id ||
            state.EffectiveUntil.HasValue && state.EffectiveUntil <= command.NowUtc)
        {
            return null;
        }

        var (action, _) = await WriteUserActionStateAsync(
            state,
            moderationCase.Id,
            null,
            command.TenantId,
            appeal.AppellantUserId,
            null,
            (int)correctiveAction,
            null,
            "治理申诉纠正",
            operationKey,
            command.OperatorUserId,
            command.OperatorName,
            command.NowUtc,
            appeal.Id);
        return action;
    }

    private async Task EnqueueChatRestoreAsync(
        ContentModerationAppeal appeal,
        ContentModerationCase moderationCase,
        ContentModerationTargetAction sourceAction,
        ContentModerationTargetAction restoreAction,
        ContentModerationAppealReliefCommand command)
    {
        var payload = new ContentModerationChatRestoreTaskPayload(
            command.TenantId,
            appeal.Id,
            restoreAction.Id,
            sourceAction.Id,
            moderationCase.TargetContentId,
            command.OperationKey,
            command.OperatorUserId,
            command.OperatorName);
        await DbProtectedClient.Insertable(new ReliableOutboxMessage
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = command.TenantId,
            TaskType = ReliableTaskTypes.ContentModerationChatRestore,
            SchemaVersion = 1,
            IdempotencyKey = $"moderation-chat-restore:{restoreAction.OperationKey}",
            AggregateType = "ContentModerationAppeal",
            AggregateId = appeal.Id.ToString(),
            PayloadJson = JsonSerializer.Serialize(payload),
            Status = ReliableOutboxStatuses.Pending,
            MaxAttempts = 6,
            OccurredAtUtc = command.NowUtc,
            AvailableAtUtc = command.NowUtc,
            CreateTime = command.NowUtc
        }).ExecuteCommandAsync();
    }
}
