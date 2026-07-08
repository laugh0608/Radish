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
  mobileNavItems?: WebShellNavItem[];
  loginLabel?: string;
}

function buildCurrentReturnPath(): string {
  if (typeof window === 'undefined') {
    return '/discover';
  }

  return `${window.location.pathname}${window.location.search}${window.location.hash}`;
}

function buildAccountActionItems(authAction: WebShellNavItem): WebShellNavItem[] {
  return [
    {
      key: 'messages',
      label: '消息',
      href: '/messages',
      icon: 'mdi:message-text-outline',
    },
    authAction,
  ];
}

export const PublicShellHeader = ({
  brandMark,
  brandName,
  brandSubline,
  onBrandClick,
  variant = 'public',
  activeKey,
  mobileNavItems,
  loginLabel = '登录',
}: PublicShellHeaderProps) => {
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const userId = useUserStore(state => state.userId);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const authAction: WebShellNavItem = loggedIn
    ? {
        key: 'me',
        label: '我的',
        href: '/me',
        icon: 'mdi:account-circle-outline',
      }
    : {
        key: 'me',
        label: loginLabel,
        href: '/me',
        icon: 'mdi:account-circle-outline',
        onClick: () => redirectToLogin({ returnPath: buildCurrentReturnPath() }),
      };
  const actionItems = buildAccountActionItems(authAction);

  return (
    <WebShellHeader
      variant={variant}
      brandMark={brandMark}
      brandName={brandName}
      brandSubline={brandSubline}
      activeKey={activeKey}
      mobileNavItems={mobileNavItems}
      actionItems={actionItems}
      onBrandClick={onBrandClick}
    />
  );
};
