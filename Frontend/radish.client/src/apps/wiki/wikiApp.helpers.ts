import type { LongId } from '@/api/user';
import type { WikiDocumentTreeNodeVo, WikiListQuery } from './types/wiki';

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

export const SORT_PRESET_VALUES = ['0', '10', '20', '30', '40', '50', '100'];

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
