import type { AppDefinition } from './types';

const CONSOLE_ACCESS_PERMISSION = 'console.access';
const ADMIN_VISIBLE_APP_IDS = new Set(['scalar']);
const CONSOLE_ENTRY_PERMISSIONS = new Set([
  'console.dashboard.view',
  'console.applications.view',
  'console.products.view',
  'console.orders.view',
  'console.users.view',
  'console.roles.view',
  'console.roles.edit',
  'console.categories.view',
  'console.tags.view',
  'console.stickers.view',
  'console.moderation.view',
  'console.coins.view',
  'console.experience.view',
  'console.system-config.view',
  'console.hangfire.view',
]);

type AppAccessContext = {
  isAuthenticated?: boolean;
  userRoles?: string[];
  userPermissions?: string[];
};

function normalizeValues(values: string[] = []): Set<string> {
  return new Set(values.map((value) => value.trim().toLowerCase()).filter(Boolean));
}

function hasConsoleOperationalPermission(permissions: Set<string>): boolean {
  for (const permission of permissions) {
    if (CONSOLE_ENTRY_PERMISSIONS.has(permission)) {
      return true;
    }
  }

  return false;
}

export function canAccessApp(app: AppDefinition, context: AppAccessContext = {}): boolean {
  const normalizedRoles = normalizeValues(context.userRoles);
  const normalizedPermissions = normalizeValues(context.userPermissions);
  const isAuthenticated = Boolean(context.isAuthenticated);
  const isAdminLike = normalizedRoles.has('admin') || normalizedRoles.has('system');

  if (app.id === 'console') {
    return isAdminLike ||
      (normalizedPermissions.has(CONSOLE_ACCESS_PERMISSION) && hasConsoleOperationalPermission(normalizedPermissions));
  }

  if (ADMIN_VISIBLE_APP_IDS.has(app.id)) {
    return isAdminLike;
  }

  const requiredRoles = (app.requiredRoles || [])
    .map((role) => role.trim().toLowerCase())
    .filter(Boolean);

  if (requiredRoles.length === 0) {
    return true;
  }

  if (requiredRoles.some((role) => normalizedRoles.has(role))) {
    return true;
  }

  return isAuthenticated && requiredRoles.includes('user');
}

export function shouldShowAppOnDesktop(app: AppDefinition, context: AppAccessContext = {}): boolean {
  if (app.id === 'console' || ADMIN_VISIBLE_APP_IDS.has(app.id)) {
    return canAccessApp(app, context);
  }

  return true;
}

export function getVisibleAppsForUser(
  apps: AppDefinition[],
  context: AppAccessContext = {}
): AppDefinition[] {
  return apps.filter((app) => shouldShowAppOnDesktop(app, context));
}
