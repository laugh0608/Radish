import { welcomeEn } from './welcome.en';
import { welcomeOpenSourceEn } from './welcome-open-source.en';
import { welcomeOpenSourceZh } from './welcome-open-source.zh';
import { welcomeZh } from './welcome.zh';
import { enAccount } from './en/account';
import { enChat } from './en/chat';
import { enCommerce } from './en/commerce';
import { enCommunity } from './en/community';
import { enContentReward } from './en/contentReward';
import { enCore } from './en/core';
import { enDiscover } from './en/discover';
import { enDocs } from './en/docs';
import { enForumRevision } from './en/forumRevision';
import { enForumAnswer } from './en/forumAnswer';
import { enPostBookmark } from './en/postBookmark';
import { enShell } from './en/shell';
import { zhAccount } from './zh/account';
import { zhChat } from './zh/chat';
import { zhCommerce } from './zh/commerce';
import { zhCommunity } from './zh/community';
import { zhContentReward } from './zh/contentReward';
import { zhCore } from './zh/core';
import { zhDiscover } from './zh/discover';
import { zhDocs } from './zh/docs';
import { zhForumRevision } from './zh/forumRevision';
import { zhForumAnswer } from './zh/forumAnswer';
import { zhPostBookmark } from './zh/postBookmark';
import { zhShell } from './zh/shell';

export const clientTranslationDomains = {
  en: [enCore, enShell, welcomeEn, welcomeOpenSourceEn, enDiscover, enCommunity, enForumRevision, enForumAnswer, enPostBookmark, enContentReward, enChat, enAccount, enCommerce, enDocs],
  zh: [zhCore, zhShell, welcomeZh, welcomeOpenSourceZh, zhDiscover, zhCommunity, zhForumRevision, zhForumAnswer, zhPostBookmark, zhContentReward, zhChat, zhAccount, zhCommerce, zhDocs],
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
      ...enForumRevision,
      ...enForumAnswer,
      ...enPostBookmark,
      ...enContentReward,
      ...enChat,
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
      ...zhForumRevision,
      ...zhForumAnswer,
      ...zhPostBookmark,
      ...zhContentReward,
      ...zhChat,
      ...zhAccount,
      ...zhCommerce,
      ...zhDocs,
    },
  },
} as const;

export type ClientTranslationKey = keyof typeof resources.zh.translation;
