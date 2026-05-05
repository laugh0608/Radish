import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { getMyBrowseHistory, type UserBrowseHistoryItem } from '@/api/user';
import { getMyQuickReplies, type UserPostQuickReply } from '@/api/forum';
import { useWindowStore } from '@/stores/windowStore';
import { log } from '@/utils/logger';
import {
  openWorkspaceNavigationTarget,
  resolveBrowseHistoryWorkspaceTarget,
  resolveForumPostWorkspaceTarget,
  type WorkspaceNavigationTarget,
} from '@/utils/workspaceNavigation';
import styles from './DesktopResumePanel.module.css';

type ResumeItemKind = 'browse' | 'quick-reply';

interface ResumeItem {
  id: string;
  kind: ResumeItemKind;
  icon: string;
  title: string;
  description: string;
  meta: string;
  target: WorkspaceNavigationTarget | null;
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
  const { openApp } = useWindowStore();
  const [browseItems, setBrowseItems] = useState<UserBrowseHistoryItem[]>([]);
  const [quickReplyItems, setQuickReplyItems] = useState<UserPostQuickReply[]>([]);
  const [loading, setLoading] = useState(true);
  const [hasIssue, setHasIssue] = useState(false);

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

  const resumeItems = useMemo<ResumeItem[]>(() => {
    const browseResumeItems = browseItems
      .map((item) => buildBrowseResumeItem(item, t('desktop.resume.noSummary')))
      .filter((item) => item.target);
    const quickReplyResumeItems = quickReplyItems
      .map((item) => buildQuickReplyResumeItem(item, t('desktop.resume.quickReplyMeta')))
      .filter((item) => item.target);

    return [...browseResumeItems, ...quickReplyResumeItems].slice(0, 5);
  }, [browseItems, quickReplyItems, t]);

  const handleOpenItem = (item: ResumeItem) => {
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

      {loading ? (
        <div className={styles.stateText}>{t('desktop.resume.loading')}</div>
      ) : resumeItems.length === 0 ? (
        <div className={styles.stateText}>{t(hasIssue ? 'desktop.resume.loadFailed' : 'desktop.resume.empty')}</div>
      ) : (
        <div className={styles.list}>
          {resumeItems.map((item) => (
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
      )}
    </aside>
  );
};
