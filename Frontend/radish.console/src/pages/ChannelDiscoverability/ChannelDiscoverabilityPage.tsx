import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiResponseError } from '@radish/http';
import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import {
  AntInput as Input,
  AntModal as Modal,
  AntSelect as Select,
  BottomSheet,
  Button,
  ClockCircleOutlined,
  EyeOutlined,
  ReloadOutlined,
  SearchOutlined,
  Space,
  Table,
  Tag,
  formatLocalizedDateTime,
  message,
  type TableColumnsType,
} from '@radish/ui';
import {
  getChannelDiscoverabilityById,
  getChannelDiscoverabilityHistory,
  getChannelDiscoverabilityPage,
  updateChannelDiscoverVisibility,
  type ChannelDiscoverabilityListPageModel,
  type ChannelDiscoverabilityVo,
  type ChannelDiscoverVisibility,
  type ChannelDiscoverVisibilityEventPageModel,
  type ChannelDiscoverVisibilityEventVo,
} from '@/api/channelDiscoverabilityApi';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleResourceList,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import { formatConsoleNumber } from '@/utils/localeFormatters';
import { log } from '@/utils/logger';
import {
  DEFAULT_CHANNEL_DISCOVERABILITY_QUERY,
  parseChannelDiscoverabilityQuery,
  serializeChannelDiscoverabilityQuery,
  type ChannelDiscoverabilityLifecycleFilter,
  type ChannelDiscoverabilityQuery,
  type ChannelDiscoverabilityVisibilityFilter,
} from './channelDiscoverabilityUrlState';
import '../adminFeature.css';
import './ChannelDiscoverabilityPage.css';

type ResourceReadState = 'loading' | 'ready' | 'unavailable' | 'stale';
type MutationTargetReadState = 'ready' | 'loading' | 'unavailable';

const HISTORY_PAGE_SIZE = 10;

const ERROR_MESSAGE_KEYS: Readonly<Record<string, string>> = {
  'ChannelDiscoverability.InvalidArgument': 'channelDiscoverability.feedback.invalidArgument',
  'ChannelDiscoverability.TargetUnavailable': 'channelDiscoverability.feedback.targetUnavailable',
  'ChannelDiscoverability.VersionConflict': 'channelDiscoverability.feedback.versionConflict',
  'ChannelDiscoverability.NotEligible': 'channelDiscoverability.feedback.notEligible',
  'ChannelDiscoverability.ReasonRequired': 'channelDiscoverability.feedback.reasonRequired',
};

function getFeedbackMessage(error: unknown, t: TFunction, fallbackKey: string): string {
  if (error instanceof ApiResponseError && error.code) {
    const messageKey = ERROR_MESSAGE_KEYS[error.code];
    if (messageKey) return t(messageKey);
  }

  return t(fallbackKey);
}

function isVersionConflict(error: unknown): boolean {
  return error instanceof ApiResponseError
    && error.code === 'ChannelDiscoverability.VersionConflict';
}

function toVisibilityValue(
  value: ChannelDiscoverabilityVisibilityFilter,
): ChannelDiscoverVisibility | undefined {
  if (value === 'summary') return 1;
  if (value === 'hidden') return 0;
  return undefined;
}

function toEnabledValue(value: ChannelDiscoverabilityLifecycleFilter): boolean | undefined {
  if (value === 'enabled') return true;
  if (value === 'disabled') return false;
  return undefined;
}

function matchesAppliedQuery(
  channel: ChannelDiscoverabilityVo,
  query: ChannelDiscoverabilityQuery,
): boolean {
  // Discoverability writes do not change textual identity, so the existing keyword
  // membership remains valid until the authoritative list refresh completes.
  const matchesVisibility = query.visibility === 'all'
    || (query.visibility === 'summary' && channel.voDiscoverVisibility === 1)
    || (query.visibility === 'hidden' && channel.voDiscoverVisibility === 0);
  const matchesLifecycle = query.lifecycle === 'all'
    || (query.lifecycle === 'enabled' && channel.voIsEnabled)
    || (query.lifecycle === 'disabled' && !channel.voIsEnabled);
  const matchesDeleted = query.includeDeleted || !channel.voIsDeleted;
  return matchesVisibility && matchesLifecycle && matchesDeleted;
}

export const ChannelDiscoverabilityPage = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('channelDiscoverability.documentTitle'));
  const canView = usePermission(CONSOLE_PERMISSIONS.channelDiscoverabilityView);
  const canManage = usePermission(CONSOLE_PERMISSIONS.channelDiscoverabilityManage);
  const [searchParams, setSearchParams] = useSearchParams();
  const query = useMemo(() => parseChannelDiscoverabilityQuery(searchParams), [searchParams]);
  const queryKey = useMemo(() => serializeChannelDiscoverabilityQuery(query).toString(), [query]);

  const [keywordDraft, setKeywordDraft] = useState(query.keyword);
  const [visibilityDraft, setVisibilityDraft] = useState(query.visibility);
  const [lifecycleDraft, setLifecycleDraft] = useState(query.lifecycle);
  const [includeDeletedDraft, setIncludeDeletedDraft] = useState(query.includeDeleted);
  const [filterSheetOpen, setFilterSheetOpen] = useState(false);

  const [listSnapshot, setListSnapshot] = useState<ChannelDiscoverabilityListPageModel | null>(null);
  const [listReadState, setListReadState] = useState<ResourceReadState>('loading');
  const [listLoading, setListLoading] = useState(false);
  const listRequestGeneration = useRef(0);
  const listSnapshotRef = useRef<ChannelDiscoverabilityListPageModel | null>(null);
  const listSnapshotQueryKey = useRef<string | undefined>(undefined);

  const [mutationTarget, setMutationTarget] = useState<ChannelDiscoverabilityVo>();
  const [mutationVisibility, setMutationVisibility] = useState<ChannelDiscoverVisibility>(0);
  const [mutationTargetReadState, setMutationTargetReadState] = useState<MutationTargetReadState>('ready');
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [conflictDetected, setConflictDetected] = useState(false);
  const mutationTargetRequestGeneration = useRef(0);

  const [historyTarget, setHistoryTarget] = useState<ChannelDiscoverabilityVo>();
  const [historyPageIndex, setHistoryPageIndex] = useState(1);
  const [historyPageSize, setHistoryPageSize] = useState(HISTORY_PAGE_SIZE);
  const [historySnapshot, setHistorySnapshot] = useState<ChannelDiscoverVisibilityEventPageModel | null>(null);
  const [historyReadState, setHistoryReadState] = useState<ResourceReadState>('loading');
  const [historyLoading, setHistoryLoading] = useState(false);
  const historyRequestGeneration = useRef(0);
  const historySnapshotRef = useRef<ChannelDiscoverVisibilityEventPageModel | null>(null);
  const historySnapshotKey = useRef<string | undefined>(undefined);

  useEffect(() => {
    setKeywordDraft(query.keyword);
    setVisibilityDraft(query.visibility);
    setLifecycleDraft(query.lifecycle);
    setIncludeDeletedDraft(query.includeDeleted);
  }, [query.includeDeleted, query.keyword, query.lifecycle, query.visibility]);

  const updateQuery = useCallback((nextQuery: ChannelDiscoverabilityQuery) => {
    setSearchParams(serializeChannelDiscoverabilityQuery(nextQuery), { replace: true });
  }, [setSearchParams]);

  const commitListSnapshot = useCallback((
    snapshot: ChannelDiscoverabilityListPageModel,
    snapshotKey: string,
  ) => {
    listSnapshotRef.current = snapshot;
    listSnapshotQueryKey.current = snapshotKey;
    setListSnapshot(snapshot);
  }, []);

  const loadPage = useCallback(async () => {
    const requestGeneration = listRequestGeneration.current + 1;
    const hasCurrentSnapshot = listSnapshotQueryKey.current === queryKey
      && listSnapshotRef.current !== null;
    listRequestGeneration.current = requestGeneration;
    setListLoading(true);
    setListReadState('loading');
    if (!hasCurrentSnapshot) {
      setListSnapshot(null);
    }

    try {
      const result = await getChannelDiscoverabilityPage({
        pageIndex: query.pageIndex,
        pageSize: query.pageSize,
        keyword: query.keyword || undefined,
        discoverVisibility: toVisibilityValue(query.visibility),
        isEnabled: toEnabledValue(query.lifecycle),
        includeDeleted: query.includeDeleted,
      }, t);
      if (requestGeneration !== listRequestGeneration.current) return;

      const availablePageCount = Math.max(1, result.pageCount);
      if (result.data.length === 0 && result.dataCount > 0 && query.pageIndex > availablePageCount) {
        updateQuery({ ...query, pageIndex: availablePageCount });
        return;
      }

      commitListSnapshot(result, queryKey);
      setListReadState('ready');
    } catch (error) {
      if (requestGeneration !== listRequestGeneration.current) return;

      log.error('ChannelDiscoverabilityPage', '加载频道公开摘要列表失败:', error);
      setListReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
      message.error(getFeedbackMessage(error, t, 'channelDiscoverability.feedback.loadFailed'));
    } finally {
      if (requestGeneration === listRequestGeneration.current) setListLoading(false);
    }
  }, [commitListSnapshot, query, queryKey, t, updateQuery]);

  useEffect(() => {
    if (!canView) {
      listRequestGeneration.current += 1;
      listSnapshotRef.current = null;
      listSnapshotQueryKey.current = undefined;
      setListSnapshot(null);
      setListLoading(false);
      setListReadState('unavailable');
      return;
    }

    void loadPage();
  }, [canView, loadPage]);

  useEffect(() => () => {
    listRequestGeneration.current += 1;
    historyRequestGeneration.current += 1;
    mutationTargetRequestGeneration.current += 1;
  }, []);

  const items = listSnapshot?.data ?? [];
  const total = listSnapshot?.dataCount ?? null;
  const pageCount = Math.max(1, listSnapshot?.pageCount ?? 1);
  const actionsAreAuthoritative = canManage
    && listReadState === 'ready'
    && listSnapshotQueryKey.current === queryKey;
  const summaryCount = items.filter((item) => item.voDiscoverVisibility === 1).length;
  const eligibleCount = items.filter((item) => item.voCanEnableSummary).length;
  const inactiveCount = items.filter((item) => !item.voIsEnabled || item.voIsDeleted).length;
  const activeFilterCount = [
    query.keyword,
    query.visibility !== 'all' ? query.visibility : undefined,
    query.lifecycle !== 'all' ? query.lifecycle : undefined,
    query.includeDeleted ? 'includeDeleted' : undefined,
  ].filter(Boolean).length;

  const visibilityLabel = (visibility: ChannelDiscoverVisibility) => t(
    visibility === 1
      ? 'channelDiscoverability.visibility.summary'
      : 'channelDiscoverability.visibility.hidden',
  );

  const consumeAuthoritativeChannel = useCallback((channel: ChannelDiscoverabilityVo) => {
    const currentSnapshot = listSnapshotRef.current;
    if (currentSnapshot && listSnapshotQueryKey.current === queryKey) {
      const existingIndex = currentSnapshot.data.findIndex((item) => (
        item.voChannelId === channel.voChannelId
      ));
      const remainsInQuery = matchesAppliedQuery(channel, query);
      const nextData = remainsInQuery
        ? currentSnapshot.data.map((item) => (
            item.voChannelId === channel.voChannelId ? channel : item
          ))
        : currentSnapshot.data.filter((item) => item.voChannelId !== channel.voChannelId);
      const nextDataCount = existingIndex >= 0 && !remainsInQuery
        ? Math.max(0, currentSnapshot.dataCount - 1)
        : currentSnapshot.dataCount;
      const nextSnapshot = {
        ...currentSnapshot,
        data: nextData,
        dataCount: nextDataCount,
        pageCount: Math.ceil(nextDataCount / currentSnapshot.pageSize),
      };
      listSnapshotRef.current = nextSnapshot;
      setListSnapshot(nextSnapshot);
    }

    setHistoryTarget((current) => (
      current?.voChannelId === channel.voChannelId ? channel : current
    ));
  }, [query, queryKey]);

  const closeMutation = useCallback(() => {
    mutationTargetRequestGeneration.current += 1;
    setMutationTarget(undefined);
    setMutationTargetReadState('ready');
    setReason('');
    setConflictDetected(false);
  }, []);

  const requestCloseMutation = () => {
    if (saving) {
      message.warning(t('channelDiscoverability.mutation.closeBusy'));
      return;
    }

    if (!reason.trim()) {
      closeMutation();
      return;
    }

    Modal.confirm({
      title: t('channelDiscoverability.mutation.discardTitle'),
      content: t('channelDiscoverability.mutation.discardDescription'),
      okText: t('channelDiscoverability.mutation.discardConfirm'),
      cancelText: t('channelDiscoverability.mutation.continueEditing'),
      okButtonProps: { danger: true },
      onOk: closeMutation,
    });
  };

  const openMutation = (record: ChannelDiscoverabilityVo) => {
    if (!canManage || !actionsAreAuthoritative) {
      message.warning(t('channelDiscoverability.feedback.authorityRequired'));
      return;
    }

    setMutationTarget(record);
    setMutationVisibility(record.voDiscoverVisibility === 1 ? 0 : 1);
    setMutationTargetReadState('ready');
    setReason('');
    setConflictDetected(false);
  };

  const refreshMutationTarget = useCallback(async () => {
    if (!mutationTarget) return;

    const channelId = mutationTarget.voChannelId;
    const requestGeneration = mutationTargetRequestGeneration.current + 1;
    mutationTargetRequestGeneration.current = requestGeneration;
    setMutationTargetReadState('loading');
    try {
      const channel = await getChannelDiscoverabilityById(channelId, t);
      if (requestGeneration !== mutationTargetRequestGeneration.current) return;

      consumeAuthoritativeChannel(channel);
      setMutationTarget(channel);
      setMutationTargetReadState('ready');
    } catch (error) {
      if (requestGeneration !== mutationTargetRequestGeneration.current) return;

      log.error('ChannelDiscoverabilityPage', '刷新冲突频道权威状态失败:', error);
      setMutationTargetReadState('unavailable');
      message.error(getFeedbackMessage(
        error,
        t,
        'channelDiscoverability.feedback.targetRefreshFailed',
      ));
    }
  }, [consumeAuthoritativeChannel, mutationTarget, t]);

  const submitMutation = async () => {
    if (!mutationTarget || !reason.trim()) {
      message.error(t('channelDiscoverability.feedback.reasonRequired'));
      return;
    }
    if (!canManage || !actionsAreAuthoritative || mutationTargetReadState !== 'ready' || saving) {
      message.warning(t('channelDiscoverability.feedback.authorityRequired'));
      return;
    }

    try {
      setSaving(true);
      const result = await updateChannelDiscoverVisibility(mutationTarget.voChannelId, {
        discoverVisibility: mutationVisibility,
        expectedVersion: mutationTarget.voDiscoverVisibilityVersion,
        reason: reason.trim(),
      }, t);
      consumeAuthoritativeChannel(result.voChannel);
      setMutationTarget(result.voChannel);
      message.success(t(result.voChanged
        ? 'channelDiscoverability.feedback.updated'
        : 'channelDiscoverability.feedback.unchanged'));
      closeMutation();
      await loadPage();
    } catch (error) {
      log.error('ChannelDiscoverabilityPage', '更新频道公开摘要资格失败:', error);
      if (isVersionConflict(error)) {
        setConflictDetected(true);
        message.warning(t('channelDiscoverability.feedback.versionConflict'));
        await refreshMutationTarget();
      } else {
        message.error(getFeedbackMessage(error, t, 'channelDiscoverability.feedback.updateFailed'));
      }
    } finally {
      setSaving(false);
    }
  };

  const loadHistory = useCallback(async () => {
    if (!historyTarget) return;

    const snapshotKey = JSON.stringify({
      channelId: historyTarget.voChannelId,
      pageIndex: historyPageIndex,
      pageSize: historyPageSize,
    });
    const hasCurrentSnapshot = historySnapshotKey.current === snapshotKey
      && historySnapshotRef.current !== null;
    const requestGeneration = historyRequestGeneration.current + 1;
    historyRequestGeneration.current = requestGeneration;
    setHistoryLoading(true);
    setHistoryReadState('loading');
    if (!hasCurrentSnapshot) setHistorySnapshot(null);

    try {
      const result = await getChannelDiscoverabilityHistory({
        channelId: historyTarget.voChannelId,
        pageIndex: historyPageIndex,
        pageSize: historyPageSize,
      }, t);
      if (requestGeneration !== historyRequestGeneration.current) return;

      const availablePageCount = Math.max(1, result.pageCount);
      if (result.data.length === 0 && result.dataCount > 0 && historyPageIndex > availablePageCount) {
        setHistoryPageIndex(availablePageCount);
        return;
      }

      historySnapshotRef.current = result;
      historySnapshotKey.current = snapshotKey;
      setHistorySnapshot(result);
      setHistoryReadState('ready');
    } catch (error) {
      if (requestGeneration !== historyRequestGeneration.current) return;

      log.error('ChannelDiscoverabilityPage', '加载频道公开摘要历史失败:', error);
      setHistoryReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
      message.error(getFeedbackMessage(error, t, 'channelDiscoverability.feedback.historyFailed'));
    } finally {
      if (requestGeneration === historyRequestGeneration.current) setHistoryLoading(false);
    }
  }, [historyPageIndex, historyPageSize, historyTarget, t]);

  useEffect(() => {
    if (historyTarget) void loadHistory();
  }, [historyTarget, loadHistory]);

  const openHistory = (record: ChannelDiscoverabilityVo) => {
    historyRequestGeneration.current += 1;
    historySnapshotRef.current = null;
    historySnapshotKey.current = undefined;
    setHistorySnapshot(null);
    setHistoryPageIndex(1);
    setHistoryPageSize(HISTORY_PAGE_SIZE);
    setHistoryTarget(record);
    setHistoryReadState('loading');
  };

  const closeHistory = () => {
    historyRequestGeneration.current += 1;
    historySnapshotRef.current = null;
    historySnapshotKey.current = undefined;
    setHistoryTarget(undefined);
    setHistorySnapshot(null);
    setHistoryLoading(false);
  };

  const applyFilters = () => {
    updateQuery({
      ...query,
      pageIndex: 1,
      keyword: keywordDraft.trim().slice(0, 100),
      visibility: visibilityDraft,
      lifecycle: lifecycleDraft,
      includeDeleted: includeDeletedDraft,
    });
    setFilterSheetOpen(false);
  };

  const resetFilters = () => {
    setKeywordDraft('');
    setVisibilityDraft('all');
    setLifecycleDraft('all');
    setIncludeDeletedDraft(false);
    updateQuery({
      ...DEFAULT_CHANNEL_DISCOVERABILITY_QUERY,
      pageSize: query.pageSize,
    });
    setFilterSheetOpen(false);
  };

  const handlePageChange = (pageIndex: number, pageSize = query.pageSize) => {
    updateQuery({ ...query, pageIndex, pageSize });
  };

  const renderChannelIdentity = (record: ChannelDiscoverabilityVo) => (
    <div className="channel-discoverability-name">
      <span className="channel-discoverability-name__icon">{record.voIconEmoji || '＃'}</span>
      <span className="channel-discoverability-name__copy">
        <strong>{record.voName}</strong>
        <span>/{record.voSlug}</span>
      </span>
    </div>
  );

  const renderActions = (record: ChannelDiscoverabilityVo) => (
    <Space size="small" wrap>
      <Button
        variant="ghost"
        size="small"
        icon={<ClockCircleOutlined />}
        onClick={() => openHistory(record)}
      >
        {t('channelDiscoverability.actions.history')}
      </Button>
      {canManage ? (
        <Button
          variant={record.voDiscoverVisibility === 1 ? 'danger' : 'primary'}
          size="small"
          icon={<EyeOutlined />}
          disabled={!actionsAreAuthoritative
            || (record.voDiscoverVisibility === 0 && !record.voCanEnableSummary)}
          onClick={() => openMutation(record)}
        >
          {t(record.voDiscoverVisibility === 1
            ? 'channelDiscoverability.actions.hide'
            : 'channelDiscoverability.actions.expose')}
        </Button>
      ) : null}
    </Space>
  );

  const columns: TableColumnsType<ChannelDiscoverabilityVo> = [
    {
      title: t('channelDiscoverability.table.channel'),
      key: 'channel',
      width: 260,
      render: (_, record) => renderChannelIdentity(record),
    },
    {
      title: t('channelDiscoverability.table.visibility'),
      key: 'visibility',
      width: 130,
      render: (_, record) => (
        <Tag color={record.voDiscoverVisibility === 1 ? 'processing' : 'default'}>
          {visibilityLabel(record.voDiscoverVisibility)}
        </Tag>
      ),
    },
    {
      title: t('channelDiscoverability.table.eligibility'),
      key: 'eligibility',
      width: 210,
      render: (_, record) => record.voCanEnableSummary
        ? <Tag color="success">{t('channelDiscoverability.eligibility.ready')}</Tag>
        : (
            <Space size={[4, 4]} wrap>
              {record.voEligibilityIssues.map((issue) => (
                <Tag key={issue} color="warning">
                  {t(`channelDiscoverability.eligibility.${issue}`, { defaultValue: issue })}
                </Tag>
              ))}
            </Space>
          ),
    },
    {
      title: t('channelDiscoverability.table.lifecycle'),
      key: 'lifecycle',
      width: 120,
      render: (_, record) => record.voIsDeleted
        ? <Tag>{t('channelDiscoverability.lifecycle.deleted')}</Tag>
        : (
            <Tag color={record.voIsEnabled ? 'success' : 'error'}>
              {t(record.voIsEnabled
                ? 'channelDiscoverability.lifecycle.enabled'
                : 'channelDiscoverability.lifecycle.disabled')}
            </Tag>
          ),
    },
    {
      title: t('channelDiscoverability.table.lastActivity'),
      dataIndex: 'voLastMessageTime',
      key: 'voLastMessageTime',
      width: 180,
      render: (value?: string | null) => value ? formatLocalizedDateTime(value, language) : '-',
    },
    {
      title: t('channelDiscoverability.table.modifiedBy'),
      key: 'modifiedBy',
      width: 190,
      render: (_, record) => (
        <span>
          {record.voModifyBy || '-'}
          <br />
          {record.voModifyTime ? formatLocalizedDateTime(record.voModifyTime, language) : '-'}
        </span>
      ),
    },
    {
      title: t('channelDiscoverability.table.actions'),
      key: 'actions',
      width: 240,
      fixed: 'right',
      render: (_, record) => renderActions(record),
    },
  ];

  const historyColumns: TableColumnsType<ChannelDiscoverVisibilityEventVo> = [
    {
      title: t('channelDiscoverability.history.time'),
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (value: string) => formatLocalizedDateTime(value, language),
    },
    {
      title: t('channelDiscoverability.history.change'),
      key: 'change',
      width: 190,
      render: (_, record) => `${visibilityLabel(record.voFromVisibility)} → ${visibilityLabel(record.voToVisibility)}`,
    },
    {
      title: t('channelDiscoverability.history.reason'),
      dataIndex: 'voReason',
      key: 'voReason',
      ellipsis: true,
    },
    {
      title: t('channelDiscoverability.history.operator'),
      dataIndex: 'voActorName',
      key: 'voActorName',
      width: 150,
    },
    {
      title: t('channelDiscoverability.history.version'),
      dataIndex: 'voResultVersion',
      key: 'voResultVersion',
      width: 100,
      render: (value: number) => `v${value}`,
    },
  ];

  const filterControls = (
    <div className="console-resource-filter-controls console-resource-filter-controls--wide">
      <Input
        allowClear
        prefix={<SearchOutlined />}
        placeholder={t('channelDiscoverability.filter.keyword')}
        value={keywordDraft}
        onChange={(event) => setKeywordDraft(event.target.value)}
        onPressEnter={applyFilters}
      />
      <Select<ChannelDiscoverabilityVisibilityFilter>
        value={visibilityDraft}
        onChange={setVisibilityDraft}
        options={[
          { value: 'all', label: t('channelDiscoverability.filter.allVisibility') },
          { value: 'summary', label: t('channelDiscoverability.visibility.summary') },
          { value: 'hidden', label: t('channelDiscoverability.visibility.hidden') },
        ]}
      />
      <Select<ChannelDiscoverabilityLifecycleFilter>
        value={lifecycleDraft}
        onChange={setLifecycleDraft}
        options={[
          { value: 'all', label: t('channelDiscoverability.filter.allLifecycle') },
          { value: 'enabled', label: t('channelDiscoverability.lifecycle.enabled') },
          { value: 'disabled', label: t('channelDiscoverability.lifecycle.disabled') },
        ]}
      />
      <Select<'include' | 'exclude'>
        value={includeDeletedDraft ? 'include' : 'exclude'}
        onChange={(value) => setIncludeDeletedDraft(value === 'include')}
        options={[
          { value: 'exclude', label: t('channelDiscoverability.filter.hideDeleted') },
          { value: 'include', label: t('channelDiscoverability.filter.showDeleted') },
        ]}
      />
      <div className="console-resource-filter-controls__actions">
        <Button onClick={resetFilters}>{t('channelDiscoverability.actions.reset')}</Button>
        <Button variant="primary" icon={<SearchOutlined />} onClick={applyFilters}>
          {t('channelDiscoverability.actions.search')}
        </Button>
      </div>
    </div>
  );

  const listReadNotice = listReadState === 'stale' || listReadState === 'unavailable' ? (
    <div className={`console-resource-list-notice console-resource-list-notice--${listReadState}`} role="alert">
      <div>
        <strong>{t(`channelDiscoverability.list.${listReadState}Title`)}</strong>
        <span>{t(`channelDiscoverability.list.${listReadState}Description`)}</span>
      </div>
      <Button size="small" onClick={() => void loadPage()}>
        {t('channelDiscoverability.actions.retry')}
      </Button>
    </div>
  ) : null;

  const historyReadNotice = historyReadState === 'stale' || historyReadState === 'unavailable' ? (
    <div className={`console-resource-list-notice console-resource-list-notice--${historyReadState}`} role="alert">
      <div>
        <strong>{t(`channelDiscoverability.history.${historyReadState}Title`)}</strong>
        <span>{t(`channelDiscoverability.history.${historyReadState}Description`)}</span>
      </div>
      <Button size="small" onClick={() => void loadHistory()}>
        {t('channelDiscoverability.actions.retry')}
      </Button>
    </div>
  ) : null;

  const historyItems = historySnapshot?.data ?? [];
  const historyPageCount = Math.max(1, historySnapshot?.pageCount ?? 1);
  const metricValue = (value: number | null) => value === null
    ? t('channelDiscoverability.state.noSnapshot')
    : formatConsoleNumber(value, language);

  return (
    <div className="admin-feature-page channel-discoverability-page">
      <ConsolePageHeader
        eyebrow={t('channelDiscoverability.page.eyebrow')}
        title={t('channelDiscoverability.page.title')}
        description={t('channelDiscoverability.page.description')}
        icon={<EyeOutlined />}
        status={(
          <ConsoleStatusChip tone={listReadState === 'ready' ? (canManage ? 'warning' : 'neutral') : 'danger'}>
            {t(listReadState === 'ready'
              ? canManage
                ? 'channelDiscoverability.page.writable'
                : 'channelDiscoverability.page.readOnly'
              : `channelDiscoverability.state.${listReadState}`)}
          </ConsoleStatusChip>
        )}
        actions={(
          <Button
            icon={<ReloadOutlined />}
            disabled={!canView || listLoading}
            onClick={() => { void loadPage(); }}
          >
            {t(listLoading
              ? 'channelDiscoverability.actions.refreshing'
              : 'channelDiscoverability.actions.refresh')}
          </Button>
        )}
      />

      <ConsoleMetricGrid label={t('channelDiscoverability.metrics.ariaLabel')}>
        <ConsoleMetricCard label={t('channelDiscoverability.metrics.total')} value={metricValue(total)} description={t('channelDiscoverability.metrics.totalDescription')} tone="info" />
        <ConsoleMetricCard label={t('channelDiscoverability.metrics.summary')} value={metricValue(listSnapshot ? summaryCount : null)} description={t('channelDiscoverability.metrics.summaryDescription')} tone="warning" />
        <ConsoleMetricCard label={t('channelDiscoverability.metrics.eligible')} value={metricValue(listSnapshot ? eligibleCount : null)} description={t('channelDiscoverability.metrics.eligibleDescription')} tone="success" />
        <ConsoleMetricCard label={t('channelDiscoverability.metrics.inactive')} value={metricValue(listSnapshot ? inactiveCount : null)} description={t('channelDiscoverability.metrics.inactiveDescription')} />
      </ConsoleMetricGrid>

      <ConsoleResourceList
        toolbar={(
          <ConsoleToolbar
            title={t('channelDiscoverability.filter.title')}
            description={t('channelDiscoverability.filter.description')}
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0
                  ? t('channelDiscoverability.filter.count', { count: activeFilterCount })
                  : t('channelDiscoverability.filter.none')}
              </ConsoleStatusChip>
            )}
          >
            {filterControls}
          </ConsoleToolbar>
        )}
        mobileToolbar={(
          <div className="console-resource-mobile-summary">
            <div className="console-resource-mobile-summary__copy">
              <strong>{total === null
                ? t('channelDiscoverability.state.noSnapshot')
                : t('channelDiscoverability.mobile.results', { count: total })}</strong>
              <span>{activeFilterCount > 0
                ? t('channelDiscoverability.filter.count', { count: activeFilterCount })
                : t('channelDiscoverability.filter.none')}</span>
            </div>
            <div className="console-resource-mobile-summary__actions">
              <Button size="small" icon={<SearchOutlined />} onClick={() => setFilterSheetOpen(true)}>
                {t('channelDiscoverability.mobile.filter')}
              </Button>
              <Button size="small" icon={<ReloadOutlined />} disabled={listLoading} onClick={() => void loadPage()}>
                {t('channelDiscoverability.actions.refresh')}
              </Button>
            </div>
          </div>
        )}
        desktopList={(
          <section className="admin-table-panel">
            {listReadNotice}
            <Table<ChannelDiscoverabilityVo>
              rowKey="voChannelId"
              columns={columns}
              dataSource={items}
              loading={listLoading}
              scroll={{ x: 1350 }}
              pagination={{
                current: query.pageIndex,
                pageSize: query.pageSize,
                total: total ?? 0,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (count) => t('channelDiscoverability.list.total', { count }),
                onChange: handlePageChange,
              }}
            />
          </section>
        )}
        mobileList={(
          <>
            {listReadNotice}
            {listLoading && !listSnapshot ? (
              <div className="console-resource-mobile-loading">{t('channelDiscoverability.mobile.loading')}</div>
            ) : null}
            {listReadState === 'ready' && items.length === 0 ? (
              <div className="console-resource-mobile-empty">
                <strong>{t('channelDiscoverability.mobile.emptyTitle')}</strong>
                <span>{t('channelDiscoverability.mobile.emptyDescription')}</span>
              </div>
            ) : null}
            {items.map((record) => (
              <article className="console-resource-mobile-card channel-discoverability-mobile-card" key={record.voChannelId}>
                <div className="console-resource-mobile-card__header">
                  {renderChannelIdentity(record)}
                  <Tag color={record.voDiscoverVisibility === 1 ? 'processing' : 'default'}>
                    {visibilityLabel(record.voDiscoverVisibility)}
                  </Tag>
                </div>
                <div className="console-resource-mobile-card__facts">
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('channelDiscoverability.table.eligibility')}</span>
                    <strong>{t(record.voCanEnableSummary
                      ? 'channelDiscoverability.eligibility.ready'
                      : 'channelDiscoverability.eligibility.blocked')}</strong>
                  </div>
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('channelDiscoverability.table.lifecycle')}</span>
                    <strong>{t(record.voIsDeleted
                      ? 'channelDiscoverability.lifecycle.deleted'
                      : record.voIsEnabled
                        ? 'channelDiscoverability.lifecycle.enabled'
                        : 'channelDiscoverability.lifecycle.disabled')}</strong>
                  </div>
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('channelDiscoverability.history.version')}</span>
                    <strong>v{record.voDiscoverVisibilityVersion}</strong>
                  </div>
                </div>
                {record.voDescription ? <p className="console-resource-mobile-card__description">{record.voDescription}</p> : null}
                {!record.voCanEnableSummary && record.voEligibilityIssues.length > 0 ? (
                  <Space size={[4, 4]} wrap>
                    {record.voEligibilityIssues.map((issue) => (
                      <Tag key={issue} color="warning">{t(`channelDiscoverability.eligibility.${issue}`, { defaultValue: issue })}</Tag>
                    ))}
                  </Space>
                ) : null}
                <div className="console-resource-mobile-card__footer">
                  <span className="admin-feature-subtle">#{record.voChannelId}</span>
                  {renderActions(record)}
                </div>
              </article>
            ))}
            {items.length > 0 ? (
              <div className="console-resource-mobile-pagination">
                <Button size="small" disabled={query.pageIndex <= 1 || listLoading} onClick={() => handlePageChange(query.pageIndex - 1)}>{t('channelDiscoverability.mobile.previous')}</Button>
                <span>{t('channelDiscoverability.mobile.page', { current: query.pageIndex, total: pageCount })}</span>
                <Button size="small" disabled={query.pageIndex >= pageCount || listLoading} onClick={() => handlePageChange(query.pageIndex + 1)}>{t('channelDiscoverability.mobile.next')}</Button>
              </div>
            ) : null}
          </>
        )}
        context={(
          <>
            <h3>{t('channelDiscoverability.boundary.title')}</h3>
            <p className="admin-feature-subtle">{t('channelDiscoverability.boundary.description')}</p>
            <div className="admin-table-summary">
              <div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('channelDiscoverability.boundary.exposed')}</span><span className="admin-table-summary__value">{t('channelDiscoverability.boundary.exposedValue')}</span></div>
              <div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('channelDiscoverability.boundary.excluded')}</span><span className="admin-table-summary__value">{t('channelDiscoverability.boundary.excludedValue')}</span></div>
              <div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('channelDiscoverability.boundary.tenant')}</span><span className="admin-table-summary__value">{t('channelDiscoverability.boundary.tenantValue')}</span></div>
            </div>
          </>
        )}
      />

      <BottomSheet
        isOpen={filterSheetOpen}
        onClose={() => setFilterSheetOpen(false)}
        closeLabel={t('channelDiscoverability.mobile.closeFilters')}
        title={t('channelDiscoverability.filter.title')}
        height="auto"
        className="console-resource-filter-sheet"
      >
        {filterControls}
      </BottomSheet>

      <Modal
        title={t(mutationVisibility === 1
          ? 'channelDiscoverability.mutation.exposeTitle'
          : 'channelDiscoverability.mutation.hideTitle')}
        open={Boolean(mutationTarget)}
        onCancel={requestCloseMutation}
        onOk={() => { void submitMutation(); }}
        confirmLoading={saving}
        maskClosable={!saving}
        keyboard={!saving}
        okText={t('channelDiscoverability.mutation.confirm')}
        cancelText={t('channelDiscoverability.mutation.cancel')}
        okButtonProps={{
          danger: mutationVisibility === 0,
          disabled: !reason.trim()
            || !actionsAreAuthoritative
            || mutationTargetReadState !== 'ready',
        }}
        destroyOnHidden
      >
        <p className="channel-discoverability-modal-note">
          {mutationTarget
            ? t(mutationVisibility === 1
                ? 'channelDiscoverability.mutation.exposeDescription'
                : 'channelDiscoverability.mutation.hideDescription',
              { name: mutationTarget.voName })
            : null}
        </p>
        <div className="channel-discoverability-mutation-snapshot">
          <span>{t('channelDiscoverability.mutation.snapshot')}</span>
          <strong>{mutationTarget
            ? `${visibilityLabel(mutationTarget.voDiscoverVisibility)} · v${mutationTarget.voDiscoverVisibilityVersion}`
            : '-'}</strong>
        </div>
        {conflictDetected ? (
          <div className="channel-discoverability-conflict" role="alert">
            <strong>{t('channelDiscoverability.mutation.conflictTitle')}</strong>
            <span>{t(mutationTargetReadState === 'ready'
              ? 'channelDiscoverability.mutation.conflictRefreshed'
              : mutationTargetReadState === 'loading'
                ? 'channelDiscoverability.mutation.conflictRefreshing'
                : 'channelDiscoverability.mutation.conflictUnavailable')}</span>
            {mutationTargetReadState === 'unavailable' ? (
              <Button size="small" onClick={() => void refreshMutationTarget()}>
                {t('channelDiscoverability.actions.retry')}
              </Button>
            ) : null}
          </div>
        ) : null}
        {!actionsAreAuthoritative ? (
          <div className="channel-discoverability-conflict" role="alert">
            <strong>{t('channelDiscoverability.mutation.authorityTitle')}</strong>
            <span>{t('channelDiscoverability.mutation.authorityDescription')}</span>
          </div>
        ) : null}
        <div className="channel-discoverability-modal-field">
          <label htmlFor="channel-discoverability-reason">{t('channelDiscoverability.mutation.reason')}</label>
          <Input.TextArea
            id="channel-discoverability-reason"
            rows={4}
            maxLength={500}
            showCount
            disabled={saving}
            value={reason}
            placeholder={t('channelDiscoverability.mutation.reasonPlaceholder')}
            onChange={(event) => setReason(event.target.value)}
          />
        </div>
      </Modal>

      <Modal
        title={t('channelDiscoverability.history.title')}
        open={Boolean(historyTarget)}
        onCancel={closeHistory}
        footer={null}
        width={920}
        destroyOnHidden
      >
        <div className="channel-discoverability-history-summary">
          <div>
            <strong>{historyTarget?.voName}</strong>
            <span>/{historyTarget?.voSlug}</span>
          </div>
          <div className="channel-discoverability-history-summary__actions">
            <ConsoleStatusChip tone={historyReadState === 'ready' ? 'success' : historyReadState === 'stale' ? 'warning' : 'danger'}>
              {t(`channelDiscoverability.state.${historyReadState}`)}
            </ConsoleStatusChip>
            <Button size="small" icon={<ReloadOutlined />} disabled={historyLoading} onClick={() => void loadHistory()}>
              {t('channelDiscoverability.actions.refresh')}
            </Button>
          </div>
        </div>
        {historyReadNotice}
        <div className="channel-discoverability-history-desktop">
          <Table<ChannelDiscoverVisibilityEventVo>
            rowKey="voId"
            columns={historyColumns}
            dataSource={historyItems}
            loading={historyLoading}
            pagination={{
              current: historyPageIndex,
              pageSize: historyPageSize,
              total: historySnapshot?.dataCount ?? 0,
              showSizeChanger: true,
              showTotal: (count) => t('channelDiscoverability.history.total', { count }),
              onChange: (page, size) => {
                setHistoryPageIndex(page);
                setHistoryPageSize(size);
              },
            }}
            scroll={{ x: 850 }}
          />
        </div>
        <div className="channel-discoverability-history-mobile">
          {historyLoading && !historySnapshot ? <div className="console-resource-mobile-loading">{t('channelDiscoverability.history.loading')}</div> : null}
          {historyReadState === 'ready' && historyItems.length === 0 ? <div className="console-resource-mobile-empty"><strong>{t('channelDiscoverability.history.emptyTitle')}</strong><span>{t('channelDiscoverability.history.emptyDescription')}</span></div> : null}
          {historyItems.map((event) => (
            <article className="console-resource-mobile-card channel-discoverability-event-card" key={event.voId}>
              <div className="console-resource-mobile-card__header">
                <div className="console-resource-mobile-card__identity">
                  <strong>{visibilityLabel(event.voFromVisibility)} → {visibilityLabel(event.voToVisibility)}</strong>
                  <span>{formatLocalizedDateTime(event.voCreateTime, language)}</span>
                </div>
                <Tag>v{event.voResultVersion}</Tag>
              </div>
              <p className="console-resource-mobile-card__description">{event.voReason}</p>
              <div className="console-resource-mobile-card__footer">
                <span>{event.voActorName}</span>
                <span>#{event.voActorUserId}</span>
              </div>
            </article>
          ))}
          {historyItems.length > 0 ? (
            <div className="console-resource-mobile-pagination">
              <Button size="small" disabled={historyPageIndex <= 1 || historyLoading} onClick={() => setHistoryPageIndex((current) => current - 1)}>{t('channelDiscoverability.mobile.previous')}</Button>
              <span>{t('channelDiscoverability.mobile.page', { current: historyPageIndex, total: historyPageCount })}</span>
              <Button size="small" disabled={historyPageIndex >= historyPageCount || historyLoading} onClick={() => setHistoryPageIndex((current) => current + 1)}>{t('channelDiscoverability.mobile.next')}</Button>
            </div>
          ) : null}
        </div>
      </Modal>
    </div>
  );
};
