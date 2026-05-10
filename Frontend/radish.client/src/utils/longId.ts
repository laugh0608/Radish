import type { LongId } from '@/api/user';

export function normalizePositiveLongIdKey(value: unknown): string | null {
  if (typeof value === 'number') {
    return Number.isSafeInteger(value) && value > 0 ? String(value) : null;
  }

  if (typeof value !== 'string') {
    return null;
  }

  const trimmed = value.trim();
  return /^[1-9]\d*$/.test(trimmed) ? trimmed : null;
}

export function isSameLongId(
  left: LongId | null | undefined,
  right: LongId | null | undefined
): boolean {
  const leftKey = normalizePositiveLongIdKey(left);
  const rightKey = normalizePositiveLongIdKey(right);
  return leftKey !== null && rightKey !== null && leftKey === rightKey;
}
