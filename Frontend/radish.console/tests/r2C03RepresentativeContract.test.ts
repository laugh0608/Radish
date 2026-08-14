import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const readConsoleSource = (relativePath: string) => readFileSync(
  new URL(`../${relativePath}`, import.meta.url),
  'utf8',
);

test('R2-C03 Mobile 角色目录应保持连续只读选择路径', () => {
  const source = readConsoleSource('src/pages/Roles/RoleList.tsx');

  assert.match(source, /if \(isMobile\) \{[\s\S]*role-list-mobile-directory/);
  assert.match(source, /roles\.map\(\(role\) => \([\s\S]*roles\.actions\.viewPermissions/);
  assert.match(source, /role\.voIsBuiltIn[\s\S]*roles\.mobile\.builtInProtection/);
  assert.match(source, /roles\.mobile\.boundaryDescription/);
  assert.doesNotMatch(
    source.slice(source.indexOf('if (isMobile) {'), source.indexOf('\n  return (', source.indexOf('if (isMobile) {'))),
    /handleCreate|handleEdit|handleToggleStatus|handleDelete/,
  );
});

test('R2-C03 Mobile 权限详情应展示角色上下文、键值含义与只读结果', () => {
  const source = readConsoleSource('src/pages/Roles/RolePermissionPage.tsx');
  const mobileBranchStart = source.indexOf('if (isMobile) {');
  const desktopBranchStart = source.indexOf('\n  return (', mobileBranchStart);
  const mobileBranch = source.slice(mobileBranchStart, desktopBranchStart);

  assert.match(source, /function getPermissionMeaning/);
  assert.match(mobileBranch, /role-permission-mobile-context/);
  assert.match(mobileBranch, /role-permission-mobile-key__copy/);
  assert.match(mobileBranch, /rolePermissions\.mobile\.allowed/);
  assert.match(mobileBranch, /rolePermissions\.mobile\.notGranted/);
  assert.doesNotMatch(mobileBranch, /handleSave|saveRoleAuthorization|Checkbox/);
});

test('R2-C03 只读 Operator 与内建角色不应得到授权保存表面', () => {
  const source = readConsoleSource('src/pages/Roles/RolePermissionPage.tsx');

  assert.match(source, /canMutateRole \? \([\s\S]*rolePermissions\.actions\.save/);
  assert.match(source, /snapshot\?\.voRoleIsBuiltIn[\s\S]*role-permission-protected-role/);
  assert.match(source, /rolePermissions\.protection\.description/);
});

test('R2-C03 Mobile 系统设置应只开放 Low 编辑并复用 BottomSheet', () => {
  const listSource = readConsoleSource('src/pages/SystemConfig/SystemConfigList.tsx');
  const formSource = readConsoleSource('src/pages/SystemConfig/SystemConfigForm.tsx');

  assert.match(listSource, /record\.voRiskLevel === 'Low'[\s\S]*systemConfig\.mobile\.editLow/);
  assert.match(listSource, /systemConfig\.mobile\.mediumPcOnly/);
  assert.match(listSource, /system-config-mobile-boundary/);
  assert.match(formSource, /<BottomSheet/);
  assert.match(formSource, /closeOnOverlayClick=\{false\}/);
  assert.match(formSource, /isMobile && config\.voRiskLevel !== 'Low'/);
  assert.match(formSource, /mobileRiskBlocked/);
  assert.match(formSource, /systemConfig\.confirm\.lowTitle/);
});

test('R2-C03 设置编辑应显式展示 dirty、CAS、409 与 Medium 确认', () => {
  const source = readConsoleSource('src/pages/SystemConfig/SystemConfigForm.tsx');

  assert.match(source, /system-config-dirty-note/);
  assert.match(source, /system-config-cas-note/);
  assert.match(source, /expectedVersion: config\.voVersion/);
  assert.match(source, /SystemConfig\.VersionConflict/);
  assert.match(source, /confirmRiskLevel/);
  assert.match(source, /confirmKey/);
  assert.match(source, /requestReloadAuthority/);
  assert.doesNotMatch(source, /config\?\.voRiskLevel !== 'Low'/);
});
