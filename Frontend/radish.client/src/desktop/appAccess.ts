import type { AppDefinition } from './types';

const CONSOLE_ACCESS_PERMISSION = 'console.access';

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
    if (permission.startsWith('console.') && permission !== CONSOLE_ACCESS_PERMISSION) {
      return true;
    }
  }

  return false;
}

export function canAccessApp(app: AppDefinition, context: AppAccessContext = {}): boolean {
  const normalizedRoles = normalizeValues(context.userRoles);
  const normalizedPermissions = normalizeValues(context.userPermissions);
  const isAuthenticated = Boolean(context.isAuthenticated);

  if (app.id === 'console') {
    return normalizedRoles.has('admin') ||
      normalizedRoles.has('system') ||
      (normalizedPermissions.has(CONSOLE_ACCESS_PERMISSION) && hasConsoleOperationalPermission(normalizedPermissions));
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
  if (app.id === 'console') {
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
