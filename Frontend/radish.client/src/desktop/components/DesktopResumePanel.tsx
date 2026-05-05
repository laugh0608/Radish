import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Icon } from '@radish/ui/icon';
import { getMyBrowseHistory, type UserBrowseHistoryItem } from '@/api/user';
import { getMyQuickReplies, type UserPostQuickReply } from '@/api/forum';
import { useWindowStore } from '@/stores/windowStore';
import { log } from '@/utils/logger';
import { getAppById } from '@/desktop/AppRegistry';
import {
  readRecentDesktopApps,
  RECENT_DESKTOP_APPS_CHANGED_EVENT,
  type RecentDesktopAppItem,
} from '@/utils/desktopRecentApps';
import {
  openWorkspaceNavigationTarget,
  resolveBrowseHistoryWorkspaceTarget,
  resolveForumPostWorkspaceTarget,
  type WorkspaceNavigationTarget,
} from '@/utils/workspaceNavigation';
import styles from './DesktopResumePanel.module.css';

type ResumeItemKind = 'app' | 'browse' | 'quick-reply';

interface ResumeItem {
  id: string;
  kind: ResumeItemKind;
  appId?: string;
  icon: string;
  title: string;
  description: string;
  meta: string;
  target: WorkspaceNavigationTarget | null;
}

interface ResumeSection {
  id: string;
  title: string;
  items: ResumeItem[];
}

const getBrowseIcon = (targetType: string): string => {
  switch (targetType) {
    case 'Post':
      return 'mdi:forum-outline';
    case 'Product':
      return 'mdi:shopping-outline';
    case 'Wiki':
      return 'mdi:file-document-outline';
    default:
      return 'mdi:history';
  }
};

const buildAppResumeItem = (item: RecentDesktopAppItem, t: TFunction): ResumeItem | null => {
  const app = getAppById(item.appId);
  if (!app) {
    return null;
  }

  return {
    id: `app-${item.appId}`,
    kind: 'app',
    appId: item.appId,
    icon: app.icon,
    title: t(app.nameKey || app.name),
    description: t(app.descriptionKey || app.description || 'desktop.resume.appDescriptionFallback'),
    meta: t('desktop.resume.appMeta'),
    target: null,
  };
};

const buildBrowseResumeItem = (item: UserBrowseHistoryItem, fallbackSummary: string): ResumeItem => ({
  id: `browse-${String(item.voId)}`,
  kind: 'browse',
  icon: getBrowseIcon(item.voTargetType),
  title: item.voTitle?.trim() || item.voTargetTypeDisplay,
  description: item.voSummary?.trim() || fallbackSummary,
  meta: item.voTargetTypeDisplay,
  target: resolveBrowseHistoryWorkspaceTarget(item),
});

const buildQuickReplyResumeItem = (item: UserPostQuickReply, meta: string): ResumeItem => ({
  id: `quick-reply-${String(item.voId)}`,
  kind: 'quick-reply',
  icon: 'mdi:comment-quote-outline',
  title: item.voPostTitle?.trim() || meta,
  description: item.voContent?.trim() || meta,
  meta,
  target: resolveForumPostWorkspaceTarget(item.voPostId),
});

export const DesktopResumePanel = () => {
  const { t } = useTranslation();
  const { openApp, openOrReuseApp } = useWindowStore();
  const [recentApps, setRecentApps] = useState<RecentDesktopAppItem[]>([]);
  const [browseItems, setBrowseItems] = useState<UserBrowseHistoryItem[]>([]);
  const [quickReplyItems, setQuickReplyItems] = useState<UserPostQuickReply[]>([]);
  const [loading, setLoading] = useState(true);
  const [hasIssue, setHasIssue] = useState(false);

  useEffect(() => {
    const refreshRecentApps = () => {
      setRecentApps(readRecentDesktopApps());
    };

    refreshRecentApps();
    window.addEventListener(RECENT_DESKTOP_APPS_CHANGED_EVENT, refreshRecentApps);

    return () => {
      window.removeEventListener(RECENT_DESKTOP_APPS_CHANGED_EVENT, refreshRecentApps);
    };
  }, []);

  useEffect(() => {
    let cancelled = false;

    const loadResumeItems = async () => {
      setLoading(true);
      setHasIssue(false);

      const [browseResult, quickReplyResult] = await Promise.allSettled([
        getMyBrowseHistory(1, 3),
        getMyQuickReplies(1, 2),
      ]);

      if (cancelled) {
        return;
      }

      if (browseResult.status === 'fulfilled') {
        setBrowseItems(browseResult.value.voItems || []);
      } else {
        setBrowseItems([]);
        setHasIssue(true);
        log.error('DesktopResumePanel', '加载桌面最近浏览失败：', browseResult.reason);
      }

      if (quickReplyResult.status === 'fulfilled') {
        setQuickReplyItems(quickReplyResult.value.voItems || []);
      } else {
        setQuickReplyItems([]);
        setHasIssue(true);
        log.error('DesktopResumePanel', '加载桌面轻回应失败：', quickReplyResult.reason);
      }

      setLoading(false);
    };

    void loadResumeItems();

    return () => {
      cancelled = true;
    };
  }, []);

  const resumeSections = useMemo<ResumeSection[]>(() => {
    const appResumeItems = recentApps
      .map((item) => buildAppResumeItem(item, t))
      .filter((item): item is ResumeItem => item !== null)
      .slice(0, 3);
    const browseResumeItems = browseItems
      .map((item) => buildBrowseResumeItem(item, t('desktop.resume.noSummary')))
      .filter((item) => item.target)
      .slice(0, 2);
    const quickReplyResumeItems = quickReplyItems
      .map((item) => buildQuickReplyResumeItem(item, t('desktop.resume.quickReplyMeta')))
      .filter((item) => item.target)
      .slice(0, 2);

    return [
      {
        id: 'apps',
        title: t('desktop.resume.sectionApps'),
        items: appResumeItems,
      },
      {
        id: 'browse',
        title: t('desktop.resume.sectionBrowse'),
        items: browseResumeItems,
      },
      {
        id: 'quick-replies',
        title: t('desktop.resume.sectionQuickReplies'),
        items: quickReplyResumeItems,
      },
    ].filter((section) => section.items.length > 0);
  }, [browseItems, quickReplyItems, recentApps, t]);

  const hasResumeContent = resumeSections.length > 0;

  const handleOpenItem = (item: ResumeItem) => {
    if (item.kind === 'app' && item.appId) {
      openOrReuseApp(item.appId);
      return;
    }

    openWorkspaceNavigationTarget(openApp, item.target);
  };

  return (
    <aside className={styles.panel} aria-labelledby="desktop-resume-title">
      <div className={styles.header}>
        <div>
          <div className={styles.kicker}>{t('desktop.resume.kicker')}</div>
          <h2 id="desktop-resume-title" className={styles.title}>
            {t('desktop.resume.title')}
          </h2>
        </div>
        <button
          type="button"
          className={styles.profileButton}
          onClick={() => openApp('profile')}
          title={t('desktop.resume.openProfileTitle')}
        >
          <Icon icon="mdi:account-clock-outline" size={18} />
        </button>
      </div>

      {!hasResumeContent && loading ? (
        <div className={styles.stateText}>{t('desktop.resume.loading')}</div>
      ) : !hasResumeContent ? (
        <div className={styles.stateText}>{t(hasIssue ? 'desktop.resume.loadFailed' : 'desktop.resume.empty')}</div>
      ) : (
        <div className={styles.sections}>
          {resumeSections.map((section) => (
            <section key={section.id} className={styles.section}>
              <div className={styles.sectionTitle}>{section.title}</div>
              <div className={styles.list}>
                {section.items.map((item) => (
                  <button
                    key={item.id}
                    type="button"
                    className={styles.item}
                    onClick={() => handleOpenItem(item)}
                  >
                    <span className={styles.itemIcon}>
                      <Icon icon={item.icon} size={18} />
                    </span>
                    <span className={styles.itemContent}>
                      <span className={styles.itemMeta}>{item.meta}</span>
                      <span className={styles.itemTitle}>{item.title}</span>
                      <span className={styles.itemDescription}>{item.description}</span>
                    </span>
                    <Icon icon="mdi:arrow-right" size={16} className={styles.itemArrow} />
                  </button>
                ))}
              </div>
            </section>
          ))}
          {loading ? <div className={styles.inlineState}>{t('desktop.resume.loading')}</div> : null}
          {!loading && hasIssue ? <div className={styles.inlineState}>{t('desktop.resume.partialLoadFailed')}</div> : null}
        </div>
      )}
    </aside>
  );
};
