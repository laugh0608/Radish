import { CONSOLE_PERMISSIONS, type ConsolePermission } from '@/constants/permissions';
import { hasPermission } from '@/hooks/usePermission';
import type { UserInfo } from '@/types/user';

export interface ConsoleRouteMeta {
  key: string;
  path: string;
  title: string;
  requiredPermission?: ConsolePermission;
  authOnly?: boolean;
  sidebarVisible?: boolean;
  searchVisible?: boolean;
  defaultEntry?: boolean;
}

export const consoleRouteMeta: readonly ConsoleRouteMeta[] = [
  {
    key: 'dashboard',
    path: '/',
    title: '仪表盘',
    requiredPermission: CONSOLE_PERMISSIONS.dashboardView,
    sidebarVisible: true,
    searchVisible: true,
  },
  {
    key: 'applications',
    path: '/applications',
    title: '应用管理',
    requiredPermission: CONSOLE_PERMISSIONS.applicationsView,
    sidebarVisible: true,
    searchVisible: true,
  },
  {
    key: 'products',
    path: '/products',
    title: '商品管理',
    requiredPermission: CONSOLE_PERMISSIONS.productsView,
    sidebarVisible: true,
    searchVisible: true,
  },
  {
    key: 'orders',
    path: '/orders',
    title: '订单管理',
    requiredPermission: CONSOLE_PERMISSIONS.ordersView,
    sidebarVisible: true,
    searchVisible: true,
  },
  {
    key: 'users',
    path: '/users',
    title: '用户管理',
    requiredPermission: CONSOLE_PERMISSIONS.usersView,
    sidebarVisible: true,
    searchVisible: true,
  },
  {
    key: 'user-detail',
    path: '/users/:userId',
    title: '用户详情',
    requiredPermission: CONSOLE_PERMISSIONS.usersView,
    searchVisible: false,
    defaultEntry: false,
  },
  {
    key: 'roles',
    path: '/roles',
    title: '角色管理',
    requiredPermission: CONSOLE_PERMISSIONS.rolesView,
    sidebarVisible: true,
    searchVisible: true,
  },
  {
    key: 'role-permissions',
    path: '/roles/:roleId/permissions',
    title: '权限配置',
    requiredPermission: CONSOLE_PERMISSIONS.rolesEdit,
    searchVisible: false,
    defaultEntry: false,
  },
  {
    key: 'categories',
    path: '/categories',
    title: '分类管理',
    requiredPermission: CONSOLE_PERMISSIONS.categoriesView,
    sidebarVisible: true,
    searchVisible: true,
  },
  {
    key: 'tags',
    path: '/tags',
    title: '标签管理',
    requiredPermission: CONSOLE_PERMISSIONS.tagsView,
    sidebarVisible: true,
    searchVisible: true,
  },
  {
    key: 'stickers',
    path: '/stickers',
    title: '表情包管理',
    requiredPermission: CONSOLE_PERMISSIONS.stickersView,
    sidebarVisible: true,
    searchVisible: true,
  },
  {
    key: 'moderation',
    path: '/moderation',
    title: '内容治理',
    requiredPermission: CONSOLE_PERMISSIONS.moderationView,
    sidebarVisible: true,
    searchVisible: true,
  },
  {
    key: 'coins',
    path: '/coins',
    title: '胡萝卜管理',
    requiredPermission: CONSOLE_PERMISSIONS.coinsView,
    sidebarVisible: true,
    searchVisible: true,
  },
  {
    key: 'experience',
    path: '/experience',
    title: '经验等级',
    requiredPermission: CONSOLE_PERMISSIONS.experienceView,
    sidebarVisible: true,
    searchVisible: true,
  },
  {
    key: 'sticker-items',
    path: '/stickers/:groupId/items',
    title: '表情包详情',
    requiredPermission: CONSOLE_PERMISSIONS.stickersView,
    searchVisible: false,
    defaultEntry: false,
  },
  {
    key: 'system-config',
    path: '/system-config',
    title: '系统配置',
    requiredPermission: CONSOLE_PERMISSIONS.systemConfigView,
    sidebarVisible: true,
    searchVisible: true,
  },
  {
    key: 'profile',
    path: '/profile',
    title: '个人信息',
    authOnly: true,
    searchVisible: true,
  },
  {
    key: 'settings',
    path: '/settings',
    title: '设置',
    authOnly: true,
    searchVisible: true,
  },
  {
    key: 'hangfire',
    path: '/hangfire',
    title: '定时任务',
    requiredPermission: CONSOLE_PERMISSIONS.hangfireView,
    sidebarVisible: true,
    searchVisible: true,
  },
  {
    key: 'theme-test',
    path: '/theme-test',
    title: '主题测试',
    authOnly: true,
    searchVisible: false,
    defaultEntry: false,
  },
] as const;

export const consoleRouteMetaMap = Object.fromEntries(
  consoleRouteMeta.map((route) => [route.key, route])
) as Record<string, ConsoleRouteMeta>;

export const routeTitleMap: Record<string, string> = Object.fromEntries(
  [
    ...consoleRouteMeta.map((route) => [route.path, route.title] as const),
    ['/login', '登录'] as const,
    ['/callback', 'OIDC 回调'] as const,
  ]
);

export function canAccessConsoleRoute(route: ConsoleRouteMeta, user: UserInfo | null | undefined): boolean {
  if (route.authOnly) {
    return Boolean(user);
  }

  return hasPermission(user, route.requiredPermission);
}

export function hasConsoleOperationalPermission(user: UserInfo | null | undefined): boolean {
  return consoleRouteMeta.some((route) => Boolean(route.requiredPermission) && canAccessConsoleRoute(route, user));
}

export function canEnterConsole(user: UserInfo | null | undefined): boolean {
  if (!user) {
    return false;
  }

  const normalizedRoles = (user.roles || [])
    .map((role) => role.trim().toLowerCase())
    .filter(Boolean);

  if (normalizedRoles.includes('admin') || normalizedRoles.includes('system')) {
    return true;
  }

  return hasPermission(user, CONSOLE_PERMISSIONS.consoleAccess) && hasConsoleOperationalPermission(user);
}

export function getSidebarRoutes(user: UserInfo | null | undefined): ConsoleRouteMeta[] {
  return consoleRouteMeta.filter((route) => route.sidebarVisible && canAccessConsoleRoute(route, user));
}

export function getSearchableRoutes(user: UserInfo | null | undefined): ConsoleRouteMeta[] {
  return consoleRouteMeta.filter((route) => route.searchVisible && canAccessConsoleRoute(route, user));
}

export function getDefaultAuthorizedPath(user: UserInfo | null | undefined): string {
  return consoleRouteMeta.find((route) => route.defaultEntry !== false && canAccessConsoleRoute(route, user))?.path ?? '/profile';
}

export function getActiveMenuKey(pathname: string): string {
  const sidebarRoutes = consoleRouteMeta.filter((route) => route.sidebarVisible);
  const exactMatch = sidebarRoutes.find((route) => route.path === pathname);
  if (exactMatch) {
    return exactMatch.key;
  }

  const prefixMatch = sidebarRoutes.find(
    (route) => route.path !== '/' && pathname.startsWith(`${route.path}/`)
  );

  return prefixMatch?.key ?? '';
}
