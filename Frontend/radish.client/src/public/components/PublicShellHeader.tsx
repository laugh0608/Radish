import { WebShellHeader, type WebShellNavItem, type WebShellVariant } from '@/components/web-shell';

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
}

function buildPublicNavItems(discoverHref: string, discoverLabel: string, onNavigateToDiscover?: () => void): WebShellNavItem[] {
  return [
    { key: 'discover', label: discoverLabel, href: discoverHref, icon: 'mdi:compass-outline', onClick: onNavigateToDiscover },
    { key: 'forum', label: '论坛', href: '/forum', icon: 'mdi:forum-outline' },
    { key: 'docs', label: '文档', href: '/docs', icon: 'mdi:file-document-outline' },
    { key: 'leaderboard', label: '榜单', href: '/leaderboard', icon: 'mdi:trophy-outline' },
    { key: 'shop', label: '商城', href: '/shop', icon: 'mdi:shopping-outline' },
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
}: Required<Pick<PublicShellHeaderProps, 'variant' | 'discoverHref' | 'discoverLabel' | 'circleHref' | 'circleLabel' | 'showCircleAction' | 'desktopHref' | 'desktopLabel'>> & {
  onNavigateToDiscover?: () => void;
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

export const PublicShellHeader = ({
  brandMark,
  brandName,
  brandSubline,
  onBrandClick,
  variant = 'public',
  activeKey,
  onNavigateToDiscover,
  discoverHref = '/discover',
  discoverLabel = '社区发现',
  circleHref = '/circle',
  circleLabel = '圈子',
  showCircleAction = true,
  desktopHref = '/workbench',
  desktopLabel = '工作台',
}: PublicShellHeaderProps) => {
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
    showCircleAction,
    desktopHref,
    desktopLabel,
  });

  return (
    <WebShellHeader
      variant={variant}
      brandMark={brandMark}
      brandName={brandName}
      brandSubline={brandSubline}
      activeKey={activeKey}
      navItems={navItems}
      actionItems={actionItems}
      onBrandClick={onBrandClick}
    />
  );
};
