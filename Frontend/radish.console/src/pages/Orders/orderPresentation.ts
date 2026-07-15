import type { TFunction } from 'i18next';
import type { Order, UserBenefit } from '@/api/types';

type OrderStatusName = 'Pending' | 'Paid' | 'Completed' | 'Cancelled' | 'Refunded' | 'Failed' | 'Unknown';

const ORDER_STATUS_BY_VALUE: Record<string, OrderStatusName> = {
  '0': 'Pending',
  '1': 'Paid',
  '2': 'Completed',
  '3': 'Cancelled',
  '4': 'Refunded',
  '5': 'Failed',
  pending: 'Pending',
  paid: 'Paid',
  completed: 'Completed',
  cancelled: 'Cancelled',
  refunded: 'Refunded',
  failed: 'Failed',
};

export function normalizeOrderStatus(value: unknown): OrderStatusName {
  return ORDER_STATUS_BY_VALUE[String(value ?? '').trim().toLowerCase()] ?? 'Unknown';
}

export function isOrderInStatus(order: Order, status: Exclude<OrderStatusName, 'Unknown'>): boolean {
  return normalizeOrderStatus(order.voStatus) === status;
}

export function getOrderStatusColor(value: unknown): string {
  switch (normalizeOrderStatus(value)) {
    case 'Pending':
      return '#faad14';
    case 'Paid':
    case 'Completed':
      return '#52c41a';
    case 'Cancelled':
    case 'Failed':
      return '#ff4d4f';
    case 'Refunded':
      return '#722ed1';
    default:
      return '#d9d9d9';
  }
}

function isPaymentFailureStage(value: unknown): boolean {
  const normalized = String(value ?? '').trim().toLowerCase();
  return normalized === '1' || normalized === 'payment';
}

export function getOrderStatusLabel(order: Pick<Order, 'voStatus' | 'voFailureStage'>, t: TFunction): string {
  switch (normalizeOrderStatus(order.voStatus)) {
    case 'Pending':
      return t('orders.status.pending');
    case 'Paid':
      return t('orders.status.paid');
    case 'Completed':
      return t('orders.status.completed');
    case 'Cancelled':
      return t('orders.status.cancelled');
    case 'Refunded':
      return t('orders.status.refunded');
    case 'Failed':
      return t(isPaymentFailureStage(order.voFailureStage)
        ? 'orders.status.paymentFailed'
        : 'orders.status.fulfillmentFailed');
    default:
      return t('orders.status.unknown');
  }
}

export function getOrderFailureStageLabel(value: unknown, t: TFunction): string {
  const normalized = String(value ?? '').trim().toLowerCase();
  if (normalized === '0' || normalized === 'none') {
    return t('orders.failure.none');
  }
  if (normalized === '1' || normalized === 'payment') {
    return t('orders.failure.payment');
  }
  if (normalized === '2' || normalized === 'fulfillment') {
    return t('orders.failure.fulfillment');
  }
  return t('orders.failure.unknown');
}

export function getOrderProductTypeLabel(value: unknown, t: TFunction): string {
  const normalized = String(value ?? '').trim().toLowerCase();
  if (normalized === '1' || normalized === 'benefit') {
    return t('orders.productType.benefit');
  }
  if (normalized === '2' || normalized === 'consumable') {
    return t('orders.productType.consumable');
  }
  if (normalized === '99' || normalized === 'physical') {
    return t('orders.productType.physical');
  }
  return t('orders.productType.unknown');
}

export function getOrderDurationLabel(
  order: Pick<Order, 'voDurationType' | 'voDurationDays' | 'voFixedExpiresAt'>,
  t: TFunction,
  formatTime: (value: string) => string,
): string {
  const normalized = String(order.voDurationType ?? '').trim().toLowerCase();
  if (normalized === '0' || normalized === 'permanent') {
    return t('orders.duration.permanent');
  }
  if (normalized === '1' || normalized === 'days') {
    return typeof order.voDurationDays === 'number'
      ? t('orders.duration.days', { count: order.voDurationDays })
      : t('orders.duration.unknown');
  }
  if (normalized === '2' || normalized === 'fixeddate') {
    return order.voFixedExpiresAt
      ? t('orders.duration.until', { time: formatTime(order.voFixedExpiresAt) })
      : t('orders.duration.unknown');
  }
  return t('orders.duration.unknown');
}

export function getCoinTransactionTypeLabel(value: unknown, t: TFunction): string {
  switch (String(value ?? '').trim().toUpperCase()) {
    case 'TRANSFER':
      return t('users.detail.coinType.transfer');
    case 'CONSUME':
      return t('users.detail.coinType.consume');
    case 'REWARD':
      return t('users.detail.coinType.reward');
    case 'REFUND':
      return t('users.detail.coinType.refund');
    case 'ADJUST':
      return t('users.detail.coinType.adjust');
    default:
      return t('users.detail.coinType.unknown');
  }
}

export function getBenefitStatusLabel(value: unknown, t: TFunction): string {
  const normalized = String(value ?? '').trim().toLowerCase();
  if (normalized === '0' || normalized === 'available') {
    return t('users.detail.benefitStatus.available');
  }
  if (normalized === '1' || normalized === 'active') {
    return t('users.detail.benefitStatus.active');
  }
  if (normalized === '2' || normalized === 'expired') {
    return t('users.detail.benefitStatus.expired');
  }
  if (normalized === '3' || normalized === 'revoked') {
    return t('users.detail.benefitStatus.revoked');
  }
  return t('users.detail.benefitStatus.unknown');
}

export function getBenefitSourceLabel(value: unknown, t: TFunction): string {
  switch (String(value ?? '').trim().toLowerCase()) {
    case 'purchase':
      return t('users.detail.benefitSource.purchase');
    case 'system':
      return t('users.detail.benefitSource.system');
    case 'activity':
      return t('users.detail.benefitSource.activity');
    case 'gift':
      return t('users.detail.benefitSource.gift');
    default:
      return t('users.detail.benefitSource.unknown');
  }
}

export function getBenefitTypeLabel(value: unknown, t: TFunction): string {
  const normalized = String(value ?? '').trim().toLowerCase();
  const keys: Record<string, string> = {
    '1': 'users.detail.benefitType.badge',
    badge: 'users.detail.benefitType.badge',
    '2': 'users.detail.benefitType.avatarFrame',
    avatarframe: 'users.detail.benefitType.avatarFrame',
    '3': 'users.detail.benefitType.title',
    title: 'users.detail.benefitType.title',
    '4': 'users.detail.benefitType.theme',
    theme: 'users.detail.benefitType.theme',
    '5': 'users.detail.benefitType.signature',
    signature: 'users.detail.benefitType.signature',
    '6': 'users.detail.benefitType.nameColor',
    namecolor: 'users.detail.benefitType.nameColor',
    '7': 'users.detail.benefitType.likeEffect',
    likeeffect: 'users.detail.benefitType.likeEffect',
  };
  return t(keys[normalized] ?? 'users.detail.benefitType.unknown');
}

export function getConsumableTypeLabel(value: unknown, t: TFunction): string {
  const normalized = String(value ?? '').trim().toLowerCase();
  const keys: Record<string, string> = {
    '1': 'users.detail.consumableType.renameCard',
    renamecard: 'users.detail.consumableType.renameCard',
    '4': 'users.detail.consumableType.expCard',
    expcard: 'users.detail.consumableType.expCard',
    '5': 'users.detail.consumableType.coinCard',
    coincard: 'users.detail.consumableType.coinCard',
    '6': 'users.detail.consumableType.doubleExpCard',
    doubleexpcard: 'users.detail.consumableType.doubleExpCard',
    '7': 'users.detail.consumableType.lotteryTicket',
    '99': 'users.detail.consumableType.lotteryTicket',
    lotteryticket: 'users.detail.consumableType.lotteryTicket',
  };
  return t(keys[normalized] ?? 'users.detail.consumableType.unknown');
}

export function getBenefitDurationLabel(
  benefit: Pick<UserBenefit, 'voExpiresAt'>,
  t: TFunction,
  formatTime: (value: string) => string,
): string {
  return benefit.voExpiresAt
    ? t('users.detail.duration.until', { time: formatTime(benefit.voExpiresAt) })
    : t('users.detail.duration.permanent');
}
