import { WelcomeApp } from '@/apps/welcome/WelcomeApp';
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
    requiredRoles: ['User'], // 所有登录用户都可以访问
    category: 'system'
  },
  // 后续可以添加更多应用...
  // {
  //   id: 'forum',
  //   name: '论坛',
  //   icon: 'mdi:forum',
  //   description: '社区讨论与内容分享',
  //   component: ForumApp,
  //   type: 'window',
  //   defaultSize: { width: 1200, height: 800 },
  //   requiredRoles: ['User'],
  //   category: 'content'
  // },
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
export const getVisibleApps = (userRoles: string[] = []): AppDefinition[] => {
  return appRegistry.filter(app => {
    // 如果应用没有设置角色要求，默认所有人都可见
    if (!app.requiredRoles || app.requiredRoles.length === 0) {
      return true;
    }

    // 检查用户是否有任意一个所需角色
    return app.requiredRoles.some(role => userRoles.includes(role));
  });
};
