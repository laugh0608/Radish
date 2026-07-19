using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ChatSchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public void ChatDirectConversationMigration_ShouldUpgradeLegacyTablesAndRemainRepeatable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-direct-migration-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path, "Chat");
        using var services = new ServiceCollection().BuildServiceProvider();

        try
        {
            db.CodeFirst.InitTables<Channel>();
            db.CodeFirst.InitTables<ChannelMember>();
            db.CodeFirst.InitTables<ChannelMessage>();
            db.Ado.ExecuteCommand("DROP INDEX IF EXISTS \"idx_channel_message_client_request\"");
            db.Ado.ExecuteCommand("ALTER TABLE \"ChannelMember\" DROP COLUMN \"ArchivedAt\"");
            db.Ado.ExecuteCommand("ALTER TABLE \"ChannelMessage\" DROP COLUMN \"ClientRequestId\"");

            var migration = ChatDirectConversationSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.True(db.DbMaintenance.IsAnyTable("DirectConversation", false));
            Assert.True(db.DbMaintenance.IsAnyColumn("ChannelMember", "ArchivedAt", false));
            Assert.True(db.DbMaintenance.IsAnyColumn("ChannelMessage", "ClientRequestId", false));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_channel_message_client_request"));

            db.Insertable(CreateDirectConversation(1001, 5001)).ExecuteCommand();
            Assert.ThrowsAny<Exception>(() =>
                db.Insertable(CreateDirectConversation(1002, 5002)).ExecuteCommand());

            db.Insertable(CreateMessage(2001, "same-request")).ExecuteCommand();
            Assert.ThrowsAny<Exception>(() =>
                db.Insertable(CreateMessage(2002, "same-request")).ExecuteCommand());
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
    [Trait("Database", "PostgreSQL")]
    public async Task ChatDirectConversationMigration_ShouldSupportPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 Chat PostgreSQL 迁移测试");

        var schema = $"chat_direct_{Guid.NewGuid():N}";
        using var adminDb = CreatePostgreSqlClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = CreatePostgreSqlClient(connectionString);
            using var services = new ServiceCollection().BuildServiceProvider();
            db.CodeFirst.InitTables<Channel>();
            db.CodeFirst.InitTables<ChannelMember>();
            db.CodeFirst.InitTables<ChannelMessage>();
            db.Ado.ExecuteCommand("DROP INDEX IF EXISTS \"idx_channel_message_client_request\"");
            DropColumn(db, "ChannelMember", "ArchivedAt");
            DropColumn(db, "ChannelMessage", "ClientRequestId");

            var migration = ChatDirectConversationSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            db.Insertable(CreateDirectConversation(3001, 7001)).ExecuteCommand();
            Assert.ThrowsAny<Exception>(() =>
                db.Insertable(CreateDirectConversation(3002, 7002)).ExecuteCommand());
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    [Fact]
    public void ChatReliableMessageMigration_ShouldAddClaimAndUniqueAttachmentReference()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-reliable-message-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path, "Chat");
        using var services = new ServiceCollection().BuildServiceProvider();

        try
        {
            db.CodeFirst.InitTables<Channel>();
            db.CodeFirst.InitTables<ChannelMember>();
            db.CodeFirst.InitTables<DirectConversation>();
            db.CodeFirst.InitTables<ChannelMessage>();
            db.Ado.ExecuteCommand("DROP INDEX IF EXISTS \"idx_channel_message_attachment\"");
            db.Ado.ExecuteCommand("ALTER TABLE \"DirectConversation\" DROP COLUMN \"RequestMessageId\"");

            var migration = ChatReliableMessageSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            var conversation = CreateDirectConversation(4001, 8001);
            var requestMessage = CreateMessage(5001, "request-message");
            requestMessage.ChannelId = conversation.ChannelId;
            requestMessage.AttachmentId = 6001;
            db.Insertable(conversation).ExecuteCommand();
            db.Insertable(requestMessage).ExecuteCommand();
            db.Updateable<DirectConversation>()
                .SetColumns(item => item.RequestMessageId == requestMessage.Id)
                .Where(item => item.Id == conversation.Id)
                .ExecuteCommand();

            Assert.Empty(migration.Verify(db, services));
            Assert.True(db.DbMaintenance.IsAnyColumn("DirectConversation", "RequestMessageId", false));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_channel_message_attachment"));

            var duplicateAttachmentMessage = CreateMessage(5002, "another-request");
            duplicateAttachmentMessage.ChannelId = conversation.ChannelId;
            duplicateAttachmentMessage.AttachmentId = requestMessage.AttachmentId;
            Assert.ThrowsAny<Exception>(() => db.Insertable(duplicateAttachmentMessage).ExecuteCommand());

            db.Updateable<DirectConversation>()
                .SetColumns(item => item.RequestMessageId == 999999L)
                .Where(item => item.Id == conversation.Id)
                .ExecuteCommand();
            Assert.Contains(
                migration.Verify(db, services),
                issue => issue.Contains("首条请求消息声明与消息事实不一致", StringComparison.Ordinal));
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
    public void ChatReliableMessageVerify_ShouldRemainCompatibleBeforeSearchColumnMigration()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-before-search-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path, "Chat");
        using var services = new ServiceCollection().BuildServiceProvider();

        try
        {
            db.CodeFirst.InitTables<DirectConversation>();
            db.CodeFirst.InitTables<ChannelMessage>();
            var conversation = CreateDirectConversation(8301, 8401);
            var requestMessage = CreateMessage(8501, "before-search");
            requestMessage.ChannelId = conversation.ChannelId;
            db.Insertable(conversation).ExecuteCommand();
            db.Insertable(requestMessage).ExecuteCommand();
            db.Updateable<DirectConversation>()
                .SetColumns(item => item.RequestMessageId == requestMessage.Id)
                .Where(item => item.Id == conversation.Id)
                .ExecuteCommand();
            db.Ado.ExecuteCommand("DROP INDEX IF EXISTS \"idx_channel_message_channel_search_order\"");
            db.Ado.ExecuteCommand("DROP INDEX IF EXISTS \"idx_channel_message_tenant_search_order\"");
            db.Ado.ExecuteCommand("ALTER TABLE \"ChannelMessage\" DROP COLUMN \"SearchText\"");

            var issues = ChatReliableMessageSchemaMigration.Instance.Verify(db, services);

            Assert.Empty(issues);
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
    [Trait("Database", "PostgreSQL")]
    public async Task ChatReliableMessageMigration_ShouldSupportPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 Chat 可靠消息 PostgreSQL 迁移测试");

        var schema = $"chat_reliable_{Guid.NewGuid():N}";
        using var adminDb = CreatePostgreSqlClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = CreatePostgreSqlClient(connectionString);
            using var services = new ServiceCollection().BuildServiceProvider();
            db.CodeFirst.InitTables<DirectConversation>();
            db.CodeFirst.InitTables<ChannelMessage>();
            db.Ado.ExecuteCommand("DROP INDEX IF EXISTS \"idx_channel_message_attachment\"");
            DropColumn(db, "DirectConversation", "RequestMessageId");

            var migration = ChatReliableMessageSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            var conversation = CreateDirectConversation(6001, 9001);
            var requestMessage = CreateMessage(7001, "postgres-request");
            requestMessage.ChannelId = conversation.ChannelId;
            requestMessage.AttachmentId = 8001;
            db.Insertable(conversation).ExecuteCommand();
            db.Insertable(requestMessage).ExecuteCommand();
            db.Updateable<DirectConversation>()
                .SetColumns(item => item.RequestMessageId == requestMessage.Id)
                .Where(item => item.Id == conversation.Id)
                .ExecuteCommand();

            Assert.Empty(migration.Verify(db, services));
            var duplicateAttachmentMessage = CreateMessage(7002, "postgres-duplicate");
            duplicateAttachmentMessage.ChannelId = conversation.ChannelId;
            duplicateAttachmentMessage.AttachmentId = requestMessage.AttachmentId;
            Assert.ThrowsAny<Exception>(() => db.Insertable(duplicateAttachmentMessage).ExecuteCommand());
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    [Fact]
    public void ChatMessageSearchMigration_ShouldBackfillVisibleTextAndRemainRepeatable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-search-migration-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path, "Chat");
        using var services = new ServiceCollection().BuildServiceProvider();

        try
        {
            db.CodeFirst.InitTables<ChannelMessage>();
            var searchable = CreateMessage(8001, "searchable");
            searchable.Content = "Hello @[萝卜](12345)";
            var resourceOnly = CreateMessage(8002, "resource");
            resourceOnly.Content = "sticker://radish/hello";
            var recalled = CreateMessage(8003, "recalled");
            recalled.Content = "private text";
            recalled.IsDeleted = true;
            db.Insertable(new[] { searchable, resourceOnly, recalled }).ExecuteCommand();
            db.Ado.ExecuteCommand("DROP INDEX IF EXISTS \"idx_channel_message_channel_search_order\"");
            db.Ado.ExecuteCommand("DROP INDEX IF EXISTS \"idx_channel_message_tenant_search_order\"");
            db.Ado.ExecuteCommand("ALTER TABLE \"ChannelMessage\" DROP COLUMN \"SearchText\"");

            var migration = ChatMessageSearchSchemaMigration.Instance;
            Assert.Contains(migration.Diagnose(db, services), warning => warning.Contains("3 条历史", StringComparison.Ordinal));

            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_channel_message_channel_search_order"));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_channel_message_tenant_search_order"));
            var messages = db.Queryable<ChannelMessage>().OrderBy(message => message.Id).ToList();
            Assert.Equal("hello @萝卜", messages[0].SearchText);
            Assert.Equal("Hello @[萝卜](12345)", messages[0].Content);
            Assert.Null(messages[1].SearchText);
            Assert.Null(messages[2].SearchText);
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
    public void ChatMessageSearchMigration_ShouldReapplyAfterSqliteBackupRestore()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-search-restore-{Guid.NewGuid():N}.db");
        var backupPath = $"{path}.before-search";
        using var services = new ServiceCollection().BuildServiceProvider();

        try
        {
            using (var legacyDb = CreateClient(path, "Chat"))
            {
                legacyDb.CodeFirst.InitTables<ChannelMessage>();
                var message = CreateMessage(8201, "restore-search");
                message.Content = "Restore @[Tester](20001)";
                legacyDb.Insertable(message).ExecuteCommand();
                legacyDb.Ado.ExecuteCommand("DROP INDEX IF EXISTS \"idx_channel_message_channel_search_order\"");
                legacyDb.Ado.ExecuteCommand("DROP INDEX IF EXISTS \"idx_channel_message_tenant_search_order\"");
                legacyDb.Ado.ExecuteCommand("ALTER TABLE \"ChannelMessage\" DROP COLUMN \"SearchText\"");
            }

            File.Copy(path, backupPath, overwrite: false);
            ApplyAndVerifySearchMigration(path, services, 8201, "restore @tester");

            File.Copy(backupPath, path, overwrite: true);
            ApplyAndVerifySearchMigration(path, services, 8201, "restore @tester");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task ChatMessageSearchMigration_ShouldSupportPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 Chat 搜索 PostgreSQL 迁移测试");

        var schema = $"chat_search_{Guid.NewGuid():N}";
        using var adminDb = CreatePostgreSqlClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = CreatePostgreSqlClient(connectionString);
            using var services = new ServiceCollection().BuildServiceProvider();
            db.CodeFirst.InitTables<ChannelMessage>();
            var message = CreateMessage(8101, "postgres-search");
            message.Content = "PostgreSQL %_ literal";
            db.Insertable(message).ExecuteCommand();
            db.Ado.ExecuteCommand("DROP INDEX IF EXISTS \"idx_channel_message_channel_search_order\"");
            db.Ado.ExecuteCommand("DROP INDEX IF EXISTS \"idx_channel_message_tenant_search_order\"");
            DropColumn(db, "ChannelMessage", "SearchText");

            var migration = ChatMessageSearchSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.Equal("postgresql %_ literal", db.Queryable<ChannelMessage>().InSingle(8101).SearchText);
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    [Fact]
    public void ChatMessageReactionMigration_ShouldUpgradeLegacyMessagesAndRemainRepeatable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-reaction-migration-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path, "Chat");
        using var services = new ServiceCollection().BuildServiceProvider();

        try
        {
            db.CodeFirst.InitTables<ChannelMessage>();
            db.Insertable(CreateMessage(8601, "reaction-legacy")).ExecuteCommand();
            db.Ado.ExecuteCommand("ALTER TABLE \"ChannelMessage\" DROP COLUMN \"ReactionRevision\"");

            var migration = ChatMessageReactionSchemaMigration.Instance;
            Assert.Contains(
                migration.Diagnose(db, services),
                warning => warning.Contains("1 条历史 Chat 消息", StringComparison.Ordinal));

            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.Equal(0, db.Queryable<ChannelMessage>().InSingle(8601).ReactionRevision);
            Assert.True(db.DbMaintenance.IsAnyTable("ChatMessageReaction", false));
            Assert.True(db.DbMaintenance.IsAnyTable("ChatMessageReactionOperation", false));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_chat_reaction_unique"));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_chat_reaction_operation_unique"));

            db.Insertable(CreateReaction(8701, 8601)).ExecuteCommand();
            Assert.ThrowsAny<Exception>(() =>
                db.Insertable(CreateReaction(8702, 8601)).ExecuteCommand());
            db.Insertable(CreateReactionOperation(8801, "reaction-operation")).ExecuteCommand();
            Assert.ThrowsAny<Exception>(() =>
                db.Insertable(CreateReactionOperation(8802, "reaction-operation")).ExecuteCommand());
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
    [Trait("Database", "PostgreSQL")]
    public async Task ChatMessageReactionMigration_ShouldSupportPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 Chat Reaction PostgreSQL 迁移测试");

        var schema = $"chat_reaction_{Guid.NewGuid():N}";
        using var adminDb = CreatePostgreSqlClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = CreatePostgreSqlClient(connectionString);
            using var services = new ServiceCollection().BuildServiceProvider();
            db.CodeFirst.InitTables<ChannelMessage>();
            db.Insertable(CreateMessage(8901, "reaction-postgres")).ExecuteCommand();
            DropColumn(db, "ChannelMessage", "ReactionRevision");

            var migration = ChatMessageReactionSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.Equal(0, db.Queryable<ChannelMessage>().InSingle(8901).ReactionRevision);
            db.Insertable(CreateReaction(9001, 8901)).ExecuteCommand();
            Assert.ThrowsAny<Exception>(() =>
                db.Insertable(CreateReaction(9002, 8901)).ExecuteCommand());
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    [Fact]
    public void ChatAttachmentPrivacyMigration_ShouldOnlyMakeChatAttachmentsPrivate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-attachment-migration-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path, "Main");
        using var services = new ServiceCollection().BuildServiceProvider();

        try
        {
            db.CodeFirst.InitTables<Attachment>();
            db.Insertable(new[]
            {
                CreateAttachment(101, "Chat"),
                CreateAttachment(102, "Post")
            }).ExecuteCommand();

            var migration = ChatAttachmentPrivacySchemaMigration.Instance;
            Assert.Contains(migration.Diagnose(db, services), warning => warning.Contains("1 个公开 Chat 附件", StringComparison.Ordinal));

            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            var attachments = db.Queryable<Attachment>().OrderBy(attachment => attachment.Id).ToList();
            Assert.False(attachments[0].IsPublic);
            Assert.True(attachments[1].IsPublic);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static Attachment CreateAttachment(long id, string businessType)
    {
        return new Attachment
        {
            Id = id,
            OriginalName = $"{id}.png",
            StoredName = $"{id}.png",
            Extension = ".png",
            FileSize = 4,
            MimeType = "image/png",
            StoragePath = $"{businessType}/{id}.png",
            UploaderId = 20001,
            UploaderName = "Tester",
            BusinessType = businessType,
            IsPublic = true,
            IsEnabled = true,
            TenantId = 0,
            CreateTime = DateTime.UtcNow,
            CreateBy = "Tester",
            CreateId = 20001
        };
    }

    private static void ApplyAndVerifySearchMigration(
        string path,
        IServiceProvider services,
        long messageId,
        string expectedSearchText)
    {
        using var db = CreateClient(path, "Chat");
        var migration = ChatMessageSearchSchemaMigration.Instance;
        migration.Apply(db, services);
        Assert.Empty(migration.Verify(db, services));
        Assert.Equal(expectedSearchText, db.Queryable<ChannelMessage>().InSingle(messageId).SearchText);
    }

    private static DirectConversation CreateDirectConversation(long id, long channelId)
    {
        return new DirectConversation
        {
            Id = id,
            ChannelId = channelId,
            ParticipantLowUserId = 20001,
            ParticipantHighUserId = 20002,
            RequestedByUserId = 20001,
            RequestStatus = DirectConversationRequestStatus.Accepted,
            TenantId = 30000,
            CreateTime = DateTime.UtcNow,
            CreateBy = "Tester",
            CreateId = 20001
        };
    }

    private static ChannelMessage CreateMessage(long id, string clientRequestId)
    {
        return new ChannelMessage
        {
            Id = id,
            ChannelId = 5001,
            UserId = 20001,
            UserName = "Tester",
            ClientRequestId = clientRequestId,
            Type = MessageType.Text,
            Content = "hello",
            TenantId = 30000,
            CreateTime = DateTime.UtcNow
        };
    }

    private static ChatMessageReaction CreateReaction(long id, long messageId)
    {
        return new ChatMessageReaction
        {
            Id = id,
            TenantId = 30000,
            ChannelId = 5001,
            MessageId = messageId,
            UserId = 20001,
            UserName = "Tester",
            EmojiType = "unicode",
            EmojiValue = "👍",
            CreateTime = DateTime.UtcNow,
            CreateBy = "Tester",
            CreateId = 20001
        };
    }

    private static ChatMessageReactionOperation CreateReactionOperation(long id, string operationId)
    {
        return new ChatMessageReactionOperation
        {
            Id = id,
            TenantId = 30000,
            UserId = 20001,
            ClientOperationId = operationId,
            ChannelId = 5001,
            MessageId = 8601,
            EmojiType = "unicode",
            EmojiValue = "👍",
            IsActive = true,
            ResultRevision = 1,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            CreateTime = DateTime.UtcNow,
            CreateBy = "Tester",
            CreateId = 20001
        };
    }

    private static SqlSugarScope CreateClient(string path, string configId)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = configId,
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static SqlSugarClient CreatePostgreSqlClient(string connectionString)
    {
        return PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "Chat",
            ConnectionString = connectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static void DropColumn(ISqlSugarClient db, string tableName, string columnName)
    {
        var column = DatabaseIdentifierResolver.ResolveColumn(db, tableName, columnName)
                     ?? throw new InvalidOperationException($"{tableName}.{columnName} 不存在。");
        db.Ado.ExecuteCommand(
            $"ALTER TABLE {QuoteIdentifier(column.TableName)} DROP COLUMN {QuoteIdentifier(column.ColumnName)}");
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
