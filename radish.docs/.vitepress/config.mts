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
      { text: '开发文档', link: '/guide' },
      { text: '架构设计', link: '/DevelopmentFramework' },
      { text: '部署运维', link: '/DeploymentGuide' }
    ],
    sidebar: {
      '/': [
        {
          text: '快速开始',
          items: [
            { text: '项目概览', link: '/' }
          ]
        },
        {
          text: '开发指南',
          items: [
            { text: '开发规范', link: '/DevelopmentSpecifications' },
            { text: '开发框架说明', link: '/DevelopmentFramework' },
            { text: '认证与权限', link: '/AuthenticationGuide' },
            { text: '前端设计', link: '/FrontendDesign' },
            { text: 'Gateway 规划', link: '/GatewayPlan' }
          ]
        },
        {
          text: '前端组件库',
          items: [
            { text: 'UI 组件库', link: '/UIComponentLibrary' },
            { text: '组件详细说明', link: '/UIComponentsSummary' },
            { text: '快速参考', link: '/UIQuickReference' }
          ]
        },
        {
          text: '部署与运维',
          items: [
            { text: '部署指南', link: '/DeploymentGuide' },
            { text: '开发计划', link: '/DevelopmentPlan' },
            { text: '开发日志', link: '/DevelopmentLog' }
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
