import type { LongId } from '@/api/user';

export const SHOP_ORDERS_PATH = '/shop/orders';
export const SHOP_INVENTORY_PATH = '/shop/inventory';

export interface ShopOrdersRoute {
  kind: 'orders';
}

export interface ShopOrderDetailRoute {
  kind: 'order-detail';
  orderId: LongId;
}

export interface ShopInventoryRoute {
  kind: 'inventory';
}

export type ShopRoute =
  | ShopOrdersRoute
  | ShopOrderDetailRoute
  | ShopInventoryRoute;

export function createDefaultShopRoute(): ShopOrdersRoute {
  return {
    kind: 'orders'
  };
}

function normalizePositiveLongId(value: string | undefined): LongId | undefined {
  const normalized = value?.trim();
  return normalized && /^[1-9]\d*$/.test(normalized) ? normalized : undefined;
}

export function parseShopRoute(pathname: string): ShopRoute | null {
  if (pathname === SHOP_ORDERS_PATH || pathname === `${SHOP_ORDERS_PATH}/`) {
    return createDefaultShopRoute();
  }

  if (pathname === SHOP_INVENTORY_PATH || pathname === `${SHOP_INVENTORY_PATH}/`) {
    return {
      kind: 'inventory'
    };
  }

  const orderMatched = pathname.match(/^\/shop\/order\/([1-9]\d*)\/?$/);
  const orderId = normalizePositiveLongId(orderMatched?.[1]);
  if (orderId) {
    return {
      kind: 'order-detail',
      orderId
    };
  }

  return null;
}

export function isShopPathname(pathname: string): boolean {
  return parseShopRoute(pathname) !== null;
}

export function buildShopPath(route: ShopRoute): string {
  if (route.kind === 'orders') {
    return SHOP_ORDERS_PATH;
  }

  if (route.kind === 'inventory') {
    return SHOP_INVENTORY_PATH;
  }

  return `/shop/order/${route.orderId}`;
}
