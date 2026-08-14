import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');
const readClientFile = (relativePath: string): string => readFileSync(resolve(clientRoot, relativePath), 'utf8');

const docsAppSource = readClientFile('src/public/docs/PublicDocsApp.tsx');
const docsListSource = readClientFile('src/public/docs/PublicDocsList.tsx');
const docsSearchSource = readClientFile('src/public/docs/PublicDocsSearch.tsx');
const docsDetailSource = readClientFile('src/public/docs/PublicDocsDetail.tsx');
const docsRailsSource = readClientFile('src/public/docs/PublicDocsRails.tsx');
const docsSupportSource = readClientFile('src/public/docs/publicDocsViewSupport.ts');
const docsStatusSource = readClientFile('src/public/docs/PublicDocsStatusCard.tsx');
const docsStylesSource = readClientFile('src/public/docs/PublicDocsApp.module.css');
const docsApiSource = readClientFile('src/public/docs/publicDocsApi.ts');
const publicDocsHeadSource = readClientFile('src/public/docs/publicDocsHead.ts');
const publicHeadSource = readClientFile('src/public/publicHead.ts');
const legalSource = readClientFile('src/public/legal/PublicCommitmentsApp.tsx');
const legalStylesSource = readClientFile('src/public/legal/PublicCommitmentsApp.module.css');
const legalRouteSource = readClientFile('src/public/legalRouteState.ts');
const publicEntrySource = readClientFile('src/public/PublicEntry.tsx');

test('R3 Docs 路由应按页面职责拆分且由轻量编排器统一分发', () => {
  const splitSources = [
    ['PublicDocsApp', docsAppSource],
    ['PublicDocsList', docsListSource],
    ['PublicDocsSearch', docsSearchSource],
    ['PublicDocsDetail', docsDetailSource],
    ['PublicDocsRails', docsRailsSource],
    ['PublicDocsViewSupport', docsSupportSource],
    ['PublicDocsStatusCard', docsStatusSource],
  ] as const;

  for (const [name, source] of splitSources) {
    assert.ok(source.split('\n').length <= 600, `${name} should stay within the route-level ownership budget`);
  }

  assert.match(docsAppSource, /import \{ PublicDocsList/);
  assert.match(docsAppSource, /import \{ PublicDocsSearch/);
  assert.match(docsAppSource, /import \{ PublicDocsDetail/);
  assert.match(docsAppSource, /<PublicDocsList/);
  assert.match(docsAppSource, /<PublicDocsSearch/);
  assert.match(docsAppSource, /<PublicDocsDetail/);
  assert.match(docsListSource, /data-public-docs-view="list"/);
  assert.match(docsSearchSource, /data-public-docs-view="search"/);
  assert.match(docsDetailSource, /data-public-docs-view="detail"/);
});

test('R3 Docs 页面应保持唯一主阅读轴并让辅助栏晚于主内容出现', () => {
  assert.ok(docsListSource.indexOf('className={styles.indexMainColumn}') < docsListSource.indexOf('<PublicDocsListRail'));
  assert.ok(docsSearchSource.indexOf('className={styles.searchMainColumn}') < docsSearchSource.indexOf('<PublicDocsSearchRail'));
  assert.ok(docsDetailSource.indexOf('className={styles.articleMainColumn}') < docsDetailSource.indexOf('<PublicDocsDetailRail'));
  assert.match(
    docsStylesSource,
    /\.docsIndexGrid\s*\{[\s\S]*?grid-template-columns: minmax\(0, 1fr\) minmax\(280px, 320px\);/,
  );
  assert.doesNotMatch(
    docsStylesSource,
    /\.docsIndexGrid\s*\{[\s\S]*?grid-template-columns:[^;]*minmax\([^;]*\)\s+minmax\([^;]*\)\s+minmax\(/,
  );
  assert.match(
    docsStylesSource,
    /@media \(max-width: 1120px\)[\s\S]*?\.docsIndexGrid,[\s\S]*?grid-template-columns: minmax\(0, 1fr\);/,
  );

  assert.doesNotMatch(docsRailsSource, /indexRailSearchTitle|searchRailDirectoryTitle/);
  assert.doesNotMatch(docsRailsSource, /detailRailSourceTitle|detailRailMetaTitle|detailRailAuthorTitle/);
});

test('R3 Legal 应按承诺概览、安全边界、章节导航、正文的层级呈现', () => {
  const heroIndex = legalSource.indexOf('<section className={styles.hero}>');
  const summaryIndex = legalSource.indexOf('className={styles.summaryGrid}');
  const safetyIndex = legalSource.indexOf('<PrivacySafetyBoundaryPanel />');
  const anchorIndex = legalSource.indexOf('<nav');
  const sectionsIndex = legalSource.indexOf('className={styles.sectionList}');

  assert.ok(heroIndex >= 0);
  assert.ok(heroIndex < summaryIndex);
  assert.ok(summaryIndex < safetyIndex);
  assert.ok(safetyIndex < anchorIndex);
  assert.ok(anchorIndex < sectionsIndex);
  assert.match(
    legalStylesSource,
    /@media \(max-width: 720px\)[\s\S]*?\.anchorRail\s*\{[\s\S]*?flex-wrap: nowrap;[\s\S]*?overflow-x: auto;/,
  );
  assert.match(
    legalStylesSource,
    /@media \(max-width: 720px\)[\s\S]*?\.anchorRail a\s*\{[\s\S]*?flex: 0 0 auto;[\s\S]*?white-space: nowrap;/,
  );
  assert.match(legalRouteSource, /anchor\?: string/);
  assert.match(legalRouteSource, /buildPublicLegalPath[\s\S]*?encodeURIComponent\(route\.anchor\)/);
  assert.match(publicEntrySource, /parsePublicLegalRoute\(window\.location\.pathname, window\.location\.hash\)/);
  assert.match(publicEntrySource, /<PublicCommitmentsApp route=\{route\.route\}/);
  assert.match(legalSource, /target\.scrollIntoView\(\{ block: 'start' \}\)/);
});

test('R3 Docs 应保留按当前身份读取与受保护附件边界', () => {
  assert.match(docsApiSource, /Wiki\/GetList/);
  assert.match(docsApiSource, /Wiki\/GetTree/);
  assert.match(docsApiSource, /Wiki\/GetBySlug/);
  assert.equal(docsApiSource.match(/withAuth: true/g)?.length, 3);
  assert.doesNotMatch(docsApiSource, /Wiki\/PublicGet/);
  assert.match(
    docsDetailSource,
    /documentDetail\.voVisibility === WikiDocumentVisibility\.Public[\s\S]*?\? undefined[\s\S]*?: protectedAttachments/,
  );
  assert.match(
    docsDetailSource,
    /documentDetail\.voVisibility !== WikiDocumentVisibility\.Public[\s\S]*?return null/,
  );
  assert.match(publicHeadSource, /function buildDocsHead[\s\S]*?kind === 'detail'[\s\S]*?indexable: false/);
  assert.match(publicDocsHeadSource, /indexable: true/);
});

test('R3 Public Docs 与 Legal 样式应继续只消费主题 token', () => {
  assert.doesNotMatch(`${docsStylesSource}\n${legalStylesSource}`, /#[0-9a-fA-F]{3,8}\b/);
  assert.doesNotMatch(
    `${docsAppSource}\n${docsListSource}\n${docsSearchSource}\n${docsDetailSource}\n${docsRailsSource}`,
    /文档作者台|编辑文档/,
  );
});
