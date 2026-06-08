import assert from 'node:assert/strict';
import test from 'node:test';
import type { ConsoleResourceTreeNodeVo, RoleAuthorizationSnapshotVo } from '../src/api/consoleAuthorization.ts';
import {
  buildNextSelectedResourceIds,
  buildResourceIndex,
  buildRoleAuthorizationSavePayload,
  collectPermissionKeys,
  collectPreviewBindings,
  ensureAncestorSelection,
  isValidRoleId,
  shouldDisableRoleAuthorizationSave,
  shouldDisableRoleAuthorizationToggle,
} from '../src/pages/Roles/rolePermissionHelpers.ts';

const resourceTree: ConsoleResourceTreeNodeVo[] = [
  {
    voId: '2042219067430928384',
    voTitle: '系统管理',
    voResourceKey: 'System',
    voResourceType: 'Menu',
    voChecked: false,
    voIndeterminate: false,
    voApiBindings: [],
    voChildren: [
      {
        voId: '2042219067430928385',
        voTitle: '用户管理',
        voResourceKey: 'User:View',
        voResourceType: 'Page',
        voChecked: false,
        voIndeterminate: false,
        voApiBindings: [
          {
            voResourceId: '2042219067430928385',
            voResourceKey: 'User:View',
            voApiModuleId: '2042219067430928390',
            voApiModuleName: '查询用户',
            voLinkUrl: '/api/v1/User/GetList',
            voRelationType: 'Required',
          },
        ],
        voChildren: [],
      },
      {
        voId: '2042219067430928386',
        voTitle: '角色授权',
        voResourceKey: 'Role:Permission',
        voResourceType: 'Page',
        voChecked: false,
        voIndeterminate: false,
        voApiBindings: [
          {
            voResourceId: '2042219067430928386',
            voResourceKey: 'Role:Permission',
            voApiModuleId: '2042219067430928391',
            voApiModuleName: '保存授权',
            voLinkUrl: '/api/v1/ConsoleAuthorization/SaveRoleAuthorization',
            voRelationType: 'Required',
          },
          {
            voResourceId: '2042219067430928386',
            voResourceKey: 'Role:Permission',
            voApiModuleId: '2042219067430928391',
            voApiModuleName: '保存授权',
            voLinkUrl: '/api/v1/ConsoleAuthorization/SaveRoleAuthorization',
            voRelationType: 'Required',
          },
        ],
        voChildren: [],
      },
    ],
  },
];

test('角色授权 helper 应以字符串保留资源祖先链路', () => {
  const resourceIndex = buildResourceIndex(resourceTree);
  const selectedIds = ensureAncestorSelection(
    new Set(['2042219067430928386']),
    resourceIndex,
  );

  assert.deepEqual(
    Array.from(selectedIds).sort((left, right) => left.localeCompare(right, 'zh-CN')),
    ['2042219067430928384', '2042219067430928386'],
  );
});

test('角色授权勾选父级应选中整棵子树并保留字符串 ID', () => {
  const selectedIds = buildNextSelectedResourceIds({
    currentSelectedIds: [],
    node: resourceTree[0],
    checked: true,
    resourceIndex: buildResourceIndex(resourceTree),
  });

  assert.deepEqual(selectedIds, [
    '2042219067430928384',
    '2042219067430928385',
    '2042219067430928386',
  ]);
});

test('角色授权取消子级应清理不再需要的父级继承资源', () => {
  const resourceIndex = buildResourceIndex(resourceTree);
  const selectedIds = buildNextSelectedResourceIds({
    currentSelectedIds: [
      '2042219067430928384',
      '2042219067430928386',
    ],
    node: resourceTree[0].voChildren[1],
    checked: false,
    resourceIndex,
  });

  assert.deepEqual(selectedIds, []);
});

test('角色授权取消部分子级应保留仍被其他子级依赖的父级资源', () => {
  const resourceIndex = buildResourceIndex(resourceTree);
  const selectedIds = buildNextSelectedResourceIds({
    currentSelectedIds: [
      '2042219067430928384',
      '2042219067430928385',
      '2042219067430928386',
    ],
    node: resourceTree[0].voChildren[1],
    checked: false,
    resourceIndex,
  });

  assert.deepEqual(selectedIds, [
    '2042219067430928384',
    '2042219067430928385',
  ]);
});

test('角色授权预览应按字符串资源与接口 ID 去重', () => {
  const selectedIds = new Set(['2042219067430928384', '2042219067430928386']);
  const bindings = collectPreviewBindings(resourceTree, selectedIds);

  assert.equal(bindings.length, 1);
  assert.equal(bindings[0].voResourceId, '2042219067430928386');
  assert.equal(bindings[0].voApiModuleId, '2042219067430928391');
  assert.deepEqual(collectPermissionKeys(resourceTree, selectedIds), [
    'Role:Permission',
    'System',
  ]);
});

test('角色授权保存 payload 应保留 LongId 字符串并稳定排序', () => {
  const snapshot: RoleAuthorizationSnapshotVo = {
    voRoleId: '2042219067430928400',
    voRoleName: '管理员',
    voRoleDescription: '系统管理员',
    voRoleIsEnabled: true,
    voLastModifyTime: '2026-06-06T08:00:00Z',
    voGrantedResourceIds: [],
    voGrantedPermissionKeys: [],
    voDerivedApiModules: [],
  };

  const payload = buildRoleAuthorizationSavePayload(snapshot, [
    '2042219067430928386',
    '2042219067430928384',
    '2042219067430928386',
  ]);

  assert.deepEqual(payload, {
    roleId: '2042219067430928400',
    resourceIds: ['2042219067430928384', '2042219067430928386'],
    expectedModifyTime: '2026-06-06T08:00:00Z',
  });
});

test('角色授权保存按钮应在保存中禁用以避免重复提交', () => {
  assert.equal(isValidRoleId('2042219067430928400'), true);
  assert.equal(isValidRoleId('2042219067430928400.1'), false);
  assert.equal(
    shouldDisableRoleAuthorizationSave({
      canEditRole: true,
      isDirty: true,
      loading: false,
      saving: true,
    }),
    true,
  );
  assert.equal(
    shouldDisableRoleAuthorizationSave({
      canEditRole: true,
      isDirty: true,
      loading: false,
      saving: false,
    }),
    false,
  );
});

test('角色授权勾选应在加载保存或无权限时禁用', () => {
  assert.equal(
    shouldDisableRoleAuthorizationToggle({
      canEditRole: false,
      loading: false,
      saving: false,
    }),
    true,
  );
  assert.equal(
    shouldDisableRoleAuthorizationToggle({
      canEditRole: true,
      loading: true,
      saving: false,
    }),
    true,
  );
  assert.equal(
    shouldDisableRoleAuthorizationToggle({
      canEditRole: true,
      loading: false,
      saving: true,
    }),
    true,
  );
  assert.equal(
    shouldDisableRoleAuthorizationToggle({
      canEditRole: true,
      loading: false,
      saving: false,
    }),
    false,
  );
});
