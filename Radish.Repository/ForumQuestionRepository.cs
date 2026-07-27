using Radish.Common;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.Constants;
using SqlSugar;

namespace Radish.Repository;

/// <summary>问答回答、Revision 与采纳状态的 Main 权威仓储。</summary>
public sealed class ForumQuestionRepository : BaseRepository<PostAnswer>, IForumQuestionRepository
{
    public ForumQuestionRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<ForumQuestionContext?> QueryQuestionAsync(long tenantId, string postIdentifier)
    {
        var normalized = postIdentifier?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var numericId = long.TryParse(normalized, out var parsedId) ? parsedId : 0;
        var row = await DbProtectedClient.Queryable<Post, PostQuestion>(
                (post, question) => post.Id == question.PostId)
            .Where((post, question) =>
                post.TenantId == tenantId &&
                question.TenantId == tenantId &&
                !post.IsDeleted &&
                post.IsPublished &&
                !question.IsDeleted &&
                (post.Id == numericId || post.PublicId == normalized))
            .Select((post, question) => new
            {
                Post = post,
                Question = question
            })
            .FirstAsync();
        return row == null ? null : new ForumQuestionContext(row.Post, row.Question);
    }

    public async Task<PostAnswer?> QueryAnswerAsync(long tenantId, string answerPublicId, bool includeDeleted = false)
    {
        var normalized = answerPublicId?.Trim() ?? string.Empty;
        var query = DbProtectedClient.Queryable<PostAnswer>()
            .Where(item => item.TenantId == tenantId && item.PublicId == normalized);
        if (!includeDeleted)
        {
            query = query.Where(item => !item.IsDeleted && item.IsEnabled);
        }
        return await query.FirstAsync();
    }

    public async Task<PostAnswer?> QueryAnswerByIdAsync(long tenantId, long answerId, bool includeDeleted = false)
    {
        var query = DbProtectedClient.Queryable<PostAnswer>()
            .Where(item => item.TenantId == tenantId && item.Id == answerId);
        if (!includeDeleted)
        {
            query = query.Where(item => !item.IsDeleted && item.IsEnabled);
        }
        return await query.FirstAsync();
    }

    public async Task<(IReadOnlyList<PostAnswer> Items, int Total)> QueryAnswerPageAsync(
        long tenantId,
        long postId,
        int pageIndex,
        int pageSize,
        string sort)
    {
        var safePageIndex = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var query = DbProtectedClient.Queryable<PostAnswer>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.PostId == postId &&
                !item.IsDeleted &&
                item.IsEnabled);
        var total = await query.CountAsync();
        query = string.Equals(sort, "latest", StringComparison.OrdinalIgnoreCase)
            ? query.OrderBy(item => item.CreateTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
            : query.OrderBy(item => item.IsAccepted, OrderByType.Desc)
                .OrderBy(item => item.CreateTime, OrderByType.Asc)
                .OrderBy(item => item.Id, OrderByType.Asc);
        var items = await query
            .Skip((safePageIndex - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();
        return (items, total);
    }

    public async Task<IReadOnlyList<PostAnswerContentRevision>> QueryAnswerRevisionsAsync(
        long tenantId,
        long answerId)
    {
        return await DbProtectedClient.Queryable<PostAnswerContentRevision>()
            .Where(item => item.TenantId == tenantId && item.AnswerId == answerId)
            .OrderBy(item => item.RevisionNumber, OrderByType.Desc)
            .ToListAsync();
    }

    public async Task<PostAnswerContentRevision?> QueryAnswerRevisionAsync(
        long tenantId,
        long answerId,
        int revisionNumber)
    {
        return await DbProtectedClient.Queryable<PostAnswerContentRevision>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.AnswerId == answerId &&
                item.RevisionNumber == revisionNumber)
            .FirstAsync();
    }

    public async Task<long> InsertAnswerAsync(PostAnswer answer, PostAnswerContentRevision revision)
    {
        answer.Id = SnowFlakeSingle.Instance.NextId();
        revision.Id = SnowFlakeSingle.Instance.NextId();
        revision.AnswerId = answer.Id;
        await DbProtectedClient.Insertable(answer).ExecuteCommandAsync();
        await DbProtectedClient.Insertable(revision).ExecuteCommandAsync();
        var affected = await DbProtectedClient.Updateable<PostQuestion>()
            .SetColumns(item => new PostQuestion
            {
                AnswerCount = item.AnswerCount + 1,
                ModifyTime = answer.CreateTime,
                ModifyBy = answer.CreateBy,
                ModifyId = answer.CreateId
            })
            .Where(item =>
                item.TenantId == answer.TenantId &&
                item.PostId == answer.PostId &&
                !item.IsDeleted)
            .ExecuteCommandAsync();
        if (affected != 1)
        {
            throw new InvalidOperationException("回答计数更新失败。");
        }
        return answer.Id;
    }

    public async Task<bool> UpdateAnswerContentAsync(
        PostAnswer answer,
        int expectedRevision,
        PostAnswerContentRevision revision)
    {
        var affected = await DbProtectedClient.Updateable<PostAnswer>()
            .SetColumns(item => new PostAnswer
            {
                Content = answer.Content,
                ContentRevision = answer.ContentRevision,
                EditCount = answer.EditCount,
                ModifyTime = answer.ModifyTime,
                ModifyBy = answer.ModifyBy,
                ModifyId = answer.ModifyId
            })
            .Where(item =>
                item.Id == answer.Id &&
                item.TenantId == answer.TenantId &&
                !item.IsDeleted &&
                item.IsEnabled &&
                !item.IsAccepted &&
                item.ContentRevision == expectedRevision)
            .ExecuteCommandAsync();
        if (affected != 1)
        {
            return false;
        }

        revision.Id = SnowFlakeSingle.Instance.NextId();
        await DbProtectedClient.Insertable(revision).ExecuteCommandAsync();
        return true;
    }

    public async Task<bool> SoftDeleteAnswerAsync(
        PostAnswer answer,
        int expectedRevision,
        string operatorName,
        long operatorId,
        DateTime now)
    {
        var affected = await DbProtectedClient.Updateable<PostAnswer>()
            .SetColumns(item => new PostAnswer
            {
                IsDeleted = true,
                DeletedAt = now,
                DeletedBy = operatorName,
                ModifyTime = now,
                ModifyBy = operatorName,
                ModifyId = operatorId
            })
            .Where(item =>
                item.Id == answer.Id &&
                item.TenantId == answer.TenantId &&
                !item.IsDeleted &&
                item.IsEnabled &&
                !item.IsAccepted &&
                item.ContentRevision == expectedRevision)
            .ExecuteCommandAsync();
        if (affected != 1)
        {
            return false;
        }

        var questionAffected = await DbProtectedClient.Updateable<PostQuestion>()
            .SetColumns(item => new PostQuestion
            {
                AnswerCount = item.AnswerCount > 0 ? item.AnswerCount - 1 : 0,
                ModifyTime = now,
                ModifyBy = operatorName,
                ModifyId = operatorId
            })
            .Where(item =>
                item.TenantId == answer.TenantId &&
                item.PostId == answer.PostId &&
                !item.IsDeleted)
            .ExecuteCommandAsync();
        if (questionAffected != 1)
        {
            throw new InvalidOperationException("回答计数更新失败。");
        }
        return true;
    }

    public async Task<bool> ChangeAcceptanceAsync(
        ForumQuestionContext context,
        PostAnswer? previousAnswer,
        PostAnswer? nextAnswer,
        int expectedAcceptanceRevision,
        PostAnswerAcceptanceEvent acceptanceEvent,
        string operatorName,
        long operatorId,
        DateTime now)
    {
        if (previousAnswer != null)
        {
            await DbProtectedClient.Updateable<PostAnswer>()
                .SetColumns(item => new PostAnswer
                {
                    IsAccepted = false,
                    ModifyTime = now,
                    ModifyBy = operatorName,
                    ModifyId = operatorId
                })
                .Where(item =>
                    item.Id == previousAnswer.Id &&
                    item.TenantId == context.Question.TenantId &&
                    item.IsAccepted)
                .ExecuteCommandAsync();
        }

        if (nextAnswer != null)
        {
            var answerAffected = await DbProtectedClient.Updateable<PostAnswer>()
                .SetColumns(item => new PostAnswer
                {
                    IsAccepted = true,
                    ModifyTime = now,
                    ModifyBy = operatorName,
                    ModifyId = operatorId
                })
                .Where(item =>
                    item.Id == nextAnswer.Id &&
                    item.TenantId == context.Question.TenantId &&
                    item.PostId == context.Post.Id &&
                    !item.IsDeleted &&
                    item.IsEnabled &&
                    !item.IsAccepted)
                .ExecuteCommandAsync();
            if (answerAffected != 1)
            {
                return false;
            }
        }

        var nextAcceptanceRevision = expectedAcceptanceRevision + 1;
        var isSolved = nextAnswer != null;
        long? acceptedAnswerId = nextAnswer?.Id;
        int? acceptedAnswerContentRevision = nextAnswer?.ContentRevision;
        var questionAffected = await DbProtectedClient.Updateable<PostQuestion>()
            .SetColumns(item => new PostQuestion
            {
                IsSolved = isSolved,
                AcceptedAnswerId = acceptedAnswerId,
                AcceptedAnswerContentRevision = acceptedAnswerContentRevision,
                AcceptanceRevision = nextAcceptanceRevision,
                ModifyTime = now,
                ModifyBy = operatorName,
                ModifyId = operatorId
            })
            .Where(item =>
                item.Id == context.Question.Id &&
                item.TenantId == context.Question.TenantId &&
                !item.IsDeleted &&
                item.AcceptanceRevision == expectedAcceptanceRevision)
            .ExecuteCommandAsync();
        if (questionAffected != 1)
        {
            return false;
        }

        acceptanceEvent.Id = SnowFlakeSingle.Instance.NextId();
        acceptanceEvent.AcceptanceRevision = nextAcceptanceRevision;
        await DbProtectedClient.Insertable(acceptanceEvent).ExecuteCommandAsync();
        return true;
    }

    public async Task BindAnswerAttachmentsAsync(
        long tenantId,
        long answerId,
        long revisionId,
        long authorId,
        string operatorName,
        IReadOnlySet<long> attachmentIds,
        DateTime now)
    {
        if (attachmentIds.Count == 0)
        {
            return;
        }

        await DbProtectedClient.Updateable<Attachment>()
            .SetColumns(item => new Attachment
            {
                BusinessType = AttachmentBusinessTypes.PostAnswer,
                BusinessId = answerId,
                ModifyTime = now,
                ModifyBy = operatorName,
                ModifyId = authorId
            })
            .Where(item =>
                attachmentIds.Contains(item.Id) &&
                item.TenantId == tenantId &&
                item.UploaderId == authorId &&
                !item.IsDeleted &&
                !item.BusinessId.HasValue)
            .ExecuteCommandAsync();

        var attachments = await DbProtectedClient.Queryable<Attachment>()
            .Where(item =>
                attachmentIds.Contains(item.Id) &&
                item.TenantId == tenantId &&
                item.UploaderId == authorId &&
                item.IsEnabled &&
                !item.IsDeleted &&
                item.BusinessType == AttachmentBusinessTypes.PostAnswer &&
                item.BusinessId == answerId)
            .ToListAsync();
        if (attachments.Count != attachmentIds.Count)
        {
            throw new ForumAnswerAttachmentUnavailableException();
        }

        await DbProtectedClient.Insertable(attachmentIds.Select(attachmentId =>
            new ForumContentRevisionAttachment
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = tenantId,
                TargetType = ForumContentRevisionTargetTypes.PostAnswer,
                TargetId = answerId,
                RevisionId = revisionId,
                AttachmentId = attachmentId,
                ReferenceKind = ForumContentRevisionReferenceKinds.Content,
                CreateTime = now,
                CreateBy = operatorName,
                CreateId = authorId
            }).ToList()).ExecuteCommandAsync();
    }
}
