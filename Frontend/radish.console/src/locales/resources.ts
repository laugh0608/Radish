import { enCore } from './en/core';
import { enDashboard } from './en/dashboard';
import { enShell } from './en/shell';
import { zhCore } from './zh/core';
import { zhDashboard } from './zh/dashboard';
import { zhShell } from './zh/shell';

export const consoleTranslationDomains = {
  en: [enCore, enShell, enDashboard],
  zh: [zhCore, zhShell, zhDashboard],
} as const;

export const resources = {
  en: { translation: { ...enCore, ...enShell, ...enDashboard } },
  zh: { translation: { ...zhCore, ...zhShell, ...zhDashboard } },
} as const;
