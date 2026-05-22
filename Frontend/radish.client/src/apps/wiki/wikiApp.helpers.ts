import type { LongId } from '@/api/user';
import type { TFunction } from 'i18next';
import type {
  CreateWikiDocumentRequest,
  UpdateWikiDocumentRequest,
  WikiDocumentRevisionItemVo,
  WikiDocumentTreeNodeVo,
  WikiDocumentVo,
  WikiListQuery,
} from './types/wiki';
import { WikiDocumentVisibility } from './types/wiki.ts';

export type ParentOption = {
  id: LongId;
  label: string;
};

export type WikiTreeRow = {
  id: LongId;
  title: string;
  status: number;
  depth: number;
  childCount: number;
};

export type EditorDraft = {
  title: string;
  slug: string;
  summary: string;
  markdownContent: string;
  parentId: string;
  sort: string;
  coverAttachmentId: string;
  changeSummary: string;
  visibility: string;
  allowedRoles: string;
  allowedPermissions: string;
};

export const SORT_PRESET_VALUES = ['0', '10', '20', '30', '40', '50', '100'];

export const EMPTY_DRAFT: EditorDraft = {
  title: '',
  slug: '',
  summary: '',
  markdownContent: '',
  parentId: '',
  sort: '0',
  coverAttachmentId: '',
  changeSummary: '',
  visibility: String(WikiDocumentVisibility.Authenticated),
  allowedRoles: '',
  allowedPermissions: '',
};

export function flattenTree(nodes: WikiDocumentTreeNodeVo[]): LongId[] {
  return nodes.flatMap((node) => [node.voId, ...flattenTree(node.voChildren || [])]);
}

export function flattenTreeRows(nodes: WikiDocumentTreeNodeVo[], depth: number = 0): WikiTreeRow[] {
  return nodes.flatMap((node) => {
    const children = node.voChildren || [];

    return [
      {
        id: node.voId,
        title: node.voTitle,
        status: node.voStatus,
        depth,
        childCount: children.length,
      },
      ...flattenTreeRows(children, depth + 1),
    ];
  });
}

export function flattenTreeOptions(nodes: WikiDocumentTreeNodeVo[], depth: number = 0): ParentOption[] {
  return nodes.flatMap((node) => [
    {
      id: node.voId,
      label: `${depth > 0 ? `${'　'.repeat(depth)}└ ` : ''}${node.voTitle}`,
    },
    ...flattenTreeOptions(node.voChildren || [], depth + 1),
  ]);
}

export function collectDescendantIds(nodes: WikiDocumentTreeNodeVo[], targetId: LongId): Set<LongId> {
  const result = new Set<LongId>();
  const targetIdKey = String(targetId);

  const visit = (currentNodes: WikiDocumentTreeNodeVo[], collecting: boolean) => {
    currentNodes.forEach((node) => {
      const shouldCollect = collecting || String(node.voId) === targetIdKey;
      if (collecting) {
        result.add(node.voId);
      }

      visit(node.voChildren || [], shouldCollect);
    });
  };

  visit(nodes, false);
  return result;
}

export function collectExpandableNodeIds(nodes: WikiDocumentTreeNodeVo[]): LongId[] {
  return nodes.flatMap((node) => {
    const children = node.voChildren || [];
    return children.length > 0
      ? [node.voId, ...collectExpandableNodeIds(children)]
      : collectExpandableNodeIds(children);
  });
}

export function findAncestorIds(nodes: WikiDocumentTreeNodeVo[], targetId: LongId, trail: LongId[] = []): LongId[] {
  const targetIdKey = String(targetId);
  for (const node of nodes) {
    if (String(node.voId) === targetIdKey) {
      return trail;
    }

    const nextTrail = [...trail, node.voId];
    const result = findAncestorIds(node.voChildren || [], targetId, nextTrail);
    if (result.length > 0) {
      return result;
    }
  }

  return [];
}

export function getSuggestedSortValue(
  nodes: WikiDocumentTreeNodeVo[],
  parentId?: LongId | null,
  currentDocumentId?: LongId
): number {
  const parentIdKey = parentId == null ? '' : String(parentId);
  const currentDocumentIdKey = currentDocumentId == null ? '' : String(currentDocumentId);
  const siblingNodes = flattenNodes(nodes).filter(
    (node) =>
      String(node.voParentId ?? '') === parentIdKey
      && String(node.voId) !== currentDocumentIdKey
  );

  if (siblingNodes.length === 0) {
    return 0;
  }

  const maxSort = Math.max(...siblingNodes.map((node) => node.voSort || 0));
  if (maxSort < 0) {
    return 0;
  }

  return (Math.floor(maxSort / 10) + 1) * 10;
}

export function formatWikiTime(value: string | null | undefined, language?: string): string {
  if (!value) {
    return '--';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString(resolveDateLocale(language), {
    hour12: false,
  });
}

export function pickInitialDocumentId(tree: WikiDocumentTreeNodeVo[], list: WikiDocumentVo[]): LongId | null {
  if (list.length > 0) {
    return list[0].voId;
  }

  if (tree.length > 0) {
    return tree[0].voId;
  }

  return null;
}

export function normalizeOptionalString(value: string): string | undefined {
  const trimmed = value.trim();
  return trimmed ? trimmed : undefined;
}

export function normalizeOptionalNumber(value: string): number | undefined {
  const trimmed = value.trim();
  if (!trimmed) {
    return undefined;
  }

  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : undefined;
}

export function normalizeOptionalLongId(value: string): LongId | undefined {
  const trimmed = value.trim();
  return /^[1-9]\d*$/.test(trimmed) ? trimmed : undefined;
}

export function normalizePositiveLongId(value: unknown): LongId | undefined {
  if (typeof value === 'number') {
    return Number.isSafeInteger(value) && value > 0 ? value : undefined;
  }

  if (typeof value !== 'string') {
    return undefined;
  }

  const trimmed = String(value).trim();
  return /^[1-9]\d*$/.test(trimmed) ? trimmed : undefined;
}

export function normalizeAccessList(value: string): string[] {
  return value
    .split(/[\n,，]/)
    .map((item) => item.trim())
    .filter(Boolean);
}

export function parseWikiWindowParams(appParams?: Record<string, unknown> | null): { documentId?: LongId; slug?: string } {
  if (!appParams) {
    return {};
  }

  const documentId = normalizePositiveLongId(appParams.documentId);
  const slug = typeof appParams.slug === 'string' ? appParams.slug.trim() : '';

  return {
    documentId,
    slug: slug || undefined,
  };
}

export function buildCreateRequest(draft: EditorDraft): CreateWikiDocumentRequest {
  return {
    title: draft.title.trim(),
    slug: normalizeOptionalString(draft.slug),
    summary: normalizeOptionalString(draft.summary),
    markdownContent: draft.markdownContent,
    parentId: normalizeOptionalLongId(draft.parentId),
    sort: normalizeOptionalNumber(draft.sort) ?? 0,
    coverAttachmentId: normalizeOptionalLongId(draft.coverAttachmentId),
    visibility: normalizeOptionalNumber(draft.visibility) ?? WikiDocumentVisibility.Authenticated,
    allowedRoles: normalizeAccessList(draft.allowedRoles),
    allowedPermissions: normalizeAccessList(draft.allowedPermissions),
  };
}

export function buildUpdateRequest(draft: EditorDraft): UpdateWikiDocumentRequest {
  return {
    ...buildCreateRequest(draft),
    changeSummary: normalizeOptionalString(draft.changeSummary),
  };
}

export function describeRevisionSummary(t: TFunction, revision: WikiDocumentRevisionItemVo): string {
  if (revision.voChangeSummary?.trim()) {
    return revision.voChangeSummary;
  }

  return revision.voIsCurrent ? t('wiki.revision.currentSnapshot') : t('wiki.revision.noChangeSummary');
}

export function buildWikiListUrl(query: WikiListQuery = {}): string {
  const params = new URLSearchParams();

  params.set('pageIndex', String(query.pageIndex ?? 1));
  params.set('pageSize', String(query.pageSize ?? 100));

  if (query.keyword?.trim()) {
    params.set('keyword', query.keyword.trim());
  }

  if (typeof query.status === 'number') {
    params.set('status', String(query.status));
  }

  if (query.parentId != null) {
    params.set('parentId', String(query.parentId));
  }

  if (query.includeDeleted) {
    params.set('includeDeleted', 'true');
  }

  if (query.deletedOnly) {
    params.set('deletedOnly', 'true');
  }

  return `/api/v1/Wiki/GetList?${params.toString()}`;
}

function flattenNodes(nodes: WikiDocumentTreeNodeVo[]): WikiDocumentTreeNodeVo[] {
  return nodes.flatMap((node) => [node, ...flattenNodes(node.voChildren || [])]);
}

function resolveDateLocale(language?: string): string {
  return language?.toLowerCase().startsWith('zh') ? 'zh-CN' : 'en-US';
}
