import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Button,
  Checkbox,
  Descriptions,
  Space,
  Tag,
  message,
} from '@radish/ui';
import { LeftOutlined, ReloadOutlined, SafetyOutlined } from '@radish/ui';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import {
  getResourceTree,
  getRoleAuthorization,
  getRolePermissionPreview,
  saveRoleAuthorization,
  type ConsoleResourceTreeNodeVo,
  type ResourceApiBindingVo,
  type RoleAuthorizationSnapshotVo,
} from '@/api/consoleAuthorization';
import { log } from '@/utils/logger';
import '../adminFeature.css';
import './RolePermissionPage.css';

interface ResourceIndexItem {
  node: ConsoleResourceTreeNodeVo;
  parentId: string | null;
}

interface NodeVisualState {
  checked: boolean;
  indeterminate: boolean;
}

function deduplicateApiBindings(bindings: ResourceApiBindingVo[]): ResourceApiBindingVo[] {
  const map = new Map<string, ResourceApiBindingVo>();

  bindings.forEach((binding) => {
    map.set(`${binding.voResourceId}:${binding.voApiModuleId}`, binding);
  });

  return Array.from(map.values()).sort((left, right) => {
    if (left.voResourceKey !== right.voResourceKey) {
      return left.voResourceKey.localeCompare(right.voResourceKey, 'zh-CN');
    }

    return left.voLinkUrl.localeCompare(right.voLinkUrl, 'zh-CN');
  });
}

function buildResourceIndex(
  nodes: ConsoleResourceTreeNodeVo[],
  parentId: string | null = null,
  index = new Map<string, ResourceIndexItem>()
): Map<string, ResourceIndexItem> {
  nodes.forEach((node) => {
    index.set(node.voId, { node, parentId });
    buildResourceIndex(node.voChildren ?? [], node.voId, index);
  });

  return index;
}

function collectDescendantIds(node: ConsoleResourceTreeNodeVo): string[] {
  return [
    node.voId,
    ...node.voChildren.flatMap((child) => collectDescendantIds(child)),
  ];
}

function ensureAncestorSelection(selectedIds: Set<string>, resourceIndex: Map<string, ResourceIndexItem>): Set<string> {
  const normalized = new Set(selectedIds);

  normalized.forEach((resourceId) => {
    let currentParentId = resourceIndex.get(resourceId)?.parentId ?? null;

    while (currentParentId && resourceIndex.has(currentParentId)) {
      normalized.add(currentParentId);
      currentParentId = resourceIndex.get(currentParentId)?.parentId ?? null;
    }
  });

  return normalized;
}

function collectPermissionKeys(nodes: ConsoleResourceTreeNodeVo[], selectedIds: Set<string>): string[] {
  const keys = new Set<string>();

  const walk = (currentNodes: ConsoleResourceTreeNodeVo[]) => {
    currentNodes.forEach((node) => {
      if (selectedIds.has(node.voId) && node.voResourceKey) {
        keys.add(node.voResourceKey);
      }

      if (node.voChildren.length > 0) {
        walk(node.voChildren);
      }
    });
  };

  walk(nodes);

  return Array.from(keys).sort((left, right) => left.localeCompare(right, 'zh-CN'));
}

function collectPreviewBindings(nodes: ConsoleResourceTreeNodeVo[], selectedIds: Set<string>): ResourceApiBindingVo[] {
  const bindings: ResourceApiBindingVo[] = [];

  const walk = (currentNodes: ConsoleResourceTreeNodeVo[]) => {
    currentNodes.forEach((node) => {
      if (selectedIds.has(node.voId)) {
        bindings.push(...node.voApiBindings);
      }

      if (node.voChildren.length > 0) {
        walk(node.voChildren);
      }
    });
  };

  walk(nodes);

  return deduplicateApiBindings(bindings);
}

function buildVisualState(
  node: ConsoleResourceTreeNodeVo,
  selectedIds: Set<string>,
  cache: Map<string, NodeVisualState>
): NodeVisualState {
  const cached = cache.get(node.voId);
  if (cached) {
    return cached;
  }

  if (node.voChildren.length <= 0) {
    const leafState = {
      checked: selectedIds.has(node.voId),
      indeterminate: false,
    };
    cache.set(node.voId, leafState);
    return leafState;
  }

  const childStates = node.voChildren.map((child) => buildVisualState(child, selectedIds, cache));
  const checkedChildrenCount = childStates.filter((child) => child.checked).length;
  const hasIndeterminateChild = childStates.some((child) => child.indeterminate);
  const allChildrenChecked = childStates.length > 0 && childStates.every((child) => child.checked);
  const checked = selectedIds.has(node.voId);
  const indeterminate = hasIndeterminateChild || (checkedChildrenCount > 0 && !allChildrenChecked);

  const state = {
    checked,
    indeterminate,
  };
  cache.set(node.voId, state);
  return state;
}

interface ResourceNodeProps {
  node: ConsoleResourceTreeNodeVo;
  level: number;
  selectedIds: Set<string>;
  onToggle: (node: ConsoleResourceTreeNodeVo, checked: boolean) => void;
  visualStateCache: Map<string, NodeVisualState>;
}

function ResourceNode({
  node,
  level,
  selectedIds,
  onToggle,
  visualStateCache,
}: ResourceNodeProps) {
  const state = buildVisualState(node, selectedIds, visualStateCache);

  return (
    <div className="role-permission-node">
      <div
        className="role-permission-node__row"
        style={{ paddingLeft: `${level * 24}px` }}
      >
        <Checkbox
          checked={state.checked}
          indeterminate={state.indeterminate}
          onChange={(event) => onToggle(node, event.target.checked)}
        >
          <span className="role-permission-node__title">{node.voTitle}</span>
        </Checkbox>
        <Space size={[8, 8]} wrap>
          <Tag color="processing">{node.voResourceType}</Tag>
          <Tag>{node.voResourceKey}</Tag>
          {node.voApiBindings.length > 0 ? (
            <Tag color="success">{node.voApiBindings.length} 个接口</Tag>
          ) : (
            <Tag>无接口映射</Tag>
          )}
        </Space>
      </div>

      {node.voApiBindings.length > 0 ? (
        <div
          className="role-permission-node__apis"
          style={{ paddingLeft: `${level * 24 + 28}px` }}
        >
          {node.voApiBindings.map((binding) => (
            <code key={`${binding.voResourceId}:${binding.voApiModuleId}`}>
              {binding.voLinkUrl}
            </code>
          ))}
        </div>
      ) : null}

      {node.voChildren.length > 0 ? (
        <div className="role-permission-node__children">
          {node.voChildren.map((child) => (
            <ResourceNode
              key={child.voId}
              node={child}
              level={level + 1}
              selectedIds={selectedIds}
              onToggle={onToggle}
              visualStateCache={visualStateCache}
            />
          ))}
        </div>
      ) : null}
    </div>
  );
}

export const RolePermissionPage = () => {
  useDocumentTitle('角色权限配置');

  const navigate = useNavigate();
  const { roleId } = useParams<{ roleId: string }>();
  const canEditRole = usePermission(CONSOLE_PERMISSIONS.rolesEdit);
  const normalizedRoleId = roleId?.trim();

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [resourceTree, setResourceTree] = useState<ConsoleResourceTreeNodeVo[]>([]);
  const [snapshot, setSnapshot] = useState<RoleAuthorizationSnapshotVo | null>(null);
  const [savedPreview, setSavedPreview] = useState<ResourceApiBindingVo[]>([]);
  const [selectedResourceIds, setSelectedResourceIds] = useState<string[]>([]);

  const resourceIndex = useMemo(() => buildResourceIndex(resourceTree), [resourceTree]);
  const selectedResourceIdSet = useMemo(() => new Set(selectedResourceIds), [selectedResourceIds]);
  const permissionKeys = useMemo(
    () => collectPermissionKeys(resourceTree, selectedResourceIdSet),
    [resourceTree, selectedResourceIdSet]
  );
  const livePreview = useMemo(
    () => collectPreviewBindings(resourceTree, selectedResourceIdSet),
    [resourceTree, selectedResourceIdSet]
  );

  const isDirty = useMemo(() => {
    if (!snapshot) {
      return false;
    }

    const currentIds = [...selectedResourceIds].sort((left, right) => left.localeCompare(right, 'zh-CN'));
    const savedIds = [...snapshot.voGrantedResourceIds].sort((left, right) => left.localeCompare(right, 'zh-CN'));

    if (currentIds.length !== savedIds.length) {
      return true;
    }

    return currentIds.some((item, index) => item !== savedIds[index]);
  }, [selectedResourceIds, snapshot]);

  const loadAuthorization = async () => {
    if (!normalizedRoleId || !/^[1-9]\d*$/u.test(normalizedRoleId)) {
      message.error('角色 ID 无效');
      navigate('/roles', { replace: true });
      return;
    }

    try {
      setLoading(true);
      const [tree, currentSnapshot, preview] = await Promise.all([
        getResourceTree(),
        getRoleAuthorization(normalizedRoleId),
        getRolePermissionPreview(normalizedRoleId),
      ]);

      setResourceTree(tree);
      setSnapshot(currentSnapshot);
      setSavedPreview(preview);
      setSelectedResourceIds(currentSnapshot.voGrantedResourceIds ?? []);
    } catch (error) {
      log.error('RolePermissionPage', '加载角色授权信息失败:', error);
      message.error(error instanceof Error ? error.message : '加载角色授权信息失败');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadAuthorization();
  }, [normalizedRoleId]);

  const handleToggleNode = (node: ConsoleResourceTreeNodeVo, checked: boolean) => {
    const nextSelectedIds = new Set(selectedResourceIds);

    if (checked) {
      nextSelectedIds.add(node.voId);
      const normalized = ensureAncestorSelection(nextSelectedIds, resourceIndex);
      setSelectedResourceIds(Array.from(normalized).sort((left, right) => left.localeCompare(right, 'zh-CN')));
    } else {
      const descendants = collectDescendantIds(node);
      descendants.forEach((resourceId) => nextSelectedIds.delete(resourceId));
      setSelectedResourceIds(Array.from(nextSelectedIds).sort((left, right) => left.localeCompare(right, 'zh-CN')));
    }
  };

  const handleSave = async () => {
    if (!snapshot) {
      return;
    }

    try {
      setSaving(true);
      await saveRoleAuthorization({
        roleId: snapshot.voRoleId,
        resourceIds: [...selectedResourceIds].sort((left, right) => left.localeCompare(right, 'zh-CN')),
        expectedModifyTime: snapshot.voLastModifyTime,
      });
      message.success('角色权限保存成功');
      await loadAuthorization();
    } catch (error) {
      log.error('RolePermissionPage', '保存角色权限失败:', error);
      message.error(error instanceof Error ? error.message : '保存角色权限失败');
    } finally {
      setSaving(false);
    }
  };

  const visualStateCache = useMemo(() => new Map<string, NodeVisualState>(), [selectedResourceIds]);

  return (
    <div className="admin-feature-page role-permission-page">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div className="role-permission-page__heading">
            <Button icon={<LeftOutlined />} onClick={() => navigate('/roles')}>
              返回角色列表
            </Button>
            <div>
              <h2>
                <SafetyOutlined /> 角色权限配置
              </h2>
              <p className="admin-feature-subtle">维护 Console 菜单、按钮与接口的第一阶段授权资源。</p>
            </div>
          </div>

          <div className="role-permission-page__actions">
            <Button
              icon={<ReloadOutlined />}
              onClick={() => {
                void loadAuthorization();
              }}
            >
              刷新
            </Button>
            <Button
              variant="primary"
              icon={<SafetyOutlined />}
              onClick={() => {
                void handleSave();
              }}
              disabled={!canEditRole || !isDirty}
            >
              {saving ? '保存中...' : '保存权限'}
            </Button>
          </div>
        </div>
      </section>

      <section className="admin-feature-metrics" aria-label="角色权限指标">
        <div className="admin-feature-metric">
          已选资源
          <strong>{selectedResourceIds.length}</strong>
        </div>
        <div className="admin-feature-metric">
          权限键
          <strong>{permissionKeys.length}</strong>
        </div>
        <div className="admin-feature-metric">
          接口映射
          <strong>{livePreview.length}</strong>
        </div>
        <div className="admin-feature-metric">
          保存状态
          <strong>{isDirty ? '待保存' : '已同步'}</strong>
        </div>
      </section>

      <section className="admin-feature-card">
        <div className="role-permission-page__role-card">
          <Descriptions
            title="角色信息"
            column={2}
            items={[
              {
                key: 'roleName',
                label: '角色名称',
                children: snapshot?.voRoleName ?? '-',
              },
              {
                key: 'roleStatus',
                label: '状态',
                children: (
                  <Tag color={snapshot?.voRoleIsEnabled ? 'success' : 'error'}>
                    {snapshot?.voRoleIsEnabled ? '启用' : '禁用'}
                  </Tag>
                ),
              },
              {
                key: 'roleDescription',
                label: '角色描述',
                children: snapshot?.voRoleDescription || '-',
              },
              {
                key: 'lastModifyTime',
                label: '最近授权时间',
                children: snapshot?.voLastModifyTime
                  ? new Date(snapshot.voLastModifyTime).toLocaleString('zh-CN')
                  : '暂无',
              },
            ]}
          />
        </div>
      </section>

      <div className="role-permission-page__content">
        <section className="role-permission-panel">
          <div className="role-permission-panel__header">
            <div>
              <h3>资源树</h3>
              <p>勾选页面资源后，会自动继承菜单显示与后端接口映射。</p>
            </div>
            {loading ? <Tag color="processing">加载中</Tag> : null}
          </div>

          <div className="role-permission-panel__body">
            {resourceTree.map((node) => (
              <ResourceNode
                key={node.voId}
                node={node}
                level={0}
                selectedIds={selectedResourceIdSet}
                onToggle={handleToggleNode}
                visualStateCache={visualStateCache}
              />
            ))}
          </div>
        </section>

        <section className="role-permission-panel role-permission-panel--preview">
          <div className="role-permission-panel__header">
            <div>
              <h3>权限预览</h3>
              <p>当前勾选结果会实时汇总出页面按钮权限与接口授权范围。</p>
            </div>
            {isDirty ? <Tag color="warning">有未保存变更</Tag> : <Tag color="success">已同步</Tag>}
          </div>

          <div className="role-permission-preview">
            <div className="role-permission-preview__section">
              <h4>权限键</h4>
              <div className="role-permission-preview__tags">
                {permissionKeys.length > 0 ? permissionKeys.map((key) => (
                  <Tag key={key}>{key}</Tag>
                )) : <span className="role-permission-preview__empty">暂无权限键</span>}
              </div>
            </div>

            <div className="role-permission-preview__section">
              <h4>接口映射</h4>
              <div className="role-permission-preview__apis">
                {livePreview.length > 0 ? livePreview.map((binding) => (
                  <div
                    key={`${binding.voResourceId}:${binding.voApiModuleId}`}
                    className="role-permission-preview__api-item"
                  >
                    <div>
                      <strong>{binding.voApiModuleName}</strong>
                      <span>{binding.voResourceKey}</span>
                    </div>
                    <code>{binding.voLinkUrl}</code>
                  </div>
                )) : <span className="role-permission-preview__empty">暂无接口映射</span>}
              </div>
            </div>

            <div className="role-permission-preview__section">
              <h4>已生效快照</h4>
              <div className="role-permission-preview__apis">
                {savedPreview.length > 0 ? savedPreview.map((binding) => (
                  <div
                    key={`saved:${binding.voResourceId}:${binding.voApiModuleId}`}
                    className="role-permission-preview__api-item"
                  >
                    <div>
                      <strong>{binding.voApiModuleName}</strong>
                      <span>{binding.voResourceKey}</span>
                    </div>
                    <code>{binding.voLinkUrl}</code>
                  </div>
                )) : <span className="role-permission-preview__empty">当前角色尚未保存授权</span>}
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  );
};
