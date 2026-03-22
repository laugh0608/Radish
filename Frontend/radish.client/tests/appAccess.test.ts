import assert from 'node:assert/strict';
import test from 'node:test';
import type { AppDefinition } from '../src/desktop/types.ts';
import { canAccessApp, getVisibleAppsForUser } from '../src/desktop/appAccess.ts';

const userApp: AppDefinition = {
  id: 'document',
  name: '文档',
  icon: 'mdi:notebook-edit-outline',
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
  assert.equal(canAccessApp(userApp, {
    isAuthenticated: false,
    userRoles: [],
    userPermissions: [],
  }), false);
});

test('canAccessApp 在已登录时应允许打开需要 User 角色的应用', () => {
  assert.equal(canAccessApp(userApp, {
    isAuthenticated: true,
    userRoles: [],
    userPermissions: [],
  }), true);
});

test('getVisibleAppsForUser 应按登录态和控制台权限过滤应用', () => {
  const apps = [userApp, consoleApp];

  const anonymousVisible = getVisibleAppsForUser(apps, {
    isAuthenticated: false,
    userRoles: [],
    userPermissions: [],
  });
  assert.deepEqual(anonymousVisible.map((app) => app.id), []);

  const authenticatedVisible = getVisibleAppsForUser(apps, {
    isAuthenticated: true,
    userRoles: ['User'],
    userPermissions: [],
  });
  assert.deepEqual(authenticatedVisible.map((app) => app.id), ['document']);

  const consoleVisible = getVisibleAppsForUser(apps, {
    isAuthenticated: true,
    userRoles: ['User'],
    userPermissions: ['console.access'],
  });
  assert.deepEqual(consoleVisible.map((app) => app.id), ['document', 'console']);
});
