import { createContext, useContext, useEffect, useRef } from 'react';

export type BrowserNavigationLockUpdater = (token: symbol, locked: boolean) => void;

export const BrowserNavigationLockContext = createContext<BrowserNavigationLockUpdater | null>(null);

export function useBrowserNavigationLock(locked: boolean): void {
  const updateNavigationLock = useContext(BrowserNavigationLockContext);
  const lockTokenRef = useRef(Symbol('browser-navigation-lock'));

  useEffect(() => {
    if (!locked || !updateNavigationLock) {
      return;
    }

    const lockToken = lockTokenRef.current;
    updateNavigationLock(lockToken, true);
    return () => {
      updateNavigationLock(lockToken, false);
    };
  }, [locked, updateNavigationLock]);
}
