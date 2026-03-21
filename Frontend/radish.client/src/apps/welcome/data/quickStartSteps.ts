/**
 * 欢迎页快速上手指南数据
 */

export interface QuickStartStep {
  title: string;
  description: string;
  icon: string;
}

export interface QuickStartCategory {
  category: string;
  icon: string;
  steps: QuickStartStep[];
}

export const quickStartSteps: QuickStartCategory[] = [
  {
    category: '桌面基础操作',
    icon: 'mdi:monitor-cellphone',
    steps: [
      {
        title: '双击桌面图标打开应用',
        description: '桌面图标是最直接的入口。双击后会打开独立窗口，适合同时浏览多个应用内容。',
        icon: 'mdi:cursor-default-click-outline'
      },
      {
        title: '使用顶部 Dock 管理运行中的窗口',
        description: 'Dock 会显示已打开应用、通知中心和账户入口。点击图标可以恢复、切换或再次聚焦窗口。',
        icon: 'mdi:dock-top'
      },
      {
        title: '窗口支持最小化、还原和关闭',
        description: '窗口标题栏提供基础控制；需要临时收起内容时优先使用最小化，而不是直接关闭。',
        icon: 'mdi:window-restore'
      },
      {
        title: '主题与语言切换会即时生效',
        description: 'Dock 右侧提供主题和语言状态按钮，切换后会同步影响桌面壳层与已适配页面。',
        icon: 'mdi:translate-variant'
      }
    ]
  },
  {
    category: '常用应用入口',
    icon: 'mdi:apps-box',
    steps: [
      {
        title: '论坛负责主要互动内容',
        description: '论坛应用承接发帖、评论、问答、投票等主要社区互动，是当前最值得优先体验的内容区。',
        icon: 'mdi:forum-outline'
      },
      {
        title: '文档应用提供说明与知识入口',
        description: '如果想了解规则、说明、Wiki 页面或固定内容，优先从文档应用进入。',
        icon: 'mdi:book-open-outline'
      },
      {
        title: '通知中心查看动态提醒',
        description: '通知中心聚合系统提醒和互动消息，适合快速回看最近发生的变化。',
        icon: 'mdi:bell-outline'
      },
      {
        title: '个人主页与排行榜用于回看状态',
        description: '个人主页可查看个人内容与行为记录，排行榜与等级页可快速了解当前成长状态。',
        icon: 'mdi:account-badge-outline'
      }
    ]
  },
  {
    category: '进阶体验路径',
    icon: 'mdi:route',
    steps: [
      {
        title: '萝卜坑集中承载业务功能',
        description: '萝卜坑已经覆盖账户总览、交易记录、安全设置和统计视图，是当前复杂业务体验的核心窗口。',
        icon: 'mdi:wallet-outline'
      },
      {
        title: '组件库适合查看前端能力样例',
        description: '组件库应用用于预览共享 UI 组件和交互状态，适合前端联调时快速核对视觉与行为。',
        icon: 'mdi:view-grid-plus-outline'
      },
      {
        title: '控制台与文档链路是外围能力入口',
        description: '具备权限时可以继续进入控制台、认证和 API 文档等外围能力，帮助你了解整套系统的完整边界。',
        icon: 'mdi:application-cog-outline'
      }
    ]
  },
  {
    category: '遇到问题时',
    icon: 'mdi:lifebuoy',
    steps: [
      {
        title: '优先以桌面中可见入口为准',
        description: '欢迎页只做概览说明，功能是否可用、内容是否接入，以桌面应用当前实际状态为准。',
        icon: 'mdi:map-marker-path'
      },
      {
        title: '规范类信息优先查看 Docs',
        description: '涉及配置、流程、视觉规范和开发口径时，请优先参考 Docs 和文档应用中的最新内容。',
        icon: 'mdi:file-document-outline'
      },
      {
        title: '发现文案过时或体验异常及时反馈',
        description: '如果发现欢迎页内容、桌面交互或主题适配有偏差，请在联调阶段尽早提出，便于统一收口。',
        icon: 'mdi:message-alert-outline'
      }
    ]
  }
];
