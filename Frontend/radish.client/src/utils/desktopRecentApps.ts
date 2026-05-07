export interface RecentDesktopAppItem {
  appId: string;
  openedAt: number;
  appParams?: RecentDesktopAppParams;
}

const RECENT_DESKTOP_APPS_STORAGE_KEY = 'radish.desktop.recentApps.v1';
export const RECENT_DESKTOP_APPS_CHANGED_EVENT = 'radish:desktop-recent-apps-changed';

const MAX_RECENT_DESKTOP_APPS = 5;
const EXCLUDED_RECENT_APP_IDS = new Set(['welcome', 'showcase', 'console', 'scalar']);

export type RecentDesktopAppParamValue =
  | string
  | number
  | boolean
  | null
  | RecentDesktopAppParamValue[]
  | { [key: string]: RecentDesktopAppParamValue };

export type RecentDesktopAppParams = Record<string, RecentDesktopAppParamValue>;

function resolveStorage(storage?: Storage | null): Storage | null {
  if (storage) {
    return storage;
  }

  if (typeof window === 'undefined') {
    return null;
  }

  return window.localStorage;
}

function normalizeRecentDesktopAppParamValue(value: unknown): RecentDesktopAppParamValue | undefined {
  if (value === undefined) {
    return undefined;
  }

  if (value === null) {
    return null;
  }

  if (typeof value === 'string' || typeof value === 'boolean') {
    return value;
  }

  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : undefined;
  }

  if (Array.isArray(value)) {
    return value
      .map((item) => normalizeRecentDesktopAppParamValue(item))
      .filter((item): item is RecentDesktopAppParamValue => item !== undefined);
  }

  if (typeof value === 'object') {
    const normalizedEntries = Object.entries(value as Record<string, unknown>)
      .filter(([key]) => key.trim() && !key.startsWith('__'))
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, entryValue]) => [key, normalizeRecentDesktopAppParamValue(entryValue)] as const)
      .filter(([, entryValue]) => entryValue !== undefined);

    if (normalizedEntries.length === 0) {
      return undefined;
    }

    return Object.fromEntries(normalizedEntries) as RecentDesktopAppParams;
  }

  return undefined;
}

function normalizeRecentDesktopAppParams(value: unknown): RecentDesktopAppParams | undefined {
  const normalized = normalizeRecentDesktopAppParamValue(value);

  if (!normalized || Array.isArray(normalized) || typeof normalized !== 'object') {
    return undefined;
  }

  return Object.keys(normalized).length > 0 ? (normalized as RecentDesktopAppParams) : undefined;
}

function normalizeRecentDesktopAppItem(value: unknown): RecentDesktopAppItem | null {
  if (!value || typeof value !== 'object') {
    return null;
  }

  const raw = value as Partial<RecentDesktopAppItem>;
  const appId = typeof raw.appId === 'string' ? raw.appId.trim() : '';
  const openedAt = typeof raw.openedAt === 'number' && Number.isFinite(raw.openedAt)
    ? raw.openedAt
    : 0;

  if (!appId || openedAt <= 0) {
    return null;
  }

  const appParams = normalizeRecentDesktopAppParams(raw.appParams);
  return appParams
    ? { appId, openedAt, appParams }
    : { appId, openedAt };
}

function emitRecentDesktopAppsChanged(): void {
  if (typeof window === 'undefined') {
    return;
  }

  window.dispatchEvent(new Event(RECENT_DESKTOP_APPS_CHANGED_EVENT));
}

export function isTrackableDesktopApp(appId: string): boolean {
  return !EXCLUDED_RECENT_APP_IDS.has(appId);
}

export function readRecentDesktopApps(storage?: Storage | null): RecentDesktopAppItem[] {
  const targetStorage = resolveStorage(storage);
  if (!targetStorage) {
    return [];
  }

  try {
    const raw = targetStorage.getItem(RECENT_DESKTOP_APPS_STORAGE_KEY);
    if (!raw) {
      return [];
    }

    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed
      .map(normalizeRecentDesktopAppItem)
      .filter((item): item is RecentDesktopAppItem => item !== null)
      .filter((item) => isTrackableDesktopApp(item.appId))
      .sort((left, right) => right.openedAt - left.openedAt)
      .slice(0, MAX_RECENT_DESKTOP_APPS);
  } catch {
    return [];
  }
}

export function findRecentDesktopApp(
  appId: string,
  storage?: Storage | null
): RecentDesktopAppItem | undefined {
  const normalizedAppId = appId.trim();
  if (!normalizedAppId) {
    return undefined;
  }

  return readRecentDesktopApps(storage).find((item) => item.appId === normalizedAppId);
}

export function recordRecentDesktopApp(
  appId: string,
  options: { storage?: Storage | null; now?: number; appParams?: Record<string, unknown> } = {}
): RecentDesktopAppItem[] {
  const normalizedAppId = appId.trim();
  const targetStorage = resolveStorage(options.storage);
  if (!targetStorage || !isTrackableDesktopApp(normalizedAppId)) {
    return readRecentDesktopApps(targetStorage);
  }

  const now = typeof options.now === 'number' && Number.isFinite(options.now)
    ? options.now
    : Date.now();
  const appParams = normalizeRecentDesktopAppParams(options.appParams);
  const nextItem = appParams
    ? { appId: normalizedAppId, openedAt: now, appParams }
    : { appId: normalizedAppId, openedAt: now };
  const nextItems = [
    nextItem,
    ...readRecentDesktopApps(targetStorage).filter((item) => item.appId !== normalizedAppId),
  ].slice(0, MAX_RECENT_DESKTOP_APPS);

  try {
    targetStorage.setItem(RECENT_DESKTOP_APPS_STORAGE_KEY, JSON.stringify(nextItems));
    emitRecentDesktopAppsChanged();
  } catch {
    // Ignore storage write failures; recent apps are a non-critical local enhancement.
  }

  return nextItems;
}
