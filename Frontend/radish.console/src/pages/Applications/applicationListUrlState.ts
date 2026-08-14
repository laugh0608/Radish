export interface ApplicationListQuery {
  page: number;
  pageSize: number;
  keyword: string;
}

export const DEFAULT_APPLICATION_LIST_QUERY: ApplicationListQuery = {
  page: 1,
  pageSize: 20,
  keyword: '',
};

function parsePositiveInteger(value: string | null, fallback: number, maximum?: number) {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return fallback;
  }

  return maximum ? Math.min(parsed, maximum) : parsed;
}

export function parseApplicationListQuery(searchParams: URLSearchParams): ApplicationListQuery {
  return {
    page: parsePositiveInteger(searchParams.get('page'), DEFAULT_APPLICATION_LIST_QUERY.page),
    pageSize: parsePositiveInteger(
      searchParams.get('pageSize'),
      DEFAULT_APPLICATION_LIST_QUERY.pageSize,
      100,
    ),
    keyword: searchParams.get('keyword')?.trim().slice(0, 100) ?? '',
  };
}

export function serializeApplicationListQuery(query: ApplicationListQuery) {
  const searchParams = new URLSearchParams();
  if (query.page > 1) searchParams.set('page', String(query.page));
  if (query.pageSize !== DEFAULT_APPLICATION_LIST_QUERY.pageSize) {
    searchParams.set('pageSize', String(query.pageSize));
  }
  if (query.keyword) searchParams.set('keyword', query.keyword);
  return searchParams;
}
