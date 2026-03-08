import type { WikiDocumentTreeNodeVo, WikiListQuery } from './types/wiki';

export type ParentOption = {
  id: number;
  label: string;
};

export type WikiTreeRow = {
  id: number;
  title: string;
  status: number;
  depth: number;
  childCount: number;
};

export const SORT_PRESET_VALUES = ['0', '10', '20', '30', '40', '50', '100'];

export function flattenTree(nodes: WikiDocumentTreeNodeVo[]): number[] {
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

export function collectDescendantIds(nodes: WikiDocumentTreeNodeVo[], targetId: number): Set<number> {
  const result = new Set<number>();

  const visit = (currentNodes: WikiDocumentTreeNodeVo[], collecting: boolean) => {
    currentNodes.forEach((node) => {
      const shouldCollect = collecting || node.voId === targetId;
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
  parentId?: number | null,
  currentDocumentId?: number
): number {
  const siblingNodes = flattenNodes(nodes).filter(
    (node) => (node.voParentId ?? null) === (parentId ?? null) && node.voId !== currentDocumentId
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

  if (typeof query.parentId === 'number') {
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
