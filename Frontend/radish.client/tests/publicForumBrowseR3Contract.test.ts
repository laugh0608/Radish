import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDirectory, '..');
const readClientSource = (relativePath: string): string => (
  readFileSync(resolve(clientRoot, relativePath), 'utf8')
);

const browseRouteSources = [
  'src/public/forum/PublicForumList.tsx',
  'src/public/forum/PublicForumSearch.tsx',
  'src/public/forum/PublicForumTag.tsx',
  'src/public/forum/PublicForumTypeFeed.tsx',
];

test('R3-P04 浏览路由应共享 Forum 专属样式所有权并保持详情与发布器隔离', () => {
  const styleOwnerSource = readClientSource('src/public/forum/publicForumBrowseStyles.ts');
  const legacyStyleSource = readClientSource('src/public/forum/PublicForumApp.module.css');
  const browseStyleSource = readClientSource('src/public/forum/PublicForumBrowse.module.css');

  assert.match(styleOwnerSource, /PublicForumApp\.module\.css/);
  assert.match(styleOwnerSource, /PublicForumBrowse\.module\.css/);
  assert.ok(legacyStyleSource.split('\n').length <= 1500);
  assert.ok(browseStyleSource.split('\n').length <= 900);
  assert.doesNotMatch(browseStyleSource, /#[0-9a-fA-F]{3,8}\b/);

  for (const sourcePath of browseRouteSources) {
    const source = readClientSource(sourcePath);
    assert.match(source, /publicForumBrowseStyles as styles/);
    assert.doesNotMatch(source, /import styles from '\.\/PublicForumApp\.module\.css'/);
  }

  const detailSource = readClientSource('src/public/forum/PublicForumDetailView.tsx');
  const composeSource = readClientSource('src/public/forum/PublicForumCompose.tsx');
  assert.match(detailSource, /import styles from '\.\/PublicForumApp\.module\.css'/);
  assert.match(composeSource, /import styles from '\.\/PublicForumApp\.module\.css'/);
});

test('R3-P04 四类浏览页应以结果流为主轴并把阅读上下文置于其后', () => {
  for (const sourcePath of browseRouteSources) {
    const source = readClientSource(sourcePath);
    const mainSectionIndex = source.indexOf('<section className={`${styles.sectionCard} ${styles.listSectionCard}`}>');
    const asideIndex = source.indexOf('<aside className={styles.forumSideRail}');

    assert.match(source, /data-public-forum-browse=/);
    assert.ok(mainSectionIndex >= 0, `${sourcePath} 缺少浏览主轴`);
    assert.ok(asideIndex > mainSectionIndex, `${sourcePath} 的从属上下文必须位于主轴之后`);
  }

  const searchSource = readClientSource('src/public/forum/PublicForumSearch.tsx');
  const tagSource = readClientSource('src/public/forum/PublicForumTag.tsx');
  const typeSource = readClientSource('src/public/forum/PublicForumTypeFeed.tsx');

  for (const source of [searchSource, tagSource, typeSource]) {
    assert.match(source, /<PublicReadingGuide[\s\S]*className=\{styles\.sideReadingGuide\}/);
    assert.doesNotMatch(source, /className=\{styles\.kicker\}/);
  }

  assert.match(
    tagSource,
    /const pageTitle = selectedTag\?\.voName \|\| t\('forum\.public\.tagTitle'\);/
  );
  assert.doesNotMatch(tagSource, /#\{selectedTag\.voSlug\}/);
  assert.equal(tagSource.match(/styles\.readOnlyBadge/g)?.length, 1);
});

test('R3-P04 响应式契约应保持 PC 连续主轴与 Mobile 无横向卡片溢出', () => {
  const styleSource = readClientSource('src/public/forum/PublicForumBrowse.module.css');

  assert.match(styleSource, /\.forumGrid \{[\s\S]*grid-template-columns: minmax\(0, 1fr\) minmax\(280px, 320px\);/);
  assert.match(styleSource, /\.pageTitle \{[\s\S]*font-family: var\(--theme-font-sans\);/);
  assert.match(styleSource, /\.sectionCard \{[\s\S]*overflow: hidden;[\s\S]*border-radius: 16px;/);
  assert.match(styleSource, /@media \(max-width: 1040px\)[\s\S]*\.forumGrid \{\s*grid-template-columns: minmax\(0, 1fr\);/);
  assert.match(styleSource, /@media \(max-width: 720px\)[\s\S]*\.segmented,[\s\S]*flex-wrap: nowrap;[\s\S]*overflow-x: auto;/);
  assert.match(styleSource, /@media \(max-width: 720px\)[\s\S]*\.forumSideRail \{\s*display: flex;/);
});
