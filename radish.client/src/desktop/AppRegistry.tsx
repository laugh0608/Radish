import { WelcomeApp } from '@/apps/welcome/WelcomeApp';
import { ShowcaseApp } from '@/apps/showcase/ShowcaseApp';
import { ForumApp } from '@/apps/forum/ForumApp';
import { ProfileApp } from '@/apps/profile/ProfileApp';
import { CoinRewardDemo } from '@/apps/coin-demo';
import { NotificationApp } from '@/apps/notification/NotificationApp';
import { LeaderboardApp } from '@/apps/leaderboard/LeaderboardApp';
import { ExperienceDetailApp } from '@/apps/experience-detail/ExperienceDetailApp';
import type { AppDefinition } from './types';

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
 * 详见: radish.docs/docs/FrontendDesign.md 第 10.4 节
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
    id: 'docs',
    name: '文档中心',
    icon: 'mdi:book-open-page-variant',
    description: 'Radish 项目文档',
    component: () => null, // iframe 应用不需要实际组件
    type: 'iframe',
    url: typeof window !== 'undefined' &&
      (window.location.origin === 'https://localhost:5000' || window.location.origin === 'http://localhost:5000')
      ? '/docs/'
      : 'http://localhost:3100/docs/',
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
      : 'http://localhost:3200',
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
  {
    id: 'profile',
    name: '个人主页',
    icon: 'mdi:account',
    description: '查看个人信息、帖子和评论',
    component: ProfileApp,
    type: 'window',
    defaultSize: { width: 1000, height: 700 },
    requiredRoles: ['User'],
    category: 'user'
  },
  {
    id: 'coin-demo',
    name: '萝卜币演示',
    icon: 'mdi:carrot',
    description: '萝卜币奖励通知演示',
    component: CoinRewardDemo,
    type: 'window',
    defaultSize: { width: 900, height: 750 },
    requiredRoles: ['User'],
    category: 'development'
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
    category: 'system'
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
    category: 'user'
  },
  {
    id: 'experience-detail',
    name: '经验详情',
    icon: 'mdi:chart-line',
    description: '经验值详情与统计',
    component: ExperienceDetailApp,
    type: 'window',
    defaultSize: { width: 1000, height: 800 },
    requiredRoles: ['User'],
    category: 'user'
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
export const getVisibleApps = (_userRoles: string[] = []): AppDefinition[] => {
  return appRegistry;
};
