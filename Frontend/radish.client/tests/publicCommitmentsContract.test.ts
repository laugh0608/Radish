import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  publicCommitmentSections,
  publicCommitmentSummaries,
  publicCommitmentsSourceLanguage,
} from '../src/public/legal/publicCommitmentsData.ts';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');

test('公开承诺应保留稳定章节与发布语言原文边界', () => {
  assert.equal(publicCommitmentsSourceLanguage, 'zh-CN');
  assert.deepEqual(
    publicCommitmentSections.map((section) => section.id),
    ['community', 'privacy', 'terms', 'virtual-assets', 'minors', 'support'],
  );
  assert.equal(publicCommitmentSummaries.length, 3);
  assert.match(publicCommitmentSections[0]?.items[0] ?? '', /禁止发布违法违规/);
});

test('公开承诺壳词元应本地化，运营长文应从稳定数据源原样渲染', () => {
  const appSource = readFileSync(resolve(clientRoot, 'src/public/legal/PublicCommitmentsApp.tsx'), 'utf8');
  const stylesSource = readFileSync(resolve(clientRoot, 'src/public/legal/PublicCommitmentsApp.module.css'), 'utf8');

  assert.match(appSource, /publicCommitmentsSourceLanguage/);
  assert.match(appSource, /lang=\{publicCommitmentsSourceLanguage\}/);
  assert.match(appSource, /t\('legal\.originalLanguageNote'\)/);
  assert.match(appSource, /publicCommitmentSections\.map/);
  assert.doesNotMatch(appSource, /禁止发布违法违规/);
  assert.match(stylesSource, /overflow-wrap: anywhere;/);
  assert.match(stylesSource, /@media \(max-width: 720px\)/);
});
