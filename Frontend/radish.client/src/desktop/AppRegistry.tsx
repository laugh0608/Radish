import { lazy, Suspense, type ComponentType } from 'react';
import type { AppDefinition } from './types';

const createLazyWindowApp = (loader: () => Promise<{ default: ComponentType }>): ComponentType => {
  const LazyComponent = lazy(loader);

  return function LazyWindowApp() {
    return (
      <Suspense fallback={<div style={{ padding: '1rem', textAlign: 'center' }}>应用加载中...</div>}>
        <LazyComponent />
      </Suspense>
    );
  };
};

const WelcomeApp = createLazyWindowApp(() =>
  import('@/apps/welcome/WelcomeApp').then((module) => ({ default: module.WelcomeApp }))
);

const ShowcaseApp = createLazyWindowApp(() =>
  import('@/apps/showcase/ShowcaseApp').then((module) => ({ default: module.ShowcaseApp }))
);

const ForumApp = createLazyWindowApp(() =>
  import('@/apps/forum/ForumApp').then((module) => ({ default: module.ForumApp }))
);

const ChatApp = createLazyWindowApp(() =>
  import('@/apps/chat/ChatApp').then((module) => ({ default: module.ChatApp }))
);

const ProfileApp = createLazyWindowApp(() =>
  import('@/apps/profile/ProfileApp').then((module) => ({ default: module.ProfileApp }))
);

const RadishPitApp = createLazyWindowApp(() =>
  import('@/apps/radish-pit').then((module) => ({ default: module.RadishPitApp }))
);

const NotificationApp = createLazyWindowApp(() =>
  import('@/apps/notification/NotificationApp').then((module) => ({ default: module.NotificationApp }))
);

const LeaderboardApp = createLazyWindowApp(() =>
  import('@/apps/leaderboard/LeaderboardApp').then((module) => ({ default: module.LeaderboardApp }))
);

const ExperienceDetailApp = createLazyWindowApp(() =>
  import('@/apps/experience-detail/ExperienceDetailApp').then((module) => ({ default: module.ExperienceDetailApp }))
);

const ShopApp = createLazyWindowApp(() =>
  import('@/apps/shop/ShopApp').then((module) => ({ default: module.ShopApp }))
);

const WikiApp = createLazyWindowApp(() =>
  import('@/apps/wiki/WikiApp').then((module) => ({ default: module.WikiApp }))
);

/**
 * 判断是否通过 Gateway 访问（5000端口）
 */
const isAccessingViaGateway = (): boolean => {
  if (typeof window === 'undefined') return false;
  return window.location.port === '5000';
};

/**
 * 应用注册表
 *
 * Radish WebOS 支持三种应用集成方式:
 *
 * 1. 内置应用 (type: 'window')
 *    - 简单功能模块,无需独立部署
 *    - 直接使用 React 组件,在 WebOS 窗口中渲染
 *    - 可共享认证状态和全局状态
 *    - 示例: Forum(论坛), Chat(聊天), Settings(设置)
 *
 * 2. 嵌入应用 (type: 'iframe')
 *    - 展示型应用,无需认证或简单认证
 *    - 通过 iframe 嵌入,但在 WebOS 窗口内显示
 *    - 无复杂路由,用户主要被动浏览
 *    - 示例: Docs(文档站), Help(帮助中心)
 *
 * 3. 外部应用 (type: 'external')
 *    - 完整的独立 SPA,有自己的 OIDC 认证流程
 *    - 复杂的路由系统,需要控制浏览器地址栏
 *    - 在新标签页打开,完全独立运行
 *    - 需要独立访问和部署
 *    - 示例: Console(管理控制台), Shop(商城)
 *
 * 为什么不把所有应用都嵌入 WebOS?
 * - OIDC 认证流程在 iframe 中无法正常工作
 * - 复杂路由会与 WebOS 路由冲突
 * - 关注点分离: 用户应用 vs 管理应用
 * - 部署灵活性: 公网 vs 内网
 * - 代码体积控制: 避免普通用户加载管理功能
 *
 * 详见: Docs/radish.docs/docs/FrontendDesign.md 第 10.4 节
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
    category: 'system',
  },
  {
    id: 'showcase',
    name: '组件库',
    icon: 'mdi:view-grid-plus',
    description: '@radish/ui 组件库预览',
    component: ShowcaseApp,
    type: 'window',
    defaultSize: { width: 1200, height: 800 },
    requiredRoles: ['User'],
    category: 'development',
  },
  {
    id: 'docs',
    name: '文档中心',
    icon: 'mdi:book-open-page-variant',
    description: 'Radish 项目文档',
    component: () => null,
    type: 'iframe',
    url: isAccessingViaGateway() ? '/docs/' : 'http://localhost:4000/docs/',
    defaultSize: { width: 1200, height: 800 },
    requiredRoles: ['User'],
    category: 'development',
  },
  {
    id: 'console',
    name: '控制台',
    icon: 'mdi:console',
    description: 'Radish 管理控制台',
    component: () => null,
    type: 'external',
    externalUrl: isAccessingViaGateway() ? '/console/' : 'http://localhost:3100',
    requiredRoles: ['User'],
    category: 'system',
  },
  {
    id: 'wiki',
    name: '知识库',
    icon: 'mdi:notebook-edit-outline',
    description: 'Wiki 与 Markdown 文档中心',
    component: WikiApp,
    type: 'window',
    defaultSize: { width: 1280, height: 820 },
    requiredRoles: ['User'],
    category: 'content',
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
    category: 'content',
  },
  {
    id: 'chat',
    name: '聊天室',
    icon: 'mdi:message-text',
    description: '频道制实时聊天',
    component: ChatApp,
    type: 'window',
    defaultSize: { width: 1100, height: 750 },
    requiredRoles: ['User'],
    category: 'content',
  },
  {
    id: 'profile',
    name: '个人主页',
    icon: 'mdi:account',
    description: '查看个人信息、帖子和评论',
    component: ProfileApp,
    type: 'window',
    defaultSize: { width: 1000, height: 700 },
    requiredRoles: ['User'],
    category: 'user',
  },
  {
    id: 'radish-pit',
    name: '萝卜坑',
    icon: 'mdi:wallet',
    description: '萝卜管理中心 - 转移、记录、统计、安全',
    component: RadishPitApp,
    type: 'window',
    defaultSize: { width: 1200, height: 800 },
    requiredRoles: ['User'],
    category: 'user',
  },
  {
    id: 'notification',
    name: '通知中心',
    icon: 'mdi:bell',
    description: '查看和管理系统通知',
    component: NotificationApp,
    type: 'window',
    defaultSize: { width: 800, height: 700 },
    requiredRoles: ['User'],
    category: 'system',
  },
  {
    id: 'leaderboard',
    name: '排行榜',
    icon: 'mdi:trophy',
    description: '经验值排行榜',
    component: LeaderboardApp,
    type: 'window',
    defaultSize: { width: 900, height: 700 },
    requiredRoles: ['User'],
    category: 'user',
  },
  {
    id: 'experience-detail',
    name: '等级',
    icon: 'mdi:star-circle',
    description: '等级与经验值详情',
    component: ExperienceDetailApp,
    type: 'window',
    defaultSize: { width: 1000, height: 800 },
    requiredRoles: ['User'],
    category: 'user',
  },
  {
    id: 'shop',
    name: '萝卜商城',
    icon: 'mdi:shopping',
    description: '使用胡萝卜购买权益和道具',
    component: ShopApp,
    type: 'window',
    defaultSize: { width: 1200, height: 800 },
    requiredRoles: ['User'],
    category: 'user',
  },
];

/**
 * 根据 ID 获取应用定义
 */
export const getAppById = (id: string): AppDefinition | undefined => {
  return appRegistry.find((app) => app.id === id);
};

/**
 * 根据用户角色过滤可见应用
 */
export const getVisibleApps = (_userRoles: string[] = []): AppDefinition[] => {
  return appRegistry;
};
