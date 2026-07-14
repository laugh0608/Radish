import { welcomeEn } from './welcome.en';
import { welcomeOpenSourceEn } from './welcome-open-source.en';
import { welcomeOpenSourceZh } from './welcome-open-source.zh';
import { welcomeZh } from './welcome.zh';
import { enAccount } from './en/account';
import { enCommerce } from './en/commerce';
import { enCommunity } from './en/community';
import { enCore } from './en/core';
import { enDiscover } from './en/discover';
import { enDocs } from './en/docs';
import { enShell } from './en/shell';
import { zhAccount } from './zh/account';
import { zhCommerce } from './zh/commerce';
import { zhCommunity } from './zh/community';
import { zhCore } from './zh/core';
import { zhDiscover } from './zh/discover';
import { zhDocs } from './zh/docs';
import { zhShell } from './zh/shell';

export const clientTranslationDomains = {
  en: [enCore, enShell, welcomeEn, welcomeOpenSourceEn, enDiscover, enCommunity, enAccount, enCommerce, enDocs],
  zh: [zhCore, zhShell, welcomeZh, welcomeOpenSourceZh, zhDiscover, zhCommunity, zhAccount, zhCommerce, zhDocs],
} as const;

export const resources = {
  en: {
    translation: {
      ...enCore,
      ...enShell,
      ...welcomeEn,
      ...welcomeOpenSourceEn,
      ...enDiscover,
      ...enCommunity,
      ...enAccount,
      ...enCommerce,
      ...enDocs,
    },
  },
  zh: {
    translation: {
      ...zhCore,
      ...zhShell,
      ...welcomeZh,
      ...welcomeOpenSourceZh,
      ...zhDiscover,
      ...zhCommunity,
      ...zhAccount,
      ...zhCommerce,
      ...zhDocs,
    },
  },
} as const;

export type ClientTranslationKey = keyof typeof resources.zh.translation;
