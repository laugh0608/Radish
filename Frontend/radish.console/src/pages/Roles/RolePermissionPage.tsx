import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import {
  Button,
  Checkbox,
  Descriptions,
  AntModal as Modal,
  Space,
  Tag,
  message,
} from '@radish/ui';
import { Grid } from 'antd';
import { ApiResponseError } from '@radish/http';
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
import { formatConsoleDateTime } from '@/utils/localeFormatters';
import '../adminFeature.css';
import './RolePermissionPage.css';

function formatAuthorizationTime(
  value: string | null | undefined,
  language: string,
  t: TFunction,
): string {
  if (!value) {
    return t('rolePermissions.snapshot.none');
  }
  return formatConsoleDateTime(value, language);
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

function collectResourceNodes(node: ConsoleResourceTreeNodeVo): ConsoleResourceTreeNodeVo[] {
  return [
    node,
    ...node.voChildren.flatMap((child) => collectResourceNodes(child)),
  ];
}

function getPermissionMeaning(node: ConsoleResourceTreeNodeVo, t: TFunction): string {
  return t(`rolePermissions.meaning.${node.voResourceKey}`, {
    defaultValue: node.voTitle,
  });
}

interface ResourceNodeProps {
  node: ConsoleResourceTreeNodeVo;
  level: number;
  selectedIds: Set<string>;
  onToggle: (node: ConsoleResourceTreeNodeVo, checked: boolean) => void;
  visualStateCache: Map<string, NodeVisualState>;
  disabled: boolean;
  t: TFunction;
}

function ResourceNode({
  node,
  level,
  selectedIds,
  onToggle,
  visualStateCache,
  disabled,
  t,
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
            <Tag color="success">{t('rolePermissions.count.apis', { count: node.voApiBindings.length })}</Tag>
          ) : (
            <Tag>{t('rolePermissions.matrix.noApi')}</Tag>
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
              t={t}
            />
          ))}
        </div>
      ) : null}
    </div>
  );
}

export const RolePermissionPage = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('rolePermissions.documentTitle'));

  const navigate = useNavigate();
  const { roleId } = useParams<{ roleId: string }>();
  const canEditRole = usePermission(CONSOLE_PERMISSIONS.rolesEdit);
  const normalizedRoleId = roleId?.trim();
  const screens = Grid.useBreakpoint();
  const isMobile = !screens.md;

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [resourceTree, setResourceTree] = useState<ConsoleResourceTreeNodeVo[]>([]);
  const [snapshot, setSnapshot] = useState<RoleAuthorizationSnapshotVo | null>(null);
  const [savedPreview, setSavedPreview] = useState<ResourceApiBindingVo[]>([]);
  const [selectedResourceIds, setSelectedResourceIds] = useState<string[]>([]);
  const [loadError, setLoadError] = useState<string>();
  const [versionConflict, setVersionConflict] = useState(false);
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
  const mobileGrantedResourceIdSet = useMemo(() => new Set(savedResourceIds), [savedResourceIds]);
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
  const snapshotTimeText = formatAuthorizationTime(snapshot?.voLastModifyTime, language, t);
  const changedResourceCount = addedResourceIds.length + removedResourceIds.length;
  const liveApiDelta = livePreview.length - savedPreview.length;
  const canMutateRole = canEditRole && !isMobile && snapshot?.voRoleIsBuiltIn !== true;
  const mobileResourceGroups = useMemo(() => resourceTree.map((root) => ({
    root,
    nodes: collectResourceNodes(root),
  })), [resourceTree]);

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
    canEditRole: canMutateRole,
    isDirty,
    loading,
    saving,
  });
  const toggleDisabled = shouldDisableRoleAuthorizationToggle({
    canEditRole: canMutateRole,
    loading,
    saving,
  });

  const loadAuthorization = useCallback(async () => {
    if (!isValidRoleId(normalizedRoleId)) {
      message.error(t('rolePermissions.feedback.invalidRoleId'));
      navigate('/roles', { replace: true });
      return;
    }

    try {
      setLoading(true);
      setLoadError(undefined);
      setVersionConflict(false);
      setResourceTree([]);
      setSnapshot(null);
      setSavedPreview([]);
      setSelectedResourceIds([]);
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
      const errorMessage = error instanceof Error ? error.message : t('rolePermissions.feedback.loadFailed');
      setLoadError(errorMessage);
      message.error(errorMessage);
    } finally {
      setLoading(false);
    }
  }, [navigate, normalizedRoleId, t]);

  useEffect(() => {
    void loadAuthorization();
  }, [loadAuthorization]);

  useEffect(() => {
    if (!isDirty) {
      return;
    }

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [isDirty]);

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

  const performSave = async () => {
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
      message.success(t('rolePermissions.feedback.saved'));
      await loadAuthorization();
    } catch (error) {
      log.error('RolePermissionPage', '保存角色权限失败:', error);
      const isConflict = error instanceof ApiResponseError &&
        (error.httpStatus === 409 || error.statusCode === 409 || error.code === 'RoleAuthorization.VersionConflict');
      if (isConflict) {
        setVersionConflict(true);
        message.warning(t('rolePermissions.feedback.conflict'));
      } else {
        message.error(error instanceof Error ? error.message : t('rolePermissions.feedback.saveFailed'));
      }
    } finally {
      savingRequestRef.current = false;
      setSaving(false);
    }
  };

  const handleSave = () => {
    if (saveDisabled) {
      return;
    }

    Modal.confirm({
      title: t('rolePermissions.confirm.title'),
      content: t('rolePermissions.confirm.description', {
        added: addedResourceIds.length,
        removed: removedResourceIds.length,
        sensitive: sensitivePermissionKeys.length,
      }),
      okText: t('roles.common.confirm'),
      cancelText: t('roles.common.cancel'),
      onOk: performSave,
    });
  };

  const confirmDiscardChanges = () => (
    !isDirty || window.confirm(t('rolePermissions.confirm.discard'))
  );

  const visualStateCache = new Map<string, NodeVisualState>();

  if (isMobile) {
    return (
      <div className="admin-feature-page role-permission-page role-permission-page--mobile">
        <section className="role-permission-mobile-header">
          <Button
            variant="ghost"
            icon={<LeftOutlined />}
            aria-label={t('rolePermissions.actions.back')}
            onClick={() => {
              if (confirmDiscardChanges()) {
                navigate('/roles');
              }
            }}
          >
            {t('rolePermissions.actions.back')}
          </Button>
          <div>
            <h1>{t('rolePermissions.mobile.title')}</h1>
            <span>{snapshot?.voRoleName ?? normalizedRoleId ?? '-'}</span>
          </div>
          <ConsoleStatusChip tone="neutral">
            {t('rolePermissions.mobile.readOnly')}
          </ConsoleStatusChip>
        </section>

        {loadError ? (
          <section className="admin-feature-card role-permission-page__unavailable" role="alert">
            <strong>{t('rolePermissions.unavailable.title')}</strong>
            <p>{loadError}</p>
            <Button icon={<ReloadOutlined />} onClick={() => void loadAuthorization()}>
              {t('rolePermissions.actions.retry')}
            </Button>
          </section>
        ) : null}

        <section className="role-permission-mobile-context">
          <div className="role-permission-mobile-context__icon">
            <SafetyOutlined />
          </div>
          <div className="role-permission-mobile-context__copy">
            <div className="role-permission-mobile-context__title">
              <strong>{snapshot?.voRoleName ?? t('rolePermissions.context.unloaded')}</strong>
              {snapshot?.voRoleIsBuiltIn ? <Tag color="gold">{t('roles.status.builtIn')}</Tag> : null}
            </div>
            <span>{snapshot?.voRoleDescription || t('rolePermissions.mobile.noDescription')}</span>
          </div>
          <strong className="role-permission-mobile-context__count">
            {t('rolePermissions.mobile.grantedCount', {
              count: snapshot?.voGrantedPermissionKeys.length ?? 0,
            })}
          </strong>
        </section>

        {snapshot?.voRoleIsBuiltIn ? (
          <section className="role-permission-mobile-protection">
            <SafetyOutlined />
            <span>{t('rolePermissions.mobile.builtInProtection')}</span>
          </section>
        ) : null}

        <section className="role-permission-mobile-matrix" aria-label={t('rolePermissions.matrix.title')}>
          {loading ? (
            <div className="role-permission-mobile-empty">
              <strong>{t('rolePermissions.state.loading')}</strong>
              <span>{t('rolePermissions.metrics.waiting')}</span>
            </div>
          ) : null}

          {!loading && !loadError && mobileResourceGroups.length === 0 ? (
            <div className="role-permission-mobile-empty">
              <strong>{t('rolePermissions.mobile.emptyTitle')}</strong>
              <span>{t('rolePermissions.mobile.emptyDescription')}</span>
            </div>
          ) : null}

          {mobileResourceGroups.map(({ root, nodes }) => {
            const grantedCount = nodes.filter((node) => mobileGrantedResourceIdSet.has(node.voId)).length;
            return (
              <section className="role-permission-mobile-group" key={root.voId}>
                <div className="role-permission-mobile-group__header">
                  <h2>{root.voTitle}</h2>
                  <span>{t('rolePermissions.mobile.groupCount', { granted: grantedCount, total: nodes.length })}</span>
                </div>
                <div className="role-permission-mobile-group__list">
                  {nodes.map((node) => {
                    const granted = mobileGrantedResourceIdSet.has(node.voId);
                    return (
                      <div
                        className={`role-permission-mobile-key${granted ? ' role-permission-mobile-key--granted' : ''}`}
                        key={node.voId}
                      >
                        <div className="role-permission-mobile-key__copy">
                          <code>{node.voResourceKey}</code>
                          <span>{getPermissionMeaning(node, t)}</span>
                        </div>
                        <ConsoleStatusChip tone={granted ? 'success' : 'neutral'}>
                          {t(granted ? 'rolePermissions.mobile.allowed' : 'rolePermissions.mobile.notGranted')}
                        </ConsoleStatusChip>
                      </div>
                    );
                  })}
                </div>
              </section>
            );
          })}
        </section>

        <section className="role-permission-mobile-boundary">
          <SafetyOutlined />
          <div>
            <strong>{t('rolePermissions.mobile.boundaryTitle')}</strong>
            <span>{t('rolePermissions.mobile.boundaryDescription')}</span>
          </div>
          <Button
            variant="ghost"
            size="small"
            icon={<ReloadOutlined />}
            disabled={loading}
            onClick={() => {
              if (confirmDiscardChanges()) {
                void loadAuthorization();
              }
            }}
          >
            {t('rolePermissions.actions.refresh')}
          </Button>
        </section>
      </div>
    );
  }

  return (
    <div className="admin-feature-page role-permission-page">
      <ConsolePageHeader
        eyebrow={t('rolePermissions.page.eyebrow')}
        title={t('rolePermissions.page.title')}
        description={t('rolePermissions.page.description')}
        icon={<SafetyOutlined />}
        status={(
          <ConsoleStatusChip tone={!canMutateRole ? 'neutral' : (isDirty ? 'warning' : 'success')}>
            {t(!canMutateRole
              ? 'rolePermissions.state.readOnly'
              : (isDirty ? 'rolePermissions.state.pending' : 'rolePermissions.state.synced'))}
          </ConsoleStatusChip>
        )}
        actions={(
          <div className="role-permission-page__actions">
            <Button icon={<LeftOutlined />} onClick={() => {
              if (confirmDiscardChanges()) {
                navigate('/roles');
              }
            }}>
              {t('rolePermissions.actions.back')}
            </Button>
            <Button
              icon={<ReloadOutlined />}
              disabled={loading || saving}
              onClick={() => {
                if (confirmDiscardChanges()) {
                  void loadAuthorization();
                }
              }}
            >
              {t('rolePermissions.actions.refresh')}
            </Button>
            {canMutateRole ? (
              <Button
                variant="primary"
                icon={<SafetyOutlined />}
                onClick={() => {
                  handleSave();
                }}
                disabled={saveDisabled}
              >
                {t(saving ? 'rolePermissions.actions.saving' : 'rolePermissions.actions.save')}
              </Button>
            ) : null}
          </div>
        )}
      />

      {loadError ? (
        <section className="admin-feature-card role-permission-page__unavailable" role="alert">
          <strong>{t('rolePermissions.unavailable.title')}</strong>
          <p>{loadError}</p>
          <Button icon={<ReloadOutlined />} onClick={() => void loadAuthorization()}>
            {t('rolePermissions.actions.retry')}
          </Button>
        </section>
      ) : null}

      {versionConflict ? (
        <section className="admin-feature-card role-permission-page__conflict" role="alert">
          <strong>{t('rolePermissions.conflict.title')}</strong>
          <p>{t('rolePermissions.conflict.description')}</p>
          <Button icon={<ReloadOutlined />} onClick={() => {
            if (confirmDiscardChanges()) {
              void loadAuthorization();
            }
          }}>
            {t('rolePermissions.conflict.reload')}
          </Button>
        </section>
      ) : null}

      <ConsoleMetricGrid label={t('rolePermissions.metrics.ariaLabel')}>
        <ConsoleMetricCard label={t('rolePermissions.metrics.selected')} value={selectedResourceIds.length} description={t('rolePermissions.metrics.selectedDescription')} tone="info" />
        <ConsoleMetricCard label={t('rolePermissions.metrics.keys')} value={permissionKeys.length} description={t('rolePermissions.metrics.keysDescription')} />
        <ConsoleMetricCard label={t('rolePermissions.metrics.apis')} value={livePreview.length} description={t('rolePermissions.metrics.apisDescription')} tone="success" />
        <ConsoleMetricCard
          label={t('rolePermissions.metrics.snapshot')}
          value={isDirty ? t('rolePermissions.metrics.changed', { count: changedResourceCount }) : t('rolePermissions.state.synced')}
          description={snapshot ? t('rolePermissions.metrics.basedOn', { time: snapshotTimeText }) : t('rolePermissions.metrics.waiting')}
          tone={isDirty ? 'warning' : 'success'}
        />
      </ConsoleMetricGrid>

      <section className="governance-task-flow" aria-label={t('rolePermissions.flow.ariaLabel')}>
        <div className="governance-task-flow__item">
          <span>1</span>
          <strong>{t('rolePermissions.flow.snapshotTitle')}</strong>
          <p>{snapshot ? t('rolePermissions.flow.snapshotReady', { role: snapshot.voRoleName, time: snapshotTimeText }) : t('rolePermissions.flow.snapshotEmpty')}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>2</span>
          <strong>{t('rolePermissions.flow.matrixTitle')}</strong>
          <p>{t('rolePermissions.flow.matrixDescription', { roots: resourceTree.length, leaves: leafResourceCount, selected: selectedResourceIds.length })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>3</span>
          <strong>{t('rolePermissions.flow.previewTitle')}</strong>
          <p>{t('rolePermissions.flow.previewDescription', { permissions: permissionKeys.length, apis: livePreview.length })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>4</span>
          <strong>{t('rolePermissions.flow.saveTitle')}</strong>
          <p>{t(canMutateRole ? (isDirty ? 'rolePermissions.flow.saveDirty' : 'rolePermissions.flow.saveSynced') : 'rolePermissions.flow.saveReadOnly')}</p>
        </div>
      </section>

      <ConsoleToolbar
        title={t('rolePermissions.context.title')}
        description={t('rolePermissions.context.description')}
        meta={(
          <ConsoleStatusChip tone={snapshot?.voRoleIsEnabled ? 'success' : 'neutral'}>
            {t(snapshot ? (snapshot.voRoleIsEnabled ? 'rolePermissions.context.enabled' : 'rolePermissions.context.disabled') : 'rolePermissions.context.unloaded')}
          </ConsoleStatusChip>
        )}
      >
        <div className="role-permission-page__role-card">
          <Descriptions
            title={t('rolePermissions.context.info')}
            column={2}
            items={[
              {
                key: 'roleName',
                label: t('rolePermissions.context.name'),
                children: snapshot?.voRoleName ?? '-',
              },
              {
                key: 'roleStatus',
                label: t('rolePermissions.context.status'),
                children: (
                  <Space size={6}>
                    <Tag color={snapshot?.voRoleIsEnabled ? 'success' : 'error'}>
                      {t(snapshot?.voRoleIsEnabled ? 'roles.status.enabled' : 'roles.status.disabled')}
                    </Tag>
                    {snapshot?.voRoleIsBuiltIn ? <Tag color="gold">{t('roles.status.builtIn')}</Tag> : null}
                  </Space>
                ),
              },
              {
                key: 'roleDescription',
                label: t('rolePermissions.context.descriptionLabel'),
                children: snapshot?.voRoleDescription || '-',
              },
              {
                key: 'lastModifyTime',
                label: t('rolePermissions.context.modifiedAt'),
                children: snapshot?.voLastModifyTime
                  ? formatConsoleDateTime(snapshot.voLastModifyTime, language)
                  : t('rolePermissions.context.none'),
              },
            ]}
          />
        </div>
      </ConsoleToolbar>

      {snapshot?.voRoleIsBuiltIn ? (
        <section className="role-permission-protected-role">
          <SafetyOutlined />
          <div>
            <strong>{t('rolePermissions.protection.title')}</strong>
            <span>{t('rolePermissions.protection.description')}</span>
          </div>
        </section>
      ) : null}

      <div className="role-permission-workspace">
        <main className="role-permission-workspace__main">
          <div className="role-permission-page__content">
            <section className="role-permission-panel">
              <div className="role-permission-panel__header">
                <div>
                  <h3>{t('rolePermissions.matrix.title')}</h3>
                  <p>{t('rolePermissions.matrix.description')}</p>
                </div>
                <Space size={[8, 8]} wrap>
                  {loading ? <Tag color="processing">{t('rolePermissions.state.loading')}</Tag> : null}
                  <Tag color={isDirty ? 'warning' : 'success'}>{t(isDirty ? 'rolePermissions.state.stale' : 'rolePermissions.state.snapshotSynced')}</Tag>
                </Space>
              </div>

              <div className="role-permission-matrix-summary">
                <span>{t('rolePermissions.count.resources', { count: resourceCount })}</span>
                <span>{t('rolePermissions.count.leaves', { count: leafResourceCount })}</span>
                <span>{t('rolePermissions.count.selected', { count: selectedResourceIds.length })}</span>
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
                    t={t}
                  />
                ))}
              </div>
            </section>

            <section className="role-permission-panel role-permission-panel--preview">
              <div className="role-permission-panel__header">
                <div>
                  <h3>{t('rolePermissions.preview.title')}</h3>
                  <p>{t('rolePermissions.preview.description')}</p>
                </div>
                {isDirty ? <Tag color="warning">{t('rolePermissions.state.unsaved')}</Tag> : <Tag color="success">{t('rolePermissions.state.synced')}</Tag>}
              </div>

              <div className="role-permission-preview">
                <div className="role-permission-preview__section">
                  <h4>{t('rolePermissions.preview.keys')}</h4>
                  <div className="role-permission-preview__tags">
                    {permissionKeys.length > 0 ? permissionKeys.map((key) => (
                      <Tag key={key}>{key}</Tag>
                    )) : <span className="role-permission-preview__empty">{t('rolePermissions.preview.noKeys')}</span>}
                  </div>
                </div>

                <div className="role-permission-preview__section">
                  <h4>{t('rolePermissions.preview.apis')}</h4>
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
                    )) : <span className="role-permission-preview__empty">{t('rolePermissions.preview.noApis')}</span>}
                  </div>
                </div>

                <div className="role-permission-preview__section">
                  <h4>{t('rolePermissions.preview.saved')}</h4>
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
                    )) : <span className="role-permission-preview__empty">{t('rolePermissions.preview.noSaved')}</span>}
                  </div>
                </div>
              </div>
            </section>
          </div>
        </main>

        <aside className="admin-feature-rail role-permission-rail" aria-label={t('rolePermissions.rail.ariaLabel')}>
          <div className="admin-feature-rail__header">
            <div>
              <span className="admin-feature-rail__eyebrow">{t('rolePermissions.rail.eyebrow')}</span>
              <h3>{t('rolePermissions.rail.title')}</h3>
            </div>
            <ConsoleStatusChip tone={isDirty ? 'warning' : 'success'}>
              {t(isDirty ? 'rolePermissions.state.pending' : 'rolePermissions.state.synced')}
            </ConsoleStatusChip>
          </div>
          <p className="admin-feature-subtle">{t('rolePermissions.rail.description')}</p>

          <div className="admin-feature-rail__list">
            <div className="admin-feature-rail__item">
              <span>{t('rolePermissions.rail.role')}</span>
              <strong>{snapshot?.voRoleName ?? normalizedRoleId ?? '-'}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>{t('rolePermissions.rail.snapshotTime')}</span>
              <strong>{snapshotTimeText}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>{t('rolePermissions.rail.changedResources')}</span>
              <strong>{changedResourceCount > 0 ? t('rolePermissions.count.changed', { count: changedResourceCount }) : t('rolePermissions.rail.noChanges')}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>{t('rolePermissions.rail.apiChanges')}</span>
              <strong>{liveApiDelta === 0 ? t('rolePermissions.rail.noNetChanges') : `${liveApiDelta > 0 ? '+' : ''}${t('rolePermissions.count.apiDelta', { count: Math.abs(liveApiDelta) })}`}</strong>
            </div>
          </div>

          <div className="admin-feature-rail__callout">
            <span>{t('rolePermissions.rail.saveState')}</span>
            <strong>{t(canMutateRole ? (saveDisabled ? 'rolePermissions.rail.waiting' : 'rolePermissions.rail.canSave') : 'rolePermissions.rail.readOnly')}</strong>
            <p>{t(isDirty ? 'rolePermissions.rail.dirtyDescription' : 'rolePermissions.rail.syncedDescription')}</p>
          </div>

          <div className="role-permission-rail__section">
            <h4>{t('rolePermissions.rail.changeClues')}</h4>
            <div className="role-permission-rail__tags">
              {changedResourceTitles.length > 0 ? changedResourceTitles.map((title) => (
                <Tag key={title} color="warning">{title}</Tag>
              )) : <span>{t('rolePermissions.rail.noResourceChanges')}</span>}
            </div>
          </div>

          <div className="role-permission-rail__section">
            <h4>{t('rolePermissions.rail.sensitiveClues')}</h4>
            <div className="role-permission-rail__tags">
              {sensitivePermissionKeys.length > 0 ? sensitivePermissionKeys.map((key) => (
                <Tag key={key} color="error">{key}</Tag>
              )) : <span>{t('rolePermissions.rail.noSensitiveClues')}</span>}
            </div>
          </div>

          <div className="admin-feature-inline-context">
            <strong>{t('rolePermissions.rail.boundary')}</strong>
            <span>{t('rolePermissions.rail.boundaryDescription')}</span>
          </div>
        </aside>
      </div>
    </div>
  );
};
