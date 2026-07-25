import type { UserInteractionChangedVo } from '@radish/http';
import { useUserStore } from '@/stores/userStore';
import { log } from '@/utils/logger';

type UserInteractionAction = 'block' | 'unblock';
type UserInteractionListener = (change: UserInteractionChangedVo) => void;

interface UserInteractionBroadcast {
  accountKey: string;
  relationshipVersion: string;
}

const OPERATION_STORAGE_PREFIX = 'radish:user-interaction:operation';
const BROADCAST_CHANNEL_NAME = 'radish:user-interaction';
const STORAGE_EVENT_KEY = 'radish:user-interaction:revision';
const listeners = new Set<UserInteractionListener>();
const memoryOperationKeys = new Map<string, string>();
const latestVersionByAccount = new Map<string, string>();
let channel: BroadcastChannel | null = null;
let browserListenersReady = false;

function getAccountKey(): string {
  return useUserStore.getState().userId.trim();
}

function createOperationKey(action: UserInteractionAction, targetUserPublicId: string): string {
  const nonce = globalThis.crypto?.randomUUID?.()
    ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
  return `user-block:${action}:${targetUserPublicId}:${nonce}`;
}

function buildOperationStorageKey(
  accountKey: string,
  action: UserInteractionAction,
  targetUserPublicId: string,
): string {
  return `${OPERATION_STORAGE_PREFIX}:${accountKey}:${action}:${targetUserPublicId}`;
}

function normalizeVersion(value: string): string {
  const normalized = value.trim().replace(/^0+(?=\d)/, '');
  return /^\d+$/.test(normalized) ? normalized : '';
}

function isNewerVersion(current: string | undefined, incoming: string): boolean {
  if (!current) {
    return true;
  }
  return incoming.length !== current.length
    ? incoming.length > current.length
    : incoming > current;
}

function emit(change: UserInteractionChangedVo): void {
  const accountKey = getAccountKey();
  const incoming = normalizeVersion(change.voRelationshipVersion);
  if (!incoming || !isNewerVersion(latestVersionByAccount.get(accountKey), incoming)) {
    return;
  }

  latestVersionByAccount.set(accountKey, incoming);
  listeners.forEach((listener) => listener(change));
}

function isBroadcast(value: unknown): value is UserInteractionBroadcast {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as Partial<UserInteractionBroadcast>;
  return typeof candidate.accountKey === 'string'
    && typeof candidate.relationshipVersion === 'string'
    && candidate.relationshipVersion.trim().length > 0;
}

function acceptBroadcast(value: unknown): void {
  if (!isBroadcast(value) || value.accountKey !== getAccountKey()) {
    return;
  }

  emit({ voRelationshipVersion: value.relationshipVersion });
}

function ensureBrowserListeners(): void {
  if (browserListenersReady || typeof window === 'undefined') {
    return;
  }

  browserListenersReady = true;
  if ('BroadcastChannel' in window) {
    channel = new BroadcastChannel(BROADCAST_CHANNEL_NAME);
    channel.addEventListener('message', (event) => acceptBroadcast(event.data));
  }

  window.addEventListener('storage', (event) => {
    if (event.key !== STORAGE_EVENT_KEY || !event.newValue) {
      return;
    }

    try {
      acceptBroadcast(JSON.parse(event.newValue));
    } catch (error) {
      log.warn('userInteractionSync', '忽略无法解析的跨标签页关系版本', error);
    }
  });
}

export function getStableUserInteractionOperationKey(
  action: UserInteractionAction,
  targetUserPublicId: string,
): string {
  const accountKey = getAccountKey();
  const storageKey = buildOperationStorageKey(accountKey, action, targetUserPublicId);

  try {
    const stored = window.localStorage.getItem(storageKey)?.trim();
    if (stored) {
      return stored;
    }
  } catch {
    const stored = memoryOperationKeys.get(storageKey);
    if (stored) {
      return stored;
    }
  }

  const operationKey = createOperationKey(action, targetUserPublicId);
  try {
    window.localStorage.setItem(storageKey, operationKey);
  } catch {
    memoryOperationKeys.set(storageKey, operationKey);
  }
  return operationKey;
}

export function completeUserInteractionOperation(
  action: UserInteractionAction,
  targetUserPublicId: string,
): void {
  const storageKey = buildOperationStorageKey(getAccountKey(), action, targetUserPublicId);
  memoryOperationKeys.delete(storageKey);
  try {
    window.localStorage.removeItem(storageKey);
  } catch {
    // 内存副本已经清理；存储不可用时无需额外兜底。
  }
}

export function publishUserInteractionChanged(change: UserInteractionChangedVo): void {
  ensureBrowserListeners();
  emit(change);

  const payload: UserInteractionBroadcast = {
    accountKey: getAccountKey(),
    relationshipVersion: change.voRelationshipVersion,
  };
  if (channel) {
    channel.postMessage(payload);
  } else {
    try {
      window.localStorage.setItem(STORAGE_EVENT_KEY, JSON.stringify(payload));
    } catch {
      // 当前标签页已经完成失效通知，跨标签页通道不可用时等待返回/刷新校准。
    }
  }
}

export function handleRealtimeUserInteractionChanged(change: UserInteractionChangedVo): void {
  ensureBrowserListeners();
  emit(change);
}

export function subscribeUserInteractionChanged(listener: UserInteractionListener): () => void {
  ensureBrowserListeners();
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
