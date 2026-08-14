import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

test('商城加载错误保留服务端结构化诊断并只显示本地化消息', () => {
  const source = readFileSync(new URL('../src/apps/shop/shopDataError.ts', import.meta.url), 'utf8');

  assert.match(source, /response\.messageKey \? \{ \.\.\.response, message: undefined \} : response/);
  assert.match(source, /error instanceof ApiResponseError && error\.messageKey/);
  assert.match(source, /t\(error\.messageKey, \{ defaultValue: fallbackMessage \}\)/);
  for (const field of ['code', 'messageKey', 'statusCode', 'httpStatus', 'traceId']) {
    assert.match(source, new RegExp(`${field}: error\\.${field}`));
  }
});

test('商城数据 Hook 不再用原始 Error 或硬编码消息替代统一错误契约', () => {
  const source = readFileSync(new URL('../src/apps/shop/hooks/useShopData.ts', import.meta.url), 'utf8');

  assert.doesNotMatch(source, /throw new Error/);
  assert.doesNotMatch(source, /setError\(['"]加载/);
  assert.match(source, /createShopResponseError/);
  assert.match(source, /createShopLoadError/);
  assert.match(source, /setState\(prev => \(\{ \.\.\.prev, loadError, checkingCanBuy: false \}\)\)/);
});
