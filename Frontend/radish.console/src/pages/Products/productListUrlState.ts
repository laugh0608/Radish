export function parseProductLongIdQuery(value: string | null): string | undefined {
  if (!value) {
    return undefined;
  }

  const trimmed = value.trim();
  return /^[1-9]\d*$/u.test(trimmed) ? trimmed : undefined;
}

export function parseProductBooleanQuery(value: string | null): boolean {
  return value === '1' || value === 'true';
}

export function normalizeProductReturnTo(value?: string | null): string | undefined {
  const normalized = value?.trim();
  return normalized?.startsWith('/') && !normalized.startsWith('//')
    ? normalized
    : undefined;
}

export function buildProductDetailSearchParams(params: {
  productId?: string;
  openDetail?: boolean;
  returnTo?: string | null;
}): URLSearchParams {
  const searchParams = new URLSearchParams();
  const normalizedReturnTo = normalizeProductReturnTo(params.returnTo);

  if (params.productId !== undefined) {
    searchParams.set('productId', params.productId);
  }

  if (params.openDetail) {
    searchParams.set('openDetail', '1');
  }

  if (normalizedReturnTo) {
    searchParams.set('returnTo', normalizedReturnTo);
  }

  return searchParams;
}

export function buildProductDetailReturnTo(params: {
  productId: string;
  returnTo?: string | null;
}): string {
  const searchParams = buildProductDetailSearchParams({
    productId: params.productId,
    openDetail: true,
    returnTo: params.returnTo,
  });

  return `/products?${searchParams.toString()}`;
}
