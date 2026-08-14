import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const readSource = (relativePath: string) => readFileSync(
  new URL(`../${relativePath}`, import.meta.url),
  'utf8',
);

test('R3-C04-E Sticker 分组完整编辑与 toggle-only 应使用独立契约', () => {
  const api = readSource('src/api/stickerApi.ts');
  const page = readSource('src/pages/Stickers/StickerGroupList.tsx');
  const controller = readSource('../../Radish.Api/Controllers/StickerController.cs');
  const resources = readSource('../../Radish.DbMigrate/InitialDataSeeder.ConsoleAuthorization.cs');

  assert.match(api, /UpdateGroupStatus\/\$\{id\}/);
  assert.match(page, /updateStickerGroupStatus\(group\.voId, enabled\)/);
  assert.doesNotMatch(page, /StickerGroupUpsertRequest/);
  assert.match(
    controller,
    /RequireConsolePermission\(ConsolePermissions\.StickersEdit\)[\s\S]*?UpdateGroup\(/,
  );
  assert.match(
    controller,
    /RequireConsolePermission\(ConsolePermissions\.StickersToggle\)[\s\S]*?UpdateGroupStatus\(/,
  );
  assert.match(resources, /61074, "\/api\/v1\/Sticker\/UpdateGroupStatus\/\.\+"/);
});

test('R3-C04-E Sticker 后端应绑定租户化分组、排序分组与级联事务', () => {
  const dto = readSource('../../Radish.Model/DtoModels/StickerDto.cs');
  const service = readSource('../../Radish.Service/StickerService.cs');

  assert.match(dto, /class BatchUpdateStickerSortDto[\s\S]*?long GroupId/);
  assert.match(service, /\[UseTran\(Propagation = Propagation\.Required\)\]\s+public async Task<bool> DeleteGroupAsync/);
  assert.match(service, /QueryByIdAsync\(entity\.GroupId\)[\s\S]*?group == null \|\| group\.IsDeleted/);
  assert.match(service, /stickers\.Any\(sticker => sticker\.GroupId != request\.GroupId\)/);
  assert.match(service, /排序快照已失效/);
});

test('R3-C04-E Sticker 列表应冻结非权威写入并提供 PC 与 Mobile 共用快照', () => {
  const groups = readSource('src/pages/Stickers/StickerGroupList.tsx');
  const items = readSource('src/pages/Stickers/StickerList.tsx');

  for (const source of [groups, items]) {
    assert.match(source, /requestSequence\.current !== requestId/);
    assert.match(source, /const actionsAreAuthoritative = readState === 'ready'/);
    assert.match(source, /<ConsoleResourceList/);
    assert.match(source, /console-resource-mobile-card/);
    assert.match(source, /<BottomSheet/);
  }
});

test('R3-C04-E 排序草稿和未绑定附件应有独立离开生命周期', () => {
  const items = readSource('src/pages/Stickers/StickerList.tsx');
  const groupForm = readSource('src/pages/Stickers/StickerGroupForm.tsx');
  const itemForm = readSource('src/pages/Stickers/StickerForm.tsx');
  const batch = readSource('src/pages/Stickers/StickerBatchUploadModal.tsx');

  assert.match(items, /groupId: normalizedGroupId/);
  assert.match(items, /confirmDiscardSortDrafts/);
  assert.match(items, /window\.addEventListener\('beforeunload'/);
  assert.match(items, /setSortDrafts\(\{\}\)/);
  for (const source of [groupForm, itemForm, batch]) {
    assert.match(source, /canSubmit: boolean/);
    assert.match(source, /if \(!canSubmit\)/);
    assert.match(source, /window\.addEventListener\('beforeunload'/);
    assert.match(source, /<BottomSheet/);
  }
  assert.match(batch, /discardUploadedDescription/);
  assert.match(batch, /hasUploadedAttachments/);
  assert.match(batch, /sticker-batch-mobile-card/);
});
