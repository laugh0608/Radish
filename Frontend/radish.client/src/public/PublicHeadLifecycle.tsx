import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { useTranslation } from 'react-i18next';
import {
  applyPublicHead,
  buildLocalizedPublicRouteHead,
  resetPublicHead,
} from './publicHead';
import {
  applyPublicStructuredData,
  buildPublicRouteStructuredData,
  removePublicStructuredData,
} from './publicStructuredData';
import type { PublicContentRouteDescriptor } from './publicRouteNavigation';
import {
  PublicHeadLifecycleContext,
  type PublicHeadLifecycleContextValue,
  resolveActivePublicHeadSnapshot,
  type PublicHeadRegistration,
  type UpdatePublicHeadRegistration,
  updatePublicHeadRegistration,
} from './publicHeadLifecycleContext';

interface PublicHeadLifecycleOwnerProps {
  route: PublicContentRouteDescriptor;
  routeKey: string;
  children: ReactNode;
}

/**
 * 公开页 head 的唯一 DOM writer。路由切换先写入可预知的基线，详情数据到达后再由当前页快照覆盖。
 */
export function PublicHeadLifecycleOwner({ route, routeKey, children }: PublicHeadLifecycleOwnerProps) {
  const { t } = useTranslation();
  const [registration, setRegistration] = useState<PublicHeadRegistration | null>(null);
  const updateRegistration = useCallback<UpdatePublicHeadRegistration>((token, targetRouteKey, snapshot) => {
    setRegistration((current) => updatePublicHeadRegistration(
      current,
      token,
      targetRouteKey,
      snapshot,
    ));
  }, []);
  const contextValue = useMemo<PublicHeadLifecycleContextValue>(() => ({
    routeKey,
    updateRegistration,
  }), [routeKey, updateRegistration]);
  const routeHead = useMemo(() => buildLocalizedPublicRouteHead(route, t), [route, t]);
  const activeSnapshot = resolveActivePublicHeadSnapshot(registration, routeKey);
  const resolvedHead = activeSnapshot?.head ?? routeHead;
  const routeStructuredData = useMemo(
    () => buildPublicRouteStructuredData(route, { head: resolvedHead }),
    [route, resolvedHead],
  );
  const resolvedStructuredData = activeSnapshot?.structuredData ?? routeStructuredData;

  useEffect(() => {
    applyPublicHead(resolvedHead);
    applyPublicStructuredData(resolvedStructuredData);
  }, [resolvedHead, resolvedStructuredData]);

  useEffect(() => () => {
    removePublicStructuredData();
    resetPublicHead();
  }, []);

  return (
    <PublicHeadLifecycleContext.Provider value={contextValue}>
      {children}
    </PublicHeadLifecycleContext.Provider>
  );
}
