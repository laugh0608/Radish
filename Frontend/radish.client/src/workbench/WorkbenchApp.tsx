import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import type { WebShellNavItem } from '@/components/web-shell';
import { PublicShellHeader } from '@/public/components/PublicShellHeader';
import styles from './WorkbenchApp.module.css';

type WorkbenchAccess = 'public' | 'private' | 'admin' | 'legacy';

interface WorkbenchLink {
  labelKey: string;
  href: string;
}

interface WorkbenchItem {
  titleKey: string;
  descriptionKey: string;
  icon: string;
  access: WorkbenchAccess;
  href: string;
  links: WorkbenchLink[];
}

interface WorkbenchGroup {
  titleKey: string;
  descriptionKey: string;
  items: WorkbenchItem[];
}

const workbenchGroups: WorkbenchGroup[] = [
  {
    titleKey: 'workbench.group.public.title',
    descriptionKey: 'workbench.group.public.description',
    items: [
      {
        titleKey: 'workbench.item.discover.title',
        descriptionKey: 'workbench.item.discover.description',
        icon: 'mdi:compass-outline',
        access: 'public',
        href: '/discover',
        links: [
          { labelKey: 'workbench.link.open', href: '/discover' },
        ],
      },
      {
        titleKey: 'workbench.item.forum.title',
        descriptionKey: 'workbench.item.forum.description',
        icon: 'mdi:forum-outline',
        access: 'public',
        href: '/forum',
        links: [
          { labelKey: 'workbench.link.read', href: '/forum' },
          { labelKey: 'workbench.link.compose', href: '/forum/compose' },
        ],
      },
      {
        titleKey: 'workbench.item.docs.title',
        descriptionKey: 'workbench.item.docs.description',
        icon: 'mdi:file-document-outline',
        access: 'public',
        href: '/docs',
        links: [
          { labelKey: 'workbench.link.read', href: '/docs' },
          { labelKey: 'workbench.link.mine', href: '/docs/mine' },
          { labelKey: 'workbench.link.compose', href: '/docs/compose' },
        ],
      },
      {
        titleKey: 'workbench.item.publicShop.title',
        descriptionKey: 'workbench.item.publicShop.description',
        icon: 'mdi:storefront-outline',
        access: 'public',
        href: '/shop',
        links: [
          { labelKey: 'workbench.link.browse', href: '/shop' },
          { labelKey: 'workbench.link.leaderboard', href: '/leaderboard' },
        ],
      },
      {
        titleKey: 'workbench.item.leaderboard.title',
        descriptionKey: 'workbench.item.leaderboard.description',
        icon: 'mdi:trophy-outline',
        access: 'public',
        href: '/leaderboard',
        links: [
          { labelKey: 'workbench.link.open', href: '/leaderboard' },
        ],
      },
    ],
  },
  {
    titleKey: 'workbench.group.private.title',
    descriptionKey: 'workbench.group.private.description',
    items: [
      {
        titleKey: 'workbench.item.circle.title',
        descriptionKey: 'workbench.item.circle.description',
        icon: 'mdi:account-group-outline',
        access: 'private',
        href: '/circle',
        links: [
          { labelKey: 'workbench.link.open', href: '/circle' },
        ],
      },
      {
        titleKey: 'workbench.item.me.title',
        descriptionKey: 'workbench.item.me.description',
        icon: 'mdi:account-circle-outline',
        access: 'private',
        href: '/me',
        links: [
          { labelKey: 'workbench.link.overview', href: '/me' },
          { labelKey: 'workbench.link.content', href: '/me/content' },
          { labelKey: 'workbench.link.history', href: '/me/history' },
          { labelKey: 'workbench.link.assets', href: '/me/assets' },
          { labelKey: 'workbench.link.attachments', href: '/me/attachments' },
          { labelKey: 'workbench.link.experience', href: '/me/experience' },
        ],
      },
      {
        titleKey: 'workbench.item.shop.title',
        descriptionKey: 'workbench.item.shop.description',
        icon: 'mdi:shopping-outline',
        access: 'private',
        href: '/shop',
        links: [
          { labelKey: 'workbench.link.browse', href: '/shop' },
          { labelKey: 'workbench.link.orders', href: '/shop/orders' },
          { labelKey: 'workbench.link.inventory', href: '/shop/inventory' },
        ],
      },
      {
        titleKey: 'workbench.item.messages.title',
        descriptionKey: 'workbench.item.messages.description',
        icon: 'mdi:message-text-outline',
        access: 'private',
        href: '/messages',
        links: [
          { labelKey: 'workbench.link.messages', href: '/messages' },
          { labelKey: 'workbench.link.notifications', href: '/notifications' },
        ],
      },
      {
        titleKey: 'workbench.item.pet.title',
        descriptionKey: 'workbench.item.pet.description',
        icon: 'mdi:sprout-outline',
        access: 'private',
        href: '/pet',
        links: [
          { labelKey: 'workbench.link.open', href: '/pet' },
        ],
      },
    ],
  },
  {
    titleKey: 'workbench.group.governance.title',
    descriptionKey: 'workbench.group.governance.description',
    items: [
      {
        titleKey: 'workbench.item.console.title',
        descriptionKey: 'workbench.item.console.description',
        icon: 'mdi:shield-crown-outline',
        access: 'admin',
        href: '/console/',
        links: [
          { labelKey: 'workbench.link.open', href: '/console/' },
        ],
      },
      {
        titleKey: 'workbench.item.desktop.title',
        descriptionKey: 'workbench.item.desktop.description',
        icon: 'mdi:view-dashboard-outline',
        access: 'legacy',
        href: '/desktop',
        links: [
          { labelKey: 'workbench.link.openDesktop', href: '/desktop' },
        ],
      },
    ],
  },
];

const accessClassNameMap: Record<WorkbenchAccess, string> = {
  public: styles.accessPublic,
  private: styles.accessPrivate,
  admin: styles.accessAdmin,
  legacy: styles.accessLegacy,
};

const publicWorkbenchMobileNavItems: WebShellNavItem[] = [
  { key: 'discover', label: '发现', href: '/discover', icon: 'mdi:compass-outline' },
  { key: 'forum', label: '论坛', href: '/forum', icon: 'mdi:forum-outline' },
  { key: 'docs', label: '文档', href: '/docs', icon: 'mdi:file-document-outline' },
  { key: 'workbench', label: '工作台', href: '/workbench', icon: 'mdi:view-dashboard-outline' },
  { key: 'me', label: '我的', href: '/me', icon: 'mdi:account-circle-outline' },
];

export const WorkbenchApp = () => {
  const { t } = useTranslation();

  useEffect(() => {
    document.title = `${t('workbench.title')} · Radish`;
  }, [t]);

  return (
    <div className={styles.page}>
      <PublicShellHeader
        variant="private"
        activeKey="workbench"
        brandMark="台"
        brandName={t('workbench.title')}
        brandSubline={t('workbench.shellSubline')}
        discoverLabel={t('public.shell.discoverAction')}
        circleLabel={t('public.shell.circleAction')}
        desktopLabel={t('public.shell.desktopAction')}
        mobileNavItems={publicWorkbenchMobileNavItems}
        onBrandClick={() => {
          window.location.href = '/workbench';
        }}
        onNavigateToDiscover={() => {
          window.location.href = '/discover';
        }}
      />

      <main className={styles.main}>
        <section className={styles.summary}>
          <div className={styles.summaryText}>
            <p className={styles.kicker}>{t('workbench.kicker')}</p>
            <h1>{t('workbench.heading')}</h1>
            <p>{t('workbench.description')}</p>
          </div>
          <div className={styles.summaryActions} aria-label={t('workbench.quickActionsLabel')}>
            <a className={styles.primaryAction} href="/me">
              <Icon icon="mdi:account-circle-outline" size={18} />
              <span>{t('workbench.quick.me')}</span>
            </a>
            <a className={styles.secondaryAction} href="/messages">
              <Icon icon="mdi:message-text-outline" size={18} />
              <span>{t('workbench.quick.messages')}</span>
            </a>
          </div>
        </section>

        <div className={styles.groups}>
          {workbenchGroups.map((group) => (
            <section className={styles.group} key={group.titleKey}>
              <div className={styles.groupHeader}>
                <h2>{t(group.titleKey)}</h2>
                <p>{t(group.descriptionKey)}</p>
              </div>
              <div className={styles.grid}>
                {group.items.map((item) => (
                  <article className={styles.item} key={item.titleKey}>
                    <a className={styles.itemMainLink} href={item.href} aria-label={t(item.titleKey)}>
                      <span className={styles.itemIcon}>
                        <Icon icon={item.icon} size={22} />
                      </span>
                      <span className={styles.itemText}>
                        <span className={styles.itemTitleRow}>
                          <span className={styles.itemTitle}>{t(item.titleKey)}</span>
                          <span className={`${styles.accessBadge} ${accessClassNameMap[item.access]}`}>
                            {t(`workbench.access.${item.access}`)}
                          </span>
                        </span>
                        <span className={styles.itemDescription}>{t(item.descriptionKey)}</span>
                      </span>
                    </a>
                    <div className={styles.links} aria-label={t('workbench.linksLabel', { name: t(item.titleKey) })}>
                      {item.links.map((link) => (
                        <a className={styles.linkChip} href={link.href} key={`${item.titleKey}:${link.href}`}>
                          {t(link.labelKey)}
                        </a>
                      ))}
                    </div>
                  </article>
                ))}
              </div>
            </section>
          ))}
        </div>
      </main>
    </div>
  );
};
