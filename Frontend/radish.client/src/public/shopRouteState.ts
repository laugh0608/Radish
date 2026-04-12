export interface PublicShopHomeRoute {
  kind: 'home';
}

export interface PublicShopProductsRoute {
  kind: 'products';
  categoryId?: string;
  keyword?: string;
  page: number;
}

export interface PublicShopDetailRoute {
  kind: 'detail';
  productId: string;
}

export type PublicShopRoute =
  | PublicShopHomeRoute
  | PublicShopProductsRoute
  | PublicShopDetailRoute;

export function createDefaultPublicShopRoute(): PublicShopHomeRoute {
  return {
    kind: 'home'
  };
}

export function createDefaultPublicShopProductsRoute(): PublicShopProductsRoute {
  return {
    kind: 'products',
    page: 1
  };
}

function normalizePositiveInteger(value: string | undefined): number | undefined {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return undefined;
  }

  return parsed;
}

function normalizePositiveIntegerString(value: string | undefined): string | undefined {
  if (!value) {
    return undefined;
  }

  const normalized = value.trim();
  if (!/^[1-9]\d*$/.test(normalized)) {
    return undefined;
  }

  return normalized;
}

function normalizeKeyword(value: string | null): string | undefined {
  if (!value) {
    return undefined;
  }

  const normalized = value.trim();
  return normalized || undefined;
}

export function parsePublicShopRoute(pathname: string, search: string): PublicShopRoute {
  if (pathname === '/shop' || pathname === '/shop/') {
    return createDefaultPublicShopRoute();
  }

  if (pathname === '/shop/products' || pathname === '/shop/products/') {
    const params = new URLSearchParams(search);
    return {
      kind: 'products',
      categoryId: normalizeKeyword(params.get('category')),
      keyword: normalizeKeyword(params.get('q')),
      page: normalizePositiveInteger(params.get('page') ?? undefined) ?? 1
    };
  }

  const detailMatched = pathname.match(/^\/shop\/product\/([1-9]\d*)\/?$/);
  const productId = normalizePositiveIntegerString(detailMatched?.[1]);
  if (productId) {
    return {
      kind: 'detail',
      productId
    };
  }

  return createDefaultPublicShopRoute();
}

export function buildPublicShopPath(route: PublicShopRoute): string {
  if (route.kind === 'home') {
    return '/shop';
  }

  if (route.kind === 'detail') {
    return `/shop/product/${route.productId}`;
  }

  const search = new URLSearchParams();
  if (route.categoryId) {
    search.set('category', route.categoryId);
  }
  if (route.keyword) {
    search.set('q', route.keyword);
  }
  if (route.page > 1) {
    search.set('page', String(route.page));
  }

  const query = search.toString();
  return query ? `/shop/products?${query}` : '/shop/products';
}
