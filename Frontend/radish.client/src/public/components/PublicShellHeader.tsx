import { WebShellHeader, type WebShellNavItem, type WebShellVariant } from '@/components/web-shell';
import { redirectToLogin } from '@/services/auth';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { resolveMediaUrl } from '@/utils/media';
import { ThemeSwitcher } from '@/theme/ThemeSwitcher';
import { LanguageSwitcher } from '@/i18n/LanguageSwitcher';
import { useTranslation } from 'react-i18next';

interface PublicShellHeaderProps {
  brandMark: string;
  brandName: string;
  brandSubline: string;
  onBrandClick: () => void;
  variant?: WebShellVariant;
  activeKey?: string;
  mobileNavItems?: WebShellNavItem[];
  hideMobileNav?: boolean;
  loginLabel?: string;
}

function buildCurrentReturnPath(): string {
  if (typeof window === 'undefined') {
    return '/discover';
  }

  return `${window.location.pathname}${window.location.search}${window.location.hash}`;
}

function buildAvatarText(displayName: string): string {
  const normalized = displayName.trim();
  return normalized.length > 0 ? normalized.slice(0, 1).toUpperCase() : '我';
}

function buildShellActionItems(authAction: WebShellNavItem, notificationsLabel: string): WebShellNavItem[] {
  return [
    {
      key: 'notifications',
      label: notificationsLabel,
      href: '/notifications',
      icon: 'mdi:bell-outline',
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
  hideMobileNav,
  loginLabel,
}: PublicShellHeaderProps) => {
  const { t } = useTranslation();
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const userId = useUserStore(state => state.userId);
  const displayName = useUserStore(state => state.displayName);
  const userName = useUserStore(state => state.userName);
  const avatarUrl = useUserStore(state => state.avatarThumbnailUrl || state.avatarUrl || null);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const userLabel = userName?.trim() || displayName?.trim() || t('public.shell.nav.me');
  const authAction: WebShellNavItem = loggedIn
    ? {
        key: 'me',
        label: userLabel,
        href: '/me',
        icon: 'mdi:account-circle-outline',
        avatarUrl: resolveMediaUrl(avatarUrl),
        avatarText: buildAvatarText(userLabel),
      }
    : {
        key: 'me',
        label: loginLabel ?? t('public.shell.loginOrRegister'),
        href: '/me',
        icon: 'mdi:account-circle-outline',
        onClick: () => redirectToLogin({ returnPath: buildCurrentReturnPath() }),
      };
  const actionItems = buildShellActionItems(authAction, t('public.shell.nav.notifications'));

  return (
    <WebShellHeader
      variant={variant}
      brandMark={brandMark}
      brandName={brandName}
      brandSubline={brandSubline}
      activeKey={activeKey}
      mobileNavItems={mobileNavItems}
      hideMobileNav={hideMobileNav}
      actionSlot={(
        <>
          <LanguageSwitcher />
          <ThemeSwitcher />
        </>
      )}
      actionItems={actionItems}
      onBrandClick={onBrandClick}
    />
  );
};
