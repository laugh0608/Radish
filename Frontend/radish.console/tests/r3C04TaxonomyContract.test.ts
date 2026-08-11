import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const readConsoleSource = (relativePath: string) => readFileSync(
  new URL(`../${relativePath}`, import.meta.url),
  'utf8',
);

test('R3-C04-A Categories 与 Tags 应保持 LongId 字符串契约', () => {
  const categoryApi = readConsoleSource('src/api/categoryApi.ts');
  const tagApi = readConsoleSource('src/api/tagApi.ts');

  assert.match(categoryApi, /voId: string;/);
  assert.match(categoryApi, /voParentId\?: string \| null;/);
  assert.match(categoryApi, /parentId\?: string \| null;/);
  assert.match(categoryApi, /updateCategory\(id: string/);
  assert.match(categoryApi, /deleteCategory\(id: string/);
  assert.match(tagApi, /voId: string;/);
  assert.match(tagApi, /updateTag\(id: string/);
  assert.match(tagApi, /deleteTag\(id: string/);
});

test('R3-C04-A 列表分页应由已应用查询驱动并忽略过期响应', () => {
  const categoryList = readConsoleSource('src/pages/Categories/CategoryList.tsx');
  const tagList = readConsoleSource('src/pages/Tags/TagList.tsx');

  for (const source of [categoryList, tagList]) {
    assert.match(source, /const \[query, setQuery\] = useState/);
    assert.match(source, /pageIndex: query\.pageIndex/);
    assert.match(source, /requestSequence\.current !== requestId/);
    assert.match(source, /snapshotQueryKey\.current === queryKey/);
    assert.match(source, /setReadState\(hasCurrentSnapshot \? 'stale' : 'unavailable'\)/);
    assert.match(source, /const actionsAreAuthoritative = readState === 'ready'/);
    assert.match(source, /pageIndex: 1,[\s\S]*keyword: keywordDraft\.trim\(\)/);
    assert.doesNotMatch(source, /load(?:Categories|Tags)\(1, pageSize\)/);
  }
});

test('R3-C04-A 两类资源应复用响应式列表表面并提供 Mobile 筛选与卡片操作', () => {
  const shell = readConsoleSource('src/components/ConsolePage/ConsolePage.tsx');
  const styles = readConsoleSource('src/components/ConsolePage/ConsolePage.css');
  const categoryList = readConsoleSource('src/pages/Categories/CategoryList.tsx');
  const tagList = readConsoleSource('src/pages/Tags/TagList.tsx');

  assert.match(shell, /export function ConsoleResourceList/);
  assert.match(styles, /\.console-resource-list__mobile[\s\S]*display: none/);
  assert.match(styles, /@media \(max-width: 768px\)[\s\S]*\.console-resource-list__mobile[\s\S]*display: grid/);

  for (const source of [categoryList, tagList]) {
    assert.match(source, /<ConsoleResourceList/);
    assert.match(source, /<BottomSheet/);
    assert.match(source, /console-resource-mobile-card/);
    assert.match(source, /renderActions\(record\)/);
  }
});

test('R3-C04-A 表单应具备权限、忙碌态与未保存变更停止线', () => {
  const categoryForm = readConsoleSource('src/pages/Categories/CategoryForm.tsx');
  const tagForm = readConsoleSource('src/pages/Tags/TagForm.tsx');

  assert.match(categoryForm, /loadAllCategoryOptions/);
  assert.match(categoryForm, /pageIndex <= firstPage\.pageCount/);

  for (const source of [categoryForm, tagForm]) {
    assert.match(source, /canSubmit: boolean/);
    assert.match(source, /if \(!canSubmit\)/);
    assert.match(source, /Modal\.confirm\(\{/);
    assert.match(source, /onValuesChange=\{\(\) => setIsDirty\(true\)\}/);
    assert.match(source, /window\.addEventListener\('beforeunload'/);
    assert.match(source, /maskClosable=\{false\}/);
    assert.match(source, /keyboard=\{false\}/);
  }
});
