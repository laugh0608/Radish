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

test('公开主页应直接消费同一次资料响应中的可空宠物名片', () => {
  const profileSource = readClientSource('src/public/profile/PublicProfileApp.tsx');

  assert.match(profileSource, /import \{ PublicPetCard \} from '\.\/PublicPetCard';/);
  assert.match(profileSource, /\{profile\?\.voPet \? <PublicPetCard pet=\{profile\.voPet\} \/> : null\}/);
  assert.doesNotMatch(profileSource, /GetPublicCard|\/api\/v1\/Pet|localStorage|sessionStorage/);
});

test('公开宠物名片只读取公开白名单字段且保持只读', () => {
  const cardSource = readClientSource('src/public/profile/PublicPetCard.tsx');

  for (const field of ['voName', 'voSpeciesKey', 'voShapeKey', 'voGrowthStage', 'voMood']) {
    assert.match(cardSource, new RegExp(`pet\\.${field}`));
  }

  assert.doesNotMatch(
    cardSource,
    /voId|voUserId|voSatiety|voCleanliness|voEnergy|voGrowthValue|voLastCareTime|voCareActions|voCreateTime/,
  );
  assert.doesNotMatch(cardSource, /<button|href=|['"]\/pet['"]|localStorage|sessionStorage/);
  assert.match(cardSource, /aria-labelledby=\{titleId\}/);
  assert.match(cardSource, /<dl className=\{styles\.metadata\}>/);
});

test('公开宠物名片应使用主题 token 并覆盖移动端长文本布局', () => {
  const stylesSource = readClientSource('src/public/profile/PublicPetCard.module.css');

  assert.doesNotMatch(stylesSource, /#[0-9a-f]{3,8}/i);
  assert.match(stylesSource, /var\(--theme-bg-surface\)/);
  assert.match(stylesSource, /var\(--theme-text-primary\)/);
  assert.match(stylesSource, /overflow-wrap: anywhere/);
  assert.match(stylesSource, /@media \(max-width: 720px\)/);
  assert.match(stylesSource, /grid-template-columns: repeat\(2, minmax\(0, 1fr\)\)/);
});

test('宠物公开名片文案应同时提供中英文资源', () => {
  const zhSource = readClientSource('src/locales/zh/account.ts');
  const enSource = readClientSource('src/locales/en/account.ts');
  const requiredKeys = [
    'pet.public.kicker',
    'pet.public.visibleBadge',
    'pet.public.description',
    'pet.public.speciesLabel',
    'pet.public.shapeLabel',
    'pet.public.stageLabel',
    'pet.public.moodLabel',
    'pet.species.radish',
    'pet.shape.sprout',
  ];

  for (const key of requiredKeys) {
    assert.ok(zhSource.includes(`'${key}'`), `中文资源缺少 ${key}`);
    assert.ok(enSource.includes(`'${key}'`), `英文资源缺少 ${key}`);
  }
});
