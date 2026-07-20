import { enCore } from './en/core';
import { enApplications } from './en/applications';
import { enCoins } from './en/coins';
import { enDashboard } from './en/dashboard';
import { enDocuments } from './en/documents';
import { enExperience } from './en/experience';
import { enShell } from './en/shell';
import { enModeration } from './en/moderation';
import { enOrders } from './en/orders';
import { enProfile } from './en/profile';
import { enProducts } from './en/products';
import { enRoles } from './en/roles';
import { enStickers } from './en/stickers';
import { enSettings } from './en/settings';
import { enSystemTools } from './en/systemTools';
import { enTaxonomy } from './en/taxonomy';
import { enUsers } from './en/users';
import { zhCore } from './zh/core';
import { zhApplications } from './zh/applications';
import { zhCoins } from './zh/coins';
import { zhDashboard } from './zh/dashboard';
import { zhDocuments } from './zh/documents';
import { zhExperience } from './zh/experience';
import { zhShell } from './zh/shell';
import { zhModeration } from './zh/moderation';
import { zhOrders } from './zh/orders';
import { zhProfile } from './zh/profile';
import { zhProducts } from './zh/products';
import { zhRoles } from './zh/roles';
import { zhStickers } from './zh/stickers';
import { zhSettings } from './zh/settings';
import { zhSystemTools } from './zh/systemTools';
import { zhTaxonomy } from './zh/taxonomy';
import { zhUsers } from './zh/users';

export const consoleTranslationDomains = {
  en: [enCore, enShell, enDashboard, enApplications, enProfile, enSystemTools, enUsers, enModeration, enOrders, enProducts, enSettings, enDocuments, enRoles, enTaxonomy, enStickers, enCoins, enExperience],
  zh: [zhCore, zhShell, zhDashboard, zhApplications, zhProfile, zhSystemTools, zhUsers, zhModeration, zhOrders, zhProducts, zhSettings, zhDocuments, zhRoles, zhTaxonomy, zhStickers, zhCoins, zhExperience],
} as const;

export const resources = {
  en: { translation: { ...enCore, ...enShell, ...enDashboard, ...enApplications, ...enProfile, ...enSystemTools, ...enUsers, ...enModeration, ...enOrders, ...enProducts, ...enSettings, ...enDocuments, ...enRoles, ...enTaxonomy, ...enStickers, ...enCoins, ...enExperience } },
  zh: { translation: { ...zhCore, ...zhShell, ...zhDashboard, ...zhApplications, ...zhProfile, ...zhSystemTools, ...zhUsers, ...zhModeration, ...zhOrders, ...zhProducts, ...zhSettings, ...zhDocuments, ...zhRoles, ...zhTaxonomy, ...zhStickers, ...zhCoins, ...zhExperience } },
} as const;
