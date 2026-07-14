import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const localesDirectory = path.resolve(testDirectory, '../src/locales');
const domainNames = ['core', 'shell', 'dashboard'];

function collectKeys(language: 'en' | 'zh'): string[] {
  return domainNames.flatMap((domain) => {
    const source = fs.readFileSync(path.join(localesDirectory, language, `${domain}.ts`), 'utf8');
    return [...source.matchAll(/^\s*'([^']+)'\s*:/gm)].map((match) => match[1]);
  });
}

test('Console 中英文资源键应完全对齐且不存在跨域重复键', () => {
  const enKeys = collectKeys('en');
  const zhKeys = collectKeys('zh');

  assert.equal(new Set(enKeys).size, enKeys.length, '英文资源存在重复键');
  assert.equal(new Set(zhKeys).size, zhKeys.length, '中文资源存在重复键');
  assert.deepEqual([...new Set(enKeys)].sort(), [...new Set(zhKeys)].sort());
});
