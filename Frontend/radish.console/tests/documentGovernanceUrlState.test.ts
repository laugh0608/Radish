import assert from 'node:assert/strict';
import test from 'node:test';
import {
  DEFAULT_DOCUMENT_GOVERNANCE_QUERY,
  parseDocumentGovernanceQuery,
  serializeDocumentGovernanceQuery,
} from '../src/pages/Documents/documentGovernanceUrlState.ts';

test('文档治理 URL 应保留列表、审核分页与显式目标', () => {
  const query = {
    ...DEFAULT_DOCUMENT_GOVERNANCE_QUERY,
    pageIndex: 3,
    pageSize: 50,
    keyword: 'release policy',
    status: 'published' as const,
    visibility: 'restricted' as const,
    sourceType: 'Imported' as const,
    deleted: 'all' as const,
    reviewPageIndex: 2,
    reviewPageSize: 25,
    selectedDocumentId: '2042219067430928385',
  };

  const searchParams = serializeDocumentGovernanceQuery(query);
  assert.deepEqual(parseDocumentGovernanceQuery(searchParams), query);
  assert.equal(searchParams.get('documentId'), '2042219067430928385');
});

test('文档治理 URL 应拒绝非法枚举、分页和非 LongId 目标', () => {
  const parsed = parseDocumentGovernanceQuery(new URLSearchParams({
    page: '-1',
    pageSize: '999',
    status: '已发布',
    visibility: 'secret',
    source: 'Generated',
    deleted: 'yes',
    reviewPage: '0',
    reviewPageSize: '200',
    documentId: '9.1',
  }));

  assert.deepEqual(parsed, {
    ...DEFAULT_DOCUMENT_GOVERNANCE_QUERY,
    pageSize: 100,
    reviewPageSize: 100,
    selectedDocumentId: undefined,
  });
  assert.equal(serializeDocumentGovernanceQuery(DEFAULT_DOCUMENT_GOVERNANCE_QUERY).toString(), '');
});
