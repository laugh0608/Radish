using Radish.Common;
using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立论坛帖子收藏权威关系并重建 CollectCount 投影。</summary>
internal sealed class ForumPostBookmarkSchemaMigration : ISchemaMigration
{
    private const string BookmarkTable = "UserPostBookmark";
    private const string MineIndex = "idx_userpostbookmark_mine";

    private static readonly string[] RequiredIndexes =
    [
        "idx_userpostbookmark_public_id",
        "idx_userpostbookmark_relation",
        MineIndex,
        "idx_userpostbookmark_post_active"
    ];

    public static ForumPostBookmarkSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260729_017_forum_post_bookmark";

    public string Scope => "Main";

    public string Description => "建立私有帖子收藏关系、稳定分页与 CollectCount 权威投影";

    public string ChecksumSource =>
        "20260729_017_forum_post_bookmark|Main|" +
        "UserPostBookmark-v1|bookmark-publicid-relation-unique-v1|" +
        "mine-stable-page-v1|post-active-projection-v1|" +
        "tenant-orphan-collectcount-strict-verify-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var hasProjectionBaseline = EnsureConsistentProjectionBaseline(db);
        var issues = Diagnose(db, services);
        if (issues.Count > 0)
        {
            throw new InvalidOperationException(
                "论坛帖子收藏迁移前置诊断未通过：" + string.Join("；", issues));
        }

        db.CodeFirst.InitTables<UserPostBookmark>();
        NormalizeBookmarkPublicIds(db);
        if (hasProjectionBaseline)
        {
            RebuildCollectCount(db);
        }
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(BookmarkTable, false))
        {
            return [];
        }

        var relationColumns = new[]
        {
            nameof(UserPostBookmark.Id),
            nameof(UserPostBookmark.TenantId),
            nameof(UserPostBookmark.UserId),
            nameof(UserPostBookmark.PostId),
            nameof(UserPostBookmark.IsDeleted)
        };
        if (relationColumns.Any(column =>
                DatabaseIdentifierResolver.ResolveColumn(db, BookmarkTable, column) == null))
        {
            return [];
        }

        var bookmarks = db.Queryable<UserPostBookmark>()
            .Select(item => new UserPostBookmark
            {
                Id = item.Id,
                TenantId = item.TenantId,
                UserId = item.UserId,
                PostId = item.PostId,
                IsDeleted = item.IsDeleted
            })
            .ToList();
        var issues = new List<string>();
        VerifyRelationUniqueness(issues, bookmarks);
        VerifyOwnership(issues, db, bookmarks);

        if (DatabaseIdentifierResolver.ResolveColumn(
                db,
                BookmarkTable,
                nameof(UserPostBookmark.PublicId)) != null)
        {
            var publicIds = db.Queryable<UserPostBookmark>()
                .Select(item => new UserPostBookmark
                {
                    Id = item.Id,
                    PublicId = item.PublicId
                })
                .ToList();
            VerifyPublicIdUniqueness(issues, publicIds);
        }

        return issues.Distinct(StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        if (!db.DbMaintenance.IsAnyTable(BookmarkTable, false))
        {
            return [$"缺少表 {BookmarkTable}。"];
        }

        var hasUserTable = db.DbMaintenance.IsAnyTable(nameof(User), false);
        var hasPostTable = db.DbMaintenance.IsAnyTable(nameof(Post), false);
        if (hasUserTable && !hasPostTable)
        {
            issues.Add("Main 已存在 User baseline，但缺少 Post 表。");
        }

        foreach (var columnName in new[]
                 {
                     nameof(UserPostBookmark.Id),
                     nameof(UserPostBookmark.PublicId),
                     nameof(UserPostBookmark.TenantId),
                     nameof(UserPostBookmark.UserId),
                     nameof(UserPostBookmark.PostId),
                     nameof(UserPostBookmark.BookmarkedAt),
                     nameof(UserPostBookmark.IsDeleted),
                     nameof(UserPostBookmark.DeletedAt),
                     nameof(UserPostBookmark.DeletedBy),
                     nameof(UserPostBookmark.CreateTime),
                     nameof(UserPostBookmark.CreateBy),
                     nameof(UserPostBookmark.CreateId),
                     nameof(UserPostBookmark.ModifyTime),
                     nameof(UserPostBookmark.ModifyBy),
                     nameof(UserPostBookmark.ModifyId)
                 })
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, BookmarkTable, columnName) == null)
            {
                issues.Add($"缺少列 {BookmarkTable}.{columnName}。");
            }
        }
        if (hasUserTable &&
            hasPostTable &&
            DatabaseIdentifierResolver.ResolveColumn(
                db,
                nameof(Post),
                nameof(Post.CollectCount)) == null)
        {
            issues.Add($"缺少列 {nameof(Post)}.{nameof(Post.CollectCount)}。");
        }
        if (issues.Count > 0)
        {
            return issues;
        }

        foreach (var indexName in RequiredIndexes)
        {
            if (!IndexExists(db, BookmarkTable, indexName))
            {
                issues.Add($"缺少索引 {indexName}。");
            }
        }
        if (IndexExists(db, BookmarkTable, MineIndex) &&
            !HasStableMineIndexDefinition(db))
        {
            issues.Add($"{MineIndex} 未包含 BookmarkedAt DESC, Id DESC 稳定分页尾键。");
        }

        var bookmarks = db.Queryable<UserPostBookmark>().ToList();
        VerifyPublicIds(issues, bookmarks);
        VerifyPublicIdUniqueness(issues, bookmarks);
        VerifyRelationUniqueness(issues, bookmarks);
        VerifyOwnership(issues, db, bookmarks);
        if (hasUserTable && hasPostTable)
        {
            VerifyCollectCount(issues, db, bookmarks);
        }
        return issues.Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool EnsureConsistentProjectionBaseline(ISqlSugarClient db)
    {
        var hasUserTable = db.DbMaintenance.IsAnyTable(nameof(User), false);
        var hasPostTable = db.DbMaintenance.IsAnyTable(nameof(Post), false);
        if (!hasUserTable)
        {
            return false;
        }
        if (!hasPostTable)
        {
            throw new InvalidOperationException(
                "论坛帖子收藏迁移已检测到 User baseline，但缺少 Post 表。");
        }
        if (DatabaseIdentifierResolver.ResolveColumn(
                db,
                nameof(Post),
                nameof(Post.CollectCount)) == null)
        {
            throw new InvalidOperationException(
                $"论坛帖子收藏迁移要求 baseline 已存在列 {nameof(Post)}.{nameof(Post.CollectCount)}。");
        }
        return true;
    }

    private static void NormalizeBookmarkPublicIds(ISqlSugarClient db)
    {
        var bookmarks = db.Queryable<UserPostBookmark>().ToList();
        foreach (var bookmark in bookmarks)
        {
            var normalized = bookmark.PublicId?.Trim().ToLowerInvariant();
            var desired = UserPostBookmark.HasPublicIdFormat(normalized)
                ? normalized!
                : UserPostBookmark.GeneratePublicId();
            if (string.Equals(bookmark.PublicId, desired, StringComparison.Ordinal))
            {
                continue;
            }

            db.Updateable<UserPostBookmark>()
                .SetColumns(item => new UserPostBookmark { PublicId = desired })
                .Where(item => item.Id == bookmark.Id)
                .ExecuteCommand();
        }
    }

    private static void RebuildCollectCount(ISqlSugarClient db)
    {
        var activeCounts = db.Queryable<UserPostBookmark>()
            .Where(item => !item.IsDeleted)
            .ToList()
            .GroupBy(item => (item.TenantId, item.PostId))
            .ToDictionary(group => group.Key, group => group.Count());
        var posts = db.Queryable<Post>()
            .Select(item => new Post
            {
                Id = item.Id,
                TenantId = item.TenantId,
                CollectCount = item.CollectCount
            })
            .ToList();
        foreach (var post in posts)
        {
            var expected = activeCounts.GetValueOrDefault((post.TenantId, post.Id), 0);
            if (post.CollectCount == expected)
            {
                continue;
            }

            db.Updateable<Post>()
                .SetColumns(item => new Post { CollectCount = expected })
                .Where(item => item.TenantId == post.TenantId && item.Id == post.Id)
                .ExecuteCommand();
        }
    }

    private static void VerifyPublicIds(
        ICollection<string> issues,
        IReadOnlyCollection<UserPostBookmark> bookmarks)
    {
        foreach (var bookmark in bookmarks.Where(item =>
                     !UserPostBookmark.HasPublicIdFormat(item.PublicId)))
        {
            issues.Add($"UserPostBookmark {bookmark.Id} 的 PublicId 非法。");
        }
    }

    private static void VerifyPublicIdUniqueness(
        ICollection<string> issues,
        IReadOnlyCollection<UserPostBookmark> bookmarks)
    {
        foreach (var duplicate in bookmarks
                     .Where(item => !string.IsNullOrWhiteSpace(item.PublicId))
                     .GroupBy(
                         item => item.PublicId.Trim(),
                         StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(
                $"UserPostBookmark PublicId {duplicate.Key} 存在 {duplicate.Count()} 条重复记录。");
        }
    }

    private static void VerifyRelationUniqueness(
        ICollection<string> issues,
        IReadOnlyCollection<UserPostBookmark> bookmarks)
    {
        foreach (var duplicate in bookmarks
                     .GroupBy(item => (item.TenantId, item.UserId, item.PostId))
                     .Where(group => group.Count() > 1))
        {
            issues.Add(
                $"租户 {duplicate.Key.TenantId} 用户 {duplicate.Key.UserId} 帖子 " +
                $"{duplicate.Key.PostId} 存在 {duplicate.Count()} 条收藏关系。");
        }
    }

    private static void VerifyOwnership(
        ICollection<string> issues,
        ISqlSugarClient db,
        IReadOnlyCollection<UserPostBookmark> bookmarks)
    {
        if (bookmarks.Count == 0)
        {
            return;
        }
        if (!db.DbMaintenance.IsAnyTable(nameof(User), false) ||
            !db.DbMaintenance.IsAnyTable(nameof(Post), false))
        {
            issues.Add("Main 缺少 User 或 Post，不能验证收藏关系归属。");
            return;
        }

        var userIds = bookmarks.Select(item => item.UserId).Distinct().ToList();
        var postIds = bookmarks.Select(item => item.PostId).Distinct().ToList();
        var users = db.Queryable<User>()
            .Where(item => userIds.Contains(item.Id))
            .Select(item => new User { Id = item.Id, TenantId = item.TenantId })
            .ToList()
            .ToDictionary(item => item.Id);
        var posts = db.Queryable<Post>()
            .Where(item => postIds.Contains(item.Id))
            .Select(item => new Post { Id = item.Id, TenantId = item.TenantId })
            .ToList()
            .ToDictionary(item => item.Id);
        foreach (var bookmark in bookmarks)
        {
            if (!users.TryGetValue(bookmark.UserId, out var user) ||
                user.TenantId != bookmark.TenantId)
            {
                issues.Add(
                    $"UserPostBookmark {bookmark.Id} 指向不存在或跨租户的用户 {bookmark.UserId}。");
            }
            if (!posts.TryGetValue(bookmark.PostId, out var post) ||
                post.TenantId != bookmark.TenantId)
            {
                issues.Add(
                    $"UserPostBookmark {bookmark.Id} 指向不存在或跨租户的帖子 {bookmark.PostId}。");
            }
        }
    }

    private static void VerifyCollectCount(
        ICollection<string> issues,
        ISqlSugarClient db,
        IReadOnlyCollection<UserPostBookmark> bookmarks)
    {
        var activeCounts = bookmarks
            .Where(item => !item.IsDeleted)
            .GroupBy(item => (item.TenantId, item.PostId))
            .ToDictionary(group => group.Key, group => group.Count());
        var posts = db.Queryable<Post>()
            .Select(item => new Post
            {
                Id = item.Id,
                TenantId = item.TenantId,
                CollectCount = item.CollectCount
            })
            .ToList();
        foreach (var post in posts)
        {
            if (post.CollectCount < 0)
            {
                issues.Add($"Post {post.Id} 的 CollectCount 小于 0。");
                continue;
            }

            var expected = activeCounts.GetValueOrDefault((post.TenantId, post.Id), 0);
            if (post.CollectCount != expected)
            {
                issues.Add(
                    $"Post {post.Id} 的 CollectCount={post.CollectCount}，" +
                    $"与有效收藏重建值 {expected} 不一致。");
            }
        }
    }

    private static bool HasStableMineIndexDefinition(ISqlSugarClient db)
    {
        var requiredColumns = new[]
        {
            nameof(UserPostBookmark.TenantId),
            nameof(UserPostBookmark.UserId),
            nameof(UserPostBookmark.IsDeleted),
            nameof(UserPostBookmark.BookmarkedAt),
            nameof(UserPostBookmark.Id)
        };
        if (db.CurrentConnectionConfig.DbType == DbType.Sqlite)
        {
            var rows = db.Ado.SqlQuery<SqliteIndexColumn>(
                $"PRAGMA index_xinfo(\"{MineIndex}\")")
                .Where(item => item.IsKey == 1)
                .OrderBy(item => item.SeqNo)
                .ToList();
            return rows
                .OrderBy(item => item.SeqNo)
                .Select(item => item.Name)
                .SequenceEqual(requiredColumns, StringComparer.OrdinalIgnoreCase) &&
                   rows.Single(item => string.Equals(
                       item.Name,
                       nameof(UserPostBookmark.BookmarkedAt),
                       StringComparison.OrdinalIgnoreCase)).IsDescending == 1 &&
                   rows.Single(item => string.Equals(
                       item.Name,
                       nameof(UserPostBookmark.Id),
                       StringComparison.OrdinalIgnoreCase)).IsDescending == 1;
        }
        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return true;
        }

        var tableName = db.EntityMaintenance.GetEntityInfo<UserPostBookmark>().DbTableName;
        var definition = db.Ado.GetString(
            """
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = current_schema()
              AND lower(tablename) = lower(@tableName)
              AND lower(indexname) = lower(@indexName)
            LIMIT 1
            """,
            new SugarParameter("@tableName", tableName),
            new SugarParameter("@indexName", MineIndex));
        if (string.IsNullOrWhiteSpace(definition))
        {
            return false;
        }

        var normalized = definition.Replace("\"", string.Empty, StringComparison.Ordinal);
        var searchStart = 0;
        foreach (var column in requiredColumns)
        {
            var index = normalized.IndexOf(column, searchStart, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }
            searchStart = index + column.Length;
        }
        return normalized.Contains(
                   $"{nameof(UserPostBookmark.BookmarkedAt)} DESC",
                   StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains(
                   $"{nameof(UserPostBookmark.Id)} DESC",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IndexExists(ISqlSugarClient db, string tableName, string indexName)
    {
        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return db.DbMaintenance.IsAnyIndex(indexName);
        }

        var physicalTableName =
            DatabaseIdentifierResolver.ResolveColumn(db, tableName, nameof(UserPostBookmark.Id))
                ?.TableName
            ?? tableName;
        return db.DbMaintenance.GetIndexList(physicalTableName)
            .Any(index => string.Equals(index, indexName, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class SqliteIndexColumn
    {
        [SugarColumn(ColumnName = "seqno")]
        public int SeqNo { get; set; }

        [SugarColumn(ColumnName = "name")]
        public string Name { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "desc")]
        public int IsDescending { get; set; }

        [SugarColumn(ColumnName = "key")]
        public int IsKey { get; set; }
    }
}
