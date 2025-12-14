import { defineConfig } from 'vitepress'

// Radish 文档站配置
export default defineConfig({
  lang: 'zh-CN',
  title: 'Radish 文档',
  description: 'Radish 社区平台文档',

  ignoreDeadLinks: true,

  // 指向当前工程内 docs 目录（不再依赖符号链接或 junction）
  srcDir: './docs',

  vite: {
    resolve: {
      preserveSymlinks: true
    },
    ssr: {
      noExternal: ['vue', '@vue/server-renderer']
    }
  },
  // 构建输出到 radish.docs 内部 dist 目录，由独立 docs 服务托管
  outDir: './dist',
  // 通过 Gateway 在 /docs 路径下反向代理访问
  base: '/docs/',

  themeConfig: {
    nav: [
      { text: '首页', link: '/' },
      { text: '开发指南', link: '/guide/getting-started' },
      { text: '架构设计', link: '/architecture/framework' },
      { text: '开发日志', link: '/changelog/' },
      { text: '部署运维', link: '/deployment/guide' }
    ],
    sidebar: {
      '/': [
        {
          text: '快速开始',
          items: [
            { text: '项目概览', link: '/' },
            { text: '开发计划', link: '/development-plan' }
          ]
        },
        {
          text: '开发指南',
          collapsible: true,
          items: [
            { text: '快速开始', link: '/guide/getting-started' },
            { text: '认证与权限', link: '/guide/authentication' },
            { text: '配置管理', link: '/guide/configuration' },
            { text: '密码安全', link: '/guide/password-security' }
          ]
        },
        {
          text: '前端开发',
          collapsible: true,
          items: [
            { text: '前端设计', link: '/frontend/design' },
            { text: 'UI 组件库概览', link: '/frontend/ui-library' },
            { text: 'API 客户端', link: '/frontend/api-client' },
            { text: '错误处理', link: '/frontend/error-handling' },
            { text: 'DataTable 组件', link: '/frontend/data-table' },
            { text: '组件详细说明', link: '/frontend/components' },
            { text: '快速参考', link: '/frontend/quick-reference' },
            { text: '开发指南', link: '/frontend/development' },
            { text: 'WebOS 快速开始', link: '/frontend/webos-quick-start' }
          ]
        },
        {
          text: '架构设计',
          collapsible: true,
          items: [
            { text: '开发框架说明', link: '/architecture/framework' },
            { text: '开发规范', link: '/architecture/specifications' },
            { text: 'Gateway 规划', link: '/architecture/gateway-plan' },
            { text: '国际化指南', link: '/architecture/i18n' }
          ]
        },
        {
          text: '部署与运维',
          collapsible: true,
          items: [
            { text: '部署指南', link: '/deployment/guide' }
          ]
        },
        {
          text: '特定功能',
          collapsible: true,
          items: [
            { text: '论坛应用评估', link: '/features/forum-assessment' },
            { text: '论坛重构总结', link: '/features/forum-refactoring' },
            { text: '开放平台指南', link: '/features/open-platform' }
          ]
        },
        {
          text: '开发日志',
          collapsible: true,
          items: [
            { text: '日志概览', link: '/changelog/' },
            { text: '2025年12月', link: '/changelog/2025-12' },
            { text: '2025年11月', link: '/changelog/2025-11' },
            { text: '2025年10月', link: '/changelog/2025-10' },
            { text: '2025年9月', link: '/changelog/2025-09' }
          ]
        }
      ]
    },
    outline: 'deep',
    socialLinks: [
      // 可按需补充 GitHub 链接等
      // { icon: 'github', link: 'https://github.com/xxx/Radish' }
    ]
  }
})
