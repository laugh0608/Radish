export type DocumentStatusFilter = 'all' | 'draft' | 'published' | 'archived';
export type DocumentVisibilityFilter = 'all' | 'public' | 'authenticated' | 'restricted';
export type DocumentSourceFilter = 'all' | 'Custom' | 'Imported' | 'BuiltIn';
export type DocumentDeletedFilter = 'active' | 'all' | 'deleted';

export interface DocumentGovernanceQuery {
  pageIndex: number;
  pageSize: number;
  keyword: string;
  status: DocumentStatusFilter;
  visibility: DocumentVisibilityFilter;
  sourceType: DocumentSourceFilter;
  deleted: DocumentDeletedFilter;
  reviewPageIndex: number;
  reviewPageSize: number;
  selectedDocumentId?: string;
}

export const DEFAULT_DOCUMENT_GOVERNANCE_QUERY: DocumentGovernanceQuery = {
  pageIndex: 1,
  pageSize: 20,
  keyword: '',
  status: 'all',
  visibility: 'all',
  sourceType: 'all',
  deleted: 'active',
  reviewPageIndex: 1,
  reviewPageSize: 10,
};

function positiveInteger(value: string | null, fallback: number, maximum?: number): number {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) return fallback;
  return maximum ? Math.min(parsed, maximum) : parsed;
}

function longId(value: string | null): string | undefined {
  return value && /^[1-9]\d*$/.test(value) ? value : undefined;
}

export function parseDocumentGovernanceQuery(searchParams: URLSearchParams): DocumentGovernanceQuery {
  const status = searchParams.get('status');
  const visibility = searchParams.get('visibility');
  const sourceType = searchParams.get('source');
  const deleted = searchParams.get('deleted');
  return {
    pageIndex: positiveInteger(searchParams.get('page'), 1),
    pageSize: positiveInteger(searchParams.get('pageSize'), 20, 100),
    keyword: searchParams.get('keyword')?.trim().slice(0, 100) ?? '',
    status: status === 'draft' || status === 'published' || status === 'archived' ? status : 'all',
    visibility: visibility === 'public' || visibility === 'authenticated' || visibility === 'restricted'
      ? visibility
      : 'all',
    sourceType: sourceType === 'Custom' || sourceType === 'Imported' || sourceType === 'BuiltIn'
      ? sourceType
      : 'all',
    deleted: deleted === 'all' || deleted === 'deleted' ? deleted : 'active',
    reviewPageIndex: positiveInteger(searchParams.get('reviewPage'), 1),
    reviewPageSize: positiveInteger(searchParams.get('reviewPageSize'), 10, 100),
    selectedDocumentId: longId(searchParams.get('documentId')),
  };
}

export function serializeDocumentGovernanceQuery(query: DocumentGovernanceQuery): URLSearchParams {
  const searchParams = new URLSearchParams();
  if (query.pageIndex > 1) searchParams.set('page', String(query.pageIndex));
  if (query.pageSize !== 20) searchParams.set('pageSize', String(query.pageSize));
  if (query.keyword) searchParams.set('keyword', query.keyword);
  if (query.status !== 'all') searchParams.set('status', query.status);
  if (query.visibility !== 'all') searchParams.set('visibility', query.visibility);
  if (query.sourceType !== 'all') searchParams.set('source', query.sourceType);
  if (query.deleted !== 'active') searchParams.set('deleted', query.deleted);
  if (query.reviewPageIndex > 1) searchParams.set('reviewPage', String(query.reviewPageIndex));
  if (query.reviewPageSize !== 10) searchParams.set('reviewPageSize', String(query.reviewPageSize));
  if (query.selectedDocumentId) searchParams.set('documentId', query.selectedDocumentId);
  return searchParams;
}
