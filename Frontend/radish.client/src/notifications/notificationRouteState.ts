export const NOTIFICATIONS_ENTRY_PATH = '/notifications';

export function isNotificationsPathname(pathname: string): boolean {
  return pathname === NOTIFICATIONS_ENTRY_PATH || pathname === `${NOTIFICATIONS_ENTRY_PATH}/`;
}
