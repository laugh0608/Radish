using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class NotificationInboxRepositoryTest
{
    private static readonly DateTime NowUtc = new(2026, 7, 18, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PersistAsync_ShouldAggregateReplayPartialRecipientsAndKeepSummaryConsistent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-notification-repository-{Guid.NewGuid():N}.db");
        using var scope = CreateScope(path);
        try
        {
            var db = scope.GetConnectionScope("message");
            InitializeSchema(db);
            var repository = CreateRepository(scope);

            var first = await repository.PersistAsync(
                CreatePostLikedNotification(1001, "event:like:1001", 5001, NowUtc),
                [new NotificationInboxRecipient(3001, true)],
                NowUtc);
            var second = await repository.PersistAsync(
                CreatePostLikedNotification(1002, "event:like:1002", 5001, NowUtc.AddMinutes(30)),
                [new NotificationInboxRecipient(3001, true)],
                NowUtc.AddMinutes(30));
            var replayWithMissingRecipient = await repository.PersistAsync(
                CreatePostLikedNotification(1001, "event:like:1001", 5001, NowUtc),
                [
                    new NotificationInboxRecipient(3001, true),
                    new NotificationInboxRecipient(3002, false)
                ],
                NowUtc.AddMinutes(31));

            Assert.True(first.EventCreated);
            Assert.True(second.EventCreated);
            Assert.Single(replayWithMissingRecipient.RecipientChanges);
            Assert.Equal(3002, replayWithMissingRecipient.RecipientChanges[0].UserId);
            Assert.Equal(2, db.Queryable<Notification>().SplitTable().Count());
            Assert.Equal(3, db.Queryable<UserNotification>().Count());

            var firstUserGroup = Assert.Single(db.Queryable<NotificationInboxGroup>()
                .Where(group => group.UserId == 3001)
                .ToList());
            Assert.Equal(2, firstUserGroup.OccurrenceCount);
            Assert.Equal(2, firstUserGroup.UnreadOccurrenceCount);
            Assert.Equal(2, firstUserGroup.DistinctTriggerCount);
            var firstUserSummary = await repository.GetSummaryAsync(9, 3001);
            Assert.Equal(1, firstUserSummary.UnreadGroupCount);
            Assert.Equal(2, firstUserSummary.UnreadOccurrenceCount);
            Assert.Equal(2, firstUserSummary.Revision);

            var secondUserGroup = Assert.Single(db.Queryable<NotificationInboxGroup>()
                .Where(group => group.UserId == 3002)
                .ToList());
            Assert.Equal(1, secondUserGroup.OccurrenceCount);
            Assert.Equal(1, secondUserGroup.UnreadOccurrenceCount);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task PersistAsync_ShouldRollbackAndThrowWhenCurrentRelationCannotBeWritten()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-notification-write-failure-{Guid.NewGuid():N}.db");
        using var scope = CreateScope(path);
        try
        {
            var db = scope.GetConnectionScope("message");
            InitializeSchema(db);
            db.Ado.ExecuteCommand(
                "ALTER TABLE \"UserNotification\" ADD COLUMN \"DeliveryStatus\" TEXT NOT NULL");
            db.Aop.OnError = _ => { };
            var repository = CreateRepository(scope);

            await Assert.ThrowsAnyAsync<Exception>(() => repository.PersistAsync(
                CreatePostLikedNotification(1051, "event:like:write-failure", 5051, NowUtc),
                [new NotificationInboxRecipient(3051, true)],
                NowUtc));

            Assert.Empty(db.Queryable<Notification>().SplitTable().ToList());
            Assert.Empty(db.Queryable<UserNotification>().ToList());
            Assert.Empty(db.Queryable<NotificationInboxGroup>().ToList());
            Assert.Empty(db.Queryable<NotificationInboxState>().ToList());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ReadAndDelete_ShouldUseGroupCutoffAndRebuildAuthoritativeState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-notification-mutation-{Guid.NewGuid():N}.db");
        using var scope = CreateScope(path);
        try
        {
            var db = scope.GetConnectionScope("message");
            InitializeSchema(db);
            var repository = CreateRepository(scope);
            await repository.PersistAsync(
                CreatePostLikedNotification(1101, "event:like:1101", 5101, NowUtc),
                [new NotificationInboxRecipient(3101, true)],
                NowUtc);
            var group = Assert.Single(db.Queryable<NotificationInboxGroup>().ToList());

            var read = await repository.MarkGroupsAsReadAsync(9, 3101, [group.Id], NowUtc.AddMinutes(1));
            Assert.Equal(1, read.AffectedRows);
            Assert.Equal(0, read.Summary.UnreadGroupCount);

            await repository.PersistAsync(
                CreatePostLikedNotification(1102, "event:like:1102", 5101, NowUtc.AddMinutes(2)),
                [new NotificationInboxRecipient(3101, true)],
                NowUtc.AddMinutes(2));
            var afterNewEvent = await repository.GetSummaryAsync(9, 3101);
            Assert.Equal(1, afterNewEvent.UnreadGroupCount);
            Assert.Equal(1, afterNewEvent.UnreadOccurrenceCount);

            var deleted = await repository.DeleteGroupAsync(9, 3101, group.Id, NowUtc.AddMinutes(3));
            Assert.True(deleted.AffectedRows > 0);
            Assert.Equal(0, deleted.Summary.UnreadGroupCount);
            Assert.Equal(0, deleted.Summary.UnreadOccurrenceCount);
            Assert.All(
                db.Queryable<UserNotification>().ToList(),
                relation => Assert.True(relation.IsDeleted));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task UpsertPreferencesAsync_ShouldKeepOneRowPerCategory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-notification-preference-{Guid.NewGuid():N}.db");
        using var scope = CreateScope(path);
        try
        {
            var db = scope.GetConnectionScope("message");
            InitializeSchema(db);
            var repository = CreateRepository(scope);

            await repository.UpsertPreferencesAsync(
                9,
                3201,
                [CreatePreference(false, true)],
                NowUtc);
            var updated = await repository.UpsertPreferencesAsync(
                9,
                3201,
                [CreatePreference(true, false)],
                NowUtc.AddMinutes(1));

            var preference = Assert.Single(updated);
            Assert.True(preference.InAppEnabled);
            Assert.False(preference.RealtimePreviewEnabled);
            Assert.Equal(1, db.Queryable<NotificationSetting>().Count());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task CleanupAsync_ShouldRespectRetentionRemainIdempotentAndNeverDeleteUnreadForCapacity()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-notification-cleanup-{Guid.NewGuid():N}.db");
        using var scope = CreateScope(path);
        try
        {
            var db = scope.GetConnectionScope("message");
            InitializeSchema(db);
            var repository = CreateRepository(scope);
            const long userId = 3301;

            await repository.PersistAsync(
                CreatePostLikedNotification(1201, "event:like:1201", 5201, NowUtc.AddDays(-200)),
                [new NotificationInboxRecipient(userId, true)],
                NowUtc.AddDays(-200));
            var readGroup = db.Queryable<NotificationInboxGroup>()
                .Single(group => group.LatestNotificationId == 1201);
            await repository.MarkGroupsAsReadAsync(
                9,
                userId,
                [readGroup.Id],
                NowUtc.AddDays(-199));

            await repository.PersistAsync(
                CreatePostLikedNotification(1202, "event:like:1202", 5202, NowUtc.AddDays(-200)),
                [new NotificationInboxRecipient(userId, true)],
                NowUtc.AddDays(-200));
            var deletedGroup = db.Queryable<NotificationInboxGroup>()
                .Single(group => group.LatestNotificationId == 1202);
            await repository.DeleteGroupAsync(9, userId, deletedGroup.Id, NowUtc.AddDays(-31));

            await repository.PersistAsync(
                CreatePostLikedNotification(1203, "event:like:1203", 5203, NowUtc),
                [new NotificationInboxRecipient(userId, true)],
                NowUtc);
            await repository.PersistAsync(
                CreatePostLikedNotification(1204, "event:like:1204", 5204, NowUtc.AddMinutes(1)),
                [new NotificationInboxRecipient(userId, true)],
                NowUtc.AddMinutes(1));

            var cleanupResults = await Task.WhenAll(
                repository.CleanupAsync(NowUtc.AddMinutes(2), 10, 1),
                CreateRepository(scope).CleanupAsync(NowUtc.AddMinutes(2), 10, 1));
            var result = Assert.Single(cleanupResults, item => item.DeletedGroupCount > 0);

            Assert.Equal(2, result.DeletedRelationCount);
            Assert.Equal(2, result.DeletedGroupCount);
            Assert.Equal(2, result.DeletedNotificationCount);
            Assert.Equal(2, cleanupResults.Sum(item => item.DeletedRelationCount));
            Assert.Equal(2, cleanupResults.Sum(item => item.DeletedGroupCount));
            Assert.Equal(2, cleanupResults.Sum(item => item.DeletedNotificationCount));
            Assert.All(cleanupResults, item => Assert.Single(item.CapacityWarnings));
            var warning = Assert.Single(result.CapacityWarnings);
            Assert.Equal(userId, warning.UserId);
            Assert.Equal(2, warning.RelationCount);
            Assert.Equal(2, db.Queryable<UserNotification>().Count());
            Assert.Equal(2, db.Queryable<NotificationInboxGroup>().Count());
            Assert.Equal(2, db.Queryable<Notification>().SplitTable().Count());
            Assert.All(
                db.Queryable<UserNotification>().ToList(),
                relation => Assert.False(relation.IsRead || relation.IsDeleted));
            var summary = await repository.GetSummaryAsync(9, userId);
            Assert.Equal(2, summary.UnreadGroupCount);
            Assert.Equal(2, summary.UnreadOccurrenceCount);

            var replay = await repository.CleanupAsync(NowUtc.AddMinutes(3), 10, 1);
            Assert.Equal(0, replay.DeletedRelationCount);
            Assert.Equal(0, replay.DeletedGroupCount);
            Assert.Equal(0, replay.DeletedNotificationCount);
            Assert.Single(replay.CapacityWarnings);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static Notification CreatePostLikedNotification(
        long id,
        string businessKey,
        long postId,
        DateTime occurredAtUtc)
    {
        return new Notification(new NotificationInitializationOptions(NotificationType.PostLiked, "帖子被点赞")
        {
            Category = NotificationCategory.Reaction,
            TemplateKey = "notification.PostLiked",
            TemplateArgumentsJson = "{\"targetTitle\":\"测试帖子\"}",
            TargetKind = NotificationTargetKind.ForumPost,
            TargetDataJson = new NotificationTargetData { PostId = postId }.ToJson(),
            OccurredAtUtc = occurredAtUtc,
            TenantId = 9
        })
        {
            Id = id,
            BusinessKey = businessKey,
            BusinessType = BusinessType.Post,
            BusinessId = postId,
            TriggerId = id + 10000,
            CreateTime = occurredAtUtc
        };
    }

    private static NotificationSetting CreatePreference(bool inAppEnabled, bool realtimePreviewEnabled)
    {
        return new NotificationSetting
        {
            Category = NotificationCategory.Reaction,
            InAppEnabled = inAppEnabled,
            RealtimePreviewEnabled = realtimePreviewEnabled,
            CreateBy = "Tester",
            CreateId = 3201
        };
    }

    private static NotificationRepository CreateRepository(SqlSugarScope scope)
    {
        return new NotificationRepository(new UnitOfWorkManage(
            scope,
            NullLogger<UnitOfWorkManage>.Instance));
    }

    private static void InitializeSchema(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<Notification>();
        db.CodeFirst.InitTables<UserNotification>();
        db.CodeFirst.InitTables<NotificationSetting>();
        db.CodeFirst.InitTables<NotificationInboxGroup>();
        db.CodeFirst.InitTables<NotificationInboxState>();
    }

    private static SqlSugarScope CreateScope(string path)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "message",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }
}
