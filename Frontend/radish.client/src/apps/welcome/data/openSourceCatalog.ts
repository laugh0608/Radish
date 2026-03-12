export interface OpenSourceProject {
  name: string;
  purpose: string;
  license: string;
  website: string;
  note?: string;
}

export const openSourceOverview = {
  title: '开源软件说明',
  summary:
    'Radish 基于多项优秀的开源项目构建。当前页面先展示项目级核心依赖与基础许可证信息，用于欢迎页说明与合规入口提示。',
  scope: '项目级核心依赖',
  source: '当前仓库静态维护',
  nextStep: '后续可升级为构建时自动生成完整 THIRD-PARTY-NOTICES'
};

export const openSourceProjects: OpenSourceProject[] = [
  {
    name: 'ASP.NET Core',
    purpose: '承载 API、网关与认证服务的后端框架',
    license: 'MIT',
    website: 'https://github.com/dotnet/aspnetcore'
  },
  {
    name: 'React',
    purpose: '承载 WebOS 桌面与业务应用界面的前端 UI 框架',
    license: 'MIT',
    website: 'https://github.com/facebook/react'
  },
  {
    name: 'TypeScript',
    purpose: '提供前端类型系统与工程化开发体验',
    license: 'Apache-2.0',
    website: 'https://github.com/microsoft/TypeScript'
  },
  {
    name: 'Vite (Rolldown)',
    purpose: '承担前端开发服务器、构建与打包流程',
    license: 'MIT',
    website: 'https://github.com/vitejs/vite',
    note: '当前仓库通过 rolldown-vite 适配使用 Vite 工作流'
  },
  {
    name: 'Zustand',
    purpose: '管理 WebOS 窗口、用户与应用状态',
    license: 'MIT',
    website: 'https://github.com/pmndrs/zustand'
  },
  {
    name: 'SignalR',
    purpose: '支持通知与聊天室等实时通信能力',
    license: 'MIT',
    website: 'https://github.com/dotnet/aspnetcore'
  },
  {
    name: 'PostgreSQL',
    purpose: '作为主要关系型数据库方案，开发环境可切换 SQLite',
    license: 'PostgreSQL License',
    website: 'https://github.com/postgres/postgres'
  },
  {
    name: 'Iconify',
    purpose: '提供欢迎页与 WebOS 桌面所使用的图标能力',
    license: 'MIT',
    website: 'https://github.com/iconify/iconify'
  }
];
