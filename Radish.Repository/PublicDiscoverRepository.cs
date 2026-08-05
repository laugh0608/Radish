using Radish.IRepository;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Repository;

/// <summary>Main 库公开发现来源的资格、去重、计数与稳定 keyset 查询。</summary>
public sealed class PublicDiscoverRepository : BaseRepository<Post>, IPublicDiscoverRepository
{
    private const string PostPublicIdPrefix = "pst_";

    public PublicDiscoverRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public Task<IReadOnlyList<PublicDiscoverSourceProjection>> QueryMemberActivitiesAsync(
        PublicDiscoverSourceWindow window)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var published = (int)WikiDocumentStatusEnum.Published;
            var publicVisibility = (int)WikiDocumentVisibilityEnum.Public;
            var query = DbProtectedClient.Queryable<WikiDocument, User>(
                    (document, user) => new JoinQueryInfos(
                        JoinType.Inner,
                        document.OwnerUserId == user.Id && document.TenantId == user.TenantId))
                .Where((document, user) =>
                    document.TenantId == window.TenantId &&
                    document.Status == published &&
                    document.Visibility == publicVisibility &&
                    !document.IsDeleted &&
                    document.PublishedAt != null &&
                    document.PublishedAt.Value <= window.SnapshotCutoffUtc &&
                    document.Title != "" &&
                    document.Slug != "" &&
                    user.Id > 0 &&
                    user.IsEnable &&
                    !user.IsDeleted &&
                    user.PublicId != null &&
                    user.PublicId.StartsWith(User.PublicIdPrefix) &&
                    user.PublicId.Length == 36);
            query = ApplyMemberActivityCursor(query, window);
            var items = await query
                .OrderBy((document, user) => document.PublishedAt, OrderByType.Desc)
                .OrderBy((document, user) => document.Id, OrderByType.Desc)
                .Select((document, user) => new PublicDiscoverSourceProjection
                {
                    SourceId = document.Id,
                    Kind = PublicDiscoverItemKind.MemberActivity,
                    OccurredAtUtc = document.PublishedAt!.Value,
                    Title = document.Title,
                    Summary = document.Summary,
                    ActorPublicId = user.PublicId,
                    ActorDisplayName = user.UserName,
                    TargetKind = PublicDiscoverTargetKind.Docs,
                    DocumentSlug = document.Slug,
                    RequiresAuthentication = false
                })
                .Take(window.Take)
                .ToListAsync();
            return (IReadOnlyList<PublicDiscoverSourceProjection>)items
                .Where(item => User.HasPublicIdFormat(item.ActorPublicId))
                .ToList();
        });
    }

    public Task<IReadOnlyList<PublicDiscoverSourceProjection>> QueryHighlightedCommentsAsync(
        PublicDiscoverSourceWindow window)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var query = DbProtectedClient.Queryable<CommentHighlight, Comment, Post, User>(
                    (highlight, comment, post, user) => new JoinQueryInfos(
                        JoinType.Inner,
                        highlight.CommentId == comment.Id && highlight.TenantId == comment.TenantId,
                        JoinType.Inner,
                        highlight.PostId == post.Id && highlight.TenantId == post.TenantId,
                        JoinType.Inner,
                        comment.AuthorId == user.Id && comment.TenantId == user.TenantId))
                .Where((highlight, comment, post, user) =>
                    highlight.TenantId == window.TenantId &&
                    highlight.IsCurrent &&
                    highlight.HighlightType == 1 &&
                    comment.LikeCount > 0 &&
                    comment.IsEnabled &&
                    !comment.IsDeleted &&
                    post.IsPublished &&
                    post.IsEnabled &&
                    !post.IsDeleted &&
                    post.PublicId != null &&
                    post.PublicId.StartsWith(PostPublicIdPrefix) &&
                    post.PublicId.Length == 36 &&
                    user.Id > 0 &&
                    user.IsEnable &&
                    !user.IsDeleted &&
                    user.PublicId != null &&
                    user.PublicId.StartsWith(User.PublicIdPrefix) &&
                    user.PublicId.Length == 36 &&
                    highlight.CreateTime <= window.SnapshotCutoffUtc);
            query = ApplyHighlightedCommentCursor(query, window);
            var items = await query
                .OrderBy((highlight, comment, post, user) => highlight.CreateTime, OrderByType.Desc)
                .OrderBy((highlight, comment, post, user) => highlight.Id, OrderByType.Desc)
                .Select((highlight, comment, post, user) => new PublicDiscoverSourceProjection
                {
                    SourceId = highlight.Id,
                    Kind = PublicDiscoverItemKind.HighlightedComment,
                    OccurredAtUtc = highlight.CreateTime,
                    Title = post.Title,
                    Summary = comment.Content,
                    ActorPublicId = user.PublicId,
                    ActorDisplayName = user.UserName,
                    TargetKind = PublicDiscoverTargetKind.ForumPost,
                    PostPublicId = post.PublicId,
                    CommentId = comment.Id,
                    RequiresAuthentication = false,
                    MetricKind = PublicDiscoverMetricKind.Likes,
                    MetricValue = comment.LikeCount
                })
                .Take(window.Take)
                .ToListAsync();
            return (IReadOnlyList<PublicDiscoverSourceProjection>)items
                .Where(item =>
                    User.HasPublicIdFormat(item.ActorPublicId) &&
                    HasPostPublicIdFormat(item.PostPublicId))
                .ToList();
        });
    }

    public Task<IReadOnlyList<PublicDiscoverSourceProjection>> QueryPostsAsync(
        PublicDiscoverSourceWindow window)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var query = BuildPublicPostQuery(window.TenantId)
                .Where(post =>
                    (post.PublishTime ?? post.CreateTime) <= window.SnapshotCutoffUtc &&
                    !SqlFunc.Subqueryable<PostQuestion>()
                        .Where(question =>
                            question.TenantId == window.TenantId &&
                            question.PostId == post.Id &&
                            !question.IsDeleted)
                        .Any());
            query = ApplyPostCursor(query, window, PublicDiscoverKindOrder.Post);
            var posts = await query
                .OrderByDescending(post => post.PublishTime ?? post.CreateTime)
                .OrderByDescending(post => post.Id)
                .Take(window.Take)
                .ToListAsync();
            return await MapPostsAsync(posts, PublicDiscoverItemKind.Post);
        });
    }

    public Task<IReadOnlyList<PublicDiscoverSourceProjection>> QueryQuestionsAsync(
        PublicDiscoverSourceWindow window)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var query = DbProtectedClient.Queryable<PostQuestion, Post>(
                    (question, post) => new JoinQueryInfos(
                        JoinType.Inner,
                        question.PostId == post.Id && question.TenantId == post.TenantId))
                .Where((question, post) =>
                    question.TenantId == window.TenantId &&
                    !question.IsDeleted &&
                    post.IsPublished &&
                    post.IsEnabled &&
                    !post.IsDeleted &&
                    post.PublicId != null &&
                    post.PublicId.StartsWith(PostPublicIdPrefix) &&
                    post.PublicId.Length == 36 &&
                    post.Title != "" &&
                    (post.PublishTime ?? post.CreateTime) <= window.SnapshotCutoffUtc);
            query = ApplyQuestionCursor(query, window);
            var rows = await query
                .OrderBy((question, post) => post.PublishTime ?? post.CreateTime, OrderByType.Desc)
                .OrderBy((question, post) => question.Id, OrderByType.Desc)
                .Select((question, post) => new PublicDiscoverQuestionRow
                {
                    SourceId = question.Id,
                    PostId = post.Id,
                    AuthorId = post.AuthorId,
                    OccurredAtUtc = post.PublishTime ?? post.CreateTime,
                    Title = post.Title,
                    Summary = post.Summary,
                    PostPublicId = post.PublicId,
                    AnswerCount = question.AnswerCount
                })
                .Take(window.Take)
                .ToListAsync();
            var actors = await QueryEligibleActorMapAsync(
                window.TenantId,
                rows.Select(row => row.AuthorId));
            return (IReadOnlyList<PublicDiscoverSourceProjection>)rows
                .Where(row => HasPostPublicIdFormat(row.PostPublicId))
                .Select(row =>
                {
                    actors.TryGetValue(row.AuthorId, out var actor);
                    return new PublicDiscoverSourceProjection
                    {
                        SourceId = row.SourceId,
                        Kind = PublicDiscoverItemKind.Question,
                        OccurredAtUtc = row.OccurredAtUtc,
                        Title = row.Title,
                        Summary = row.Summary,
                        ActorPublicId = actor?.PublicId,
                        ActorDisplayName = actor?.DisplayName,
                        TargetKind = PublicDiscoverTargetKind.ForumPost,
                        PostPublicId = row.PostPublicId,
                        RequiresAuthentication = false,
                        MetricKind = PublicDiscoverMetricKind.Answers,
                        MetricValue = row.AnswerCount
                    };
                })
                .ToList();
        });
    }

    public Task<PublicDiscoverMainPulseCounts> QueryPulseAsync(
        long tenantId,
        DateTime windowStartedAtUtc,
        DateTime windowEndedAtUtc)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var wikiCount = await BuildMemberActivityPulseQuery(
                    tenantId,
                    windowStartedAtUtc,
                    windowEndedAtUtc)
                .CountAsync();
            var highlightedCommentCount = await BuildHighlightedCommentPulseQuery(
                    tenantId,
                    windowStartedAtUtc,
                    windowEndedAtUtc)
                .CountAsync();
            var postCount = await BuildPublicPostQuery(tenantId)
                .Where(post =>
                    (post.PublishTime ?? post.CreateTime) >= windowStartedAtUtc &&
                    (post.PublishTime ?? post.CreateTime) <= windowEndedAtUtc &&
                    !SqlFunc.Subqueryable<PostQuestion>()
                        .Where(question =>
                            question.TenantId == tenantId &&
                            question.PostId == post.Id &&
                            !question.IsDeleted)
                        .Any())
                .CountAsync();
            var questionCount = await DbProtectedClient.Queryable<PostQuestion, Post>(
                    (question, post) => new JoinQueryInfos(
                        JoinType.Inner,
                        question.PostId == post.Id && question.TenantId == post.TenantId))
                .Where((question, post) =>
                    question.TenantId == tenantId &&
                    !question.IsDeleted &&
                    post.IsPublished &&
                    post.IsEnabled &&
                    !post.IsDeleted &&
                    post.PublicId != null &&
                    post.PublicId.StartsWith(PostPublicIdPrefix) &&
                    post.PublicId.Length == 36 &&
                    post.Title != "" &&
                    (post.PublishTime ?? post.CreateTime) >= windowStartedAtUtc &&
                    (post.PublishTime ?? post.CreateTime) <= windowEndedAtUtc)
                .CountAsync();
            return new PublicDiscoverMainPulseCounts(
                wikiCount + highlightedCommentCount + postCount + questionCount,
                wikiCount);
        });
    }

    private ISugarQueryable<Post> BuildPublicPostQuery(long tenantId)
    {
        return DbProtectedClient.Queryable<Post>()
            .Where(post =>
                post.TenantId == tenantId &&
                post.IsPublished &&
                post.IsEnabled &&
                !post.IsDeleted &&
                post.PublicId != null &&
                post.PublicId.StartsWith(PostPublicIdPrefix) &&
                post.PublicId.Length == 36 &&
                post.Title != "");
    }

    private ISugarQueryable<WikiDocument, User> BuildMemberActivityPulseQuery(
        long tenantId,
        DateTime windowStartedAtUtc,
        DateTime windowEndedAtUtc)
    {
        var published = (int)WikiDocumentStatusEnum.Published;
        var publicVisibility = (int)WikiDocumentVisibilityEnum.Public;
        return DbProtectedClient.Queryable<WikiDocument, User>(
                (document, user) => new JoinQueryInfos(
                    JoinType.Inner,
                    document.OwnerUserId == user.Id && document.TenantId == user.TenantId))
            .Where((document, user) =>
                document.TenantId == tenantId &&
                document.Status == published &&
                document.Visibility == publicVisibility &&
                !document.IsDeleted &&
                document.PublishedAt != null &&
                document.PublishedAt.Value >= windowStartedAtUtc &&
                document.PublishedAt.Value <= windowEndedAtUtc &&
                document.Title != "" &&
                document.Slug != "" &&
                user.Id > 0 &&
                user.IsEnable &&
                !user.IsDeleted &&
                user.PublicId != null &&
                user.PublicId.StartsWith(User.PublicIdPrefix) &&
                user.PublicId.Length == 36);
    }

    private ISugarQueryable<CommentHighlight, Comment, Post, User> BuildHighlightedCommentPulseQuery(
        long tenantId,
        DateTime windowStartedAtUtc,
        DateTime windowEndedAtUtc)
    {
        return DbProtectedClient.Queryable<CommentHighlight, Comment, Post, User>(
                (highlight, comment, post, user) => new JoinQueryInfos(
                    JoinType.Inner,
                    highlight.CommentId == comment.Id && highlight.TenantId == comment.TenantId,
                    JoinType.Inner,
                    highlight.PostId == post.Id && highlight.TenantId == post.TenantId,
                    JoinType.Inner,
                    comment.AuthorId == user.Id && comment.TenantId == user.TenantId))
            .Where((highlight, comment, post, user) =>
                highlight.TenantId == tenantId &&
                highlight.IsCurrent &&
                highlight.HighlightType == 1 &&
                highlight.CreateTime >= windowStartedAtUtc &&
                highlight.CreateTime <= windowEndedAtUtc &&
                comment.LikeCount > 0 &&
                comment.IsEnabled &&
                !comment.IsDeleted &&
                post.IsPublished &&
                post.IsEnabled &&
                !post.IsDeleted &&
                post.PublicId != null &&
                post.PublicId.StartsWith(PostPublicIdPrefix) &&
                post.PublicId.Length == 36 &&
                user.Id > 0 &&
                user.IsEnable &&
                !user.IsDeleted &&
                user.PublicId != null &&
                user.PublicId.StartsWith(User.PublicIdPrefix) &&
                user.PublicId.Length == 36);
    }

    private async Task<IReadOnlyList<PublicDiscoverSourceProjection>> MapPostsAsync(
        IReadOnlyList<Post> posts,
        PublicDiscoverItemKind kind)
    {
        var validPosts = posts.Where(post => HasPostPublicIdFormat(post.PublicId)).ToList();
        var actors = await QueryEligibleActorMapAsync(
            validPosts.FirstOrDefault()?.TenantId ?? 0,
            validPosts.Select(post => post.AuthorId));
        return validPosts.Select(post =>
        {
            actors.TryGetValue(post.AuthorId, out var actor);
            return new PublicDiscoverSourceProjection
            {
                SourceId = post.Id,
                Kind = kind,
                OccurredAtUtc = post.PublishTime ?? post.CreateTime,
                Title = post.Title,
                Summary = post.Summary,
                ActorPublicId = actor?.PublicId,
                ActorDisplayName = actor?.DisplayName,
                TargetKind = PublicDiscoverTargetKind.ForumPost,
                PostPublicId = post.PublicId,
                RequiresAuthentication = false,
                MetricKind = PublicDiscoverMetricKind.Comments,
                MetricValue = post.CommentCount
            };
        }).ToList();
    }

    private async Task<Dictionary<long, PublicActorRow>> QueryEligibleActorMapAsync(
        long tenantId,
        IEnumerable<long> actorIds)
    {
        var ids = actorIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var actors = await DbProtectedClient.Queryable<User>()
            .Where(user =>
                user.TenantId == tenantId &&
                ids.Contains(user.Id) &&
                user.IsEnable &&
                !user.IsDeleted &&
                user.PublicId != null &&
                user.PublicId.StartsWith(User.PublicIdPrefix) &&
                user.PublicId.Length == 36)
            .Select(user => new PublicActorRow
            {
                UserId = user.Id,
                PublicId = user.PublicId,
                DisplayName = user.UserName
            })
            .ToListAsync();
        return actors
            .Where(actor => User.HasPublicIdFormat(actor.PublicId))
            .ToDictionary(actor => actor.UserId);
    }

    private static ISugarQueryable<Post> ApplyPostCursor(
        ISugarQueryable<Post> query,
        PublicDiscoverSourceWindow window,
        int kindOrder)
    {
        if (!HasCursor(window))
        {
            return query;
        }

        var lastOccurredAtUtc = window.LastOccurredAtUtc!.Value;
        if (kindOrder > window.LastKindOrder!.Value)
        {
            return query.Where(post => (post.PublishTime ?? post.CreateTime) <= lastOccurredAtUtc);
        }

        if (kindOrder < window.LastKindOrder.Value)
        {
            return query.Where(post => (post.PublishTime ?? post.CreateTime) < lastOccurredAtUtc);
        }

        var lastSourceId = window.LastSourceId!.Value;
        return query.Where(post =>
            (post.PublishTime ?? post.CreateTime) < lastOccurredAtUtc ||
            (post.PublishTime ?? post.CreateTime) == lastOccurredAtUtc && post.Id < lastSourceId);
    }

    private static ISugarQueryable<WikiDocument, User> ApplyMemberActivityCursor(
        ISugarQueryable<WikiDocument, User> query,
        PublicDiscoverSourceWindow window)
    {
        if (!HasCursor(window))
        {
            return query;
        }

        var lastOccurredAtUtc = window.LastOccurredAtUtc!.Value;
        if (PublicDiscoverKindOrder.MemberActivity > window.LastKindOrder!.Value)
        {
            return query.Where((document, user) => document.PublishedAt!.Value <= lastOccurredAtUtc);
        }

        if (PublicDiscoverKindOrder.MemberActivity < window.LastKindOrder.Value)
        {
            return query.Where((document, user) => document.PublishedAt!.Value < lastOccurredAtUtc);
        }

        var lastSourceId = window.LastSourceId!.Value;
        return query.Where((document, user) =>
            document.PublishedAt!.Value < lastOccurredAtUtc ||
            document.PublishedAt.Value == lastOccurredAtUtc && document.Id < lastSourceId);
    }

    private static ISugarQueryable<CommentHighlight, Comment, Post, User> ApplyHighlightedCommentCursor(
        ISugarQueryable<CommentHighlight, Comment, Post, User> query,
        PublicDiscoverSourceWindow window)
    {
        if (!HasCursor(window))
        {
            return query;
        }

        var lastOccurredAtUtc = window.LastOccurredAtUtc!.Value;
        if (PublicDiscoverKindOrder.HighlightedComment > window.LastKindOrder!.Value)
        {
            return query.Where((highlight, comment, post, user) => highlight.CreateTime <= lastOccurredAtUtc);
        }

        if (PublicDiscoverKindOrder.HighlightedComment < window.LastKindOrder.Value)
        {
            return query.Where((highlight, comment, post, user) => highlight.CreateTime < lastOccurredAtUtc);
        }

        var lastSourceId = window.LastSourceId!.Value;
        return query.Where((highlight, comment, post, user) =>
            highlight.CreateTime < lastOccurredAtUtc ||
            highlight.CreateTime == lastOccurredAtUtc && highlight.Id < lastSourceId);
    }

    private static ISugarQueryable<PostQuestion, Post> ApplyQuestionCursor(
        ISugarQueryable<PostQuestion, Post> query,
        PublicDiscoverSourceWindow window)
    {
        if (!HasCursor(window))
        {
            return query;
        }

        var lastOccurredAtUtc = window.LastOccurredAtUtc!.Value;
        if (PublicDiscoverKindOrder.Question > window.LastKindOrder!.Value)
        {
            return query.Where((question, post) => (post.PublishTime ?? post.CreateTime) <= lastOccurredAtUtc);
        }

        if (PublicDiscoverKindOrder.Question < window.LastKindOrder.Value)
        {
            return query.Where((question, post) => (post.PublishTime ?? post.CreateTime) < lastOccurredAtUtc);
        }

        var lastSourceId = window.LastSourceId!.Value;
        return query.Where((question, post) =>
            (post.PublishTime ?? post.CreateTime) < lastOccurredAtUtc ||
            (post.PublishTime ?? post.CreateTime) == lastOccurredAtUtc && question.Id < lastSourceId);
    }

    private static bool HasCursor(PublicDiscoverSourceWindow window) =>
        window.LastOccurredAtUtc.HasValue &&
        window.LastKindOrder.HasValue &&
        window.LastSourceId.HasValue;

    private static bool HasPostPublicIdFormat(string? value)
    {
        var normalized = value?.Trim();
        return normalized is { Length: 36 } &&
               normalized.StartsWith(PostPublicIdPrefix, StringComparison.OrdinalIgnoreCase) &&
               normalized[PostPublicIdPrefix.Length..].All(Uri.IsHexDigit);
    }

    private sealed class PublicActorRow
    {
        public long UserId { get; set; }

        public string? PublicId { get; set; }

        public string? DisplayName { get; set; }
    }

    private sealed class PublicDiscoverQuestionRow
    {
        public long SourceId { get; set; }

        public long PostId { get; set; }

        public long AuthorId { get; set; }

        public DateTime OccurredAtUtc { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Summary { get; set; }

        public string? PostPublicId { get; set; }

        public int AnswerCount { get; set; }
    }
}
