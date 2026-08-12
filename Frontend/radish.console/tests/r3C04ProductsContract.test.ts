import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const readSource = (relativePath: string) => readFileSync(
  new URL(`../${relativePath}`, import.meta.url),
  'utf8',
);

test('R3-C04-D Product Create / Update 应禁止直接提交上下架状态', () => {
  const types = readSource('src/api/types.ts');
  const form = readSource('src/pages/Products/ProductForm.tsx');
  const backendDto = readSource('../../Radish.Model/ViewModels/ProductVo.cs');
  const backendService = readSource('../../Radish.Service/ProductService.cs');

  const frontendCreateDto = types.match(/export interface CreateProductDto \{[\s\S]*?\n\}/)?.[0] ?? '';
  const backendCreateDto = backendDto.match(/public class CreateProductDto[\s\S]*?\n\}/)?.[0] ?? '';
  const backendUpdateMethod = backendService.match(
    /public async Task<bool> UpdateProductAsync[\s\S]*?(?=\n {4}\/\/\/ <summary>上架商品)/,
  )?.[0] ?? '';

  assert.doesNotMatch(frontendCreateDto, /isOnSale/i);
  assert.doesNotMatch(backendCreateDto, /IsOnSale/);
  assert.doesNotMatch(form, /name="isOnSale"/);
  assert.match(backendService, /product\.IsOnSale = false;/);
  assert.match(backendService, /product\.OnSaleTime = null;/);
  assert.match(backendService, /product\.OffSaleTime = null;/);
  assert.doesNotMatch(backendUpdateMethod, /IsOnSale\s*=/);
});

test('R3-C04-D Products 列表应使用 URL 权威查询、请求代际与响应式资源表面', () => {
  const page = readSource('src/pages/Products/ProductList.tsx');

  assert.match(page, /parseProductListQuery\(urlSearchParams\)/);
  assert.match(page, /pageIndex: query\.pageIndex/);
  assert.match(page, /pageSize: query\.pageSize/);
  assert.match(page, /snapshotQueryKey\.current === queryKey/);
  assert.match(page, /requestSequence\.current !== requestId/);
  assert.match(page, /setReadState\(hasCurrentSnapshot \? 'stale' : 'unavailable'\)/);
  assert.match(page, /const actionsAreAuthoritative = readState === 'ready'/);
  assert.match(page, /<ConsoleResourceList/);
  assert.match(page, /<BottomSheet/);
  assert.match(page, /console-resource-mobile-card product-mobile-card/);
  assert.match(page, /listQuery: query/);
});

test('R3-C04-D 商品写操作应在 handler 与 Form 双重复核权限和权威状态', () => {
  const page = readSource('src/pages/Products/ProductList.tsx');
  const form = readSource('src/pages/Products/ProductForm.tsx');
  const detail = readSource('src/pages/Products/ProductDetail.tsx');

  assert.match(page, /if \(!canCreateProduct \|\| !actionsAreAuthoritative \|\| !metadataIsAuthoritative\)/);
  assert.match(page, /if \(!canEditProduct \|\| !actionsAreAuthoritative \|\| !metadataIsAuthoritative\)/);
  assert.match(page, /if \(!canToggleProductSale \|\| !actionsAreAuthoritative/);
  assert.match(page, /if \(!canDeleteProductPermission \|\| !actionsAreAuthoritative\)/);
  assert.match(page, /<ConfirmDialog[\s\S]*saleConfirmProduct/);
  assert.match(form, /canSubmit: boolean/);
  assert.match(form, /if \(!canSubmit\)/);
  assert.match(form, /if \(!metadataReady\)/);
  assert.match(form, /window\.addEventListener\('beforeunload'/);
  assert.match(form, /onValuesChange=\{\(\) => setIsDirty\(true\)\}/);
  assert.match(detail, /hasAuthoritativeProduct/);
  assert.match(detail, /currentProduct && onEdit && hasAuthoritativeProduct/);
});
