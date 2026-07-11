import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
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
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import {
  getResourceTree,
  getRoleAuthorization,
  getRolePermissionPreview,
  saveRoleAuthorization,
  type ConsoleResourceTreeNodeVo,
  type ResourceApiBindingVo,
  type RoleAuthorizationSnapshotVo,
} from '@/api/consoleAuthorization';
import {
  buildResourceIndex,
  buildNextSelectedResourceIds,
  buildRoleAuthorizationSavePayload,
  buildVisualState,
  collectPermissionKeys,
  collectPreviewBindings,
  isValidRoleId,
  shouldDisableRoleAuthorizationSave,
  shouldDisableRoleAuthorizationToggle,
  sortResourceIds,
  type NodeVisualState,
} from './rolePermissionHelpers';
import { log } from '@/utils/logger';
import '../adminFeature.css';
import './RolePermissionPage.css';

function formatAuthorizationTime(value?: string | null): string {
  if (!value) {
    return '暂无快照';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString('zh-CN');
}

function countResourceNodes(nodes: ConsoleResourceTreeNodeVo[]): number {
  return nodes.reduce((total, node) => total + 1 + countResourceNodes(node.voChildren), 0);
}

function countLeafResourceNodes(nodes: ConsoleResourceTreeNodeVo[]): number {
  return nodes.reduce((total, node) => {
    if (node.voChildren.length <= 0) {
      return total + 1;
    }

    return total + countLeafResourceNodes(node.voChildren);
  }, 0);
}

function collectChangedResourceTitles(
  resourceIndex: Map<string, { node: ConsoleResourceTreeNodeVo }>,
  resourceIds: string[]
): string[] {
  return resourceIds
    .map((resourceId) => resourceIndex.get(resourceId)?.node.voTitle)
    .filter((title): title is string => Boolean(title))
    .slice(0, 6);
}

function collectSensitivePermissionKeys(permissionKeys: string[]): string[] {
  const sensitiveKeywords = ['delete', 'adjust', 'rollback', 'permissions', 'hangfire', 'toggle'];

  return permissionKeys
    .filter((key) => sensitiveKeywords.some((keyword) => key.toLowerCase().includes(keyword)))
    .slice(0, 8);
}

interface ResourceNodeProps {
  node: ConsoleResourceTreeNodeVo;
  level: number;
  selectedIds: Set<string>;
  onToggle: (node: ConsoleResourceTreeNodeVo, checked: boolean) => void;
  visualStateCache: Map<string, NodeVisualState>;
  disabled: boolean;
}

function ResourceNode({
  node,
  level,
  selectedIds,
  onToggle,
  visualStateCache,
  disabled,
}: ResourceNodeProps) {
  const state = buildVisualState(node, selectedIds, visualStateCache);

  return (
    <div className="role-permission-node">
      <div className="role-permission-node__row">
        <Checkbox
          checked={state.checked}
          indeterminate={state.indeterminate}
          disabled={disabled}
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
        <div className="role-permission-node__apis">
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
              disabled={disabled}
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
  const savingRequestRef = useRef(false);

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
  const savedResourceIds = useMemo(
    () => sortResourceIds(snapshot?.voGrantedResourceIds ?? []),
    [snapshot]
  );
  const savedResourceIdSet = useMemo(() => new Set(savedResourceIds), [savedResourceIds]);
  const addedResourceIds = useMemo(
    () => selectedResourceIds.filter((resourceId) => !savedResourceIdSet.has(resourceId)),
    [selectedResourceIds, savedResourceIdSet]
  );
  const removedResourceIds = useMemo(
    () => savedResourceIds.filter((resourceId) => !selectedResourceIdSet.has(resourceId)),
    [savedResourceIds, selectedResourceIdSet]
  );
  const changedResourceTitles = useMemo(
    () => [
      ...collectChangedResourceTitles(resourceIndex, addedResourceIds),
      ...collectChangedResourceTitles(resourceIndex, removedResourceIds),
    ].slice(0, 6),
    [addedResourceIds, removedResourceIds, resourceIndex]
  );
  const sensitivePermissionKeys = useMemo(
    () => collectSensitivePermissionKeys(permissionKeys),
    [permissionKeys]
  );
  const resourceCount = useMemo(() => countResourceNodes(resourceTree), [resourceTree]);
  const leafResourceCount = useMemo(() => countLeafResourceNodes(resourceTree), [resourceTree]);
  const snapshotTimeText = formatAuthorizationTime(snapshot?.voLastModifyTime);
  const changedResourceCount = addedResourceIds.length + removedResourceIds.length;
  const liveApiDelta = livePreview.length - savedPreview.length;

  const isDirty = useMemo(() => {
    if (!snapshot) {
      return false;
    }

    const currentIds = sortResourceIds(selectedResourceIds);
    const savedIds = sortResourceIds(snapshot.voGrantedResourceIds);

    if (currentIds.length !== savedIds.length) {
      return true;
    }

    return currentIds.some((item, index) => item !== savedIds[index]);
  }, [selectedResourceIds, snapshot]);

  const saveDisabled = shouldDisableRoleAuthorizationSave({
    canEditRole,
    isDirty,
    loading,
    saving,
  });
  const toggleDisabled = shouldDisableRoleAuthorizationToggle({
    canEditRole,
    loading,
    saving,
  });

  const loadAuthorization = useCallback(async () => {
    if (!isValidRoleId(normalizedRoleId)) {
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
      setSelectedResourceIds(sortResourceIds(currentSnapshot.voGrantedResourceIds ?? []));
    } catch (error) {
      log.error('RolePermissionPage', '加载角色授权信息失败:', error);
      message.error(error instanceof Error ? error.message : '加载角色授权信息失败');
    } finally {
      setLoading(false);
    }
  }, [navigate, normalizedRoleId]);

  useEffect(() => {
    void loadAuthorization();
  }, [loadAuthorization]);

  const handleToggleNode = (node: ConsoleResourceTreeNodeVo, checked: boolean) => {
    if (toggleDisabled) {
      return;
    }

    setSelectedResourceIds(buildNextSelectedResourceIds({
      currentSelectedIds: selectedResourceIds,
      node,
      checked,
      resourceIndex,
    }));
  };

  const handleSave = async () => {
    if (saveDisabled) {
      return;
    }

    if (!snapshot) {
      return;
    }

    if (savingRequestRef.current || saving) {
      return;
    }

    try {
      savingRequestRef.current = true;
      setSaving(true);
      await saveRoleAuthorization(buildRoleAuthorizationSavePayload(snapshot, selectedResourceIds));
      message.success('角色权限保存成功');
      await loadAuthorization();
    } catch (error) {
      log.error('RolePermissionPage', '保存角色权限失败:', error);
      message.error(error instanceof Error ? error.message : '保存角色权限失败');
    } finally {
      savingRequestRef.current = false;
      setSaving(false);
    }
  };

  const visualStateCache = new Map<string, NodeVisualState>();

  return (
    <div className="admin-feature-page role-permission-page">
      <ConsolePageHeader
        eyebrow="权限矩阵"
        title="角色权限配置"
        description="维护 Console 菜单、按钮与接口的第一阶段授权资源。"
        icon={<SafetyOutlined />}
        status={(
          <ConsoleStatusChip tone={isDirty ? 'warning' : 'success'}>
            {isDirty ? '待保存' : '已同步'}
          </ConsoleStatusChip>
        )}
        actions={(
          <div className="role-permission-page__actions">
            <Button icon={<LeftOutlined />} onClick={() => navigate('/roles')}>
              返回角色列表
            </Button>
            <Button
              icon={<ReloadOutlined />}
              disabled={loading || saving}
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
              disabled={saveDisabled}
            >
              {saving ? '保存中...' : '保存权限'}
            </Button>
          </div>
        )}
      />

      <ConsoleMetricGrid label="角色权限指标">
        <ConsoleMetricCard label="已选资源" value={selectedResourceIds.length} description="当前勾选授权资源" tone="info" />
        <ConsoleMetricCard label="权限键" value={permissionKeys.length} description="实时汇总权限键" />
        <ConsoleMetricCard label="接口映射" value={livePreview.length} description="当前勾选关联接口" tone="success" />
        <ConsoleMetricCard
          label="授权快照"
          value={isDirty ? `${changedResourceCount} 项变更` : '已同步'}
          description={snapshot ? `基于 ${snapshotTimeText}` : '等待加载角色授权'}
          tone={isDirty ? 'warning' : 'success'}
        />
      </ConsoleMetricGrid>

      <section className="governance-task-flow" aria-label="角色权限矩阵任务流">
        <div className="governance-task-flow__item">
          <span>1</span>
          <strong>角色快照</strong>
          <p>{snapshot ? `${snapshot.voRoleName}，保存时携带 ${snapshotTimeText}。` : '加载角色后形成授权快照。'}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>2</span>
          <strong>资源矩阵</strong>
          <p>{resourceTree.length} 个根资源、{leafResourceCount} 个叶子资源，当前勾选 {selectedResourceIds.length} 项。</p>
        </div>
        <div className="governance-task-flow__item">
          <span>3</span>
          <strong>授权预览</strong>
          <p>{permissionKeys.length} 个权限键、{livePreview.length} 个接口映射会随勾选实时变化。</p>
        </div>
        <div className="governance-task-flow__item">
          <span>4</span>
          <strong>保存反馈</strong>
          <p>{canEditRole ? (isDirty ? '存在未保存变更，提交后刷新最新快照。' : '当前授权与已生效快照一致。') : '当前账号仅可查看授权。'}</p>
        </div>
      </section>

      <ConsoleToolbar
        title="角色上下文"
        description="确认当前角色状态、描述和最近授权时间，再调整资源树。"
        meta={(
          <ConsoleStatusChip tone={snapshot?.voRoleIsEnabled ? 'success' : 'neutral'}>
            {snapshot ? (snapshot.voRoleIsEnabled ? '角色启用' : '角色禁用') : '未加载角色'}
          </ConsoleStatusChip>
        )}
      >
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
      </ConsoleToolbar>

      <div className="role-permission-workspace">
        <main className="role-permission-workspace__main">
          <div className="role-permission-page__content">
            <section className="role-permission-panel">
              <div className="role-permission-panel__header">
                <div>
                  <h3>权限矩阵</h3>
                  <p>勾选页面资源后，会自动继承菜单显示与后端接口映射。</p>
                </div>
                <Space size={[8, 8]} wrap>
                  {loading ? <Tag color="processing">加载中</Tag> : null}
                  <Tag color={isDirty ? 'warning' : 'success'}>{isDirty ? '旧快照待提交' : '快照同步'}</Tag>
                </Space>
              </div>

              <div className="role-permission-matrix-summary">
                <span>{resourceCount} 个资源节点</span>
                <span>{leafResourceCount} 个叶子资源</span>
                <span>{selectedResourceIds.length} 项当前授权</span>
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
                    disabled={toggleDisabled}
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
        </main>

        <aside className="admin-feature-rail role-permission-rail" aria-label="角色授权证据">
          <div className="admin-feature-rail__header">
            <div>
              <span className="admin-feature-rail__eyebrow">授权快照</span>
              <h3>授权证据</h3>
            </div>
            <ConsoleStatusChip tone={isDirty ? 'warning' : 'success'}>
              {isDirty ? '待保存' : '已同步'}
            </ConsoleStatusChip>
          </div>
          <p className="admin-feature-subtle">保存动作继续使用当前授权快照时间，服务端检测到旧快照时会拒绝写入并要求刷新。</p>

          <div className="admin-feature-rail__list">
            <div className="admin-feature-rail__item">
              <span>角色</span>
              <strong>{snapshot?.voRoleName ?? normalizedRoleId ?? '-'}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>快照时间</span>
              <strong>{snapshotTimeText}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>变更资源</span>
              <strong>{changedResourceCount > 0 ? `${changedResourceCount} 项` : '无变更'}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>接口变化</span>
              <strong>{liveApiDelta === 0 ? '无净变化' : `${liveApiDelta > 0 ? '+' : ''}${liveApiDelta} 个映射`}</strong>
            </div>
          </div>

          <div className="admin-feature-rail__callout">
            <span>保存状态</span>
            <strong>{canEditRole ? (saveDisabled ? '等待有效变更' : '可以保存授权') : '仅可查看'}</strong>
            <p>{isDirty ? '保存后会重新读取资源树、角色快照和已生效接口映射。' : '当前页面展示的是已生效授权快照。'}</p>
          </div>

          <div className="role-permission-rail__section">
            <h4>变更线索</h4>
            <div className="role-permission-rail__tags">
              {changedResourceTitles.length > 0 ? changedResourceTitles.map((title) => (
                <Tag key={title} color="warning">{title}</Tag>
              )) : <span>暂无资源变更</span>}
            </div>
          </div>

          <div className="role-permission-rail__section">
            <h4>敏感权限线索</h4>
            <div className="role-permission-rail__tags">
              {sensitivePermissionKeys.length > 0 ? sensitivePermissionKeys.map((key) => (
                <Tag key={key} color="error">{key}</Tag>
              )) : <span>当前勾选未命中敏感权限关键字</span>}
            </div>
          </div>

          <div className="admin-feature-inline-context">
            <strong>权限边界</strong>
            <span>本页只维护角色与 Console 授权资源关系，不新增权限键、资源种子、接口映射或审批流。</span>
          </div>
        </aside>
      </div>
    </div>
  );
};
