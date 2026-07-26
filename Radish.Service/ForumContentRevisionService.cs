using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared;
using Radish.Shared.Constants;
using SqlSugar;

namespace Radish.Service;

/// <summary>论坛作者内容不可变版本服务。</summary>
public sealed class ForumContentRevisionService : IForumContentRevisionService
{
    private readonly IBaseRepository<Post> _postRepository;
    private readonly IBaseRepository<Comment> _commentRepository;
    private readonly IBaseRepository<PostContentRevision> _postRevisionRepository;
    private readonly IBaseRepository<PostContentRevisionTag> _postRevisionTagRepository;
    private readonly IBaseRepository<CommentContentRevision> _commentRevisionRepository;
    private readonly IBaseRepository<ForumContentRevisionAttachment> _revisionAttachmentRepository;
    private readonly IBaseRepository<PostTag> _postTagRepository;
    private readonly IBaseRepository<Tag> _tagRepository;
    private readonly IBaseRepository<Category> _categoryRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IPostService _postService;
    private readonly ICommentService _commentService;
    private readonly TimeProvider _timeProvider;

    public ForumContentRevisionService(
        IBaseRepository<Post> postRepository,
        IBaseRepository<Comment> commentRepository,
        IBaseRepository<PostContentRevision> postRevisionRepository,
        IBaseRepository<PostContentRevisionTag> postRevisionTagRepository,
        IBaseRepository<CommentContentRevision> commentRevisionRepository,
        IBaseRepository<ForumContentRevisionAttachment> revisionAttachmentRepository,
        IBaseRepository<PostTag> postTagRepository,
        IBaseRepository<Tag> tagRepository,
        IBaseRepository<Category> categoryRepository,
        IBaseRepository<Attachment> attachmentRepository,
        IPostService postService,
        ICommentService commentService,
        TimeProvider? timeProvider = null)
    {
        _postRepository = postRepository;
        _commentRepository = commentRepository;
        _postRevisionRepository = postRevisionRepository;
        _postRevisionTagRepository = postRevisionTagRepository;
        _commentRevisionRepository = commentRevisionRepository;
        _revisionAttachmentRepository = revisionAttachmentRepository;
        _postTagRepository = postTagRepository;
        _tagRepository = tagRepository;
        _categoryRepository = categoryRepository;
        _attachmentRepository = attachmentRepository;
        _postService = postService;
        _commentService = commentService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    [UseTran]
    public async Task<ForumContentRevisionWriteResult> AppendPostRevisionAsync(
        long postId,
        string sourceType,
        long? restoredFromRevisionId,
        long editorId,
        string editorName)
    {
        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted)
        {
            throw CreateException("帖子不存在", 404, ForumContentRevisionErrorCodes.NotFound);
        }

        var existing = await _postRevisionRepository.QueryFirstAsync(
            revision => revision.PostId == postId && revision.RevisionNumber == post.ContentRevision);
        if (existing != null)
        {
            return ToWriteResult(postId, existing.Id, existing.RevisionNumber);
        }

        ValidateSourceType(sourceType);
        var safeEditorName = NormalizeOperatorName(editorName);
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var category = await _categoryRepository.QueryByIdAsync(post.CategoryId);
        if (category == null || category.IsDeleted || !category.IsEnabled)
        {
            throw CreateException("帖子分类不存在或不可用", 409, ForumContentRevisionErrorCodes.CategoryUnavailable);
        }

        var tags = await ResolveCurrentPostTagsAsync(postId);
        var attachmentReferences = await ResolveCurrentAttachmentReferencesAsync(
            ForumContentRevisionTargetTypes.Post,
            postId,
            post.TenantId,
            post.Content,
            post.CoverAttachmentId);

        var revision = new PostContentRevision
        {
            TenantId = post.TenantId,
            PostId = post.Id,
            RevisionNumber = post.ContentRevision,
            SourceType = sourceType,
            RestoredFromRevisionId = restoredFromRevisionId,
            IntegrityStatus = ForumContentRevisionIntegrityStatuses.Complete,
            Title = post.Title,
            Content = post.Content,
            ContentType = post.ContentType,
            CategoryId = category.Id,
            CategoryNameSnapshot = category.Name,
            CoverAttachmentId = post.CoverAttachmentId,
            EditorId = editorId,
            EditorName = safeEditorName,
            CreateTime = nowUtc,
            CreateBy = safeEditorName,
            CreateId = editorId
        };
        var revisionId = await _postRevisionRepository.AddAsync(revision);

        if (tags.Count > 0)
        {
            await _postRevisionTagRepository.AddRangeAsync(tags
                .Select((tag, index) => new PostContentRevisionTag
                {
                    TenantId = post.TenantId,
                    RevisionId = revisionId,
                    TagId = tag.Id,
                    TagNameSnapshot = tag.Name,
                    SortOrder = index,
                    CreateTime = nowUtc,
                    CreateBy = safeEditorName,
                    CreateId = editorId
                })
                .ToList());
        }

        await AddAttachmentReferencesAsync(
            attachmentReferences,
            post.TenantId,
            ForumContentRevisionTargetTypes.Post,
            postId,
            revisionId,
            editorId,
            safeEditorName);

        return ToWriteResult(postId, revisionId, post.ContentRevision);
    }

    [UseTran]
    public async Task<ForumContentRevisionWriteResult> AppendCommentRevisionAsync(
        long commentId,
        string sourceType,
        long? restoredFromRevisionId,
        long editorId,
        string editorName)
    {
        var comment = await _commentRepository.QueryByIdAsync(commentId);
        if (comment == null || comment.IsDeleted)
        {
            throw CreateException("评论不存在", 404, ForumContentRevisionErrorCodes.NotFound);
        }

        var existing = await _commentRevisionRepository.QueryFirstAsync(
            revision => revision.CommentId == commentId && revision.RevisionNumber == comment.ContentRevision);
        if (existing != null)
        {
            return ToWriteResult(commentId, existing.Id, existing.RevisionNumber);
        }

        ValidateSourceType(sourceType);
        var safeEditorName = NormalizeOperatorName(editorName);
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var attachmentReferences = await ResolveCurrentAttachmentReferencesAsync(
            ForumContentRevisionTargetTypes.Comment,
            commentId,
            comment.TenantId,
            comment.Content,
            null);
        var revision = new CommentContentRevision
        {
            TenantId = comment.TenantId,
            CommentId = comment.Id,
            PostId = comment.PostId,
            RevisionNumber = comment.ContentRevision,
            SourceType = sourceType,
            RestoredFromRevisionId = restoredFromRevisionId,
            IntegrityStatus = ForumContentRevisionIntegrityStatuses.Complete,
            Content = comment.Content,
            EditorId = editorId,
            EditorName = safeEditorName,
            CreateTime = nowUtc,
            CreateBy = safeEditorName,
            CreateId = editorId
        };
        var revisionId = await _commentRevisionRepository.AddAsync(revision);
        await AddAttachmentReferencesAsync(
            attachmentReferences,
            comment.TenantId,
            ForumContentRevisionTargetTypes.Comment,
            commentId,
            revisionId,
            editorId,
            safeEditorName);

        return ToWriteResult(commentId, revisionId, comment.ContentRevision);
    }

    public async Task<ForumContentRevisionWriteResult> GetCurrentPostRevisionAsync(long postId)
    {
        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted)
        {
            throw CreateException("帖子不存在", 404, ForumContentRevisionErrorCodes.NotFound);
        }

        var revision = await _postRevisionRepository.QueryFirstAsync(
            current => current.PostId == postId && current.RevisionNumber == post.ContentRevision)
            ?? throw CreateException("帖子当前版本不存在", 409, ForumContentRevisionErrorCodes.Incomplete);
        return ToWriteResult(postId, revision.Id, revision.RevisionNumber);
    }

    public async Task<ForumContentRevisionWriteResult> GetCurrentCommentRevisionAsync(long commentId)
    {
        var comment = await _commentRepository.QueryByIdAsync(commentId);
        if (comment == null || comment.IsDeleted)
        {
            throw CreateException("评论不存在", 404, ForumContentRevisionErrorCodes.NotFound);
        }

        var revision = await _commentRevisionRepository.QueryFirstAsync(
            current => current.CommentId == commentId && current.RevisionNumber == comment.ContentRevision)
            ?? throw CreateException("评论当前版本不存在", 409, ForumContentRevisionErrorCodes.Incomplete);
        return ToWriteResult(commentId, revision.Id, revision.RevisionNumber);
    }

    public async Task<PostContentRevisionListVo> GetPostRevisionListAsync(
        long postId,
        long viewerId,
        bool isAdmin,
        int pageIndex,
        int pageSize)
    {
        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted)
        {
            throw CreateException("帖子不存在", 404, ForumContentRevisionErrorCodes.NotFound);
        }

        var authorized = isAdmin || (viewerId > 0 && post.AuthorId == viewerId);
        var result = new PostContentRevisionListVo
        {
            VoIsEdited = post.EditCount > 0,
            VoEditCount = post.EditCount,
            VoCurrentContentRevision = post.ContentRevision,
            VoLastEditedAt = post.ModifyTime,
            VoCanViewDetails = authorized,
            VoPageIndex = NormalizePageIndex(pageIndex),
            VoPageSize = NormalizePageSize(pageSize)
        };
        if (!authorized)
        {
            return result;
        }

        var (revisions, total) = await _postRevisionRepository.QueryPageAsync(
            revision => revision.PostId == postId,
            result.VoPageIndex,
            result.VoPageSize,
            revision => revision.RevisionNumber,
            OrderByType.Desc);
        result.VoItems = await BuildPostSummariesAsync(revisions, post.ContentRevision);
        result.VoTotal = total;
        return result;
    }

    public async Task<CommentContentRevisionListVo> GetCommentRevisionListAsync(
        long commentId,
        long viewerId,
        bool isAdmin,
        int pageIndex,
        int pageSize)
    {
        var comment = await _commentRepository.QueryByIdAsync(commentId);
        if (comment == null || comment.IsDeleted)
        {
            throw CreateException("评论不存在", 404, ForumContentRevisionErrorCodes.NotFound);
        }

        var authorized = isAdmin || (viewerId > 0 && comment.AuthorId == viewerId);
        var result = new CommentContentRevisionListVo
        {
            VoIsEdited = comment.EditCount > 0,
            VoEditCount = comment.EditCount,
            VoCurrentContentRevision = comment.ContentRevision,
            VoLastEditedAt = comment.ModifyTime,
            VoCanViewDetails = authorized,
            VoPageIndex = NormalizePageIndex(pageIndex),
            VoPageSize = NormalizePageSize(pageSize)
        };
        if (!authorized)
        {
            return result;
        }

        var (revisions, total) = await _commentRevisionRepository.QueryPageAsync(
            revision => revision.CommentId == commentId,
            result.VoPageIndex,
            result.VoPageSize,
            revision => revision.RevisionNumber,
            OrderByType.Desc);
        result.VoItems = await BuildCommentSummariesAsync(revisions, comment.ContentRevision);
        result.VoTotal = total;
        return result;
    }

    public async Task<PostContentRevisionDetailVo> GetPostRevisionDetailAsync(
        long revisionId,
        long viewerId,
        bool isAdmin)
    {
        var revision = await _postRevisionRepository.QueryByIdAsync(revisionId)
            ?? throw CreateException("帖子版本不存在", 404, ForumContentRevisionErrorCodes.NotFound);
        var post = await _postRepository.QueryByIdAsync(revision.PostId);
        EnsureAuthorized(post?.AuthorId, viewerId, isAdmin);

        var tags = await _postRevisionTagRepository.QueryAsync(tag => tag.RevisionId == revision.Id);
        var references = await _revisionAttachmentRepository.QueryAsync(reference =>
            reference.TargetType == ForumContentRevisionTargetTypes.Post &&
            reference.RevisionId == revision.Id);
        var summary = (await BuildPostSummariesAsync([revision], post!.ContentRevision)).Single();

        return new PostContentRevisionDetailVo
        {
            VoSummary = summary,
            VoPostId = revision.PostId,
            VoTitle = revision.Title,
            VoContent = revision.Content,
            VoContentType = revision.ContentType,
            VoCategoryId = revision.CategoryId,
            VoCategoryName = revision.CategoryNameSnapshot,
            VoCoverAttachmentId = revision.CoverAttachmentId,
            VoTags = tags
                .OrderBy(tag => tag.SortOrder)
                .Select(tag => new ForumContentRevisionTagVo
                {
                    VoTagId = tag.TagId,
                    VoTagName = tag.TagNameSnapshot,
                    VoSortOrder = tag.SortOrder
                })
                .ToList(),
            VoAttachmentIds = references
                .Where(reference => reference.ReferenceKind == ForumContentRevisionReferenceKinds.Content)
                .Select(reference => reference.AttachmentId)
                .Distinct()
                .ToList(),
            VoExpectedContentRevision = post.ContentRevision
        };
    }

    public async Task<CommentContentRevisionDetailVo> GetCommentRevisionDetailAsync(
        long revisionId,
        long viewerId,
        bool isAdmin)
    {
        var revision = await _commentRevisionRepository.QueryByIdAsync(revisionId)
            ?? throw CreateException("评论版本不存在", 404, ForumContentRevisionErrorCodes.NotFound);
        var comment = await _commentRepository.QueryByIdAsync(revision.CommentId);
        EnsureAuthorized(comment?.AuthorId, viewerId, isAdmin);

        var references = await _revisionAttachmentRepository.QueryAsync(reference =>
            reference.TargetType == ForumContentRevisionTargetTypes.Comment &&
            reference.RevisionId == revision.Id);
        var summary = (await BuildCommentSummariesAsync([revision], comment!.ContentRevision)).Single();

        return new CommentContentRevisionDetailVo
        {
            VoSummary = summary,
            VoCommentId = revision.CommentId,
            VoPostId = revision.PostId,
            VoContent = revision.Content,
            VoAttachmentIds = references.Select(reference => reference.AttachmentId).Distinct().ToList(),
            VoExpectedContentRevision = comment.ContentRevision
        };
    }

    [UseTran]
    public async Task<ForumContentRevisionWriteResult> RestorePostAsync(
        long postId,
        long revisionId,
        int expectedContentRevision,
        long operatorId,
        string operatorName,
        bool isAdmin)
    {
        var post = await _postRepository.QueryByIdAsync(postId);
        if (post == null || post.IsDeleted)
        {
            throw CreateException("帖子不存在", 404, ForumContentRevisionErrorCodes.NotFound);
        }
        EnsureAuthorized(post.AuthorId, operatorId, isAdmin);
        EnsureExpectedRevision(post.ContentRevision, expectedContentRevision);

        var revision = await _postRevisionRepository.QueryByIdAsync(revisionId);
        if (revision == null || revision.PostId != postId)
        {
            throw CreateException("帖子版本不存在", 404, ForumContentRevisionErrorCodes.NotFound);
        }
        EnsureRestorable(revision.IntegrityStatus);
        if (revision.RevisionNumber == post.ContentRevision)
        {
            throw CreateException("目标版本已经是当前版本", 409, ForumContentRevisionErrorCodes.Conflict);
        }

        var category = await _categoryRepository.QueryByIdAsync(revision.CategoryId);
        if (category == null || category.IsDeleted || !category.IsEnabled)
        {
            throw CreateException("目标版本的分类已不可用", 409, ForumContentRevisionErrorCodes.CategoryUnavailable);
        }

        var revisionTags = await _postRevisionTagRepository.QueryAsync(tag => tag.RevisionId == revisionId);
        var tagIds = revisionTags.Select(tag => tag.TagId).Distinct().ToList();
        var activeTags = tagIds.Count == 0
            ? []
            : await _tagRepository.QueryAsync(tag => tagIds.Contains(tag.Id) && tag.IsEnabled && !tag.IsDeleted);
        if (activeTags.Count != tagIds.Count)
        {
            throw CreateException("目标版本包含已不可用的标签", 409, ForumContentRevisionErrorCodes.TagUnavailable);
        }

        var currentTagIds = (await _postTagRepository.QueryAsync(postTag => postTag.PostId == postId))
            .Select(postTag => postTag.TagId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        if (string.Equals(post.Title.Trim(), revision.Title.Trim(), StringComparison.Ordinal) &&
            string.Equals(post.Content.Trim(), revision.Content.Trim(), StringComparison.Ordinal) &&
            post.CategoryId == revision.CategoryId &&
            post.CoverAttachmentId == revision.CoverAttachmentId &&
            currentTagIds.SequenceEqual(tagIds.OrderBy(id => id)))
        {
            throw CreateException("目标版本与当前内容一致", 409, ForumContentRevisionErrorCodes.Conflict);
        }

        await ValidateStoredAttachmentReferencesAsync(
            ForumContentRevisionTargetTypes.Post,
            postId,
            post.TenantId,
            revisionId);

        try
        {
            await _postService.UpdatePostAsync(
                postId,
                revision.Title,
                revision.Content,
                revision.CategoryId,
                revisionTags.OrderBy(tag => tag.SortOrder).Select(tag => tag.TagNameSnapshot).ToList(),
                false,
                operatorId,
                operatorName,
                isAdmin,
                expectedContentRevision,
                true,
                revision.CoverAttachmentId);
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (ArgumentException exception)
        {
            throw CreateException(exception.Message, 409, ForumContentRevisionErrorCodes.ContentRejected);
        }
        catch (InvalidOperationException exception)
        {
            var errorCode = exception.Message.Contains("次数", StringComparison.Ordinal)
                ? ForumContentRevisionErrorCodes.EditLimitReached
                : ForumContentRevisionErrorCodes.ContentRejected;
            throw CreateException(exception.Message, 409, errorCode);
        }

        return await AppendPostRevisionAsync(
            postId,
            ForumContentRevisionSourceTypes.Restore,
            revisionId,
            operatorId,
            operatorName);
    }

    [UseTran]
    public async Task<ForumContentRevisionWriteResult> RestoreCommentAsync(
        long commentId,
        long revisionId,
        int expectedContentRevision,
        long operatorId,
        string operatorName,
        bool isAdmin)
    {
        var comment = await _commentRepository.QueryByIdAsync(commentId);
        if (comment == null || comment.IsDeleted)
        {
            throw CreateException("评论不存在", 404, ForumContentRevisionErrorCodes.NotFound);
        }
        EnsureAuthorized(comment.AuthorId, operatorId, isAdmin);
        EnsureExpectedRevision(comment.ContentRevision, expectedContentRevision);

        var revision = await _commentRevisionRepository.QueryByIdAsync(revisionId);
        if (revision == null || revision.CommentId != commentId)
        {
            throw CreateException("评论版本不存在", 404, ForumContentRevisionErrorCodes.NotFound);
        }
        EnsureRestorable(revision.IntegrityStatus);
        if (revision.RevisionNumber == comment.ContentRevision ||
            string.Equals(comment.Content.Trim(), revision.Content.Trim(), StringComparison.Ordinal))
        {
            throw CreateException("目标版本已经是当前内容", 409, ForumContentRevisionErrorCodes.Conflict);
        }
        await ValidateStoredAttachmentReferencesAsync(
            ForumContentRevisionTargetTypes.Comment,
            commentId,
            comment.TenantId,
            revisionId);

        var (success, message) = await _commentService.UpdateCommentAsync(
            commentId,
            revision.Content,
            operatorId,
            operatorName,
            isAdmin,
            expectedContentRevision);
        if (!success)
        {
            throw CreateCommentRestoreException(message);
        }

        return await AppendCommentRevisionAsync(
            commentId,
            ForumContentRevisionSourceTypes.Restore,
            revisionId,
            operatorId,
            operatorName);
    }

    private async Task<List<Tag>> ResolveCurrentPostTagsAsync(long postId)
    {
        var postTags = await _postTagRepository.QueryAsync(postTag => postTag.PostId == postId);
        var tagIds = postTags.Select(postTag => postTag.TagId).Distinct().ToList();
        var tags = tagIds.Count == 0
            ? []
            : await _tagRepository.QueryAsync(tag => tagIds.Contains(tag.Id) && tag.IsEnabled && !tag.IsDeleted);
        if (tags.Count == 0 || tags.Count != tagIds.Count)
        {
            throw CreateException("帖子标签不存在或不可用", 409, ForumContentRevisionErrorCodes.TagUnavailable);
        }

        return tags.OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<List<(long AttachmentId, string ReferenceKind)>> ResolveCurrentAttachmentReferencesAsync(
        string targetType,
        long targetId,
        long tenantId,
        string content,
        long? coverAttachmentId)
    {
        if (AttachmentReferenceParser.ExtractLegacyAttachmentUrls(content).Count > 0)
        {
            throw CreateException("内容仍包含旧式附件地址，无法建立完整版本", 409, ForumContentRevisionErrorCodes.AttachmentUnavailable);
        }

        var contentIds = AttachmentReferenceParser.ExtractAttachmentIds(content);
        var allIds = contentIds.ToHashSet();
        if (coverAttachmentId.HasValue)
        {
            allIds.Add(coverAttachmentId.Value);
        }
        if (allIds.Count == 0)
        {
            return [];
        }

        var attachments = await _attachmentRepository.QueryAsync(attachment =>
            allIds.Contains(attachment.Id) &&
            attachment.TenantId == tenantId &&
            attachment.IsEnabled &&
            !attachment.IsDeleted);
        var expectedBusinessType = targetType == ForumContentRevisionTargetTypes.Post ? "Post" : "Comment";
        if (attachments.Count != allIds.Count ||
            attachments.Any(attachment =>
                attachment.BusinessType != expectedBusinessType ||
                attachment.BusinessId != targetId))
        {
            throw CreateException("内容引用的附件不存在或归属无效", 409, ForumContentRevisionErrorCodes.AttachmentUnavailable);
        }

        var result = contentIds
            .Select(id => (id, ForumContentRevisionReferenceKinds.Content))
            .ToList();
        if (coverAttachmentId.HasValue)
        {
            result.Add((coverAttachmentId.Value, ForumContentRevisionReferenceKinds.Cover));
        }
        return result;
    }

    private async Task AddAttachmentReferencesAsync(
        List<(long AttachmentId, string ReferenceKind)> references,
        long tenantId,
        string targetType,
        long targetId,
        long revisionId,
        long editorId,
        string editorName)
    {
        if (references.Count == 0)
        {
            return;
        }

        await _revisionAttachmentRepository.AddRangeAsync(references
            .Distinct()
            .Select(reference => new ForumContentRevisionAttachment
            {
                TenantId = tenantId,
                TargetType = targetType,
                TargetId = targetId,
                RevisionId = revisionId,
                AttachmentId = reference.AttachmentId,
                ReferenceKind = reference.ReferenceKind,
                CreateTime = _timeProvider.GetUtcNow().UtcDateTime,
                CreateBy = editorName,
                CreateId = editorId
            })
            .ToList());
    }

    private async Task ValidateStoredAttachmentReferencesAsync(
        string targetType,
        long targetId,
        long tenantId,
        long revisionId)
    {
        var references = await _revisionAttachmentRepository.QueryAsync(reference =>
            reference.TargetType == targetType &&
            reference.TargetId == targetId &&
            reference.RevisionId == revisionId);
        var ids = references.Select(reference => reference.AttachmentId).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var attachments = await _attachmentRepository.QueryAsync(attachment =>
            ids.Contains(attachment.Id) &&
            attachment.TenantId == tenantId &&
            attachment.IsEnabled &&
            !attachment.IsDeleted);
        var expectedBusinessType = targetType == ForumContentRevisionTargetTypes.Post ? "Post" : "Comment";
        if (attachments.Count != ids.Count ||
            attachments.Any(attachment =>
                attachment.BusinessType != expectedBusinessType ||
                attachment.BusinessId != targetId))
        {
            throw CreateException("目标版本引用的附件已不可用", 409, ForumContentRevisionErrorCodes.AttachmentUnavailable);
        }
    }

    private async Task<List<PostContentRevisionSummaryVo>> BuildPostSummariesAsync(
        List<PostContentRevision> revisions,
        int currentRevision)
    {
        var restoredIds = revisions
            .Where(revision => revision.RestoredFromRevisionId.HasValue)
            .Select(revision => revision.RestoredFromRevisionId!.Value)
            .Distinct()
            .ToList();
        var restoredNumbers = restoredIds.Count == 0
            ? new Dictionary<long, int>()
            : (await _postRevisionRepository.QueryAsync(revision => restoredIds.Contains(revision.Id)))
                .ToDictionary(revision => revision.Id, revision => revision.RevisionNumber);

        return revisions.Select(revision => new PostContentRevisionSummaryVo
        {
            VoRevisionId = revision.Id,
            VoRevisionNumber = revision.RevisionNumber,
            VoSourceType = revision.SourceType,
            VoIntegrityStatus = revision.IntegrityStatus,
            VoRestoredFromRevisionId = revision.RestoredFromRevisionId,
            VoRestoredFromRevisionNumber = revision.RestoredFromRevisionId.HasValue &&
                                           restoredNumbers.TryGetValue(revision.RestoredFromRevisionId.Value, out var number)
                ? number
                : null,
            VoEditorId = revision.EditorId,
            VoEditorName = revision.EditorName,
            VoCreateTime = revision.CreateTime,
            VoIsCurrent = revision.RevisionNumber == currentRevision,
            VoCanViewSnapshot = true,
            VoCanRestore = revision.RevisionNumber != currentRevision &&
                           revision.IntegrityStatus == ForumContentRevisionIntegrityStatuses.Complete,
            VoUnavailableReasonCode = revision.IntegrityStatus == ForumContentRevisionIntegrityStatuses.Complete
                ? null
                : ForumContentRevisionErrorCodes.Incomplete
        }).ToList();
    }

    private async Task<List<CommentContentRevisionSummaryVo>> BuildCommentSummariesAsync(
        List<CommentContentRevision> revisions,
        int currentRevision)
    {
        var restoredIds = revisions
            .Where(revision => revision.RestoredFromRevisionId.HasValue)
            .Select(revision => revision.RestoredFromRevisionId!.Value)
            .Distinct()
            .ToList();
        var restoredNumbers = restoredIds.Count == 0
            ? new Dictionary<long, int>()
            : (await _commentRevisionRepository.QueryAsync(revision => restoredIds.Contains(revision.Id)))
                .ToDictionary(revision => revision.Id, revision => revision.RevisionNumber);

        return revisions.Select(revision => new CommentContentRevisionSummaryVo
        {
            VoRevisionId = revision.Id,
            VoRevisionNumber = revision.RevisionNumber,
            VoSourceType = revision.SourceType,
            VoIntegrityStatus = revision.IntegrityStatus,
            VoRestoredFromRevisionId = revision.RestoredFromRevisionId,
            VoRestoredFromRevisionNumber = revision.RestoredFromRevisionId.HasValue &&
                                           restoredNumbers.TryGetValue(revision.RestoredFromRevisionId.Value, out var number)
                ? number
                : null,
            VoEditorId = revision.EditorId,
            VoEditorName = revision.EditorName,
            VoCreateTime = revision.CreateTime,
            VoIsCurrent = revision.RevisionNumber == currentRevision,
            VoCanViewSnapshot = true,
            VoCanRestore = revision.RevisionNumber != currentRevision &&
                           revision.IntegrityStatus == ForumContentRevisionIntegrityStatuses.Complete,
            VoUnavailableReasonCode = revision.IntegrityStatus == ForumContentRevisionIntegrityStatuses.Complete
                ? null
                : ForumContentRevisionErrorCodes.Incomplete
        }).ToList();
    }

    private static void EnsureAuthorized(long? authorId, long viewerId, bool isAdmin)
    {
        if (!authorId.HasValue)
        {
            throw CreateException("内容不存在", 404, ForumContentRevisionErrorCodes.NotFound);
        }
        if (!isAdmin && (viewerId <= 0 || authorId.Value != viewerId))
        {
            throw CreateException("无权查看或恢复该内容版本", 403, ForumContentRevisionErrorCodes.AccessDenied);
        }
    }

    private static void EnsureExpectedRevision(int currentRevision, int expectedRevision)
    {
        if (expectedRevision <= 0 || currentRevision != expectedRevision)
        {
            throw CreateException("内容已被更新，请刷新后重试", 409, ForumContentRevisionErrorCodes.Conflict);
        }
    }

    private static void EnsureRestorable(string integrityStatus)
    {
        if (integrityStatus != ForumContentRevisionIntegrityStatuses.Complete)
        {
            throw CreateException("该历史版本不完整，不能恢复", 409, ForumContentRevisionErrorCodes.Incomplete);
        }
    }

    private static void ValidateSourceType(string sourceType)
    {
        if (sourceType is not (
            ForumContentRevisionSourceTypes.Baseline or
            ForumContentRevisionSourceTypes.Edit or
            ForumContentRevisionSourceTypes.Restore))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, "未知的论坛内容版本来源。");
        }
    }

    private static BusinessException CreateCommentRestoreException(string message)
    {
        var errorCode = message.Contains("时间", StringComparison.Ordinal)
            ? ForumContentRevisionErrorCodes.CommentWindowExpired
            : message.Contains("次数", StringComparison.Ordinal)
                ? ForumContentRevisionErrorCodes.EditLimitReached
                : message.Contains("作者", StringComparison.Ordinal)
                    ? ForumContentRevisionErrorCodes.AccessDenied
                    : ForumContentRevisionErrorCodes.ContentRejected;
        var statusCode = errorCode == ForumContentRevisionErrorCodes.AccessDenied ? 403 : 409;
        return CreateException(message, statusCode, errorCode);
    }

    private static BusinessException CreateException(string message, int statusCode, string errorCode)
    {
        return new BusinessException(
            message,
            statusCode,
            errorCode,
            ForumContentRevisionErrorCodes.ResolveMessageKey(errorCode));
    }

    private static ForumContentRevisionWriteResult ToWriteResult(
        long targetId,
        long revisionId,
        int contentRevision)
    {
        return new ForumContentRevisionWriteResult
        {
            VoTargetId = targetId,
            VoRevisionId = revisionId,
            VoContentRevision = contentRevision
        };
    }

    private static int NormalizePageIndex(int pageIndex) => Math.Max(1, pageIndex);

    private static int NormalizePageSize(int pageSize) => pageSize <= 0 ? 20 : Math.Min(100, pageSize);

    private static string NormalizeOperatorName(string operatorName) =>
        string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();
}
