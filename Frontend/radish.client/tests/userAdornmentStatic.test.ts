import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');

function readClientSource(relativePath: string): string {
  return readFileSync(resolve(clientRoot, relativePath), 'utf8');
}

test('公开身份装饰应由同一共享组件承接规定消费面', () => {
  const componentSource = readClientSource('src/components/UserAdornment.tsx');
  const profileSource = readClientSource('src/public/profile/PublicProfileApp.tsx');
  const postCardSource = readClientSource('src/apps/forum/components/PostCard.tsx');
  const postDetailSource = readClientSource('src/apps/forum/components/PostDetail.tsx');
  const commentSource = readClientSource('src/apps/forum/components/CommentNode.tsx');

  assert.match(componentSource, /adornment\?\.voBadge/);
  assert.match(componentSource, /adornment\?\.voTitle/);
  assert.match(profileSource, /<UserAdornment adornment=\{profile\?\.voAdornment\}/);
  assert.match(postCardSource, /<UserAdornment adornment=\{post\.voAuthorAdornment\}/);
  assert.match(postDetailSource, /<UserAdornment adornment=\{post\.voAuthorAdornment\}/);
  assert.match(postDetailSource, /<UserAdornment adornment=\{answer\.voAuthorAdornment\}/);
  assert.match(commentSource, /<UserAdornment adornment=\{node\.voAuthorAdornment\}/);
});

test('身份装饰契约与移动布局不应包含商城内部字段', () => {
  const typeSource = readClientSource('src/types/userAdornment.ts');
  const styleSource = readClientSource('src/components/UserAdornment.module.css');

  assert.match(typeSource, /voResourceKey: string/);
  assert.match(typeSource, /voName: string/);
  assert.match(typeSource, /voImageUrl\?: string \| null/);
  assert.doesNotMatch(typeSource, /BenefitId|Order|Price|Operation/i);
  assert.match(styleSource, /flex-wrap: wrap/);
  assert.match(styleSource, /@media \(max-width: 640px\)/);
  assert.match(styleSource, /text-overflow: ellipsis/);
});

test('Client 背包应读取服务端商品能力元数据而非本地支持名单', () => {
  const inventorySource = readClientSource('src/apps/shop/pages/Inventory.tsx');
  const shopApiSource = readClientSource('src/api/shop.ts');

  assert.match(shopApiSource, /GetProductCapabilities/);
  assert.match(inventorySource, /findBenefitCapability\(capabilities/);
  assert.match(inventorySource, /findConsumableCapability\(capabilities/);
  assert.doesNotMatch(inventorySource, /normalizedType === 'PostPinCard'/);
});
