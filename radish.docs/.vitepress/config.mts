import { defineConfig } from 'vitepress'

// Radish 文档站配置
export default defineConfig({
  lang: 'zh-CN',
  title: 'Radish 文档',
  description: 'Radish 社区平台文档',

  ignoreDeadLinks: true,

  // 使用符号链接指向仓库根目录下的 docs
  srcDir: './docs',

  vite: {
    resolve: {
      preserveSymlinks: true
    },
    ssr: {
      noExternal: ['vue', '@vue/server-renderer']
    }
  },
  // 构建输出到 Gateway 的 DocsSite 目录,便于直接托管
  outDir: '../Radish.Gateway/DocsSite',
  // 与 Gateway 中 Docs.RequestPath 保持一致
  base: '/docs/',

  themeConfig: {
    nav: [
      { text: '首页', link: '/' },
      { text: '开发文档', link: '/DevelopmentSpecifications' },
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
