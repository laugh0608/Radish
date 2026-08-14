export type ChannelDiscoverabilityVisibilityFilter = 'all' | 'hidden' | 'summary';
export type ChannelDiscoverabilityLifecycleFilter = 'all' | 'enabled' | 'disabled';

export interface ChannelDiscoverabilityQuery {
  pageIndex: number;
  pageSize: number;
  keyword: string;
  visibility: ChannelDiscoverabilityVisibilityFilter;
  lifecycle: ChannelDiscoverabilityLifecycleFilter;
  includeDeleted: boolean;
}

export const DEFAULT_CHANNEL_DISCOVERABILITY_QUERY: ChannelDiscoverabilityQuery = {
  pageIndex: 1,
  pageSize: 20,
  keyword: '',
  visibility: 'all',
  lifecycle: 'all',
  includeDeleted: false,
};

function parsePositiveInteger(value: string | null, fallback: number, maximum?: number): number {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return fallback;
  }

  return maximum ? Math.min(parsed, maximum) : parsed;
}

function parseVisibility(value: string | null): ChannelDiscoverabilityVisibilityFilter {
  return value === 'hidden' || value === 'summary' ? value : 'all';
}

function parseLifecycle(value: string | null): ChannelDiscoverabilityLifecycleFilter {
  return value === 'enabled' || value === 'disabled' ? value : 'all';
}

export function parseChannelDiscoverabilityQuery(
  searchParams: URLSearchParams,
): ChannelDiscoverabilityQuery {
  return {
    pageIndex: parsePositiveInteger(
      searchParams.get('page'),
      DEFAULT_CHANNEL_DISCOVERABILITY_QUERY.pageIndex,
    ),
    pageSize: parsePositiveInteger(
      searchParams.get('pageSize'),
      DEFAULT_CHANNEL_DISCOVERABILITY_QUERY.pageSize,
      100,
    ),
    keyword: searchParams.get('keyword')?.trim().slice(0, 100) ?? '',
    visibility: parseVisibility(searchParams.get('visibility')),
    lifecycle: parseLifecycle(searchParams.get('lifecycle')),
    includeDeleted: ['1', 'true'].includes(searchParams.get('includeDeleted') ?? ''),
  };
}

export function serializeChannelDiscoverabilityQuery(
  query: ChannelDiscoverabilityQuery,
): URLSearchParams {
  const searchParams = new URLSearchParams();
  if (query.pageIndex > 1) searchParams.set('page', String(query.pageIndex));
  if (query.pageSize !== DEFAULT_CHANNEL_DISCOVERABILITY_QUERY.pageSize) {
    searchParams.set('pageSize', String(query.pageSize));
  }
  if (query.keyword) searchParams.set('keyword', query.keyword);
  if (query.visibility !== 'all') searchParams.set('visibility', query.visibility);
  if (query.lifecycle !== 'all') searchParams.set('lifecycle', query.lifecycle);
  if (query.includeDeleted) searchParams.set('includeDeleted', '1');
  return searchParams;
}
