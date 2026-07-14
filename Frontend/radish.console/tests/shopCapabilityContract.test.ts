import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const consoleRoot = resolve(testDirectory, '..');

test('Console 商品表单应消费服务端能力元数据', () => {
  const apiSource = readFileSync(resolve(consoleRoot, 'src/api/shopApi.ts'), 'utf8');
  const formSource = readFileSync(resolve(consoleRoot, 'src/pages/Products/ProductForm.tsx'), 'utf8');

  assert.match(apiSource, /GetProductCapabilities/);
  assert.match(formSource, /getProductCapabilities/);
  assert.match(formSource, /findProductCapability\(capabilities/);
  assert.match(formSource, /selectedCapability\?\.voCanSell !== true/);
  assert.match(formSource, /selectedCapability\?\.voConfigurationRequirements/);
  assert.doesNotMatch(formSource, /isUnsupportedSaleSelection/);
});
