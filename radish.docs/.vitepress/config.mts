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

  // 配置 markdown-it 来转义代码块中的 Vue 模板语法
  markdown: {
    config: (md) => {
      // 保存原始的 fence 渲染器
      const defaultFenceRender = md.renderer.rules.fence || function(tokens, idx, options, env, slf) {
        return slf.renderToken(tokens, idx, options)
      }

      // 覆盖 fence 渲染器
      md.renderer.rules.fence = (tokens, idx, options, env, slf) => {
        const token = tokens[idx]
        const info = token.info ? md.utils.unescapeAll(token.info).trim() : ''
        const langName = info.split(/(\s+)/g)[0]

        // 只处理 csharp/cs/c# 代码块
        if (langName === 'csharp' || langName === 'cs' || langName === 'c#') {
          // 转义可能导致 Vue 解析错误的字符
          // 1. 转义箭头函数语法 =>
          // 2. 转义泛型类型的尖括号 < >
          token.content = token.content
            .replace(/=>/g, '=&gt;')  // 将 => 转义为 =&gt;
            .replace(/</g, '&lt;')    // 将 < 转义为 &lt;
            .replace(/>/g, '&gt;')    // 将 > 转义为 &gt;
        }

        // 调用默认渲染器
        return defaultFenceRender(tokens, idx, options, env, slf)
      }
    }
  },

  themeConfig: {
    nav: [
      { text: '首页', link: '/' },
      { text: '开发指南', link: '/guide/getting-started' },
      { text: '架构设计', link: '/architecture/overview' },
      { text: '开发日志', link: '/changelog/' },
      { text: '部署运维', link: '/deployment/guide' }
    ],
    sidebar: {
      '/': [
        {
          text: '快速开始',
          items: [
            { text: '项目概览', link: '/' },
            { text: '开发计划', link: '/development-plan' },
            { text: '文件上传 API', link: '/api/file-upload-api' }
          ]
        },
        {
          text: '开发指南',
          collapsible: true,
          items: [
            { text: '快速开始', link: '/guide/getting-started' },
            { text: '认证与权限', link: '/guide/authentication' },
            { text: '配置管理', link: '/guide/configuration' },
            { text: '日志系统', link: '/guide/logging' },
            { text: '密码安全', link: '/guide/password-security' },
            { text: '速率限制', link: '/guide/rate-limiting' },
            {
              text: '通知系统',
              collapsed: true,
              items: [
                { text: '总体规划', link: '/guide/notification-realtime' },
                { text: '实现细节', link: '/guide/notification-implementation' },
                { text: 'API 文档', link: '/guide/notification-api' },
                { text: '前端集成', link: '/guide/notification-frontend' }
              ]
            },
            {
              text: '经验等级系统',
              collapsed: true,
              items: [
                { text: '总体设计', link: '/guide/experience-level-system' },
                { text: '核心概念', link: '/guide/experience-level-core-concepts' },
                { text: '获取机制', link: '/guide/experience-level-earning' },
                { text: '后端设计', link: '/guide/experience-level-backend' },
                { text: '前端展示', link: '/guide/experience-level-frontend' },
                { text: '运维与测试', link: '/guide/experience-level-ops' },
                { text: '实施计划', link: '/guide/experience-level-roadmap' }
              ]
            },
            {
              text: '萝卜坑应用',
              collapsed: true,
              items: [
                { text: '总体设计', link: '/guide/radish-pit-system' },
                { text: '核心概念', link: '/guide/radish-pit-core-concepts' },
                { text: '功能模块', link: '/guide/radish-pit-game-mechanics' },
                { text: '后端设计', link: '/guide/radish-pit-backend' },
                { text: '前端设计', link: '/guide/radish-pit-frontend' },
                { text: '实施计划', link: '/guide/radish-pit-roadmap' }
              ]
            },
            { text: 'Hangfire 定时任务', link: '/guide/hangfire-scheduled-jobs' },
            { text: 'Gateway 服务网关', link: '/guide/gateway' }
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
            { text: '架构总览', link: '/architecture/overview' },
            { text: '开发框架说明', link: '/architecture/framework' },
            { text: '开发规范', link: '/architecture/specifications' },
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
            { text: '开放平台指南', link: '/features/open-platform' },
            { text: '文件上传功能设计', link: '/features/file-upload-design' }
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
