import type { OrderStatus } from '../../api/types';
import { normalizeConsoleReturnTo } from '../../utils/returnTo.ts';

export { normalizeConsoleReturnTo };

export const DEFAULT_ORDER_PAGE_INDEX = 1;
export const DEFAULT_ORDER_PAGE_SIZE = 20;

export function parsePositiveIntQuery(value: string | null): number | undefined {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined;
}

export function parseLongIdQuery(value: string | null): string | undefined {
  if (!value) {
    return undefined;
  }

  const trimmed = value.trim();
  return /^[1-9]\d*$/u.test(trimmed) ? trimmed : undefined;
}

export function parseOrderStatusQuery(value: string | null): OrderStatus | undefined {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= 0 && parsed <= 5
    ? parsed as OrderStatus
    : undefined;
}

export function parseBooleanQuery(value: string | null): boolean {
  return value === '1' || value === 'true';
}

export function buildOrderSearchParams(params: {
  orderId?: string;
  userId?: string;
  status?: OrderStatus;
  productId?: string;
  orderNo?: string;
  pageIndex?: number;
  pageSize?: number;
  openDetail?: boolean;
  returnTo?: string | null;
}): URLSearchParams {
  const searchParams = new URLSearchParams();
  const normalizedOrderNo = params.orderNo?.trim() ?? '';
  const normalizedReturnTo = normalizeConsoleReturnTo(params.returnTo);

  if (params.orderId !== undefined) {
    searchParams.set('orderId', params.orderId.toString());
  }

  if (params.userId !== undefined) {
    searchParams.set('userId', params.userId.toString());
  }

  if (params.status !== undefined) {
    searchParams.set('status', params.status.toString());
  }

  if (params.productId !== undefined) {
    searchParams.set('productId', params.productId.toString());
  }

  if (normalizedOrderNo) {
    searchParams.set('orderNo', normalizedOrderNo);
  }

  if ((params.pageIndex ?? DEFAULT_ORDER_PAGE_INDEX) !== DEFAULT_ORDER_PAGE_INDEX) {
    searchParams.set('pageIndex', String(params.pageIndex));
  }

  if ((params.pageSize ?? DEFAULT_ORDER_PAGE_SIZE) !== DEFAULT_ORDER_PAGE_SIZE) {
    searchParams.set('pageSize', String(params.pageSize));
  }

  if (params.openDetail) {
    searchParams.set('openDetail', '1');
  }

  if (normalizedReturnTo) {
    searchParams.set('returnTo', normalizedReturnTo);
  }

  return searchParams;
}
