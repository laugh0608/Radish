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

const publicNavItems: WebShellNavItem[] = [
  { key: 'discover', label: '发现', href: '/discover', icon: 'mdi:compass-outline' },
  { key: 'forum', label: '论坛', href: '/forum', icon: 'mdi:forum-outline' },
  { key: 'docs', label: '文档', href: '/docs', icon: 'mdi:file-document-outline' },
  { key: 'leaderboard', label: '榜单', href: '/leaderboard', icon: 'mdi:trophy-outline' },
  { key: 'shop', label: '商城', href: '/shop', icon: 'mdi:shopping-outline' },
];

const publicMobileNavItems: WebShellNavItem[] = [
  { key: 'discover', label: '发现', href: '/discover', icon: 'mdi:compass-outline' },
  { key: 'forum', label: '论坛', href: '/forum', icon: 'mdi:forum-outline' },
  { key: 'docs', label: '文档', href: '/docs', icon: 'mdi:file-document-outline' },
  { key: 'workbench', label: '工作台', href: '/workbench', icon: 'mdi:view-dashboard-outline' },
  { key: 'me', label: '我的', href: '/me', icon: 'mdi:account-circle-outline' },
];

const privateNavItems: WebShellNavItem[] = [
  { key: 'workbench', label: '工作台', href: '/workbench', icon: 'mdi:view-dashboard-outline' },
  { key: 'me', label: '我的状态', href: '/me', icon: 'mdi:account-circle-outline' },
  { key: 'assets', label: '资产', href: '/me/assets', icon: 'mdi:wallet-outline' },
  { key: 'author', label: '创作', href: '/docs/mine', icon: 'mdi:file-document-edit-outline' },
  { key: 'messages', label: '消息', href: '/messages', icon: 'mdi:message-text-outline' },
];

const privateMobileNavItems: WebShellNavItem[] = [
  { key: 'workbench', label: '工作台', href: '/workbench', icon: 'mdi:view-dashboard-outline' },
  { key: 'assets', label: '资产', href: '/me/assets', icon: 'mdi:wallet-outline' },
  { key: 'author', label: '创作', href: '/docs/mine', icon: 'mdi:file-document-edit-outline' },
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

function getCurrentPathname(): string {
  return typeof window === 'undefined' ? '' : window.location.pathname;
}

function resolveActiveKey(variant: WebShellVariant): string {
  const pathname = getCurrentPathname();

  if (pathname === '/workbench' || pathname.startsWith('/workbench/')) {
    return 'workbench';
  }

  if (pathname === '/discover' || pathname.startsWith('/discover/')) {
    return 'discover';
  }

  if (pathname === '/forum' || pathname.startsWith('/forum/')) {
    return variant === 'private' && pathname.includes('/compose') ? 'author' : 'forum';
  }

  if (
    pathname === '/docs/mine'
    || pathname === '/docs/compose'
    || pathname.startsWith('/docs/edit/')
    || pathname.startsWith('/docs/revisions/')
  ) {
    return 'author';
  }

  if (pathname === '/docs' || pathname.startsWith('/docs/') || pathname.startsWith('/__documents__/')) {
    return 'docs';
  }

  if (pathname === '/leaderboard' || pathname.startsWith('/leaderboard/')) {
    return 'leaderboard';
  }

  if (
    pathname === '/me/assets'
    || pathname.startsWith('/me/assets/')
    || pathname === '/shop/orders'
    || pathname === '/shop/inventory'
    || pathname.startsWith('/shop/order/')
  ) {
    return variant === 'private' ? 'assets' : 'shop';
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

function getDefaultNavItems(variant: WebShellVariant): WebShellNavItem[] {
  return variant === 'private' ? privateNavItems : publicNavItems;
}

function getDefaultMobileNavItems(variant: WebShellVariant): WebShellNavItem[] {
  return variant === 'private' ? privateMobileNavItems : publicMobileNavItems;
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
        if (!item.onClick || !shouldHandleShellLinkClick(event)) {
          return;
        }

        event.preventDefault();
        item.onClick();
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
  const resolvedActiveKey = activeKey ?? resolveActiveKey(variant);
  const resolvedNavItems = navItems ?? getDefaultNavItems(variant);
  const resolvedMobileNavItems = mobileNavItems ?? getDefaultMobileNavItems(variant);
  const resolvedActionItems = actionItems ?? [];
  const headerClassName = `${styles.header} ${variant === 'private' ? styles.privateHeader : styles.publicHeader}`;

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

          <nav className={styles.navRail} aria-label={variant === 'private' ? '私域导航' : '公开导航'}>
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

          <div className={styles.actionRail} aria-label="页面动作">
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

      <nav className={styles.mobileTabBar} aria-label={variant === 'private' ? '私域移动导航' : '公开移动导航'} data-web-mobile-nav="true">
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
