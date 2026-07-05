import { WebShellHeader, type WebShellNavItem, type WebShellVariant } from '@/components/web-shell';
import { redirectToLogin } from '@/services/auth';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';

interface PublicShellHeaderProps {
  brandMark: string;
  brandName: string;
  brandSubline: string;
  onBrandClick: () => void;
  variant?: WebShellVariant;
  activeKey?: string;
  onNavigateToDiscover?: () => void;
  discoverHref?: string;
  discoverLabel?: string;
  circleHref?: string;
  circleLabel?: string;
  showCircleAction?: boolean;
  desktopHref?: string;
  desktopLabel?: string;
  mobileNavItems?: WebShellNavItem[];
  loginLabel?: string;
  myStatusLabel?: string;
}

function buildPublicNavItems(discoverHref: string, discoverLabel: string, onNavigateToDiscover?: () => void): WebShellNavItem[] {
  return [
    { key: 'discover', label: discoverLabel, href: discoverHref, icon: 'mdi:compass-outline', onClick: onNavigateToDiscover },
    { key: 'forum', label: '论坛', href: '/forum', icon: 'mdi:forum-outline' },
    { key: 'docs', label: '文档', href: '/docs', icon: 'mdi:file-document-outline' },
    { key: 'leaderboard', label: '榜单', href: '/leaderboard', icon: 'mdi:trophy-outline' },
    { key: 'shop', label: '商城', href: '/shop', icon: 'mdi:shopping-outline' },
    { key: 'legal', label: '规则', href: '/legal', icon: 'mdi:shield-check-outline' },
  ];
}

function buildActionItems({
  variant,
  discoverHref,
  discoverLabel,
  onNavigateToDiscover,
  circleHref,
  circleLabel,
  showCircleAction,
  desktopHref,
  desktopLabel,
  authAction,
}: Required<Pick<PublicShellHeaderProps, 'variant' | 'discoverHref' | 'discoverLabel' | 'circleHref' | 'circleLabel' | 'showCircleAction' | 'desktopHref' | 'desktopLabel'>> & {
  onNavigateToDiscover?: () => void;
  authAction?: WebShellNavItem;
}): WebShellNavItem[] {
  const actionItems: WebShellNavItem[] = [];

  if (variant === 'private' && discoverHref.trim()) {
    actionItems.push({
      key: 'discover-action',
      label: discoverLabel,
      href: discoverHref,
      icon: 'mdi:compass-outline',
      onClick: onNavigateToDiscover,
    });
  }

  if (variant === 'public' && authAction) {
    actionItems.push(authAction);
  }

  if (showCircleAction && circleHref.trim()) {
    actionItems.push({
      key: 'circle-action',
      label: circleLabel,
      href: circleHref,
      icon: 'mdi:account-group-outline',
    });
  }

  if (variant === 'public' && desktopHref.trim()) {
    actionItems.push({
      key: 'workbench-action',
      label: desktopLabel,
      href: desktopHref,
      icon: 'mdi:view-dashboard-outline',
    });
  }

  return actionItems;
}

function buildCurrentReturnPath(): string {
  if (typeof window === 'undefined') {
    return '/discover';
  }

  return `${window.location.pathname}${window.location.search}${window.location.hash}`;
}

export const PublicShellHeader = ({
  brandMark,
  brandName,
  brandSubline,
  onBrandClick,
  variant = 'public',
  activeKey,
  onNavigateToDiscover,
  discoverHref = '/discover',
  discoverLabel = '发现',
  circleHref = '/circle',
  circleLabel = '圈子',
  showCircleAction = true,
  desktopHref = '/workbench',
  desktopLabel = '工作台',
  mobileNavItems,
  loginLabel = '登录',
  myStatusLabel = '我的状态',
}: PublicShellHeaderProps) => {
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const userId = useUserStore(state => state.userId);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const shouldShowCircleAction = variant === 'public'
    ? showCircleAction && loggedIn
    : showCircleAction;
  const authAction: WebShellNavItem = loggedIn
    ? {
        key: 'me-action',
        label: myStatusLabel,
        href: '/me',
        icon: 'mdi:account-circle-outline',
      }
    : {
        key: 'login-action',
        label: loginLabel,
        href: '/me',
        icon: 'mdi:account-circle-outline',
        onClick: () => redirectToLogin({ returnPath: buildCurrentReturnPath() }),
      };
  const navItems = variant === 'public'
    ? buildPublicNavItems(discoverHref, discoverLabel, onNavigateToDiscover)
    : undefined;
  const actionItems = buildActionItems({
    variant,
    discoverHref,
    discoverLabel,
    onNavigateToDiscover,
    circleHref,
    circleLabel,
    showCircleAction: shouldShowCircleAction,
    desktopHref,
    desktopLabel,
    authAction,
  });

  return (
    <WebShellHeader
      variant={variant}
      brandMark={brandMark}
      brandName={brandName}
      brandSubline={brandSubline}
      activeKey={activeKey}
      navItems={navItems}
      mobileNavItems={mobileNavItems}
      actionItems={actionItems}
      onBrandClick={onBrandClick}
    />
  );
};
