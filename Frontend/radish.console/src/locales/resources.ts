import { enCore } from './en/core';
import { enDashboard } from './en/dashboard';
import { enShell } from './en/shell';
import { enModeration } from './en/moderation';
import { enOrders } from './en/orders';
import { enSettings } from './en/settings';
import { enUsers } from './en/users';
import { zhCore } from './zh/core';
import { zhDashboard } from './zh/dashboard';
import { zhShell } from './zh/shell';
import { zhModeration } from './zh/moderation';
import { zhOrders } from './zh/orders';
import { zhSettings } from './zh/settings';
import { zhUsers } from './zh/users';

export const consoleTranslationDomains = {
  en: [enCore, enShell, enDashboard, enUsers, enModeration, enOrders, enSettings],
  zh: [zhCore, zhShell, zhDashboard, zhUsers, zhModeration, zhOrders, zhSettings],
} as const;

export const resources = {
  en: { translation: { ...enCore, ...enShell, ...enDashboard, ...enUsers, ...enModeration, ...enOrders, ...enSettings } },
  zh: { translation: { ...zhCore, ...zhShell, ...zhDashboard, ...zhUsers, ...zhModeration, ...zhOrders, ...zhSettings } },
} as const;
