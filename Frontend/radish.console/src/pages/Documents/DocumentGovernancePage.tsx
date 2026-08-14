import { type ChangeEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiResponseError, WikiDraftReviewState, WikiReviewAction, loadAttachmentAssetBlob, type WikiAuthorDraftDetailVo, type WikiReviewActionValue, type WikiReviewQueueItemVo } from '@radish/http';
import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import {
  AntInput as Input,
  AntModal as Modal,
  AntSelect as Select,
  BottomSheet,
  Button,
  CheckOutlined,
  ClockCircleOutlined,
  CloseOutlined,
  DeleteOutlined,
  EyeOutlined,
  FileTextOutlined,
  LockOutlined,
  MarkdownRenderer,
  ReloadOutlined,
  SearchOutlined,
  Space,
  SyncOutlined,
  Table,
  Tag,
  UnlockOutlined,
  buildAttachmentMarkdownUrl,
  message,
  type ProtectedMarkdownAttachmentOptions,
  type TableColumnsType,
} from '@radish/ui';
import {
  archiveWikiDocument,
  deleteWikiDocument,
  exportWikiMarkdown,
  getWikiGovernanceDetail,
  getWikiGovernanceHistory,
  getWikiGovernancePage,
  getWikiReviewDraft,
  getWikiReviewQueue,
  getWikiRevisionDetail,
  getWikiRevisionList,
  importWikiMarkdown,
  publishWikiDocument,
  restoreWikiDocument,
  reviewWikiDraft,
  rollbackWikiRevision,
  unpublishWikiDocument,
  updateWikiAccessPolicy,
  type LongId,
  type WikiDocumentDetailVo,
  type WikiDocumentGovernanceEventVo,
  type WikiDocumentGovernanceMutationVo,
  type WikiDocumentRevisionDetailVo,
  type WikiDocumentRevisionItemVo,
  type WikiDocumentVo,
  type WikiPageModel,
} from '@/api/wikiGovernanceApi';
import { ConsoleMetricCard, ConsoleMetricGrid, ConsolePageHeader, ConsoleResourceList, ConsoleStatusChip, ConsoleToolbar } from '@/components/ConsolePage';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import { useUser } from '@/hooks/useUser';
import { log } from '@/utils/logger';
import {
  DOCUMENT_STATUS,
  DOCUMENT_VISIBILITY,
  formatDocumentDateTime,
  formatDocumentNumber,
  getDocumentAccessSummary,
  getDocumentSourceTypeText,
  getDocumentStatusText,
  getDocumentSummary,
  getDocumentVisibilityText,
} from './documentGovernancePresentation';
import {
  parseDocumentGovernanceQuery,
  serializeDocumentGovernanceQuery,
  type DocumentDeletedFilter,
  type DocumentGovernanceQuery,
  type DocumentSourceFilter,
  type DocumentStatusFilter,
  type DocumentVisibilityFilter,
} from './documentGovernanceUrlState';
import '../adminForm.css';
import '../adminFeature.css';
import './DocumentGovernancePage.css';

type ReadState = 'loading' | 'ready' | 'unavailable' | 'stale';
type GovernanceAction = 'publish' | 'unpublish' | 'archive' | 'delete' | 'restore';

const HISTORY_PAGE_SIZE = 10;
const REVISION_PAGE_SIZE = 10;

const ERROR_KEYS: Readonly<Record<string, string>> = {
  'Wiki.GovernanceVersionConflict': 'documents.governance.feedback.versionConflict',
  'Wiki.DocumentVersionConflict': 'documents.governance.feedback.documentVersionConflict',
  'Wiki.GovernanceReasonRequired': 'documents.governance.feedback.reasonRequired',
  'Wiki.GovernanceTargetUnavailable': 'documents.governance.feedback.targetUnavailable',
  'Wiki.GovernanceActionNotApplicable': 'documents.governance.feedback.actionNotApplicable',
  'Wiki.ChildDocumentConflict': 'error.wiki.child_document_conflict',
  'Wiki.RevisionAlreadyCurrent': 'error.wiki.revision_already_current',
};

function feedback(error: unknown, t: TFunction, fallbackKey: string): string {
  if (error instanceof ApiResponseError && error.code) {
    const key = ERROR_KEYS[error.code];
    if (key) return t(key);
  }
  return t(fallbackKey);
}

function isConflict(error: unknown): boolean {
  return error instanceof ApiResponseError && (
    error.code === 'Wiki.GovernanceVersionConflict' ||
    error.code === 'Wiki.DocumentVersionConflict'
  );
}

function statusTag(status: number, t: TFunction) {
  if (status === DOCUMENT_STATUS.published) return <Tag color="success">{getDocumentStatusText(status, t)}</Tag>;
  if (status === DOCUMENT_STATUS.archived) return <Tag>{getDocumentStatusText(status, t)}</Tag>;
  return <Tag color="warning">{getDocumentStatusText(status, t)}</Tag>;
}

function visibilityTag(visibility: number, t: TFunction) {
  if (visibility === DOCUMENT_VISIBILITY.public) return <Tag color="green">{getDocumentVisibilityText(visibility, t)}</Tag>;
  if (visibility === DOCUMENT_VISIBILITY.restricted) return <Tag color="red">{getDocumentVisibilityText(visibility, t)}</Tag>;
  return <Tag color="blue">{getDocumentVisibilityText(visibility, t)}</Tag>;
}

function reviewStateTag(state: number, t: TFunction) {
  if (state === WikiDraftReviewState.Submitted) return <Tag color="processing">{t('documents.review.state.submitted')}</Tag>;
  if (state === WikiDraftReviewState.ChangesRequested) return <Tag color="warning">{t('documents.review.state.changesRequested')}</Tag>;
  return <Tag>{t('documents.review.state.other')}</Tag>;
}

function splitAccessList(value: string): string[] {
  return value.split(/[\n,，;；\s]+/g).map((item) => item.trim()).filter(Boolean);
}

function isBuiltInDocument(record?: WikiDocumentVo | WikiDocumentDetailVo | null): boolean {
  return record?.voSourceType === 'BuiltIn';
}

function triggerDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

function statusValue(value: DocumentStatusFilter): number | undefined {
  if (value === 'draft') return DOCUMENT_STATUS.draft;
  if (value === 'published') return DOCUMENT_STATUS.published;
  if (value === 'archived') return DOCUMENT_STATUS.archived;
  return undefined;
}

function visibilityValue(value: DocumentVisibilityFilter): number | undefined {
  if (value === 'public') return DOCUMENT_VISIBILITY.public;
  if (value === 'authenticated') return DOCUMENT_VISIBILITY.authenticated;
  if (value === 'restricted') return DOCUMENT_VISIBILITY.restricted;
  return undefined;
}

function matchesQuery(document: WikiDocumentVo, query: DocumentGovernanceQuery): boolean {
  const statusMatches = query.status === 'all' || document.voStatus === statusValue(query.status);
  const visibilityMatches = query.visibility === 'all' || document.voVisibility === visibilityValue(query.visibility);
  const sourceMatches = query.sourceType === 'all' || document.voSourceType === query.sourceType;
  const deletedMatches = query.deleted === 'all' || (query.deleted === 'deleted' ? document.voIsDeleted : !document.voIsDeleted);
  return statusMatches && visibilityMatches && sourceMatches && deletedMatches;
}

export const DocumentGovernancePage = () => {
  const { t, i18n } = useTranslation();
  const { user } = useUser();
  const accountKey = user?.voUserId ?? 'anonymous';
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('documents.documentTitle'));
  const [searchParams, setSearchParams] = useSearchParams();
  const query = useMemo(() => parseDocumentGovernanceQuery(searchParams), [searchParams]);
  const queryRef = useRef(query);
  queryRef.current = query;
  const listQueryKey = useMemo(() => [
    accountKey,
    query.pageIndex,
    query.pageSize,
    query.keyword,
    query.status,
    query.visibility,
    query.sourceType,
    query.deleted,
  ].join('\u001f'), [
    accountKey,
    query.deleted,
    query.keyword,
    query.pageIndex,
    query.pageSize,
    query.sourceType,
    query.status,
    query.visibility,
  ]);
  const updateQuery = useCallback((next: DocumentGovernanceQuery) => {
    setSearchParams(serializeDocumentGovernanceQuery(next), { replace: true });
  }, [setSearchParams]);

  const canView = usePermission(CONSOLE_PERMISSIONS.docsView);
  const canReview = usePermission(CONSOLE_PERMISSIONS.docsReview);
  const canPublish = usePermission(CONSOLE_PERMISSIONS.docsPublish);
  const canArchive = usePermission(CONSOLE_PERMISSIONS.docsArchive);
  const canDelete = usePermission(CONSOLE_PERMISSIONS.docsDelete);
  const canRestore = usePermission(CONSOLE_PERMISSIONS.docsRestore);
  const canUpdatePermissions = usePermission(CONSOLE_PERMISSIONS.docsPermissions);
  const canRollback = usePermission(CONSOLE_PERMISSIONS.docsRollback);
  const canImport = usePermission(CONSOLE_PERMISSIONS.docsImport);
  const canExport = usePermission(CONSOLE_PERMISSIONS.docsExport);

  const [keywordDraft, setKeywordDraft] = useState(query.keyword);
  const [statusDraft, setStatusDraft] = useState(query.status);
  const [visibilityDraft, setVisibilityDraft] = useState(query.visibility);
  const [sourceDraft, setSourceDraft] = useState(query.sourceType);
  const [deletedDraft, setDeletedDraft] = useState(query.deleted);
  const [filterSheetOpen, setFilterSheetOpen] = useState(false);
  const importInputRef = useRef<HTMLInputElement | null>(null);
  const actionAccountEpochRef = useRef(0);
  const [importing, setImporting] = useState(false);

  const [listSnapshot, setListSnapshot] = useState<WikiPageModel<WikiDocumentVo> | null>(null);
  const listSnapshotRef = useRef<WikiPageModel<WikiDocumentVo> | null>(null);
  const listSnapshotKey = useRef<string | undefined>(undefined);
  const listGeneration = useRef(0);
  const [listReadState, setListReadState] = useState<ReadState>('loading');
  const [listLoading, setListLoading] = useState(false);

  const [reviewSnapshot, setReviewSnapshot] = useState<WikiPageModel<WikiReviewQueueItemVo> | null>(null);
  const reviewSnapshotRef = useRef<WikiPageModel<WikiReviewQueueItemVo> | null>(null);
  const reviewSnapshotKey = useRef<string | undefined>(undefined);
  const reviewGeneration = useRef(0);
  const reviewAccountEpochRef = useRef(0);
  const [reviewReadState, setReviewReadState] = useState<ReadState>('loading');
  const [reviewLoading, setReviewLoading] = useState(false);

  const [selectedDocument, setSelectedDocument] = useState<WikiDocumentDetailVo | null>(null);
  const selectedDocumentRef = useRef<WikiDocumentDetailVo | null>(null);
  const selectedDocumentAccount = useRef<string | undefined>(undefined);
  const selectedGeneration = useRef(0);
  const [selectedReadState, setSelectedReadState] = useState<ReadState>('loading');
  const [selectedLoading, setSelectedLoading] = useState(false);

  const [historySnapshot, setHistorySnapshot] = useState<WikiPageModel<WikiDocumentGovernanceEventVo> | null>(null);
  const historySnapshotRef = useRef<WikiPageModel<WikiDocumentGovernanceEventVo> | null>(null);
  const historySnapshotKey = useRef<string | undefined>(undefined);
  const historyGeneration = useRef(0);
  const [historyReadState, setHistoryReadState] = useState<ReadState>('loading');
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyPage, setHistoryPage] = useState(1);

  const [detailDocument, setDetailDocument] = useState<WikiDocumentDetailVo | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const detailGeneration = useRef(0);

  const [reviewDraft, setReviewDraft] = useState<WikiAuthorDraftDetailVo | null>(null);
  const [reviewOfficialDocument, setReviewOfficialDocument] = useState<WikiDocumentDetailVo | null>(null);
  const [reviewDraftLoading, setReviewDraftLoading] = useState(false);
  const [reviewComment, setReviewComment] = useState('');
  const [reviewSaving, setReviewSaving] = useState(false);
  const reviewEvidenceGeneration = useRef(0);

  const [revisionTarget, setRevisionTarget] = useState<WikiDocumentDetailVo | null>(null);
  const [revisionSnapshot, setRevisionSnapshot] = useState<WikiPageModel<WikiDocumentRevisionItemVo> | null>(null);
  const [revisionDetail, setRevisionDetail] = useState<WikiDocumentRevisionDetailVo | null>(null);
  const [revisionPage, setRevisionPage] = useState(1);
  const [revisionLoading, setRevisionLoading] = useState(false);
  const [revisionDetailLoading, setRevisionDetailLoading] = useState(false);
  const revisionGeneration = useRef(0);
  const revisionDetailGeneration = useRef(0);
  const [rollbackReason, setRollbackReason] = useState('');
  const [rollbackSaving, setRollbackSaving] = useState(false);
  const [rollbackConflict, setRollbackConflict] = useState(false);

  const [governanceAction, setGovernanceAction] = useState<GovernanceAction>();
  const [governanceReason, setGovernanceReason] = useState('');
  const [governanceSaving, setGovernanceSaving] = useState(false);
  const [governanceConflict, setGovernanceConflict] = useState(false);
  const [accessOpen, setAccessOpen] = useState(false);
  const [accessVisibility, setAccessVisibility] = useState(String(DOCUMENT_VISIBILITY.authenticated));
  const [accessRoles, setAccessRoles] = useState('');
  const [accessPermissions, setAccessPermissions] = useState('');
  const [accessReason, setAccessReason] = useState('');
  const [accessSaving, setAccessSaving] = useState(false);
  const [accessConflict, setAccessConflict] = useState(false);

  useEffect(() => {
    setKeywordDraft(query.keyword);
    setStatusDraft(query.status);
    setVisibilityDraft(query.visibility);
    setSourceDraft(query.sourceType);
    setDeletedDraft(query.deleted);
  }, [query.deleted, query.keyword, query.sourceType, query.status, query.visibility]);

  const loadList = useCallback(async () => {
    const generation = ++listGeneration.current;
    const hasSnapshot = listSnapshotRef.current !== null && listSnapshotKey.current === listQueryKey;
    setListLoading(true);
    setListReadState('loading');
    if (!hasSnapshot) setListSnapshot(null);
    try {
      const result = await getWikiGovernancePage({
        pageIndex: query.pageIndex,
        pageSize: query.pageSize,
        keyword: query.keyword || undefined,
        status: statusValue(query.status),
        visibility: visibilityValue(query.visibility),
        sourceType: query.sourceType === 'all' ? undefined : query.sourceType,
        includeDeleted: query.deleted !== 'active',
        deletedOnly: query.deleted === 'deleted',
      }, t);
      if (generation !== listGeneration.current) return;
      if (result.data.length === 0 && result.dataCount > 0 && query.pageIndex > Math.max(1, result.pageCount)) {
        updateQuery({ ...queryRef.current, pageIndex: Math.max(1, result.pageCount) });
        return;
      }
      listSnapshotRef.current = result;
      listSnapshotKey.current = listQueryKey;
      setListSnapshot(result);
      setListReadState('ready');
    } catch (error) {
      if (generation !== listGeneration.current) return;
      log.error('DocumentGovernancePage', '加载文档治理列表失败:', error);
      setListReadState(hasSnapshot ? 'stale' : 'unavailable');
      message.error(feedback(error, t, 'documents.feedback.loadListFailed'));
    } finally {
      if (generation === listGeneration.current) setListLoading(false);
    }
  }, [
    listQueryKey,
    query.deleted,
    query.keyword,
    query.pageIndex,
    query.pageSize,
    query.sourceType,
    query.status,
    query.visibility,
    t,
    updateQuery,
  ]);

  const loadReviewQueue = useCallback(async () => {
    if (!canReview) {
      reviewSnapshotRef.current = null;
      setReviewSnapshot(null);
      setReviewReadState('unavailable');
      return;
    }
    const generation = ++reviewGeneration.current;
    const accountEpoch = reviewAccountEpochRef.current;
    const snapshotKey = `${accountKey}:${query.reviewPageIndex}:${query.reviewPageSize}`;
    const hasSnapshot = reviewSnapshotRef.current !== null && reviewSnapshotKey.current === snapshotKey;
    setReviewLoading(true);
    setReviewReadState('loading');
    if (!hasSnapshot) setReviewSnapshot(null);
    try {
      const result = await getWikiReviewQueue(query.reviewPageIndex, query.reviewPageSize, t);
      if (generation !== reviewGeneration.current || accountEpoch !== reviewAccountEpochRef.current) return;
      if (result.data.length === 0 && result.dataCount > 0 && query.reviewPageIndex > Math.max(1, result.pageCount)) {
        updateQuery({ ...queryRef.current, reviewPageIndex: Math.max(1, result.pageCount) });
        return;
      }
      reviewSnapshotRef.current = result;
      reviewSnapshotKey.current = snapshotKey;
      setReviewSnapshot(result);
      setReviewReadState('ready');
    } catch (error) {
      if (generation !== reviewGeneration.current || accountEpoch !== reviewAccountEpochRef.current) return;
      log.error('DocumentGovernancePage', '加载文档审核队列失败:', error);
      setReviewReadState(hasSnapshot ? 'stale' : 'unavailable');
      message.error(feedback(error, t, 'documents.review.feedback.loadQueueFailed'));
    } finally {
      if (generation === reviewGeneration.current && accountEpoch === reviewAccountEpochRef.current) setReviewLoading(false);
    }
  }, [accountKey, canReview, query.reviewPageIndex, query.reviewPageSize, t, updateQuery]);

  const loadSelected = useCallback(async () => {
    if (!query.selectedDocumentId || !canView) {
      selectedGeneration.current += 1;
      selectedDocumentRef.current = null;
      selectedDocumentAccount.current = undefined;
      setSelectedDocument(null);
      setSelectedReadState('unavailable');
      return;
    }
    const generation = ++selectedGeneration.current;
    const hasSnapshot = selectedDocumentRef.current?.voId === query.selectedDocumentId &&
      selectedDocumentAccount.current === accountKey;
    setSelectedLoading(true);
    setSelectedReadState('loading');
    if (!hasSnapshot) {
      selectedDocumentRef.current = null;
      setSelectedDocument(null);
    }
    try {
      const document = await getWikiGovernanceDetail(query.selectedDocumentId, true, t);
      if (generation !== selectedGeneration.current) return;
      selectedDocumentRef.current = document;
      selectedDocumentAccount.current = accountKey;
      setSelectedDocument(document);
      setSelectedReadState('ready');
    } catch (error) {
      if (generation !== selectedGeneration.current) return;
      log.error('DocumentGovernancePage', '加载治理目标失败:', error);
      setSelectedReadState(hasSnapshot ? 'stale' : 'unavailable');
      message.error(feedback(error, t, 'documents.governance.feedback.targetUnavailable'));
    } finally {
      if (generation === selectedGeneration.current) setSelectedLoading(false);
    }
  }, [accountKey, canView, query.selectedDocumentId, t]);

  const loadHistory = useCallback(async (page: number) => {
    if (!query.selectedDocumentId || !canView) {
      historyGeneration.current += 1;
      historySnapshotRef.current = null;
      setHistorySnapshot(null);
      setHistoryReadState('unavailable');
      return;
    }
    const generation = ++historyGeneration.current;
    const snapshotKey = `${accountKey}:${query.selectedDocumentId}:${page}`;
    const hasSnapshot = historySnapshotRef.current !== null && historySnapshotKey.current === snapshotKey;
    setHistoryLoading(true);
    setHistoryReadState('loading');
    if (!hasSnapshot) setHistorySnapshot(null);
    try {
      const result = await getWikiGovernanceHistory(query.selectedDocumentId, page, HISTORY_PAGE_SIZE, t);
      if (generation !== historyGeneration.current) return;
      historySnapshotRef.current = result;
      historySnapshotKey.current = snapshotKey;
      setHistorySnapshot(result);
      setHistoryPage(result.page);
      setHistoryReadState('ready');
    } catch (error) {
      if (generation !== historyGeneration.current) return;
      log.error('DocumentGovernancePage', '加载治理事件失败:', error);
      setHistoryReadState(hasSnapshot ? 'stale' : 'unavailable');
      message.error(feedback(error, t, 'documents.governance.feedback.loadHistoryFailed'));
    } finally {
      if (generation === historyGeneration.current) setHistoryLoading(false);
    }
  }, [accountKey, canView, query.selectedDocumentId, t]);

  useEffect(() => {
    actionAccountEpochRef.current += 1;
    listGeneration.current += 1;
    reviewAccountEpochRef.current += 1;
    reviewGeneration.current += 1;
    selectedGeneration.current += 1;
    historyGeneration.current += 1;
    detailGeneration.current += 1;
    reviewEvidenceGeneration.current += 1;
    revisionGeneration.current += 1;
    revisionDetailGeneration.current += 1;
    listSnapshotRef.current = null;
    listSnapshotKey.current = undefined;
    setListSnapshot(null);
    setListReadState('loading');
    setListLoading(false);
    reviewSnapshotRef.current = null;
    reviewSnapshotKey.current = undefined;
    setReviewSnapshot(null);
    setReviewReadState('loading');
    selectedDocumentRef.current = null;
    selectedDocumentAccount.current = undefined;
    setSelectedDocument(null);
    setSelectedReadState('loading');
    setSelectedLoading(false);
    historySnapshotRef.current = null;
    historySnapshotKey.current = undefined;
    setHistorySnapshot(null);
    setHistoryReadState('loading');
    setHistoryLoading(false);
    setHistoryPage(1);
    setDetailDocument(null);
    setDetailLoading(false);
    setReviewDraft(null);
    setReviewOfficialDocument(null);
    setReviewLoading(false);
    setReviewDraftLoading(false);
    setReviewSaving(false);
    setRevisionTarget(null);
    setRevisionSnapshot(null);
    setRevisionDetail(null);
    setRevisionLoading(false);
    setRevisionDetailLoading(false);
    setRollbackReason('');
    setRollbackSaving(false);
    setGovernanceAction(undefined);
    setGovernanceReason('');
    setGovernanceSaving(false);
    setAccessOpen(false);
    setAccessReason('');
    setAccessSaving(false);
    setImporting(false);
  }, [accountKey]);
  useEffect(() => { if (canView) void loadList(); }, [canView, loadList]);
  useEffect(() => { void loadReviewQueue(); }, [loadReviewQueue, user?.voUserId]);
  useEffect(() => { void loadSelected(); }, [loadSelected]);
  useEffect(() => {
    setHistoryPage(1);
    historySnapshotRef.current = null;
    historySnapshotKey.current = undefined;
    setHistorySnapshot(null);
    void loadHistory(1);
  }, [loadHistory, query.selectedDocumentId]);
  useEffect(() => () => {
    listGeneration.current += 1;
    reviewGeneration.current += 1;
    selectedGeneration.current += 1;
    historyGeneration.current += 1;
    detailGeneration.current += 1;
    reviewEvidenceGeneration.current += 1;
    revisionGeneration.current += 1;
    revisionDetailGeneration.current += 1;
  }, []);

  const items = listSnapshot?.data ?? [];
  const total = listSnapshot?.dataCount ?? null;
  const pageCount = Math.max(1, listSnapshot?.pageCount ?? 1);
  const reviewItems = reviewSnapshot?.data ?? [];
  const reviewPageCount = Math.max(1, reviewSnapshot?.pageCount ?? 1);
  const activeFilterCount = [query.keyword, query.status !== 'all' && query.status, query.visibility !== 'all' && query.visibility, query.sourceType !== 'all' && query.sourceType, query.deleted !== 'active' && query.deleted].filter(Boolean).length;
  const publishedCount = items.filter((item) => item.voStatus === DOCUMENT_STATUS.published && !item.voIsDeleted).length;
  const restrictedCount = items.filter((item) => item.voVisibility === DOCUMENT_VISIBILITY.restricted).length;
  const builtInCount = items.filter(isBuiltInDocument).length;
  const targetAuthoritative = Boolean(selectedDocument) && selectedReadState === 'ready' && !selectedLoading;
  const revisionTargetAuthoritative = targetAuthoritative && selectedDocument?.voId === revisionTarget?.voId;

  const consumeMutation = useCallback((mutation: WikiDocumentGovernanceMutationVo) => {
    selectedDocumentRef.current = mutation.voDocument;
    selectedDocumentAccount.current = accountKey;
    setSelectedDocument(mutation.voDocument);
    setSelectedReadState('ready');
    const current = listSnapshotRef.current;
    if (current) {
      const existing = current.data.find((item) => item.voId === mutation.voDocument.voId);
      if (existing) {
        const matches = matchesQuery(mutation.voDocument, query);
        const snapshot = {
          ...current,
          data: matches
            ? current.data.map((item) => item.voId === mutation.voDocument.voId ? mutation.voDocument : item)
            : current.data.filter((item) => item.voId !== mutation.voDocument.voId),
          dataCount: matches ? current.dataCount : Math.max(0, current.dataCount - 1),
        };
        listSnapshotRef.current = snapshot;
        setListSnapshot(snapshot);
      }
      setListReadState('stale');
    }
    if (historyPage === 1 && historySnapshotRef.current) {
      const currentHistory = historySnapshotRef.current;
      const snapshot = {
        ...currentHistory,
        dataCount: currentHistory.dataCount + 1,
        data: [mutation.voEvent, ...currentHistory.data.filter((item) => item.voId !== mutation.voEvent.voId)].slice(0, HISTORY_PAGE_SIZE),
      };
      historySnapshotRef.current = snapshot;
      setHistorySnapshot(snapshot);
    }
  }, [accountKey, historyPage, query]);

  const refreshTargetAfterConflict = async () => {
    await loadSelected();
    await loadHistory(1);
  };

  const submitGovernanceAction = async () => {
    if (!selectedDocument || !governanceAction || !governanceReason.trim() || !targetAuthoritative) return;
    const actionAllowed = governanceAction === 'publish' || governanceAction === 'unpublish'
      ? canPublish
      : governanceAction === 'archive'
        ? canArchive
        : governanceAction === 'delete'
          ? canDelete
          : canRestore;
    if (!actionAllowed || isBuiltInDocument(selectedDocument)) {
      message.error(t('documents.governance.feedback.actionNotApplicable'));
      return;
    }
    const accountEpoch = actionAccountEpochRef.current;
    try {
      setGovernanceSaving(true);
      const request = { expectedGovernanceVersion: selectedDocument.voGovernanceVersion, reason: governanceReason.trim() };
      let mutation: WikiDocumentGovernanceMutationVo;
      if (governanceAction === 'publish') {
        mutation = await publishWikiDocument(selectedDocument.voId, { ...request, expectedDocumentVersion: selectedDocument.voVersion }, t);
      } else if (governanceAction === 'unpublish') {
        mutation = await unpublishWikiDocument(selectedDocument.voId, request, t);
      } else if (governanceAction === 'archive') {
        mutation = await archiveWikiDocument(selectedDocument.voId, request, t);
      } else if (governanceAction === 'delete') {
        mutation = await deleteWikiDocument(selectedDocument.voId, request, t);
      } else {
        mutation = await restoreWikiDocument(selectedDocument.voId, request, t);
      }
      if (accountEpoch !== actionAccountEpochRef.current) return;
      consumeMutation(mutation);
      message.success(t(`documents.feedback.${governanceAction === 'publish' ? 'published' : governanceAction === 'unpublish' ? 'unpublished' : governanceAction === 'archive' ? 'archived' : governanceAction === 'delete' ? 'deleted' : 'restored'}`));
      setGovernanceAction(undefined);
      setGovernanceReason('');
      setGovernanceConflict(false);
      await Promise.all([loadList(), loadHistory(1)]);
    } catch (error) {
      if (accountEpoch !== actionAccountEpochRef.current) return;
      log.error('DocumentGovernancePage', '文档治理动作失败:', error);
      if (isConflict(error)) {
        setGovernanceConflict(true);
        await refreshTargetAfterConflict();
      }
      message.error(feedback(error, t, 'documents.governance.feedback.actionFailed'));
    } finally {
      if (accountEpoch === actionAccountEpochRef.current) setGovernanceSaving(false);
    }
  };

  const submitAccessPolicy = async () => {
    const accessDocument = selectedDocument;
    if (!accessDocument || !accessReason.trim() || !targetAuthoritative) return;
    if (!canUpdatePermissions || accessDocument.voIsDeleted || isBuiltInDocument(accessDocument)) {
      message.error(t('documents.feedback.accessUnavailable'));
      return;
    }
    const allowedRoles = splitAccessList(accessRoles);
    const allowedPermissions = splitAccessList(accessPermissions);
    const visibility = Number(accessVisibility);
    if (visibility === DOCUMENT_VISIBILITY.restricted && allowedRoles.length === 0 && allowedPermissions.length === 0) {
      message.error(t('documents.feedback.restrictedAccessRequired'));
      return;
    }
    const accountEpoch = actionAccountEpochRef.current;
    try {
      setAccessSaving(true);
      const mutation = await updateWikiAccessPolicy(accessDocument.voId, {
        visibility,
        allowedRoles,
        allowedPermissions,
        expectedGovernanceVersion: accessDocument.voGovernanceVersion,
        reason: accessReason.trim(),
      }, t);
      if (accountEpoch !== actionAccountEpochRef.current) return;
      consumeMutation(mutation);
      message.success(t('documents.feedback.accessUpdated'));
      setAccessOpen(false);
      setAccessReason('');
      setAccessConflict(false);
      await Promise.all([loadList(), loadHistory(1)]);
    } catch (error) {
      if (accountEpoch !== actionAccountEpochRef.current) return;
      log.error('DocumentGovernancePage', '更新访问策略失败:', error);
      if (isConflict(error)) {
        setAccessConflict(true);
        await refreshTargetAfterConflict();
      }
      message.error(feedback(error, t, 'documents.feedback.accessUpdateFailed'));
    } finally {
      if (accountEpoch === actionAccountEpochRef.current) setAccessSaving(false);
    }
  };

  const openDetail = async (id: LongId) => {
    const generation = ++detailGeneration.current;
    setDetailDocument(null);
    setDetailLoading(true);
    try {
      const document = await getWikiGovernanceDetail(id, true, t);
      if (generation === detailGeneration.current) setDetailDocument(document);
    } catch (error) {
      if (generation === detailGeneration.current) message.error(feedback(error, t, 'documents.feedback.loadDetailFailed'));
    } finally {
      if (generation === detailGeneration.current) setDetailLoading(false);
    }
  };

  const openReview = async (record: WikiReviewQueueItemVo) => {
    const generation = ++reviewEvidenceGeneration.current;
    const accountEpoch = reviewAccountEpochRef.current;
    setReviewDraft(null);
    setReviewOfficialDocument(null);
    setReviewComment('');
    setReviewDraftLoading(true);
    try {
      const [draft, official] = await Promise.all([
        getWikiReviewDraft(record.voDraftId, t),
        getWikiGovernanceDetail(record.voDocumentId, false, t),
      ]);
      if (generation !== reviewEvidenceGeneration.current || accountEpoch !== reviewAccountEpochRef.current) return;
      setReviewDraft(draft);
      setReviewOfficialDocument(official);
    } catch (error) {
      if (generation === reviewEvidenceGeneration.current && accountEpoch === reviewAccountEpochRef.current) message.error(feedback(error, t, 'documents.review.feedback.loadDraftFailed'));
    } finally {
      if (generation === reviewEvidenceGeneration.current && accountEpoch === reviewAccountEpochRef.current) setReviewDraftLoading(false);
    }
  };

  const submitReview = async (action: WikiReviewActionValue) => {
    if (!reviewDraft || !canReview) return;
    const comment = reviewComment.trim();
    if (action !== WikiReviewAction.Apply && !comment) {
      message.error(t('documents.review.feedback.commentRequired'));
      return;
    }
    const accountEpoch = reviewAccountEpochRef.current;
    try {
      setReviewSaving(true);
      await reviewWikiDraft(reviewDraft.voDraftId, {
        action,
        expectedDraftVersion: reviewDraft.voDraftVersion,
        expectedDocumentVersion: reviewDraft.voDocumentVersion,
        comment: comment || undefined,
        finalParentId: reviewDraft.voProposedParentId,
      }, t);
      if (accountEpoch !== reviewAccountEpochRef.current) return;
      message.success(action === WikiReviewAction.Apply ? t('documents.review.feedback.applied') : t('documents.review.feedback.returned'));
      setReviewDraft(null);
      setReviewOfficialDocument(null);
      setReviewComment('');
      await Promise.all([loadReviewQueue(), loadList()]);
      if (query.selectedDocumentId === reviewDraft.voDocumentId) await loadSelected();
    } catch (error) {
      if (accountEpoch !== reviewAccountEpochRef.current) return;
      message.error(feedback(error, t, 'documents.review.feedback.actionFailed'));
    } finally {
      if (accountEpoch === reviewAccountEpochRef.current) setReviewSaving(false);
    }
  };

  const loadRevisionDetail = async (revisionId: LongId) => {
    const generation = ++revisionDetailGeneration.current;
    setRevisionDetailLoading(true);
    try {
      const detail = await getWikiRevisionDetail(revisionId, t);
      if (generation === revisionDetailGeneration.current) setRevisionDetail(detail);
    } catch (error) {
      if (generation === revisionDetailGeneration.current) message.error(feedback(error, t, 'documents.feedback.loadRevisionDetailFailed'));
    } finally {
      if (generation === revisionDetailGeneration.current) setRevisionDetailLoading(false);
    }
  };

  const loadRevisions = async (target: WikiDocumentDetailVo, page = 1) => {
    const generation = ++revisionGeneration.current;
    setRevisionTarget(target);
    setRevisionLoading(true);
    try {
      const result = await getWikiRevisionList(target.voId, page, REVISION_PAGE_SIZE, t);
      if (generation !== revisionGeneration.current) return;
      setRevisionSnapshot(result);
      setRevisionPage(result.page);
      const initial = result.data[0];
      setRevisionDetail(null);
      if (initial) await loadRevisionDetail(initial.voId);
    } catch (error) {
      if (generation === revisionGeneration.current) message.error(feedback(error, t, 'documents.feedback.loadRevisionsFailed'));
    } finally {
      if (generation === revisionGeneration.current) setRevisionLoading(false);
    }
  };

  const submitRollback = async () => {
    const revisionDocument = revisionTarget;
    if (!canRollback || !revisionDocument || revisionDocument.voIsDeleted || isBuiltInDocument(revisionDocument)) {
      message.error(t('documents.feedback.rollbackUnavailable'));
      return;
    }
    if (!revisionDetail || !rollbackReason.trim() || !revisionTargetAuthoritative || !selectedDocument) return;
    const accountEpoch = actionAccountEpochRef.current;
    try {
      setRollbackSaving(true);
      const mutation = await rollbackWikiRevision(revisionDetail.voId, {
        expectedGovernanceVersion: selectedDocument.voGovernanceVersion,
        expectedDocumentVersion: selectedDocument.voVersion,
        reason: rollbackReason.trim(),
      }, t);
      if (accountEpoch !== actionAccountEpochRef.current) return;
      consumeMutation(mutation);
      setRevisionTarget(mutation.voDocument);
      setRollbackReason('');
      setRollbackConflict(false);
      message.success(t('documents.feedback.rolledBack'));
      await Promise.all([loadRevisions(mutation.voDocument, 1), loadList(), loadHistory(1)]);
    } catch (error) {
      if (accountEpoch !== actionAccountEpochRef.current) return;
      if (isConflict(error)) {
        setRollbackConflict(true);
        await refreshTargetAfterConflict();
      }
      message.error(feedback(error, t, 'documents.feedback.rollbackFailed'));
    } finally {
      if (accountEpoch === actionAccountEpochRef.current) setRollbackSaving(false);
    }
  };

  const importFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    if (!canImport) {
      message.error(t('documents.feedback.importForbidden'));
      return;
    }
    if (!/\.(md|markdown|txt)$/i.test(file.name)) {
      message.error(t('documents.feedback.importUnsupported'));
      return;
    }
    const accountEpoch = actionAccountEpochRef.current;
    try {
      setImporting(true);
      const id = await importWikiMarkdown(file, t);
      if (accountEpoch !== actionAccountEpochRef.current) return;
      message.success(t('documents.feedback.imported', { id }));
      updateQuery({ ...query, pageIndex: 1, selectedDocumentId: id });
    } catch (error) {
      if (accountEpoch !== actionAccountEpochRef.current) return;
      message.error(feedback(error, t, 'documents.feedback.importFailed'));
    } finally {
      if (accountEpoch === actionAccountEpochRef.current) setImporting(false);
    }
  };

  const exportDocument = async (record: WikiDocumentVo) => {
    if (!canExport) {
      message.error(t('documents.feedback.exportForbidden'));
      return;
    }
    const accountEpoch = actionAccountEpochRef.current;
    try {
      const result = await exportWikiMarkdown(record.voId, t);
      if (accountEpoch !== actionAccountEpochRef.current) return;
      triggerDownload(result.blob, result.fileName);
      message.success(t('documents.feedback.exported'));
    } catch (error) {
      if (accountEpoch !== actionAccountEpochRef.current) return;
      message.error(feedback(error, t, 'documents.feedback.exportFailed'));
    }
  };

  const protectedAttachments = useMemo<ProtectedMarkdownAttachmentOptions>(() => ({
    loadBlob: loadAttachmentAssetBlob,
    scopeKey: ['console-reviewer', user?.voUserId ?? 'anonymous', detailDocument?.voId ?? 'none', reviewDraft?.voDraftId ?? 'none', revisionDetail?.voId ?? 'none'].join(':'),
    labels: {
      loading: t('documents.attachments.loading'),
      loadFailed: t('documents.attachments.loadFailed'),
      retry: t('documents.attachments.retry'),
      download: t('documents.attachments.download'),
      openImage: t('documents.attachments.openImage'),
      lightboxClose: t('documents.attachments.lightboxClose'),
      lightboxPrevious: t('documents.attachments.lightboxPrevious'),
      lightboxNext: t('documents.attachments.lightboxNext'),
    },
  }), [detailDocument?.voId, revisionDetail?.voId, reviewDraft?.voDraftId, t, user?.voUserId]);

  const applyFilters = () => {
    updateQuery({ ...query, pageIndex: 1, keyword: keywordDraft.trim(), status: statusDraft, visibility: visibilityDraft, sourceType: sourceDraft, deleted: deletedDraft });
    setFilterSheetOpen(false);
  };
  const resetFilters = () => {
    setKeywordDraft('');
    setStatusDraft('all');
    setVisibilityDraft('all');
    setSourceDraft('all');
    setDeletedDraft('active');
    updateQuery({ ...query, pageIndex: 1, keyword: '', status: 'all', visibility: 'all', sourceType: 'all', deleted: 'active' });
    setFilterSheetOpen(false);
  };

  const filterControls = (
    <div className="admin-table-toolbar__filters document-governance-filters">
      <Input allowClear prefix={<SearchOutlined />} placeholder={t('documents.filter.placeholder')} value={keywordDraft} onChange={(event) => setKeywordDraft(event.target.value)} onPressEnter={applyFilters} />
      <Select value={statusDraft} options={[
        { label: t('documents.status.all'), value: 'all' },
        { label: t('documents.status.draft'), value: 'draft' },
        { label: t('documents.status.published'), value: 'published' },
        { label: t('documents.status.archived'), value: 'archived' },
      ]} onChange={(value) => setStatusDraft(value as DocumentStatusFilter)} />
      <Select value={visibilityDraft} options={[
        { label: t('documents.visibility.all'), value: 'all' },
        { label: t('documents.visibility.public'), value: 'public' },
        { label: t('documents.visibility.authenticated'), value: 'authenticated' },
        { label: t('documents.visibility.restricted'), value: 'restricted' },
      ]} onChange={(value) => setVisibilityDraft(value as DocumentVisibilityFilter)} />
      <Select value={sourceDraft} options={[
        { label: t('documents.source.all'), value: 'all' },
        { label: t('documents.source.custom'), value: 'Custom' },
        { label: t('documents.source.imported'), value: 'Imported' },
        { label: t('documents.source.builtin'), value: 'BuiltIn' },
      ]} onChange={(value) => setSourceDraft(value as DocumentSourceFilter)} />
      <Select value={deletedDraft} options={[
        { label: t('documents.deleted.active'), value: 'active' },
        { label: t('documents.deleted.all'), value: 'all' },
        { label: t('documents.deleted.only'), value: 'deleted' },
      ]} onChange={(value) => setDeletedDraft(value as DocumentDeletedFilter)} />
      <Space wrap>
        <Button variant="primary" onClick={applyFilters}>{t('documents.filter.apply')}</Button>
        <Button variant="ghost" onClick={resetFilters}>{t('documents.filter.reset')}</Button>
      </Space>
    </div>
  );

  const renderIdentity = (record: WikiDocumentVo) => (
    <Space orientation="vertical" size={2}>
      <strong>{record.voTitle}</strong>
      <span className="admin-feature-subtle">{record.voSlug}</span>
    </Space>
  );
  const selectTarget = (record: WikiDocumentVo) => updateQuery({ ...query, selectedDocumentId: record.voId });
  const rowActions = (record: WikiDocumentVo) => (
    <Space size="small" wrap>
      <Button size="small" variant="primary" onClick={() => selectTarget(record)}>{t('documents.actions.select')}</Button>
      <Button size="small" variant="ghost" icon={<EyeOutlined />} onClick={() => void openDetail(record.voId)}>{t('documents.actions.detail')}</Button>
      {canExport ? <Button size="small" variant="ghost" icon={<FileTextOutlined />} onClick={() => void exportDocument(record)}>{t('documents.actions.export')}</Button> : null}
    </Space>
  );
  const columns: TableColumnsType<WikiDocumentVo> = [
    { title: t('documents.table.document'), key: 'document', width: 270, render: (_, record) => renderIdentity(record) },
    { title: t('documents.table.status'), key: 'status', width: 110, render: (_, record) => record.voIsDeleted ? <Tag>{t('documents.status.recycleBin')}</Tag> : statusTag(record.voStatus, t) },
    { title: t('documents.table.visibility'), key: 'visibility', width: 120, render: (_, record) => visibilityTag(record.voVisibility, t) },
    { title: t('documents.table.source'), key: 'source', width: 110, render: (_, record) => <Tag color={isBuiltInDocument(record) ? 'purple' : 'default'}>{getDocumentSourceTypeText(record.voSourceType, t)}</Tag> },
    { title: t('documents.table.version'), key: 'versions', width: 125, render: (_, record) => `v${record.voVersion} / g${record.voGovernanceVersion}` },
    { title: t('documents.table.updatedAt'), key: 'updated', width: 180, render: (_, record) => formatDocumentDateTime(record.voModifyTime ?? record.voCreateTime, language) },
    { title: t('documents.table.actions'), key: 'actions', width: 300, render: (_, record) => rowActions(record) },
  ];

  const listNotice = listReadState === 'stale' || listReadState === 'unavailable' ? (
    <div className={`document-read-notice document-read-notice--${listReadState}`}>
      <strong>{t(`documents.state.${listReadState}.title`)}</strong>
      <span>{t(`documents.state.${listReadState}.description`)}</span>
    </div>
  ) : null;

  const selectedActions = selectedDocument && !isBuiltInDocument(selectedDocument) ? (
    <Space wrap>
      {!selectedDocument.voIsDeleted && canPublish && selectedDocument.voStatus !== DOCUMENT_STATUS.published ? <Button size="small" icon={<CheckOutlined />} onClick={() => { setGovernanceAction('publish'); setGovernanceReason(''); setGovernanceConflict(false); }}>{t('documents.actions.publish')}</Button> : null}
      {!selectedDocument.voIsDeleted && canPublish && selectedDocument.voStatus !== DOCUMENT_STATUS.draft ? <Button size="small" icon={<CloseOutlined />} onClick={() => { setGovernanceAction('unpublish'); setGovernanceReason(''); setGovernanceConflict(false); }}>{t('documents.actions.toDraft')}</Button> : null}
      {!selectedDocument.voIsDeleted && canArchive && selectedDocument.voStatus !== DOCUMENT_STATUS.archived ? <Button size="small" icon={<SyncOutlined />} onClick={() => { setGovernanceAction('archive'); setGovernanceReason(''); setGovernanceConflict(false); }}>{t('documents.actions.archive')}</Button> : null}
      {!selectedDocument.voIsDeleted && canUpdatePermissions ? <Button size="small" icon={<LockOutlined />} onClick={() => {
        setAccessVisibility(String(selectedDocument.voVisibility));
        setAccessRoles(selectedDocument.voAllowedRoles.join('\n'));
        setAccessPermissions(selectedDocument.voAllowedPermissions.join('\n'));
        setAccessReason('');
        setAccessConflict(false);
        setAccessOpen(true);
      }}>{t('documents.actions.permissions')}</Button> : null}
      {!selectedDocument.voIsDeleted && canRollback ? <Button size="small" icon={<ClockCircleOutlined />} onClick={() => void loadRevisions(selectedDocument, 1)}>{t('documents.actions.revisions')}</Button> : null}
      {selectedDocument.voIsDeleted && canRestore ? <Button size="small" icon={<UnlockOutlined />} onClick={() => { setGovernanceAction('restore'); setGovernanceReason(''); setGovernanceConflict(false); }}>{t('documents.actions.restore')}</Button> : null}
      {!selectedDocument.voIsDeleted && canDelete ? <Button size="small" variant="danger" icon={<DeleteOutlined />} onClick={() => { setGovernanceAction('delete'); setGovernanceReason(''); setGovernanceConflict(false); }}>{t('documents.actions.delete')}</Button> : null}
    </Space>
  ) : null;

  const selectedContext = (
    <div className="document-governance-context">
      <div className="admin-feature-rail__header">
        <div><span className="admin-feature-rail__eyebrow">{t('documents.rail.eyebrow')}</span><h3>{t('documents.rail.title')}</h3></div>
        <ConsoleStatusChip tone={selectedReadState === 'ready' ? 'success' : selectedReadState === 'stale' ? 'warning' : 'neutral'}>{t(`documents.state.${selectedReadState}`)}</ConsoleStatusChip>
      </div>
      {!query.selectedDocumentId ? <p className="admin-feature-rail__empty">{t('documents.governance.selectTarget')}</p> : null}
      {selectedLoading && !selectedDocument ? <p className="admin-feature-subtle">{t('documents.governance.loadingTarget')}</p> : null}
      {selectedDocument ? (
        <>
          <div className="admin-feature-rail__callout">
            <strong>{selectedDocument.voTitle}</strong>
            <p>{getDocumentSummary(selectedDocument, t)}</p>
            <Space wrap>{selectedDocument.voIsDeleted ? <Tag>{t('documents.status.recycleBin')}</Tag> : statusTag(selectedDocument.voStatus, t)}{visibilityTag(selectedDocument.voVisibility, t)}</Space>
          </div>
          <div className="admin-feature-rail__list">
            <div className="admin-feature-rail__item"><span>{t('documents.governance.versions')}</span><strong>v{selectedDocument.voVersion} / g{selectedDocument.voGovernanceVersion}</strong></div>
            <div className="admin-feature-rail__item"><span>{t('documents.rail.accessPolicy')}</span><strong>{getDocumentAccessSummary(selectedDocument, t)}</strong></div>
            <div className="admin-feature-rail__item"><span>{t('documents.rail.sourcePath')}</span><strong>{selectedDocument.voSourcePath || selectedDocument.voSlug}</strong></div>
          </div>
          <div className="admin-feature-rail__actions">{targetAuthoritative ? selectedActions : <span className="admin-feature-subtle">{t('documents.governance.actionsFrozen')}</span>}</div>
          <section className="document-governance-history">
            <div className="document-governance-history__header">
              <h4>{t('documents.governance.historyTitle')}</h4>
              <Button size="small" icon={<ReloadOutlined />} disabled={historyLoading} onClick={() => void loadHistory(historyPage)}>{t('documents.actions.refresh')}</Button>
            </div>
            {historyReadState === 'stale' ? <p className="document-read-notice document-read-notice--stale">{t('documents.governance.historyStale')}</p> : null}
            {historySnapshot?.data.map((event) => (
              <article className="document-governance-event" key={event.voId}>
                <div><strong>{t(`documents.governance.action.${event.voAction}`, { defaultValue: event.voAction })}</strong><Tag>g{event.voResultGovernanceVersion}</Tag></div>
                <p>{event.voReason}</p>
                <small>{event.voActorName} · {formatDocumentDateTime(event.voCreateTime, language)}</small>
              </article>
            ))}
            {historyReadState === 'ready' && historySnapshot?.data.length === 0 ? <p className="admin-feature-subtle">{t('documents.governance.historyEmpty')}</p> : null}
            {historySnapshot && historySnapshot.pageCount > 1 ? (
              <div className="console-resource-mobile-pagination">
                <Button size="small" disabled={historyPage <= 1 || historyLoading} onClick={() => void loadHistory(historyPage - 1)}>{t('documents.pagination.previous')}</Button>
                <span>{historyPage} / {historySnapshot.pageCount}</span>
                <Button size="small" disabled={historyPage >= historySnapshot.pageCount || historyLoading} onClick={() => void loadHistory(historyPage + 1)}>{t('documents.pagination.next')}</Button>
              </div>
            ) : null}
          </section>
        </>
      ) : null}
    </div>
  );

  return (
    <div className="admin-feature-page document-governance-page">
      <ConsolePageHeader
        eyebrow={t('documents.page.eyebrow')}
        title={t('documents.page.title')}
        description={t('documents.page.description')}
        icon={<FileTextOutlined />}
        status={<ConsoleStatusChip tone={canView ? 'success' : 'danger'}>{canView ? t('documents.page.viewable') : t('documents.page.forbidden')}</ConsoleStatusChip>}
        actions={(
          <>
            <input ref={importInputRef} type="file" accept=".md,.markdown,.txt,text/markdown,text/plain" className="admin-form-hidden-input" onChange={(event) => void importFile(event)} />
            {canImport ? <Button variant="primary" icon={<FileTextOutlined />} disabled={importing} onClick={() => importInputRef.current?.click()}>{importing ? t('documents.actions.importing') : t('documents.actions.import')}</Button> : null}
            <Button variant="ghost" icon={<ReloadOutlined />} disabled={listLoading} onClick={() => void loadList()}>{t('documents.actions.refresh')}</Button>
          </>
        )}
      />

      <ConsoleMetricGrid label={t('documents.page.metricsAriaLabel')}>
        <ConsoleMetricCard label={t('documents.metrics.documents')} value={listSnapshot ? formatDocumentNumber(items.length, language) : '—'} description={t('documents.metrics.documentsDescription')} tone="info" />
        <ConsoleMetricCard label={t('documents.metrics.published')} value={listSnapshot ? formatDocumentNumber(publishedCount, language) : '—'} description={t('documents.metrics.publishedDescription')} tone="success" />
        <ConsoleMetricCard label={t('documents.metrics.restricted')} value={listSnapshot ? formatDocumentNumber(restrictedCount, language) : '—'} description={t('documents.metrics.restrictedDescription')} tone="warning" />
        <ConsoleMetricCard label={t('documents.metrics.builtIn')} value={listSnapshot ? formatDocumentNumber(builtInCount, language) : '—'} description={t('documents.metrics.builtInDescription')} />
      </ConsoleMetricGrid>

      {canReview ? (
        <section className="document-review-workbench" aria-labelledby="document-review-title">
          <div className="document-review-workbench__header">
            <div><span className="admin-feature-rail__eyebrow">{t('documents.review.eyebrow')}</span><h2 id="document-review-title">{t('documents.review.title')}</h2><p>{t('documents.review.description')}</p></div>
            <Space wrap>
              <ConsoleStatusChip tone={reviewReadState === 'ready' && (reviewSnapshot?.dataCount ?? 0) > 0 ? 'warning' : reviewReadState === 'stale' ? 'warning' : 'neutral'}>{reviewSnapshot ? t('documents.review.queueCount', { count: reviewSnapshot.dataCount }) : t(`documents.state.${reviewReadState}`)}</ConsoleStatusChip>
              <Button size="small" icon={<ReloadOutlined />} disabled={reviewLoading} onClick={() => void loadReviewQueue()}>{t('documents.actions.refresh')}</Button>
            </Space>
          </div>
          {reviewReadState === 'stale' || reviewReadState === 'unavailable' ? <div className={`document-read-notice document-read-notice--${reviewReadState}`}>{t(`documents.state.${reviewReadState}.description`)}</div> : null}
          <div className="document-review-desktop">
            <Table<WikiReviewQueueItemVo>
              rowKey="voDraftId"
              size="small"
              loading={reviewLoading}
              dataSource={reviewItems}
              pagination={{ current: query.reviewPageIndex, pageSize: query.reviewPageSize, total: reviewSnapshot?.dataCount ?? 0, showSizeChanger: true, onChange: (page, pageSize) => updateQuery({ ...query, reviewPageIndex: page, reviewPageSize: pageSize }) }}
              columns={[
                { title: t('documents.review.table.document'), key: 'document', render: (_, record) => <Space orientation="vertical" size={2}><strong>{record.voTitle}</strong><span className="admin-feature-subtle">{record.voSlug}</span></Space> },
                { title: t('documents.review.table.owner'), key: 'owner', render: (_, record) => <Space orientation="vertical" size={2}><span>{record.voOwnerUserName}</span><span className="admin-feature-subtle">{record.voOwnerUserPublicId}</span></Space> },
                { title: t('documents.review.table.version'), key: 'version', render: (_, record) => `v${record.voBaseDocumentVersion} → d${record.voDraftVersion}` },
                { title: t('documents.review.table.submittedAt'), key: 'time', render: (_, record) => formatDocumentDateTime(record.voSubmittedAt, language) },
                { title: t('documents.review.table.actions'), key: 'actions', render: (_, record) => <Button size="small" variant="primary" onClick={() => void openReview(record)}>{t('documents.review.actions.evidence')}</Button> },
              ]}
            />
          </div>
          <div className="document-review-mobile">
            {reviewItems.map((record) => (
              <article className="console-resource-mobile-card" key={record.voDraftId}>
                <div className="console-resource-mobile-card__header"><strong>{record.voTitle}</strong>{reviewStateTag(record.voReviewState, t)}</div>
                <div className="console-resource-mobile-card__facts"><div className="console-resource-mobile-card__fact"><span>{t('documents.review.table.owner')}</span><strong>{record.voOwnerUserName}</strong></div><div className="console-resource-mobile-card__fact"><span>{t('documents.review.table.version')}</span><strong>v{record.voBaseDocumentVersion} → d{record.voDraftVersion}</strong></div></div>
                <div className="console-resource-mobile-card__footer"><span>{formatDocumentDateTime(record.voSubmittedAt, language)}</span><Button size="small" variant="primary" onClick={() => void openReview(record)}>{t('documents.review.actions.evidence')}</Button></div>
              </article>
            ))}
            {reviewSnapshot && reviewPageCount > 1 ? <div className="console-resource-mobile-pagination"><Button size="small" disabled={query.reviewPageIndex <= 1 || reviewLoading} onClick={() => updateQuery({ ...query, reviewPageIndex: query.reviewPageIndex - 1 })}>{t('documents.pagination.previous')}</Button><span>{query.reviewPageIndex} / {reviewPageCount}</span><Button size="small" disabled={query.reviewPageIndex >= reviewPageCount || reviewLoading} onClick={() => updateQuery({ ...query, reviewPageIndex: query.reviewPageIndex + 1 })}>{t('documents.pagination.next')}</Button></div> : null}
          </div>
        </section>
      ) : null}

      <ConsoleResourceList
        toolbar={<ConsoleToolbar title={t('documents.filter.title')} description={t('documents.filter.description')} meta={<ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>{activeFilterCount > 0 ? t('documents.filter.active', { count: activeFilterCount }) : t('documents.filter.none')}</ConsoleStatusChip>}>{filterControls}</ConsoleToolbar>}
        mobileToolbar={<div className="console-resource-mobile-summary"><div className="console-resource-mobile-summary__copy"><strong>{total === null ? t('documents.state.noSnapshot') : t('documents.count.documents', { count: total })}</strong><span>{activeFilterCount > 0 ? t('documents.filter.active', { count: activeFilterCount }) : t('documents.filter.none')}</span></div><div className="console-resource-mobile-summary__actions"><Button size="small" icon={<SearchOutlined />} onClick={() => setFilterSheetOpen(true)}>{t('documents.filter.mobile')}</Button><Button size="small" icon={<ReloadOutlined />} disabled={listLoading} onClick={() => void loadList()}>{t('documents.actions.refresh')}</Button></div></div>}
        desktopList={<section className="admin-table-panel">{listNotice}<Table<WikiDocumentVo> rowKey="voId" loading={listLoading} columns={columns} dataSource={items} scroll={{ x: 1200 }} pagination={{ current: query.pageIndex, pageSize: query.pageSize, total: total ?? 0, showSizeChanger: true, showQuickJumper: true, onChange: (page, pageSize) => updateQuery({ ...query, pageIndex: page, pageSize }) }} /></section>}
        mobileList={<>{listNotice}{listLoading && !listSnapshot ? <div className="console-resource-mobile-loading">{t('documents.state.loading')}</div> : null}{listReadState === 'ready' && items.length === 0 ? <div className="console-resource-mobile-empty"><strong>{t('documents.state.emptyTitle')}</strong><span>{t('documents.state.emptyDescription')}</span></div> : null}{items.map((record) => <article className={`console-resource-mobile-card${query.selectedDocumentId === record.voId ? ' document-mobile-card--selected' : ''}`} key={record.voId}><div className="console-resource-mobile-card__header">{renderIdentity(record)}{record.voIsDeleted ? <Tag>{t('documents.status.recycleBin')}</Tag> : statusTag(record.voStatus, t)}</div><div className="console-resource-mobile-card__facts"><div className="console-resource-mobile-card__fact"><span>{t('documents.table.visibility')}</span><strong>{getDocumentVisibilityText(record.voVisibility, t)}</strong></div><div className="console-resource-mobile-card__fact"><span>{t('documents.table.version')}</span><strong>v{record.voVersion} / g{record.voGovernanceVersion}</strong></div><div className="console-resource-mobile-card__fact"><span>{t('documents.table.source')}</span><strong>{getDocumentSourceTypeText(record.voSourceType, t)}</strong></div></div><p className="console-resource-mobile-card__description">{getDocumentSummary(record, t)}</p><div className="console-resource-mobile-card__footer"><span>#{record.voId}</span>{rowActions(record)}</div></article>)}{items.length > 0 ? <div className="console-resource-mobile-pagination"><Button size="small" disabled={query.pageIndex <= 1 || listLoading} onClick={() => updateQuery({ ...query, pageIndex: query.pageIndex - 1 })}>{t('documents.pagination.previous')}</Button><span>{query.pageIndex} / {pageCount}</span><Button size="small" disabled={query.pageIndex >= pageCount || listLoading} onClick={() => updateQuery({ ...query, pageIndex: query.pageIndex + 1 })}>{t('documents.pagination.next')}</Button></div> : null}</>}
        context={selectedContext}
      />

      <BottomSheet isOpen={filterSheetOpen} onClose={() => setFilterSheetOpen(false)} closeLabel={t('documents.actions.cancel')} title={t('documents.filter.title')} height="auto" className="console-resource-filter-sheet">{filterControls}</BottomSheet>

      <Modal title={t('documents.detail.title')} open={detailLoading || Boolean(detailDocument)} width={860} footer={null} onCancel={() => { detailGeneration.current += 1; setDetailDocument(null); setDetailLoading(false); }}>
        {detailLoading ? <p>{t('documents.detail.loading')}</p> : detailDocument ? <Space orientation="vertical" size="middle" className="admin-feature-modal-stack"><Space wrap>{detailDocument.voIsDeleted ? <Tag>{t('documents.status.recycleBin')}</Tag> : statusTag(detailDocument.voStatus, t)}{visibilityTag(detailDocument.voVisibility, t)}<Tag>v{detailDocument.voVersion} / g{detailDocument.voGovernanceVersion}</Tag></Space><div><h3>{detailDocument.voTitle}</h3><p className="admin-feature-subtle">{detailDocument.voSlug}</p></div><p>{detailDocument.voSummary || t('documents.summaryFallback')}</p>{detailDocument.voCoverAttachmentId ? <MarkdownRenderer content={`![${t('documents.detail.cover')}](${buildAttachmentMarkdownUrl(detailDocument.voCoverAttachmentId)})`} className="document-protected-markdown" protectedAttachments={protectedAttachments} /> : null}<MarkdownRenderer content={detailDocument.voMarkdownContent} className="document-protected-markdown" protectedAttachments={protectedAttachments} /></Space> : null}
      </Modal>

      <Modal title={reviewDraft ? t('documents.review.modalTitleWithDocument', { title: reviewDraft.voTitle }) : t('documents.review.modalTitle')} open={reviewDraftLoading || Boolean(reviewDraft)} width={1120} footer={null} maskClosable={!reviewSaving} keyboard={!reviewSaving} onCancel={() => { if (!reviewSaving) { reviewEvidenceGeneration.current += 1; setReviewDraft(null); setReviewOfficialDocument(null); setReviewComment(''); } }}>
        {reviewDraftLoading ? <p>{t('documents.review.loadingEvidence')}</p> : reviewDraft ? <div className="document-review-evidence"><section className="document-review-evidence__summary"><div><span>{t('documents.review.owner')}</span><strong>{reviewDraft.voOwnerUserName}</strong><small>{reviewDraft.voOwnerUserPublicId}</small></div><div><span>{t('documents.review.versionEvidence')}</span><strong>v{reviewDraft.voBaseDocumentVersion} → d{reviewDraft.voDraftVersion}</strong><small>{t('documents.review.currentVersion', { version: reviewDraft.voDocumentVersion })}</small></div><div><span>{t('documents.review.collaborators')}</span><strong>{reviewDraft.voCollaborators.length}</strong><small>{reviewDraft.voCollaborators.map((item) => item.voUserName).join('、') || t('documents.review.none')}</small></div><div><span>{t('documents.review.parentProposal')}</span><strong>{reviewDraft.voProposedParentId || t('documents.review.root')}</strong><small>{reviewDraft.voChangeSummary || t('documents.review.noSummary')}</small></div></section><section className="document-review-evidence__comparison"><article><div className="document-review-evidence__panel-title"><strong>{t('documents.review.officialContent')}</strong><Tag>v{reviewOfficialDocument?.voVersion ?? reviewDraft.voDocumentVersion}</Tag></div>{reviewOfficialDocument?.voCoverAttachmentId ? <MarkdownRenderer content={`![${t('documents.detail.cover')}](${buildAttachmentMarkdownUrl(reviewOfficialDocument.voCoverAttachmentId)})`} className="document-protected-markdown" protectedAttachments={protectedAttachments} /> : null}<MarkdownRenderer content={reviewOfficialDocument?.voMarkdownContent || t('documents.review.noOfficialContent')} className="document-protected-markdown" protectedAttachments={protectedAttachments} /></article><article><div className="document-review-evidence__panel-title"><strong>{t('documents.review.draftContent')}</strong>{reviewStateTag(reviewDraft.voReviewState, t)}</div>{reviewDraft.voCoverAttachmentId ? <MarkdownRenderer content={`![${t('documents.detail.cover')}](${buildAttachmentMarkdownUrl(reviewDraft.voCoverAttachmentId)})`} className="document-protected-markdown" protectedAttachments={protectedAttachments} /> : null}<MarkdownRenderer content={reviewDraft.voMarkdownContent} className="document-protected-markdown" protectedAttachments={protectedAttachments} /></article></section><section className="document-review-evidence__decision"><h3>{t('documents.review.decisionTitle')}</h3><p>{t('documents.review.decisionDescription')}</p><Input.TextArea value={reviewComment} rows={4} maxLength={1000} disabled={reviewSaving} placeholder={t('documents.review.commentPlaceholder')} onChange={(event) => setReviewComment(event.target.value)} /><Space wrap><Button disabled={reviewSaving} onClick={() => void submitReview(WikiReviewAction.RequestChanges)}>{t('documents.review.actions.requestChanges')}</Button><Button variant="danger" disabled={reviewSaving} onClick={() => void submitReview(WikiReviewAction.Reject)}>{t('documents.review.actions.reject')}</Button><Button variant="primary" disabled={reviewSaving} onClick={() => void submitReview(WikiReviewAction.Apply)}>{reviewSaving ? t('documents.review.actions.processing') : t('documents.review.actions.apply')}</Button></Space></section><section className="document-review-evidence__timeline"><h3>{t('documents.review.timelineTitle')}</h3>{reviewDraft.voReviewEvents.map((event) => <article className="document-review-evidence__timeline-item" key={event.voId}><div><strong>{event.voActorName}</strong><Tag>{event.voAction}</Tag></div><p>{event.voComment || t('documents.review.noComment')}</p><small>{formatDocumentDateTime(event.voCreateTime, language)}</small></article>)}</section></div> : null}
      </Modal>

      <Modal title={t(`documents.governance.actionTitle.${governanceAction ?? 'publish'}`)} open={Boolean(governanceAction)} onCancel={() => { if (!governanceSaving && !governanceReason.trim()) setGovernanceAction(undefined); else if (!governanceSaving && window.confirm(t('documents.governance.discardReason'))) { setGovernanceAction(undefined); setGovernanceReason(''); } }} onOk={() => void submitGovernanceAction()} confirmLoading={governanceSaving} maskClosable={!governanceSaving} keyboard={!governanceSaving} okButtonProps={{ danger: governanceAction === 'delete', disabled: !governanceReason.trim() || !targetAuthoritative }} okText={t('documents.actions.confirm')} cancelText={t('documents.actions.cancel')} destroyOnHidden>
        <p>{selectedDocument ? t('documents.governance.confirmTarget', { title: selectedDocument.voTitle, version: selectedDocument.voVersion, governanceVersion: selectedDocument.voGovernanceVersion }) : null}</p>{governanceConflict ? <div className="document-read-notice document-read-notice--stale">{t('documents.governance.reconfirm')}</div> : null}<Input.TextArea value={governanceReason} rows={4} maxLength={500} disabled={governanceSaving} placeholder={t('documents.governance.reasonPlaceholder')} onChange={(event) => { setGovernanceReason(event.target.value); setGovernanceConflict(false); }} />
      </Modal>

      <Modal title={t('documents.access.title')} open={accessOpen} onCancel={() => { if (!accessSaving && !accessReason.trim()) setAccessOpen(false); else if (!accessSaving && window.confirm(t('documents.governance.discardReason'))) { setAccessOpen(false); setAccessReason(''); } }} onOk={() => void submitAccessPolicy()} confirmLoading={accessSaving} maskClosable={!accessSaving} keyboard={!accessSaving} okButtonProps={{ disabled: !accessReason.trim() || !targetAuthoritative }} okText={t('documents.actions.save')} cancelText={t('documents.actions.cancel')} destroyOnHidden>
        <Space orientation="vertical" size="middle" className="admin-feature-modal-stack"><p>{t('documents.access.description')}</p>{accessConflict ? <div className="document-read-notice document-read-notice--stale">{t('documents.governance.reconfirm')}</div> : null}<Select value={accessVisibility} options={[{ label: t('documents.visibility.public'), value: String(DOCUMENT_VISIBILITY.public) }, { label: t('documents.visibility.authenticated'), value: String(DOCUMENT_VISIBILITY.authenticated) }, { label: t('documents.visibility.restricted'), value: String(DOCUMENT_VISIBILITY.restricted) }]} onChange={setAccessVisibility} /><Input.TextArea value={accessRoles} rows={3} disabled={accessSaving} placeholder={t('documents.access.rolesPlaceholder')} onChange={(event) => setAccessRoles(event.target.value)} /><Input.TextArea value={accessPermissions} rows={3} disabled={accessSaving} placeholder={t('documents.access.permissionsPlaceholder')} onChange={(event) => setAccessPermissions(event.target.value)} /><Input.TextArea value={accessReason} rows={3} maxLength={500} disabled={accessSaving} placeholder={t('documents.governance.reasonPlaceholder')} onChange={(event) => { setAccessReason(event.target.value); setAccessConflict(false); }} /></Space>
      </Modal>

      <Modal title={revisionTarget ? t('documents.revision.titleWithDocument', { title: revisionTarget.voTitle }) : t('documents.revision.title')} open={Boolean(revisionTarget)} width={1080} footer={null} maskClosable={!rollbackSaving} keyboard={!rollbackSaving} onCancel={() => { if (!rollbackSaving && !rollbackReason.trim()) { setRevisionTarget(null); setRevisionSnapshot(null); setRevisionDetail(null); } else if (!rollbackSaving && window.confirm(t('documents.governance.discardReason'))) { setRevisionTarget(null); setRollbackReason(''); } }}>
        {revisionTarget ? <div className="document-revision-workspace"><section><Table<WikiDocumentRevisionItemVo> rowKey="voId" size="small" loading={revisionLoading} dataSource={revisionSnapshot?.data ?? []} pagination={{ current: revisionPage, pageSize: REVISION_PAGE_SIZE, total: revisionSnapshot?.dataCount ?? 0, onChange: (page) => void loadRevisions(revisionTarget, page) }} columns={[{ title: t('documents.table.version'), key: 'version', render: (_, record) => <Tag color={record.voIsCurrent ? 'success' : 'default'}>v{record.voVersion}</Tag> }, { title: t('documents.revision.description'), key: 'summary', render: (_, record) => record.voChangeSummary || t('documents.revision.noSummary') }, { title: t('documents.revision.time'), key: 'time', render: (_, record) => formatDocumentDateTime(record.voCreateTime, language) }, { title: t('documents.revision.actions'), key: 'actions', render: (_, record) => <Button size="small" onClick={() => void loadRevisionDetail(record.voId)}>{t('documents.actions.view')}</Button> }]} /></section><section className="document-revision-evidence"><h3>{t('documents.revision.contentTitle')}</h3>{revisionDetailLoading ? <p>{t('documents.revision.loadingDetail')}</p> : revisionDetail ? <><MarkdownRenderer content={revisionDetail.voMarkdownContent} className="document-protected-markdown" protectedAttachments={protectedAttachments} />{!revisionDetail.voIsCurrent && canRollback ? <div className="document-revision-rollback">{rollbackConflict || !revisionTargetAuthoritative ? <div className="document-read-notice document-read-notice--stale">{t('documents.governance.reconfirm')}</div> : null}<Input.TextArea value={rollbackReason} rows={3} maxLength={500} disabled={rollbackSaving} placeholder={t('documents.governance.reasonPlaceholder')} onChange={(event) => { setRollbackReason(event.target.value); setRollbackConflict(false); }} /><Button variant="danger" disabled={!rollbackReason.trim() || rollbackSaving || !revisionTargetAuthoritative} onClick={() => void submitRollback()}>{rollbackSaving ? t('documents.review.actions.processing') : t('documents.actions.rollback')}</Button></div> : null}</> : <p>{t('documents.revision.select')}</p>}</section></div> : null}
      </Modal>
    </div>
  );
};
