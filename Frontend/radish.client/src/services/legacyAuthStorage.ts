import { getRememberedAuthReturnPath } from './authReturnPath.ts';

type AuthStorage = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>;
const WEBOS_STORAGE_ENABLED = 'radish:web-session:separate-webos-storage';
const WEBOS_PREFIX = 'radish:webos:';
let entryStorage: { path: string; storage: AuthStorage } | undefined;

export function separateWebOsAuthStorage(): void {
  window.localStorage.setItem(WEBOS_STORAGE_ENABLED, '1');
}

export function getLegacyAuthStorage(): AuthStorage {
  const storage = window.localStorage;
  const path = window.location.pathname.toLowerCase().replace(/\/+$/, '');
  if (path !== '/desktop' && path !== '/oidc/callback') return storage;
  if (entryStorage?.path === path) return entryStorage.storage;

  // 回调消费返回路径、或其他文档开始迁移后，在途登录和刷新仍写回发起时选定的位置。
  const returnPath = path === '/oidc/callback' ? getRememberedAuthReturnPath() : null;
  const isDesktop = path === '/desktop' || /^\/desktop\/?(?:[?#]|$)/.test(returnPath ?? '');
  let selected: AuthStorage = storage;
  if (isDesktop && storage.getItem(WEBOS_STORAGE_ENABLED) === '1') {
    selected = {
      getItem: key => storage.getItem(WEBOS_PREFIX + key),
      setItem: (key, value) => storage.setItem(WEBOS_PREFIX + key, value),
      removeItem: key => storage.removeItem(WEBOS_PREFIX + key),
    };
  }
  entryStorage = { path, storage: selected };
  return selected;
}
