/**
 * 关于 Radish 当前阶段的内容数据
 */

export interface AboutSection {
  title: string;
  content: string;
  icon: string;
}

export interface Feature {
  name: string;
  description: string;
  icon: string;
  status?: 'available' | 'iterating';
}

export interface TechHighlight {
  name: string;
  description: string;
  icon: string;
}

export const aboutContent = {
  vision: {
    title: '产品定位',
    content:
      'Radish 正在构建一个以 WebOS 桌面为入口的社区工作台，把论坛、文档、通知、个人主页和萝卜坑等核心能力集中到同一套窗口化体验中，减少多页面来回切换的割裂感。',
    icon: 'mdi:compass-outline'
  } as AboutSection,

  mission: {
    title: '当前阶段',
    content:
      '当前主线已经进入主题与 i18n 的手工联调和体验优化阶段。重点是统一视觉 token、梳理双语口径、修复桌面交互细节，并让高频应用之间形成更顺手的跳转闭环。',
    icon: 'mdi:progress-wrench'
  } as AboutSection,

  features: [
    {
      name: '论坛讨论',
      description: '帖子、评论、问答、投票和编辑记录等内容链路已经接入，是当前最完整的互动主场景。',
      icon: 'mdi:forum-outline',
      status: 'available'
    },
    {
      name: '文档与 Wiki',
      description: '文档应用承接固定说明、Wiki 页面和 Markdown 内容查看，是当前说明入口与知识沉淀中心。',
      icon: 'mdi:book-open-page-variant-outline',
      status: 'available'
    },
    {
      name: '通知与个人主页',
      description: '通知中心、个人主页和排行榜已具备联动基础，用于查看提醒、个人内容和成长状态。',
      icon: 'mdi:account-star-outline',
      status: 'available'
    },
    {
      name: '萝卜坑',
      description: '萝卜坑已提供账户总览、转移、记录、安全与统计等页面，是当前最复杂的桌面业务应用之一。',
      icon: 'mdi:wallet-outline',
      status: 'iterating'
    },
    {
      name: '主题与双语',
      description: '桌面壳层已具备 default / guofeng 主题切换与 zh / en 双语基线，正在持续打磨页面适配质量。',
      icon: 'mdi:palette-swatch-outline',
      status: 'iterating'
    },
    {
      name: '外围链路',
      description: '控制台、认证、网关与 API 文档等能力已经连通，后续会继续提升桌面内外入口的一致性。',
      icon: 'mdi:transit-connection-variant',
      status: 'available'
    }
  ] as Feature[],

  techStack: [
    {
      name: '桌面化交互壳层',
      description: '窗口管理、Dock、桌面图标和应用注册构成 WebOS 体验基线。',
      icon: 'mdi:monitor-dashboard'
    },
    {
      name: '现代后端主链路',
      description: 'ASP.NET Core 10、SQLSugar、PostgreSQL / SQLite 与网关认证链路共同支撑服务侧能力。',
      icon: 'mdi:server-outline'
    },
    {
      name: '前端工程与状态',
      description: 'React 19、Vite、TypeScript、Zustand 与 SignalR 组成当前前端工作流与实时能力基础。',
      icon: 'mdi:source-branch'
    },
    {
      name: '主题与内容一致性',
      description: '统一 token、视觉角色命名和文案口径，是当前联调阶段的重点工程。',
      icon: 'mdi:format-color-fill'
    }
  ] as TechHighlight[]
};
