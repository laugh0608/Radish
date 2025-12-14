import { WelcomeApp } from '@/apps/welcome/WelcomeApp';
import { ShowcaseApp } from '@/apps/showcase/ShowcaseApp';
import { AuthTestApp } from '@/apps/auth-test/AuthTestApp';
import { ForumApp } from '@/apps/forum/ForumApp';
import type { AppDefinition } from './types';

/**
 * 应用注册表
 *
 * 所有可用应用都在这里注册
 */
export const appRegistry: AppDefinition[] = [
  {
    id: 'welcome',
    name: '欢迎',
    icon: 'mdi:hand-wave',
    description: 'Radish WebOS 欢迎页面',
    component: WelcomeApp,
    type: 'window',
    defaultSize: { width: 900, height: 700 },
    requiredRoles: ['User'],
    category: 'system'
  },
  {
    id: 'showcase',
    name: '组件展示',
    icon: 'mdi:palette',
    description: '查看和测试所有 UI 组件',
    component: ShowcaseApp,
    type: 'window',
    defaultSize: { width: 1200, height: 800 },
    requiredRoles: ['User'],
    category: 'development'
  },
  {
    id: 'auth-test',
    name: '认证测试',
    icon: 'mdi:shield-account',
    description: '测试 OIDC 登录和 API 调用',
    component: AuthTestApp,
    type: 'window',
    defaultSize: { width: 1000, height: 700 },
    requiredRoles: ['User'],
    category: 'development'
  },
  {
    id: 'docs',
    name: '文档中心',
    icon: 'mdi:book-open-page-variant',
    description: 'Radish 项目文档',
    component: () => null, // iframe 应用不需要实际组件
    type: 'iframe',
    url: 'http://localhost:3001/docs/',
    defaultSize: { width: 1200, height: 800 },
    requiredRoles: ['User'],
    category: 'development'
  },
  {
    id: 'console',
    name: '控制台',
    icon: 'mdi:console',
    description: 'Radish 管理控制台',
    component: () => null, // external 应用不需要实际组件
    type: 'external',
    // 通过 Gateway 访问时使用相对路径，直接访问时使用绝对路径
    externalUrl: typeof window !== 'undefined' &&
      (window.location.origin === 'https://localhost:5000' || window.location.origin === 'http://localhost:5000')
      ? '/console/'
      : 'http://localhost:3002',
    requiredRoles: ['User'],
    category: 'system'
  },
  {
    id: 'forum',
    name: '论坛',
    icon: 'mdi:forum',
    description: '社区讨论与内容分享',
    component: ForumApp,
    type: 'window',
    defaultSize: { width: 1200, height: 800 },
    requiredRoles: ['User'],
    category: 'content'
  },

];

/**
 * 根据 ID 获取应用定义
 */
export const getAppById = (id: string): AppDefinition | undefined => {
  return appRegistry.find(app => app.id === id);
};

/**
 * 根据用户角色过滤可见应用
 */
// 当前阶段：默认所有应用在桌面可见，后续如需按角色控制可再扩展
export const getVisibleApps = (userRoles: string[] = []): AppDefinition[] => {
  return appRegistry;
};
