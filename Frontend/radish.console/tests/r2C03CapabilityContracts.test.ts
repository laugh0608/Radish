import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const readConsoleSource = (relativePath: string) => readFileSync(
  new URL(`../${relativePath}`, import.meta.url),
  'utf8',
);

test('R2-C03 只读角色矩阵可达且 Mobile 与内建角色保持只读', () => {
  const routeMetaSource = readConsoleSource('src/router/routeMeta.ts');
  const roleListSource = readConsoleSource('src/pages/Roles/RoleList.tsx');
  const permissionSource = readConsoleSource('src/pages/Roles/RolePermissionPage.tsx');

  assert.match(routeMetaSource, /path: '\/roles\/:roleId\/permissions',[\s\S]*requiredPermission: CONSOLE_PERMISSIONS\.rolesView/);
  assert.match(roleListSource, /canViewRoles \? \([\s\S]*permissions/);
  assert.match(roleListSource, /canEditRole && !isMobile && !record\.voIsBuiltIn/);
  assert.match(roleListSource, /scroll=\{isMobile \? undefined : \{ x: 1200 \}\}/);
  assert.match(roleListSource, /setRoles\(\[\]\);[\s\S]*setLoadError\(errorMessage\)/);
  assert.match(permissionSource, /canEditRole && !isMobile && snapshot\?\.voRoleIsBuiltIn !== true/);
  assert.match(permissionSource, /RoleAuthorization\.VersionConflict/);
  assert.match(permissionSource, /window\.addEventListener\('beforeunload'/);
});

test('R2-C03 系统设置显式确认、冲突草稿和 LongId 字符串契约应保持', () => {
  const formSource = readConsoleSource('src/pages/SystemConfig/SystemConfigForm.tsx');
  const listSource = readConsoleSource('src/pages/SystemConfig/SystemConfigList.tsx');
  const apiSource = readConsoleSource('src/api/systemConfigApi.ts');

  assert.match(formSource, /confirmRiskLevel: values\.confirmRiskLevel\?\.trim\(\)/);
  assert.match(formSource, /confirmKey: values\.confirmKey\?\.trim\(\)/);
  assert.match(formSource, /SystemConfig\.VersionConflict/);
  assert.match(formSource, /setDirty\(true\)/);
  assert.match(formSource, /setConfig\(undefined\);[\s\S]*setLoadError\(errorMessage\)/);
  assert.match(listSource, /isCompactTable && record\.voRiskLevel !== 'Low'/);
  assert.match(listSource, /Modal\.confirm\(\{[\s\S]*systemConfig\.confirm\.restoreTitle/);
  assert.match(listSource, /setConfigs\(\[\]\);[\s\S]*setLoadError\(errorMessage\)/);
  assert.match(listSource, /setHistoryError\(errorMessage\)/);
  assert.match(apiSource, /voId: string;/);
  assert.match(apiSource, /voOperatorUserId\?: string;/);
});

test('R2-C03 Profile 加载失败可重试且显示名长度服从服务端动态规则', () => {
  const profileSource = readConsoleSource('src/pages/UserProfile/UserProfile.tsx');

  assert.match(profileSource, /setLoadError\(errorMessage\)/);
  assert.match(profileSource, /onClick=\{\(\) => void loadProfile\(\)\}/);
  assert.doesNotMatch(profileSource, /min: 2, max: 50/);
});
