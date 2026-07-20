import type {
  NotificationCategory,
  NotificationInboxGroupVo,
  NotificationInboxSummaryVo,
  NotificationPreferenceVo,
  UpdateNotificationPreferenceDto,
} from '@radish/http';

export interface NotificationCategoryDefinition {
  category: NotificationCategory;
  labelKey: string;
  descriptionKey: string;
  icon: string;
}

export const notificationCategoryDefinitions: readonly NotificationCategoryDefinition[] = [
  { category: 'Discussion', labelKey: 'notification.category.discussion', descriptionKey: 'notification.category.discussionDescription', icon: 'mdi:comment-text-outline' },
  { category: 'Reaction', labelKey: 'notification.category.reaction', descriptionKey: 'notification.category.reactionDescription', icon: 'mdi:heart-outline' },
  { category: 'Message', labelKey: 'notification.category.message', descriptionKey: 'notification.category.messageDescription', icon: 'mdi:message-text-outline' },
  { category: 'Relationship', labelKey: 'notification.category.relationship', descriptionKey: 'notification.category.relationshipDescription', icon: 'mdi:account-plus-outline' },
  { category: 'Commerce', labelKey: 'notification.category.commerce', descriptionKey: 'notification.category.commerceDescription', icon: 'mdi:receipt-text-outline' },
  { category: 'Growth', labelKey: 'notification.category.growth', descriptionKey: 'notification.category.growthDescription', icon: 'mdi:sprout-outline' },
  { category: 'Governance', labelKey: 'notification.category.governance', descriptionKey: 'notification.category.governanceDescription', icon: 'mdi:shield-check-outline' },
  { category: 'Knowledge', labelKey: 'notification.category.knowledge', descriptionKey: 'notification.category.knowledgeDescription', icon: 'mdi:file-document-outline' },
  { category: 'System', labelKey: 'notification.category.system', descriptionKey: 'notification.category.systemDescription', icon: 'mdi:bell-outline' },
] as const;

const categoryDefinitionMap = new Map(
  notificationCategoryDefinitions.map((definition) => [definition.category, definition]),
);

export function getNotificationCategoryDefinition(
  category: NotificationCategory,
): NotificationCategoryDefinition {
  return categoryDefinitionMap.get(category) ?? notificationCategoryDefinitions.at(-1)!;
}

export function normalizeRevision(value: string | null | undefined): string {
  const normalized = value?.trim() ?? '';
  if (!/^\d+$/u.test(normalized)) {
    return '0';
  }

  return normalized.replace(/^0+(?=\d)/u, '');
}

export function compareNotificationRevisions(
  left: string | null | undefined,
  right: string | null | undefined,
): number {
  const normalizedLeft = normalizeRevision(left);
  const normalizedRight = normalizeRevision(right);
  if (normalizedLeft.length !== normalizedRight.length) {
    return normalizedLeft.length > normalizedRight.length ? 1 : -1;
  }

  return normalizedLeft === normalizedRight ? 0 : normalizedLeft > normalizedRight ? 1 : -1;
}

export function canApplyNotificationSummary(
  incomingRevision: string,
  currentRevision: string | null | undefined,
): boolean {
  return currentRevision == null
    || compareNotificationRevisions(incomingRevision, currentRevision) >= 0;
}

export function canApplyNotificationInboxPage(
  incomingRevision: string,
  currentSummaryRevision: string | null | undefined,
  currentListRevision: string | null,
  append: boolean,
): boolean {
  if (!canApplyNotificationSummary(incomingRevision, currentSummaryRevision)) {
    return false;
  }

  return !append
    || currentListRevision === null
    || compareNotificationRevisions(incomingRevision, currentListRevision) === 0;
}

export function parseNotificationCount(value: string | null | undefined): number {
  const normalized = normalizeRevision(value);
  const parsed = Number(normalized);
  return Number.isSafeInteger(parsed) && parsed >= 0 ? parsed : Number.MAX_SAFE_INTEGER;
}

export function getUnreadGroupCount(
  summary: NotificationInboxSummaryVo | null | undefined,
): number {
  return parseNotificationCount(summary?.voUnreadGroupCount);
}

export function getUnreadOccurrenceCount(
  summary: NotificationInboxSummaryVo | null | undefined,
): number {
  return parseNotificationCount(summary?.voUnreadOccurrenceCount);
}

export function getUnreadCategoryCount(
  summary: NotificationInboxSummaryVo | null | undefined,
  category: NotificationCategory,
): number {
  return parseNotificationCount(summary?.voUnreadGroupCountByCategory[category]);
}

export function mergeNotificationGroups(
  current: NotificationInboxGroupVo[],
  incoming: NotificationInboxGroupVo[],
): NotificationInboxGroupVo[] {
  const result = current.slice();
  const indexById = new Map(result.map((group, index) => [group.voGroupId, index]));

  for (const group of incoming) {
    const existingIndex = indexById.get(group.voGroupId);
    if (existingIndex === undefined) {
      indexById.set(group.voGroupId, result.length);
      result.push(group);
    } else {
      result[existingIndex] = group;
    }
  }

  return result;
}

export function buildNotificationPreferenceUpdates(
  preferences: NotificationPreferenceVo[],
): UpdateNotificationPreferenceDto[] {
  return preferences.map((preference) => ({
    category: preference.voCategory,
    inAppEnabled: preference.voInAppEnabled,
    realtimePreviewEnabled: preference.voRealtimePreviewEnabled,
  }));
}

export function isNotificationCursorExpiredError(error: unknown): boolean {
  if (!error || typeof error !== 'object') {
    return false;
  }

  const candidate = error as { code?: unknown; messageKey?: unknown };
  const code = typeof candidate.code === 'string' ? candidate.code.trim().toLowerCase() : '';
  const messageKey = typeof candidate.messageKey === 'string'
    ? candidate.messageKey.trim().toLowerCase()
    : '';
  return code === 'notification.cursorexpired'
    || messageKey === 'error.notification.cursor_expired';
}

export function getNotificationTargetUnavailableKey(group: NotificationInboxGroupVo): string {
  const reason = group.voTarget.voUnavailableReason?.trim().toLowerCase() ?? '';
  if (reason.includes('invalid')) {
    return 'notification.targetUnavailable.invalid';
  }
  if (reason.includes('deleted')) {
    return 'notification.targetUnavailable.deleted';
  }
  if (reason.includes('forbidden') || reason.includes('denied')) {
    return 'notification.targetUnavailable.forbidden';
  }

  return 'notification.targetUnavailable.default';
}
