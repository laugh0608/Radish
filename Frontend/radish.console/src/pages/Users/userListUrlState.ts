export type UserStatusFilter = 'all' | 'enabled' | 'disabled';

export interface UserListQuery {
  pageIndex: number;
  pageSize: number;
  keyword: string;
  status: UserStatusFilter;
  roleName: string;
}

export const DEFAULT_USER_LIST_QUERY: UserListQuery = {
  pageIndex: 1,
  pageSize: 20,
  keyword: '',
  status: 'all',
  roleName: '',
};

function parsePositiveInteger(value: string | null, fallback: number, maximum?: number) {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return fallback;
  }

  return maximum ? Math.min(parsed, maximum) : parsed;
}

export function parseUserListQuery(searchParams: URLSearchParams): UserListQuery {
  const status = searchParams.get('status');
  return {
    pageIndex: parsePositiveInteger(searchParams.get('page'), DEFAULT_USER_LIST_QUERY.pageIndex),
    pageSize: parsePositiveInteger(searchParams.get('pageSize'), DEFAULT_USER_LIST_QUERY.pageSize, 100),
    keyword: searchParams.get('keyword')?.trim() ?? '',
    status: status === 'enabled' || status === 'disabled' ? status : 'all',
    roleName: searchParams.get('role')?.trim() ?? '',
  };
}

export function serializeUserListQuery(query: UserListQuery) {
  const searchParams = new URLSearchParams();
  if (query.pageIndex > 1) searchParams.set('page', String(query.pageIndex));
  if (query.pageSize !== DEFAULT_USER_LIST_QUERY.pageSize) searchParams.set('pageSize', String(query.pageSize));
  if (query.keyword) searchParams.set('keyword', query.keyword);
  if (query.status !== 'all') searchParams.set('status', query.status);
  if (query.roleName) searchParams.set('role', query.roleName);
  return searchParams;
}

export function buildUserDetailPath(userId: string, returnTo: string) {
  const searchParams = new URLSearchParams({ returnTo });
  return `/users/${encodeURIComponent(userId)}?${searchParams.toString()}`;
}
