namespace Radish.Model;

/// <summary>单个通知类型的稳定产品与数据契约。</summary>
public sealed record NotificationDefinition(
    string Kind,
    string Category,
    NotificationPriority DefaultPriority,
    string TemplateKey,
    IReadOnlySet<string> AllowedTargetKinds,
    IReadOnlySet<string> RequiredTemplateArguments,
    bool IsProducerActive,
    bool CanDisableInApp = true,
    bool CanDisableRealtimePreview = true,
    TimeSpan? AggregationWindow = null,
    int RetentionDays = 180);

/// <summary>通知定义注册表；任何生产写入都必须先通过这里。</summary>
public static class NotificationDefinitionRegistry
{
    private static readonly IReadOnlyDictionary<string, NotificationDefinition> Definitions =
        CreateDefinitions().ToDictionary(definition => definition.Kind, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [NotificationType.Mentioned] = NotificationType.ChatMentioned,
            ["Mention"] = NotificationType.ChatMentioned,
            ["Follow"] = NotificationType.Followed,
            ["CommentReply"] = NotificationType.CommentReplied,
            ["PURCHASE_SUCCESS"] = NotificationType.PurchaseSucceeded,
            ["BENEFIT_EXPIRED"] = NotificationType.BenefitExpired
        };

    public static IReadOnlyCollection<NotificationDefinition> All { get; } = Definitions.Values.ToArray();

    public static IReadOnlyCollection<string> ActiveCategories => Definitions.Values
        .Where(definition => definition.IsProducerActive)
        .Select(definition => definition.Category)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(category => Array.IndexOf(NotificationCategory.All.ToArray(), category))
        .ToArray();

    public static NotificationDefinition GetRequired(string kind)
    {
        var canonicalKind = NormalizeKind(kind);
        if (!Definitions.TryGetValue(canonicalKind, out var definition))
        {
            throw new ArgumentException($"未登记的通知类型：{kind}", nameof(kind));
        }

        return definition;
    }

    public static bool TryGet(string? kind, out NotificationDefinition? definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        return Definitions.TryGetValue(NormalizeKind(kind), out definition);
    }

    public static string NormalizeKind(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("通知类型不能为空。", nameof(kind));
        }

        var normalized = kind.Trim();
        return Aliases.TryGetValue(normalized, out var canonical) ? canonical : normalized;
    }

    public static string ResolveHistoricalCategory(string? kind)
    {
        return TryGet(kind, out var definition)
            ? definition!.Category
            : NotificationCategory.System;
    }

    private static IEnumerable<NotificationDefinition> CreateDefinitions()
    {
        yield return Define(NotificationType.CommentReplied, NotificationCategory.Discussion,
            NotificationPriority.Normal, NotificationTargetKind.ForumPost, active: true,
            requiredTemplateArguments: ["actorName"]);
        yield return Define(NotificationType.PostCommented, NotificationCategory.Discussion,
            NotificationPriority.Normal, NotificationTargetKind.ForumPost, active: true,
            requiredTemplateArguments: ["actorName"]);
        yield return Define(NotificationType.PostQuickReplied, NotificationCategory.Discussion,
            NotificationPriority.Normal, NotificationTargetKind.ForumPost, active: true,
            aggregationWindow: TimeSpan.FromHours(2), requiredTemplateArguments: ["targetTitle"]);
        yield return Define(NotificationType.PostLiked, NotificationCategory.Reaction,
            NotificationPriority.Low, NotificationTargetKind.ForumPost, active: true,
            aggregationWindow: TimeSpan.FromHours(6), requiredTemplateArguments: ["targetTitle"]);
        yield return Define(NotificationType.CommentLiked, NotificationCategory.Reaction,
            NotificationPriority.Low, NotificationTargetKind.ForumPost, active: true,
            aggregationWindow: TimeSpan.FromHours(6));
        yield return Define(NotificationType.ChatMentioned, NotificationCategory.Message,
            NotificationPriority.High, NotificationTargetKind.ChatConversation, active: true,
            requiredTemplateArguments: ["actorName", "channelName"]);
        yield return Define(NotificationType.DirectMessageRequested, NotificationCategory.Message,
            NotificationPriority.Normal, NotificationTargetKind.ChatConversation, active: true,
            requiredTemplateArguments: ["actorName"]);
        yield return Define(NotificationType.Followed, NotificationCategory.Relationship,
            NotificationPriority.Normal, NotificationTargetKind.UserProfile, active: true,
            requiredTemplateArguments: ["actorName"]);
        yield return Define(NotificationType.WikiCollaboratorInvited, NotificationCategory.Knowledge,
            NotificationPriority.Normal, NotificationTargetKind.DocsAuthorDraft, active: true,
            requiredTemplateArguments: ["actorName", "targetTitle"]);
        yield return Define(NotificationType.WikiReviewUpdated, NotificationCategory.Knowledge,
            NotificationPriority.Normal, NotificationTargetKind.DocsAuthorDraft, active: true,
            requiredTemplateArguments: ["targetTitle", "reviewAction"]);
        yield return Define(NotificationType.PurchaseSucceeded, NotificationCategory.Commerce,
            NotificationPriority.Normal, NotificationTargetKind.ShopOrder, active: true, retentionDays: 365,
            requiredTemplateArguments: ["productName"]);
        yield return Define(NotificationType.BenefitExpired, NotificationCategory.Commerce,
            NotificationPriority.Normal, NotificationTargetKind.Inventory, active: true, retentionDays: 365,
            requiredTemplateArguments: ["benefitName"]);
        yield return Define(NotificationType.LevelUp, NotificationCategory.Growth,
            NotificationPriority.High, NotificationTargetKind.Experience, active: true,
            requiredTemplateArguments: ["oldLevel", "newLevel"]);
        yield return Define(NotificationType.LotteryWon, NotificationCategory.Growth,
            NotificationPriority.High, NotificationTargetKind.ForumPost, active: true,
            requiredTemplateArguments: ["targetTitle", "prizeName"]);
        yield return Define(NotificationType.GodComment, NotificationCategory.Growth,
            NotificationPriority.High, NotificationTargetKind.ForumPost, active: false);
        yield return Define(NotificationType.Sofa, NotificationCategory.Growth,
            NotificationPriority.High, NotificationTargetKind.ForumPost, active: false);
        yield return Define(NotificationType.CoinBalanceChanged, NotificationCategory.Growth,
            NotificationPriority.Normal, NotificationTargetKind.Experience, active: false);
        yield return Define(NotificationType.SystemAnnouncement, NotificationCategory.System,
            NotificationPriority.Critical, NotificationTargetKind.None, active: false, retentionDays: 365);
        yield return new NotificationDefinition(
            NotificationType.AccountSecurity,
            NotificationCategory.System,
            NotificationPriority.Critical,
            GetTemplateKey(NotificationType.AccountSecurity),
            new HashSet<string>(StringComparer.Ordinal) { NotificationTargetKind.None },
            new HashSet<string>(StringComparer.Ordinal),
            IsProducerActive: false,
            CanDisableInApp: false,
            CanDisableRealtimePreview: true,
            AggregationWindow: null,
            RetentionDays: 730);
    }

    private static NotificationDefinition Define(
        string kind,
        string category,
        NotificationPriority priority,
        string targetKind,
        bool active,
        TimeSpan? aggregationWindow = null,
        int retentionDays = 180,
        params string[] requiredTemplateArguments)
    {
        return new NotificationDefinition(
            kind,
            category,
            priority,
            GetTemplateKey(kind),
            new HashSet<string>(StringComparer.Ordinal) { targetKind },
            new HashSet<string>(requiredTemplateArguments, StringComparer.Ordinal),
            active,
            AggregationWindow: aggregationWindow,
            RetentionDays: retentionDays);
    }

    private static string GetTemplateKey(string kind) => $"notification.{kind}";
}
