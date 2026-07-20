using SqlSugar;

namespace Radish.DbMigrate;

internal interface ISchemaMigration
{
    string MigrationId { get; }

    string Scope { get; }

    string Description { get; }

    string ChecksumSource { get; }

    void Apply(ISqlSugarClient db, IServiceProvider services);

    IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services);

    /// <summary>
    /// 在迁移应用前诊断历史数据。实现必须兼容该迁移尚未补齐的新表或新列。
    /// </summary>
    IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services) => [];
}

internal static class SchemaMigrationRegistry
{
    public static IReadOnlyList<ISchemaMigration> All { get; } =
    [
        ExperienceNaturalDateSchemaMigration.Instance,
        ShopOrderFulfillmentSafetyMigration.Instance,
        ShopEntitlementOperationSchemaMigration.Instance,
        UserActiveBenefitSchemaMigration.Instance,
        ShopEntitlementOperationSubjectNullabilityMigration.Instance,
        ChatDirectConversationSchemaMigration.Instance,
        ChatAttachmentPrivacySchemaMigration.Instance,
        ChatReliableMessageSchemaMigration.Instance,
        ChatMessageSearchSchemaMigration.Instance,
        ChatMessageReactionSchemaMigration.Instance,
        ChatMessagePinSchemaMigration.Instance,
        ChatReadReceiptSchemaMigration.Instance,
        WikiAuthorCollaborationSchemaMigration.Instance,
        NotificationInboxSchemaMigration.Instance,
        NotificationDeliveryCleanupSchemaMigration.Instance
    ];
}
