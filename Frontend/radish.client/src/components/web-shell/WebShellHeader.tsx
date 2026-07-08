import { useEffect, type MouseEvent } from 'react';
import { Icon } from '@radish/ui/icon';
import styles from './WebShellHeader.module.css';

export type WebShellVariant = 'public' | 'private';

export interface WebShellNavItem {
  key: string;
  label: string;
  href: string;
  icon: string;
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
  mobileNavItems?: WebShellNavItem[];
}

const productNavItems: WebShellNavItem[] = [
  { key: 'discover', label: '发现', href: '/discover', icon: 'mdi:compass-outline' },
  { key: 'forum', label: '论坛', href: '/forum', icon: 'mdi:forum-outline' },
  { key: 'docs', label: '文档', href: '/docs', icon: 'mdi:file-document-outline' },
  { key: 'shop', label: '商城', href: '/shop', icon: 'mdi:shopping-outline' },
  { key: 'workbench', label: '工作台', href: '/workbench', icon: 'mdi:view-dashboard-outline' },
];

const productMobileNavItems: WebShellNavItem[] = [
  { key: 'discover', label: '发现', href: '/discover', icon: 'mdi:compass-outline' },
  { key: 'forum', label: '论坛', href: '/forum', icon: 'mdi:forum-outline' },
  { key: 'workbench', label: '工作台', href: '/workbench', icon: 'mdi:view-dashboard-outline' },
  { key: 'messages', label: '消息', href: '/messages', icon: 'mdi:message-text-outline' },
  { key: 'me', label: '我的', href: '/me', icon: 'mdi:account-circle-outline' },
];

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

  if (activeKey === 'author' || activeKey === 'leaderboard' || activeKey === 'legal') {
    return 'workbench';
  }

  return activeKey;
}

function resolveActiveKey(variant: WebShellVariant): string {
  const pathname = getCurrentPathname();
  const search = typeof window === 'undefined' ? '' : window.location.search;

  if (pathname === '/workbench' || pathname.startsWith('/workbench/')) {
    return 'workbench';
  }

  if (pathname === '/desktop' || pathname.startsWith('/desktop') || pathname === '/console' || pathname.startsWith('/console/')) {
    return 'workbench';
  }

  if (pathname === '/forum/compose' || search.includes('intent=answer') || search.includes('intent=edit') || search.includes('intent=history')) {
    return 'workbench';
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
    return 'workbench';
  }

  if (pathname === '/docs' || pathname.startsWith('/docs/') || pathname.startsWith('/__documents__/')) {
    return 'docs';
  }

  if (pathname === '/leaderboard' || pathname.startsWith('/leaderboard/')) {
    return 'workbench';
  }

  if (pathname === '/legal' || pathname.startsWith('/legal/')) {
    return 'workbench';
  }

  if (
    pathname === '/me/assets'
    || pathname.startsWith('/me/assets/')
    || pathname === '/shop/orders'
    || pathname === '/shop/inventory'
    || pathname.startsWith('/shop/order/')
  ) {
    return 'workbench';
  }

  if (pathname === '/shop' || pathname.startsWith('/shop/')) {
    return 'shop';
  }

  if (pathname === '/messages' || pathname.startsWith('/messages/') || pathname === '/notifications') {
    return 'messages';
  }

  if (pathname === '/me' || pathname.startsWith('/me/') || pathname === '/circle' || pathname === '/pet') {
    return 'me';
  }

  return variant === 'private' ? 'workbench' : 'discover';
}

function getDefaultNavItems(): WebShellNavItem[] {
  return productNavItems;
}

function getDefaultMobileNavItems(): WebShellNavItem[] {
  return productMobileNavItems;
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
      <Icon icon={item.icon} size={18} />
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
  mobileNavItems,
}: WebShellHeaderProps) {
  const resolvedActiveKey = normalizeActiveKey(activeKey ?? resolveActiveKey(variant));
  const resolvedNavItems = navItems ?? getDefaultNavItems();
  const resolvedMobileNavItems = mobileNavItems ?? getDefaultMobileNavItems();
  const resolvedActionItems = actionItems ?? [];
  const headerClassName = `${styles.header} ${variant === 'private' ? styles.privateHeader : styles.publicHeader}`;
  const actionRailClassName = styles.actionRail;

  useEffect(() => {
    document.body.classList.add('radishWebShellWithMobileNav');
    return () => {
      document.body.classList.remove('radishWebShellWithMobileNav');
    };
  }, []);

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

          <nav className={styles.navRail} aria-label="产品导航">
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

      <nav className={styles.mobileTabBar} aria-label="产品移动导航" data-web-mobile-nav="true">
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
    </>
  );
}
