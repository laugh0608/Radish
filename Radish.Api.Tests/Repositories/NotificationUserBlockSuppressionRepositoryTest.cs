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

public sealed class NotificationUserBlockSuppressionRepositoryTest
{
    [Fact]
    public async Task SuppressBlockedActorsAsync_ShouldHideOnlyRelationshipNotificationsAndRebuildUnreadState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-notification-block-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);
        try
        {
            InitializeSchema(db);
            var repository = new NotificationRepository(
                new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));
            var nowUtc = new DateTime(2026, 7, 25, 8, 0, 0, DateTimeKind.Utc);
            await repository.PersistAsync(
                CreateNotification(
                    5001,
                    NotificationType.PostLiked,
                    NotificationCategory.Reaction,
                    NotificationTargetKind.ForumPost,
                    new NotificationTargetData { PostId = 7001 },
                    2002,
                    nowUtc),
                [new NotificationInboxRecipient(1001, true)],
                nowUtc);
            await repository.PersistAsync(
                CreateNotification(
                    5002,
                    NotificationType.AccountSecurity,
                    NotificationCategory.System,
                    NotificationTargetKind.None,
                    new NotificationTargetData(),
                    2002,
                    nowUtc.AddMinutes(1)),
                [new NotificationInboxRecipient(1001, true)],
                nowUtc.AddMinutes(1));

            var result = await repository.SuppressBlockedActorsAsync(
                9,
                1001,
                [2002],
                nowUtc.AddMinutes(2));

            Assert.Equal(1, result.AffectedRows);
            var relations = db.Queryable<UserNotification>().OrderBy(item => item.NotificationId).ToList();
            Assert.True(relations.Single(item => item.NotificationId == 5001).SuppressedByUserBlock);
            Assert.False(relations.Single(item => item.NotificationId == 5002).SuppressedByUserBlock);
            var summary = await repository.GetSummaryAsync(9, 1001);
            Assert.Equal(1, summary.UnreadGroupCount);
            Assert.Equal(1, summary.UnreadOccurrenceCount);

            var replay = await repository.SuppressBlockedActorsAsync(
                9,
                1001,
                [2002],
                nowUtc.AddMinutes(3));
            Assert.Equal(0, replay.AffectedRows);
            var replaySummary = await repository.GetSummaryAsync(9, 1001);
            Assert.Equal(summary.Revision, replaySummary.Revision);
            Assert.Equal(summary.UnreadGroupCount, replaySummary.UnreadGroupCount);
            Assert.Equal(summary.UnreadOccurrenceCount, replaySummary.UnreadOccurrenceCount);
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
    public async Task MarkGroupsAsReadAsync_ShouldNotRecountSuppressedRelations()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-notification-block-read-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);
        try
        {
            InitializeSchema(db);
            var repository = new NotificationRepository(
                new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));
            var nowUtc = new DateTime(2026, 7, 25, 9, 0, 0, DateTimeKind.Utc);
            var target = new NotificationTargetData { PostId = 7001 };
            await repository.PersistAsync(
                CreateNotification(
                    5101,
                    NotificationType.PostLiked,
                    NotificationCategory.Reaction,
                    NotificationTargetKind.ForumPost,
                    target,
                    2002,
                    nowUtc),
                [new NotificationInboxRecipient(1001, true)],
                nowUtc);
            await repository.PersistAsync(
                CreateNotification(
                    5102,
                    NotificationType.PostLiked,
                    NotificationCategory.Reaction,
                    NotificationTargetKind.ForumPost,
                    target,
                    3003,
                    nowUtc.AddMinutes(1)),
                [new NotificationInboxRecipient(1001, true)],
                nowUtc.AddMinutes(1));
            await repository.SuppressBlockedActorsAsync(9, 1001, [2002], nowUtc.AddMinutes(2));
            var groupId = (await repository.GetGroupIdsByNotificationIdsAsync(9, 1001, [5102])).Single();

            await repository.MarkGroupsAsReadAsync(9, 1001, [groupId], nowUtc.AddMinutes(3));

            var group = db.Queryable<NotificationInboxGroup>().InSingle(groupId);
            Assert.Equal(0, group.UnreadOccurrenceCount);
            var summary = await repository.GetSummaryAsync(9, 1001);
            Assert.Equal(0, summary.UnreadGroupCount);
            Assert.Equal(0, summary.UnreadOccurrenceCount);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static Notification CreateNotification(
        long id,
        string type,
        string category,
        string targetKind,
        NotificationTargetData target,
        long triggerId,
        DateTime occurredAtUtc)
    {
        return new Notification(new NotificationInitializationOptions(type, type)
        {
            Category = category,
            TemplateKey = $"notification.{type}",
            TemplateArgumentsJson = "{}",
            TargetKind = targetKind,
            TargetDataJson = targetKind == NotificationTargetKind.None ? null : target.ToJson(),
            OccurredAtUtc = occurredAtUtc,
            TenantId = 9
        })
        {
            Id = id,
            BusinessKey = $"notification:{id}",
            TriggerId = triggerId,
            TriggerName = "bob",
            CreateTime = occurredAtUtc
        };
    }

    private static void InitializeSchema(SqlSugarScope db)
    {
        db.CodeFirst.InitTables<Notification>();
        db.CodeFirst.InitTables<UserNotification>();
        db.CodeFirst.InitTables<NotificationInboxGroup>();
        db.CodeFirst.InitTables<NotificationInboxState>();
        db.CodeFirst.InitTables<NotificationSetting>();
    }

    private static SqlSugarScope CreateClient(string path) =>
        new(new ConnectionConfig
        {
            ConfigId = "message",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
}
