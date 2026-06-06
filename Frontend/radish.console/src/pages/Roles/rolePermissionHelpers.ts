import type {
  ConsoleResourceTreeNodeVo,
  ResourceApiBindingVo,
  RoleAuthorizationSnapshotVo,
  SaveRoleAuthorizationRequest,
} from '../../api/consoleAuthorization';

export interface ResourceIndexItem {
  node: ConsoleResourceTreeNodeVo;
  parentId: string | null;
}

export interface NodeVisualState {
  checked: boolean;
  indeterminate: boolean;
}

export function isValidRoleId(roleId?: string | null): boolean {
  const normalizedRoleId = roleId?.trim();
  return Boolean(normalizedRoleId && /^[1-9]\d*$/u.test(normalizedRoleId));
}

export function deduplicateApiBindings(bindings: ResourceApiBindingVo[]): ResourceApiBindingVo[] {
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

export function buildResourceIndex(
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

export function collectDescendantIds(node: ConsoleResourceTreeNodeVo): string[] {
  return [
    node.voId,
    ...node.voChildren.flatMap((child) => collectDescendantIds(child)),
  ];
}

export function sortResourceIds(resourceIds: Iterable<string>): string[] {
  return Array.from(new Set(
    Array.from(resourceIds)
      .map((resourceId) => resourceId.trim())
      .filter((resourceId) => resourceId.length > 0)
  )).sort((left, right) => left.localeCompare(right, 'zh-CN'));
}

export function ensureAncestorSelection(
  selectedIds: Set<string>,
  resourceIndex: Map<string, ResourceIndexItem>
): Set<string> {
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

export function collectPermissionKeys(nodes: ConsoleResourceTreeNodeVo[], selectedIds: Set<string>): string[] {
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

export function collectPreviewBindings(nodes: ConsoleResourceTreeNodeVo[], selectedIds: Set<string>): ResourceApiBindingVo[] {
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

export function buildVisualState(
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

export function buildRoleAuthorizationSavePayload(
  snapshot: RoleAuthorizationSnapshotVo,
  selectedResourceIds: string[]
): SaveRoleAuthorizationRequest {
  return {
    roleId: snapshot.voRoleId,
    resourceIds: sortResourceIds(selectedResourceIds),
    expectedModifyTime: snapshot.voLastModifyTime,
  };
}

export function shouldDisableRoleAuthorizationSave(params: {
  canEditRole: boolean;
  isDirty: boolean;
  loading: boolean;
  saving: boolean;
}): boolean {
  return !params.canEditRole || !params.isDirty || params.loading || params.saving;
}
