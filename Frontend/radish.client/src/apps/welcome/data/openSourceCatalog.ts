export type OpenSourceGroupId = 'backend' | 'data' | 'frontend' | 'realtime';

export interface OpenSourceGroup {
  id: OpenSourceGroupId;
  labelKey: string;
  descriptionKey: string;
  icon: string;
}

export interface OpenSourceProject {
  id: string;
  name: string;
  groupId: OpenSourceGroupId;
  purposeKey: string;
  scopeKey: string;
  license: string;
  licenseKey?: string;
  repository: string;
  website?: string;
  noteKey?: string;
  licenseNoteKey?: string;
  usedInKeys: string[];
  isCore: boolean;
}

export const openSourceOverview = {
  titleKey: 'welcome.openSource.overview.title',
  summaryKey: 'welcome.openSource.overview.summary',
  scopeKey: 'welcome.openSource.overview.scope',
  sourceKey: 'welcome.openSource.overview.source',
  maintenanceKey: 'welcome.openSource.overview.maintenance',
  nextStepKey: 'welcome.openSource.overview.nextStep',
  boundaryNoteKeys: [
    'welcome.openSource.boundary.note1',
    'welcome.openSource.boundary.note2',
    'welcome.openSource.boundary.note3'
  ]
};

export const openSourceGroups: OpenSourceGroup[] = [
  {
    id: 'backend',
    labelKey: 'welcome.openSource.group.backend.label',
    descriptionKey: 'welcome.openSource.group.backend.description',
    icon: 'mdi:server-outline'
  },
  {
    id: 'data',
    labelKey: 'welcome.openSource.group.data.label',
    descriptionKey: 'welcome.openSource.group.data.description',
    icon: 'mdi:database-outline'
  },
  {
    id: 'frontend',
    labelKey: 'welcome.openSource.group.frontend.label',
    descriptionKey: 'welcome.openSource.group.frontend.description',
    icon: 'mdi:monitor-dashboard'
  },
  {
    id: 'realtime',
    labelKey: 'welcome.openSource.group.realtime.label',
    descriptionKey: 'welcome.openSource.group.realtime.description',
    icon: 'mdi:lightning-bolt-outline'
  }
];

export const openSourceProjects: OpenSourceProject[] = [
  {
    id: 'aspnet-core',
    name: 'ASP.NET Core',
    groupId: 'backend',
    purposeKey: 'welcome.openSource.projects.aspnetCore.purpose',
    scopeKey: 'welcome.openSource.projects.aspnetCore.scope',
    license: 'MIT',
    repository: 'https://github.com/dotnet/aspnetcore',
    usedInKeys: ['welcome.openSource.usedIn.api', 'welcome.openSource.usedIn.gateway', 'welcome.openSource.usedIn.auth'],
    isCore: true
  },
  {
    id: 'openiddict',
    name: 'OpenIddict',
    groupId: 'backend',
    purposeKey: 'welcome.openSource.projects.openiddict.purpose',
    scopeKey: 'welcome.openSource.projects.openiddict.scope',
    license: 'Apache-2.0',
    repository: 'https://github.com/openiddict/openiddict-core',
    usedInKeys: [
      'welcome.openSource.usedIn.auth',
      'welcome.openSource.usedIn.clientOidc',
      'welcome.openSource.usedIn.consoleOidc'
    ],
    isCore: true
  },
  {
    id: 'yarp',
    name: 'YARP',
    groupId: 'backend',
    purposeKey: 'welcome.openSource.projects.yarp.purpose',
    scopeKey: 'welcome.openSource.projects.yarp.scope',
    license: 'MIT',
    repository: 'https://github.com/dotnet/yarp',
    usedInKeys: ['welcome.openSource.usedIn.gateway'],
    isCore: true
  },
  {
    id: 'hangfire',
    name: 'Hangfire',
    groupId: 'backend',
    purposeKey: 'welcome.openSource.projects.hangfire.purpose',
    scopeKey: 'welcome.openSource.projects.hangfire.scope',
    license: 'LGPL-3.0-or-later',
    repository: 'https://github.com/HangfireIO/Hangfire',
    usedInKeys: [
      'welcome.openSource.usedIn.api',
      'welcome.openSource.usedIn.scheduledTask',
      'welcome.openSource.usedIn.dashboard'
    ],
    isCore: true,
    licenseNoteKey: 'welcome.openSource.projects.hangfire.licenseNote'
  },
  {
    id: 'serilog',
    name: 'Serilog',
    groupId: 'backend',
    purposeKey: 'welcome.openSource.projects.serilog.purpose',
    scopeKey: 'welcome.openSource.projects.serilog.scope',
    license: 'Apache-2.0',
    repository: 'https://github.com/serilog/serilog',
    usedInKeys: ['welcome.openSource.usedIn.api', 'welcome.openSource.usedIn.gateway', 'welcome.openSource.usedIn.auth'],
    isCore: true
  },
  {
    id: 'autofac',
    name: 'Autofac',
    groupId: 'backend',
    purposeKey: 'welcome.openSource.projects.autofac.purpose',
    scopeKey: 'welcome.openSource.projects.autofac.scope',
    license: 'MIT',
    repository: 'https://github.com/autofac/Autofac',
    usedInKeys: ['welcome.openSource.usedIn.api', 'welcome.openSource.usedIn.gateway', 'welcome.openSource.usedIn.auth'],
    isCore: false
  },
  {
    id: 'automapper',
    name: 'AutoMapper',
    groupId: 'backend',
    purposeKey: 'welcome.openSource.projects.automapper.purpose',
    scopeKey: 'welcome.openSource.projects.automapper.scope',
    license: '',
    licenseKey: 'welcome.openSource.license.commercialRestricted',
    repository: 'https://github.com/LuckyPennySoftware/AutoMapper',
    website: 'https://automapper.io/',
    usedInKeys: ['welcome.openSource.usedIn.api', 'welcome.openSource.usedIn.auth', 'welcome.openSource.usedIn.extension'],
    isCore: false,
    noteKey: 'welcome.openSource.projects.automapper.note',
    licenseNoteKey: 'welcome.openSource.projects.automapper.licenseNote'
  },
  {
    id: 'sqlsugar',
    name: 'SqlSugar',
    groupId: 'data',
    purposeKey: 'welcome.openSource.projects.sqlsugar.purpose',
    scopeKey: 'welcome.openSource.projects.sqlsugar.scope',
    license: 'MIT',
    repository: 'https://github.com/DotNetNext/SqlSugar',
    usedInKeys: ['welcome.openSource.usedIn.api', 'welcome.openSource.usedIn.auth', 'welcome.openSource.usedIn.repository'],
    isCore: true
  },
  {
    id: 'entity-framework-core',
    name: 'Entity Framework Core',
    groupId: 'data',
    purposeKey: 'welcome.openSource.projects.efcore.purpose',
    scopeKey: 'welcome.openSource.projects.efcore.scope',
    license: 'MIT',
    repository: 'https://github.com/dotnet/efcore',
    usedInKeys: ['welcome.openSource.usedIn.auth', 'welcome.openSource.usedIn.openiddictStorage'],
    isCore: false
  },
  {
    id: 'stackexchange-redis',
    name: 'StackExchange.Redis',
    groupId: 'data',
    purposeKey: 'welcome.openSource.projects.redis.purpose',
    scopeKey: 'welcome.openSource.projects.redis.scope',
    license: 'MIT',
    repository: 'https://github.com/StackExchange/StackExchange.Redis',
    usedInKeys: [
      'welcome.openSource.usedIn.api',
      'welcome.openSource.usedIn.gateway',
      'welcome.openSource.usedIn.auth',
      'welcome.openSource.usedIn.cache'
    ],
    isCore: false
  },
  {
    id: 'postgresql',
    name: 'PostgreSQL',
    groupId: 'data',
    purposeKey: 'welcome.openSource.projects.postgresql.purpose',
    scopeKey: 'welcome.openSource.projects.postgresql.scope',
    license: 'PostgreSQL License',
    repository: 'https://github.com/postgres/postgres',
    website: 'https://www.postgresql.org/',
    usedInKeys: ['welcome.openSource.usedIn.productionDatabase', 'welcome.openSource.usedIn.businessData'],
    isCore: true
  },
  {
    id: 'sqlite',
    name: 'SQLite',
    groupId: 'data',
    purposeKey: 'welcome.openSource.projects.sqlite.purpose',
    scopeKey: 'welcome.openSource.projects.sqlite.scope',
    license: 'Public Domain',
    repository: 'https://github.com/sqlite/sqlite',
    website: 'https://www.sqlite.org/',
    usedInKeys: ['welcome.openSource.usedIn.localDevelopment', 'welcome.openSource.usedIn.defaultDatabase'],
    isCore: false,
    licenseNoteKey: 'welcome.openSource.projects.sqlite.licenseNote'
  },
  {
    id: 'react',
    name: 'React',
    groupId: 'frontend',
    purposeKey: 'welcome.openSource.projects.react.purpose',
    scopeKey: 'welcome.openSource.projects.react.scope',
    license: 'MIT',
    repository: 'https://github.com/facebook/react',
    usedInKeys: ['welcome.openSource.usedIn.client', 'welcome.openSource.usedIn.console', 'welcome.openSource.usedIn.ui'],
    isCore: true
  },
  {
    id: 'typescript',
    name: 'TypeScript',
    groupId: 'frontend',
    purposeKey: 'welcome.openSource.projects.typescript.purpose',
    scopeKey: 'welcome.openSource.projects.typescript.scope',
    license: 'Apache-2.0',
    repository: 'https://github.com/microsoft/TypeScript',
    usedInKeys: ['welcome.openSource.usedIn.client', 'welcome.openSource.usedIn.console', 'welcome.openSource.usedIn.ui'],
    isCore: true
  },
  {
    id: 'vite-rolldown',
    name: 'Vite (Rolldown)',
    groupId: 'frontend',
    purposeKey: 'welcome.openSource.projects.viteRolldown.purpose',
    scopeKey: 'welcome.openSource.projects.viteRolldown.scope',
    license: 'MIT',
    repository: 'https://github.com/vitejs/vite',
    usedInKeys: ['welcome.openSource.usedIn.client', 'welcome.openSource.usedIn.console'],
    isCore: true,
    noteKey: 'welcome.openSource.projects.viteRolldown.note'
  },
  {
    id: 'zustand',
    name: 'Zustand',
    groupId: 'frontend',
    purposeKey: 'welcome.openSource.projects.zustand.purpose',
    scopeKey: 'welcome.openSource.projects.zustand.scope',
    license: 'MIT',
    repository: 'https://github.com/pmndrs/zustand',
    usedInKeys: ['welcome.openSource.usedIn.client', 'welcome.openSource.usedIn.stateManagement'],
    isCore: true
  },
  {
    id: 'react-router-dom',
    name: 'React Router DOM',
    groupId: 'frontend',
    purposeKey: 'welcome.openSource.projects.reactRouter.purpose',
    scopeKey: 'welcome.openSource.projects.reactRouter.scope',
    license: 'MIT',
    repository: 'https://github.com/remix-run/react-router',
    website: 'https://reactrouter.com/',
    usedInKeys: ['welcome.openSource.usedIn.console'],
    isCore: true
  },
  {
    id: 'signalr',
    name: 'SignalR',
    groupId: 'realtime',
    purposeKey: 'welcome.openSource.projects.signalr.purpose',
    scopeKey: 'welcome.openSource.projects.signalr.scope',
    license: 'MIT',
    repository: 'https://github.com/dotnet/aspnetcore',
    usedInKeys: [
      'welcome.openSource.usedIn.api',
      'welcome.openSource.usedIn.client',
      'welcome.openSource.usedIn.chat',
      'welcome.openSource.usedIn.notification'
    ],
    isCore: true,
    noteKey: 'welcome.openSource.projects.signalr.note'
  }
];
