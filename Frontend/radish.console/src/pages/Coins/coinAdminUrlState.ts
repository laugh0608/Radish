export const COIN_TRANSACTION_TYPES = [
  'SYSTEM_GRANT',
  'LIKE_REWARD',
  'COMMENT_REWARD',
  'TRANSFER',
  'TIP',
  'CONSUME',
  'REFUND',
  'PENALTY',
  'ADMIN_ADJUST',
] as const;

export const COIN_TRANSACTION_STATUSES = ['PENDING', 'SUCCESS', 'FAILED'] as const;

export interface CoinAdminUrlState {
  userId?: string;
  transactionType?: string;
  status?: string;
  businessType?: string;
  businessId?: string;
  pageIndex: number;
  pageSize: number;
  returnTo?: string;
}

export function createAdminAdjustmentIdempotencyKey(): string {
  const randomPart = typeof globalThis.crypto?.randomUUID === 'function'
    ? globalThis.crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  return `coin-admin-adjust:${randomPart}`;
}

export function normalizePositiveLongIdInput(value: string): string | undefined {
  const trimmed = value.trim();
  return /^[1-9]\d*$/.test(trimmed) ? trimmed : undefined;
}

function normalizeOption(value: string | null, options: readonly string[]): string | undefined {
  return value && options.includes(value) ? value : undefined;
}

function normalizeText(value: string | null): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed.slice(0, 50) : undefined;
}

function normalizePage(value: string | null, fallback: number, maximum?: number): number {
  if (!value || !/^\d+$/.test(value)) {
    return fallback;
  }

  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed) || parsed <= 0 || (maximum !== undefined && parsed > maximum)) {
    return fallback;
  }

  return parsed;
}

export function parseCoinAdminUrlState(searchParams: URLSearchParams): CoinAdminUrlState {
  return {
    userId: normalizePositiveLongIdInput(searchParams.get('userId') ?? ''),
    transactionType: normalizeOption(searchParams.get('transactionType'), COIN_TRANSACTION_TYPES),
    status: normalizeOption(searchParams.get('status'), COIN_TRANSACTION_STATUSES),
    businessType: normalizeText(searchParams.get('businessType')),
    businessId: normalizePositiveLongIdInput(searchParams.get('businessId') ?? ''),
    pageIndex: normalizePage(searchParams.get('pageIndex'), 1),
    pageSize: normalizePage(searchParams.get('pageSize'), 10, 100),
    returnTo: searchParams.get('returnTo')?.trim() || undefined,
  };
}

export function buildCoinAdminSearchParams(state: CoinAdminUrlState): URLSearchParams {
  const searchParams = new URLSearchParams();
  if (state.userId) searchParams.set('userId', state.userId);
  if (state.transactionType) searchParams.set('transactionType', state.transactionType);
  if (state.status) searchParams.set('status', state.status);
  if (state.businessType) searchParams.set('businessType', state.businessType);
  if (state.businessId) searchParams.set('businessId', state.businessId);
  if (state.pageIndex > 1) searchParams.set('pageIndex', String(state.pageIndex));
  if (state.pageSize !== 10) searchParams.set('pageSize', String(state.pageSize));
  if (state.returnTo) searchParams.set('returnTo', state.returnTo);
  return searchParams;
}
