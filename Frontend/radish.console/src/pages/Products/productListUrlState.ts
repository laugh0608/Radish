import { normalizeConsoleReturnTo } from '../../utils/returnTo.ts';

export type ProductTypeFilter = 1 | 2 | 99;

export interface ProductListQuery {
  pageIndex: number;
  pageSize: number;
  categoryId?: string;
  productType?: ProductTypeFilter;
  isOnSale?: boolean;
  keyword: string;
}

export const DEFAULT_PRODUCT_LIST_QUERY: ProductListQuery = {
  pageIndex: 1,
  pageSize: 20,
  categoryId: undefined,
  productType: undefined,
  isOnSale: undefined,
  keyword: '',
};

function parsePositiveInteger(value: string | null, fallback: number, maximum?: number) {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return fallback;
  }

  return maximum ? Math.min(parsed, maximum) : parsed;
}

function parseProductType(value: string | null): ProductTypeFilter | undefined {
  const parsed = Number(value);
  return parsed === 1 || parsed === 2 || parsed === 99 ? parsed : undefined;
}

function parseOptionalBoolean(value: string | null): boolean | undefined {
  if (value === '1' || value === 'true') return true;
  if (value === '0' || value === 'false') return false;
  return undefined;
}

export function parseProductListQuery(searchParams: URLSearchParams): ProductListQuery {
  return {
    pageIndex: parsePositiveInteger(searchParams.get('page'), DEFAULT_PRODUCT_LIST_QUERY.pageIndex),
    pageSize: parsePositiveInteger(
      searchParams.get('pageSize'),
      DEFAULT_PRODUCT_LIST_QUERY.pageSize,
      100,
    ),
    categoryId: searchParams.get('category')?.trim() || undefined,
    productType: parseProductType(searchParams.get('productType')),
    isOnSale: parseOptionalBoolean(searchParams.get('isOnSale')),
    keyword: searchParams.get('keyword')?.trim().slice(0, 100) ?? '',
  };
}

export function serializeProductListQuery(query: ProductListQuery): URLSearchParams {
  const searchParams = new URLSearchParams();
  if (query.pageIndex > 1) searchParams.set('page', String(query.pageIndex));
  if (query.pageSize !== DEFAULT_PRODUCT_LIST_QUERY.pageSize) {
    searchParams.set('pageSize', String(query.pageSize));
  }
  if (query.categoryId) searchParams.set('category', query.categoryId);
  if (query.productType !== undefined) searchParams.set('productType', String(query.productType));
  if (query.isOnSale !== undefined) searchParams.set('isOnSale', query.isOnSale ? '1' : '0');
  if (query.keyword) searchParams.set('keyword', query.keyword);
  return searchParams;
}

export function parseProductLongIdQuery(value: string | null): string | undefined {
  if (!value) return undefined;
  const trimmed = value.trim();
  return /^[1-9]\d*$/u.test(trimmed) ? trimmed : undefined;
}

export function parseProductBooleanQuery(value: string | null): boolean {
  return value === '1' || value === 'true';
}

export const normalizeProductReturnTo = normalizeConsoleReturnTo;

export function buildProductDetailSearchParams(params: {
  productId?: string;
  openDetail?: boolean;
  returnTo?: string | null;
  listQuery?: ProductListQuery;
}): URLSearchParams {
  const searchParams = params.listQuery
    ? serializeProductListQuery(params.listQuery)
    : new URLSearchParams();
  const normalizedReturnTo = normalizeProductReturnTo(params.returnTo);

  if (params.productId !== undefined) searchParams.set('productId', params.productId);
  if (params.openDetail) searchParams.set('openDetail', '1');
  if (normalizedReturnTo) searchParams.set('returnTo', normalizedReturnTo);
  return searchParams;
}

export function buildProductDetailReturnTo(params: {
  productId: string;
  returnTo?: string | null;
  listQuery?: ProductListQuery;
}): string {
  const searchParams = buildProductDetailSearchParams({
    productId: params.productId,
    openDetail: true,
    returnTo: params.returnTo,
    listQuery: params.listQuery,
  });

  return `/products?${searchParams.toString()}`;
}
