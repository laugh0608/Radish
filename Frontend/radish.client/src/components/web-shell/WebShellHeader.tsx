import { useEffect, type MouseEvent, type ReactNode } from 'react';
import { Icon } from '@radish/ui/icon';
import styles from './WebShellHeader.module.css';
import { useTranslation } from 'react-i18next';

export type WebShellVariant = 'public' | 'private';

export interface WebShellNavItem {
  key: string;
  label: string;
  href: string;
  icon: string;
  avatarUrl?: string | null;
  avatarText?: string;
  onClick?: () => void;
}

interface WebShellHeaderProps {
  variant: WebShellVariant;
  brandMark: string;
  brandName: string;
  brandSubline: string;
  onBrandClick: () => void;
  activeKey?: string;
  navItems?: WebShellNavItem[];
  actionItems?: WebShellNavItem[];
  actionSlot?: ReactNode;
  mobileNavItems?: WebShellNavItem[];
  hideMobileNav?: boolean;
}

function shouldHandleShellLinkClick(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function navigateToShellPath(href: string): boolean {
  if (typeof window === 'undefined') {
    return false;
  }

  const nextUrl = new URL(href, window.location.origin);
  if (nextUrl.origin !== window.location.origin) {
    return false;
  }

  const nextPath = `${nextUrl.pathname}${nextUrl.search}${nextUrl.hash}`;
  const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
  if (nextPath === currentPath) {
    return true;
  }

  window.history.pushState({}, '', nextPath);
  window.dispatchEvent(new PopStateEvent('popstate', { state: window.history.state }));
  return true;
}

function getCurrentPathname(): string {
  return typeof window === 'undefined' ? '' : window.location.pathname;
}

function normalizeActiveKey(activeKey: string): string {
  if (activeKey === 'assets' || activeKey === 'circle' || activeKey === 'pet') {
    return 'me';
  }

  if (activeKey === 'messages') {
    return 'chat';
  }

  if (activeKey === 'workbench' || activeKey === 'author' || activeKey === 'leaderboard' || activeKey === 'legal') {
    return 'more';
  }

  return activeKey;
}

function resolveActiveKey(variant: WebShellVariant): string {
  const pathname = getCurrentPathname();
  const search = typeof window === 'undefined' ? '' : window.location.search;

  if (pathname === '/workbench' || pathname.startsWith('/workbench/')) {
    return 'more';
  }

  if (pathname === '/desktop' || pathname.startsWith('/desktop') || pathname === '/console' || pathname.startsWith('/console/')) {
    return 'more';
  }

  if (pathname === '/forum/compose' || search.includes('intent=answer') || search.includes('intent=edit') || search.includes('intent=history')) {
    return 'more';
  }

  if (pathname === '/discover' || pathname.startsWith('/discover/')) {
    return 'discover';
  }

  if (pathname === '/forum' || pathname.startsWith('/forum/')) {
    return 'forum';
  }

  if (
    pathname === '/docs/mine'
    || pathname === '/docs/compose'
    || pathname.startsWith('/docs/edit/')
    || pathname.startsWith('/docs/revisions/')
  ) {
    return 'more';
  }

  if (pathname === '/docs' || pathname.startsWith('/docs/') || pathname.startsWith('/__documents__/')) {
    return 'more';
  }

  if (pathname === '/leaderboard' || pathname.startsWith('/leaderboard/')) {
    return 'more';
  }

  if (pathname === '/legal' || pathname.startsWith('/legal/')) {
    return 'more';
  }

  if (
    pathname === '/me/assets'
    || pathname.startsWith('/me/assets/')
    || pathname === '/shop/orders'
    || pathname === '/shop/inventory'
    || pathname.startsWith('/shop/order/')
  ) {
    return 'more';
  }

  if (pathname === '/shop' || pathname.startsWith('/shop/')) {
    return 'more';
  }

  if (pathname === '/messages' || pathname.startsWith('/messages/')) {
    return 'chat';
  }

  if (pathname === '/notifications') {
    return 'notifications';
  }

  if (pathname === '/me' || pathname.startsWith('/me/') || pathname === '/circle' || pathname === '/pet') {
    return 'me';
  }

  return variant === 'private' ? 'more' : 'discover';
}

function getDefaultNavItems(t: (key: string) => string): WebShellNavItem[] {
  return [
    { key: 'discover', label: t('public.shell.nav.discover'), href: '/discover', icon: 'mdi:compass-outline' },
    { key: 'forum', label: t('public.shell.nav.forum'), href: '/forum', icon: 'mdi:forum-outline' },
    { key: 'chat', label: t('public.shell.nav.chat'), href: '/messages', icon: 'mdi:message-text-outline' },
    { key: 'more', label: t('public.shell.nav.more'), href: '/workbench', icon: 'mdi:dots-grid' },
  ];
}

function getDefaultMobileNavItems(t: (key: string) => string): WebShellNavItem[] {
  return [
    ...getDefaultNavItems(t),
    { key: 'me', label: t('public.shell.nav.me'), href: '/me', icon: 'mdi:account-circle-outline' },
  ];
}

interface WebShellLinkProps {
  item: WebShellNavItem;
  className: string;
  activeClassName: string;
  isActive: boolean;
}

function WebShellLink({ item, className, activeClassName, isActive }: WebShellLinkProps) {
  return (
    <a
      className={`${className} ${isActive ? activeClassName : ''}`}
      href={item.href}
      aria-current={isActive ? 'page' : undefined}
      title={item.label}
      onClick={(event) => {
        if (!shouldHandleShellLinkClick(event)) {
          return;
        }

        if (item.onClick) {
          event.preventDefault();
          item.onClick();
          return;
        }

        if (navigateToShellPath(item.href)) {
          event.preventDefault();
        }
      }}
    >
      {item.avatarUrl ? (
        <img src={item.avatarUrl} alt={item.label} className={styles.linkAvatar} loading="lazy" />
      ) : item.avatarText ? (
        <span className={styles.linkAvatarFallback} aria-hidden="true">{item.avatarText}</span>
      ) : (
        <Icon icon={item.icon} size={18} />
      )}
      <span>{item.label}</span>
    </a>
  );
}

export function WebShellHeader({
  variant,
  brandMark,
  brandName,
  brandSubline,
  onBrandClick,
  activeKey,
  navItems,
  actionItems,
  actionSlot,
  mobileNavItems,
  hideMobileNav = false,
}: WebShellHeaderProps) {
  const { t } = useTranslation();
  const resolvedActiveKey = normalizeActiveKey(activeKey ?? resolveActiveKey(variant));
  const resolvedNavItems = navItems ?? getDefaultNavItems(t);
  const resolvedMobileNavItems = mobileNavItems ?? getDefaultMobileNavItems(t);
  const resolvedActionItems = actionItems ?? [];
  const headerClassName = `${styles.header} ${variant === 'private' ? styles.privateHeader : styles.publicHeader}`;
  const actionRailClassName = styles.actionRail;

  useEffect(() => {
    if (hideMobileNav) {
      document.body.classList.remove('radishWebShellWithMobileNav');
      return;
    }

    document.body.classList.add('radishWebShellWithMobileNav');
    return () => {
      document.body.classList.remove('radishWebShellWithMobileNav');
    };
  }, [hideMobileNav]);

  return (
    <>
      <header className={headerClassName}>
        <div className={styles.inner}>
          <button type="button" className={styles.brand} onClick={onBrandClick}>
            <span className={styles.brandMark}>{brandMark}</span>
            <span className={styles.brandCopy}>
              <span className={styles.brandName}>{brandName}</span>
              <span className={styles.brandSubline}>{brandSubline}</span>
            </span>
          </button>

          <nav className={styles.navRail} aria-label={t('public.shell.navLabel')}>
            {resolvedNavItems.map((item) => (
              <WebShellLink
                key={item.key}
                item={item}
                className={styles.navItem}
                activeClassName={styles.navItemActive}
                isActive={item.key === resolvedActiveKey}
              />
            ))}
          </nav>

          <div className={actionRailClassName} aria-label="页面动作">
            {actionSlot}
            {resolvedActionItems.map((item) => (
              <WebShellLink
                key={item.key}
                item={item}
                className={styles.actionItem}
                activeClassName={styles.actionItemActive}
                isActive={item.key === resolvedActiveKey}
              />
            ))}
          </div>
        </div>
      </header>

      {!hideMobileNav && (
        <nav className={styles.mobileTabBar} aria-label={t('public.shell.mobileNavLabel')} data-web-mobile-nav="true">
          {resolvedMobileNavItems.map((item) => (
            <WebShellLink
              key={item.key}
              item={item}
              className={styles.mobileTab}
              activeClassName={styles.mobileTabActive}
              isActive={item.key === resolvedActiveKey}
            />
          ))}
        </nav>
      )}
    </>
  );
}
