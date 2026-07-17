import { enCore } from './en/core';
import { enCoins } from './en/coins';
import { enDashboard } from './en/dashboard';
import { enDocuments } from './en/documents';
import { enExperience } from './en/experience';
import { enShell } from './en/shell';
import { enModeration } from './en/moderation';
import { enOrders } from './en/orders';
import { enProducts } from './en/products';
import { enRoles } from './en/roles';
import { enStickers } from './en/stickers';
import { enSettings } from './en/settings';
import { enTaxonomy } from './en/taxonomy';
import { enUsers } from './en/users';
import { zhCore } from './zh/core';
import { zhCoins } from './zh/coins';
import { zhDashboard } from './zh/dashboard';
import { zhDocuments } from './zh/documents';
import { zhExperience } from './zh/experience';
import { zhShell } from './zh/shell';
import { zhModeration } from './zh/moderation';
import { zhOrders } from './zh/orders';
import { zhProducts } from './zh/products';
import { zhRoles } from './zh/roles';
import { zhStickers } from './zh/stickers';
import { zhSettings } from './zh/settings';
import { zhTaxonomy } from './zh/taxonomy';
import { zhUsers } from './zh/users';

export const consoleTranslationDomains = {
  en: [enCore, enShell, enDashboard, enUsers, enModeration, enOrders, enProducts, enSettings, enDocuments, enRoles, enTaxonomy, enStickers, enCoins, enExperience],
  zh: [zhCore, zhShell, zhDashboard, zhUsers, zhModeration, zhOrders, zhProducts, zhSettings, zhDocuments, zhRoles, zhTaxonomy, zhStickers, zhCoins, zhExperience],
} as const;

export const resources = {
  en: { translation: { ...enCore, ...enShell, ...enDashboard, ...enUsers, ...enModeration, ...enOrders, ...enProducts, ...enSettings, ...enDocuments, ...enRoles, ...enTaxonomy, ...enStickers, ...enCoins, ...enExperience } },
  zh: { translation: { ...zhCore, ...zhShell, ...zhDashboard, ...zhUsers, ...zhModeration, ...zhOrders, ...zhProducts, ...zhSettings, ...zhDocuments, ...zhRoles, ...zhTaxonomy, ...zhStickers, ...zhCoins, ...zhExperience } },
} as const;
