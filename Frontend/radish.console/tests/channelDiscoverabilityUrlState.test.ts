import assert from 'node:assert/strict';
import test from 'node:test';
import {
  DEFAULT_CHANNEL_DISCOVERABILITY_QUERY,
  parseChannelDiscoverabilityQuery,
  serializeChannelDiscoverabilityQuery,
} from '../src/pages/ChannelDiscoverability/channelDiscoverabilityUrlState.ts';

test('Channel Discoverability URL 应保留筛选草稿应用后的权威查询与分页', () => {
  const query = parseChannelDiscoverabilityQuery(new URLSearchParams(
    'page=3&pageSize=50&keyword=general&visibility=summary&lifecycle=disabled&includeDeleted=1',
  ));

  assert.deepEqual(query, {
    pageIndex: 3,
    pageSize: 50,
    keyword: 'general',
    visibility: 'summary',
    lifecycle: 'disabled',
    includeDeleted: true,
  });
  assert.equal(
    serializeChannelDiscoverabilityQuery(query).toString(),
    'page=3&pageSize=50&keyword=general&visibility=summary&lifecycle=disabled&includeDeleted=1',
  );
});

test('Channel Discoverability URL 应拒绝非法枚举与分页并省略默认值', () => {
  const query = parseChannelDiscoverabilityQuery(new URLSearchParams(
    'page=0&pageSize=500&visibility=public&lifecycle=deleted&includeDeleted=no',
  ));

  assert.deepEqual(query, {
    ...DEFAULT_CHANNEL_DISCOVERABILITY_QUERY,
    pageSize: 100,
  });
  assert.equal(
    serializeChannelDiscoverabilityQuery(DEFAULT_CHANNEL_DISCOVERABILITY_QUERY).toString(),
    '',
  );
});
