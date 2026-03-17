export type OpenSourceGroupId = 'backend' | 'data' | 'frontend' | 'realtime';

export interface OpenSourceGroup {
  id: OpenSourceGroupId;
  label: string;
  description: string;
  icon: string;
}

export interface OpenSourceProject {
  id: string;
  name: string;
  groupId: OpenSourceGroupId;
  purpose: string;
  scope: string;
  license: string;
  repository: string;
  website?: string;
  note?: string;
  licenseNote?: string;
  usedIn: string[];
  isCore: boolean;
}

export const openSourceOverview = {
  title: '开源软件说明',
  summary:
    'Radish 基于多项优秀的开源项目构建。欢迎页中的开源软件页当前采用静态维护方式，集中展示首批关键依赖、主要用途与许可证口径，作为用户可见的说明入口。',
  scope: '首批关键依赖（不含全部传递依赖与开发工具链）',
  source: '当前仓库按实际依赖人工校对并静态维护',
  maintenance: '阶段性人工补充与复核',
  nextStep: '后续可在发布流程中继续扩展为更完整的第三方通知与许可证清单',
  boundaryNotes: [
    '本页用于欢迎 App 内的说明入口，便于用户快速了解项目核心依赖。',
    '许可证、商用授权与附加条款请以对应项目官方仓库、官网和发布说明为准。',
    '像 AutoMapper、Hangfire 这类存在商用或附加授权差异的项目，会在条目中额外标注口径说明。'
  ]
};

export const openSourceGroups: OpenSourceGroup[] = [
  {
    id: 'backend',
    label: '后端宿主与基础设施',
    description: '承载 API、网关、认证、任务调度以及横切能力。',
    icon: 'mdi:server-outline'
  },
  {
    id: 'data',
    label: '数据访问与存储',
    description: '覆盖 ORM、身份存储、数据库与缓存等基础设施。',
    icon: 'mdi:database-outline'
  },
  {
    id: 'frontend',
    label: '前端应用与工程化',
    description: '支撑 WebOS、Console 和共享前端工程的交互体验。',
    icon: 'mdi:monitor-dashboard'
  },
  {
    id: 'realtime',
    label: '实时通信能力',
    description: '负责通知、聊天室等长连接和推送相关能力。',
    icon: 'mdi:lightning-bolt-outline'
  }
];

export const openSourceProjects: OpenSourceProject[] = [
  {
    id: 'aspnet-core',
    name: 'ASP.NET Core',
    groupId: 'backend',
    purpose: '承载 API、网关与认证服务的主要后端框架。',
    scope: '作为 Radish.Api、Radish.Gateway、Radish.Auth 的宿主基础。',
    license: 'MIT',
    repository: 'https://github.com/dotnet/aspnetcore',
    usedIn: ['API', 'Gateway', 'Auth'],
    isCore: true
  },
  {
    id: 'openiddict',
    name: 'OpenIddict',
    groupId: 'backend',
    purpose: '提供 OIDC / OAuth2 服务端能力，支撑单点登录和授权流程。',
    scope: '用于认证服务中的授权端点、客户端注册和令牌发放。',
    license: 'Apache-2.0',
    repository: 'https://github.com/openiddict/openiddict-core',
    usedIn: ['Auth', 'Client OIDC', 'Console OIDC'],
    isCore: true
  },
  {
    id: 'yarp',
    name: 'YARP',
    groupId: 'backend',
    purpose: '承担门户网关和反向代理能力。',
    scope: '用于 Gateway 对内部服务与前端入口的统一转发。',
    license: 'MIT',
    repository: 'https://github.com/dotnet/yarp',
    usedIn: ['Gateway'],
    isCore: true
  },
  {
    id: 'hangfire',
    name: 'Hangfire',
    groupId: 'backend',
    purpose: '负责后台作业、定时任务与管理面板。',
    scope: '用于访问令牌清理、评论高亮统计、商城定时任务等后台任务。',
    license: 'LGPL-3.0-or-later',
    repository: 'https://github.com/HangfireIO/Hangfire',
    usedIn: ['API', '定时任务', 'Dashboard'],
    isCore: true,
    licenseNote: '当前仓库使用 Hangfire.AspNetCore 1.8.x；若涉及二次分发或商用合规，请以官方授权说明为准。'
  },
  {
    id: 'serilog',
    name: 'Serilog',
    groupId: 'backend',
    purpose: '提供结构化日志能力，统一后端日志记录方式。',
    scope: '用于宿主日志、SQL AOP 日志与审计链路的基础输出。',
    license: 'Apache-2.0',
    repository: 'https://github.com/serilog/serilog',
    usedIn: ['API', 'Gateway', 'Auth'],
    isCore: true
  },
  {
    id: 'autofac',
    name: 'Autofac',
    groupId: 'backend',
    purpose: '提供依赖注入容器与模块化注册能力。',
    scope: '用于扩展宿主装配、分层服务注册与部分拦截能力。',
    license: 'MIT',
    repository: 'https://github.com/autofac/Autofac',
    usedIn: ['API', 'Gateway', 'Auth'],
    isCore: false
  },
  {
    id: 'automapper',
    name: 'AutoMapper',
    groupId: 'backend',
    purpose: '承担实体、DTO 与 Vo 之间的映射转换。',
    scope: '用于 Service / Controller 输出模型映射与 Profile 配置。',
    license: '商业授权 / 非宽松许可证',
    repository: 'https://github.com/LuckyPennySoftware/AutoMapper',
    website: 'https://automapper.io/',
    usedIn: ['API', 'Auth', 'Extension'],
    isCore: false,
    note: '当前仓库使用 AutoMapper 15.x，并已在配置中预留 LicenseKey 读取入口。',
    licenseNote: '许可证模式已不同于早期 MIT 版本，具体授权条款请以官方站点与所用版本说明为准。'
  },
  {
    id: 'sqlsugar',
    name: 'SqlSugar',
    groupId: 'data',
    purpose: '作为主要业务 ORM，承担大部分实体查询、分页和多租户能力。',
    scope: '用于 Repository、基础 CRUD、审计和多租户场景。',
    license: 'MIT',
    repository: 'https://github.com/DotNetNext/SqlSugar',
    usedIn: ['API', 'Auth', 'Repository'],
    isCore: true
  },
  {
    id: 'entity-framework-core',
    name: 'Entity Framework Core',
    groupId: 'data',
    purpose: '承载 OpenIddict 相关实体与认证数据存储。',
    scope: '当前主要用于 Auth 中 OpenIddict 的专属 DbContext。',
    license: 'MIT',
    repository: 'https://github.com/dotnet/efcore',
    usedIn: ['Auth', 'OpenIddict 存储'],
    isCore: false
  },
  {
    id: 'stackexchange-redis',
    name: 'StackExchange.Redis',
    groupId: 'data',
    purpose: '提供 Redis 连接能力，支撑缓存和分布式场景。',
    scope: '在启用 Redis 时为缓存层提供客户端支持。',
    license: 'MIT',
    repository: 'https://github.com/StackExchange/StackExchange.Redis',
    usedIn: ['API', 'Gateway', 'Auth', '缓存'],
    isCore: false
  },
  {
    id: 'postgresql',
    name: 'PostgreSQL',
    groupId: 'data',
    purpose: '作为主要关系型数据库方案，面向生产环境部署。',
    scope: '用于业务库、日志库及多环境正式部署方案。',
    license: 'PostgreSQL License',
    repository: 'https://github.com/postgres/postgres',
    website: 'https://www.postgresql.org/',
    usedIn: ['生产数据库', '业务数据'],
    isCore: true
  },
  {
    id: 'sqlite',
    name: 'SQLite',
    groupId: 'data',
    purpose: '作为默认本地开发数据库与轻量部署选项。',
    scope: '用于默认开发环境的业务数据库、日志库和 OpenIddict 数据库。',
    license: 'Public Domain',
    repository: 'https://github.com/sqlite/sqlite',
    website: 'https://www.sqlite.org/',
    usedIn: ['本地开发', '默认数据库'],
    isCore: false,
    licenseNote: 'SQLite 官方声明其核心代码处于 Public Domain，具体说明请以 sqlite.org 官方文本为准。'
  },
  {
    id: 'react',
    name: 'React',
    groupId: 'frontend',
    purpose: '承载 WebOS 桌面、欢迎应用与 Console 的主要 UI 框架。',
    scope: '用于 radish.client、radish.console 与共享组件的视图层。',
    license: 'MIT',
    repository: 'https://github.com/facebook/react',
    usedIn: ['Client', 'Console', 'UI'],
    isCore: true
  },
  {
    id: 'typescript',
    name: 'TypeScript',
    groupId: 'frontend',
    purpose: '提供类型系统、接口约束与更稳健的前端工程体验。',
    scope: '用于前端项目、共享组件库与类型定义。',
    license: 'Apache-2.0',
    repository: 'https://github.com/microsoft/TypeScript',
    usedIn: ['Client', 'Console', 'UI'],
    isCore: true
  },
  {
    id: 'vite-rolldown',
    name: 'Vite (Rolldown)',
    groupId: 'frontend',
    purpose: '承担前端开发服务器、构建与打包流程。',
    scope: '当前 client / console 均通过 rolldown-vite 适配使用 Vite 工作流。',
    license: 'MIT',
    repository: 'https://github.com/vitejs/vite',
    usedIn: ['Client', 'Console'],
    isCore: true,
    note: '仓库中通过 npm alias 固定到 rolldown-vite 版本。'
  },
  {
    id: 'zustand',
    name: 'Zustand',
    groupId: 'frontend',
    purpose: '管理 WebOS 窗口、用户、通知与聊天等全局状态。',
    scope: '当前主要用于 radish.client 侧的状态组织。',
    license: 'MIT',
    repository: 'https://github.com/pmndrs/zustand',
    usedIn: ['Client', '状态管理'],
    isCore: true
  },
  {
    id: 'react-router-dom',
    name: 'React Router DOM',
    groupId: 'frontend',
    purpose: '提供 Console 应用的前端路由能力。',
    scope: '用于管理后台页面导航、回调页和受保护路由。',
    license: 'MIT',
    repository: 'https://github.com/remix-run/react-router',
    website: 'https://reactrouter.com/',
    usedIn: ['Console'],
    isCore: true
  },
  {
    id: 'signalr',
    name: 'SignalR',
    groupId: 'realtime',
    purpose: '提供通知、聊天室等实时通信能力。',
    scope: '前端通过 @microsoft/signalr 客户端接入后端 Hub。',
    license: 'MIT',
    repository: 'https://github.com/dotnet/aspnetcore',
    usedIn: ['API', 'Client', '聊天室', '通知'],
    isCore: true,
    note: 'SignalR 服务端能力来自 ASP.NET Core，前端使用独立的官方客户端包。'
  }
];
