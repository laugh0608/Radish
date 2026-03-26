import assert from 'node:assert/strict';
import test from 'node:test';
import type { AppDefinition } from '../src/desktop/types.ts';
import { canAccessApp, getVisibleAppsForUser, shouldShowAppOnDesktop } from '../src/desktop/appAccess.ts';

const publicApp: AppDefinition = {
  id: 'document',
  name: '文档',
  icon: 'mdi:notebook-edit-outline',
  component: () => null,
  type: 'window',
};

const loginRequiredApp: AppDefinition = {
  id: 'chat',
  name: '聊天室',
  icon: 'mdi:message-text',
  component: () => null,
  type: 'window',
  requiredRoles: ['User'],
};

const consoleApp: AppDefinition = {
  id: 'console',
  name: '控制台',
  icon: 'mdi:console',
  component: () => null,
  type: 'external',
  externalUrl: '/console/',
};

test('canAccessApp 在未登录时应拦截需要 User 角色的应用', () => {
  assert.equal(canAccessApp(loginRequiredApp, {
    isAuthenticated: false,
    userRoles: [],
    userPermissions: [],
  }), false);
});

test('canAccessApp 在已登录时应允许打开需要 User 角色的应用', () => {
  assert.equal(canAccessApp(loginRequiredApp, {
    isAuthenticated: true,
    userRoles: [],
    userPermissions: [],
  }), true);
});

test('canAccessApp 在未登录时应允许打开公开应用', () => {
  assert.equal(canAccessApp(publicApp, {
    isAuthenticated: false,
    userRoles: [],
    userPermissions: [],
  }), true);
});

test('shouldShowAppOnDesktop 在未登录时仍应显示常规应用图标', () => {
  assert.equal(shouldShowAppOnDesktop(publicApp, {
    isAuthenticated: false,
    userRoles: [],
    userPermissions: [],
  }), true);
});

test('getVisibleAppsForUser 应保留常规桌面应用，仅按权限隐藏控制台', () => {
  const apps = [publicApp, loginRequiredApp, consoleApp];

  const anonymousVisible = getVisibleAppsForUser(apps, {
    isAuthenticated: false,
    userRoles: [],
    userPermissions: [],
  });
  assert.deepEqual(anonymousVisible.map((app) => app.id), ['document', 'chat']);

  const authenticatedVisible = getVisibleAppsForUser(apps, {
    isAuthenticated: true,
    userRoles: ['User'],
    userPermissions: [],
  });
  assert.deepEqual(authenticatedVisible.map((app) => app.id), ['document', 'chat']);

  const consoleAccessOnlyVisible = getVisibleAppsForUser(apps, {
    isAuthenticated: true,
    userRoles: ['User'],
    userPermissions: ['console.access'],
  });
  assert.deepEqual(consoleAccessOnlyVisible.map((app) => app.id), ['document', 'chat']);

  const consoleVisible = getVisibleAppsForUser(apps, {
    isAuthenticated: true,
    userRoles: ['User'],
    userPermissions: ['console.access', 'console.tags.view'],
  });
  assert.deepEqual(consoleVisible.map((app) => app.id), ['document', 'chat', 'console']);
});

test('getVisibleAppsForUser 不应因非入口型 Console 权限而显示控制台', () => {
  const apps = [publicApp, loginRequiredApp, consoleApp];

  const visibleApps = getVisibleAppsForUser(apps, {
    isAuthenticated: true,
    userRoles: ['User'],
    userPermissions: ['console.access', 'console.tags.create'],
  });

  assert.deepEqual(visibleApps.map((app) => app.id), ['document', 'chat']);
});
