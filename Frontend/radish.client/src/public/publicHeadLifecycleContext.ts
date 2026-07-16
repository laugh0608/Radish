import { createContext, useContext, useEffect, useRef } from 'react';
import type { PublicHeadDescriptor } from './publicHead';
import type { JsonLdObject } from './publicStructuredData';

export interface PublicHeadSnapshot {
  head: PublicHeadDescriptor;
  structuredData?: JsonLdObject;
}

export interface PublicHeadRegistration {
  token: symbol;
  routeKey: string;
  snapshot: PublicHeadSnapshot;
}

export type UpdatePublicHeadRegistration = (
  token: symbol,
  routeKey: string,
  snapshot: PublicHeadSnapshot | null,
) => void;

export interface PublicHeadLifecycleContextValue {
  routeKey: string;
  updateRegistration: UpdatePublicHeadRegistration;
}

export const PublicHeadLifecycleContext = createContext<PublicHeadLifecycleContextValue | null>(null);

export function updatePublicHeadRegistration(
  current: PublicHeadRegistration | null,
  token: symbol,
  routeKey: string,
  snapshot: PublicHeadSnapshot | null,
): PublicHeadRegistration | null {
  if (!snapshot) {
    return current?.token === token ? null : current;
  }

  if (
    current?.token === token
    && current.routeKey === routeKey
    && current.snapshot === snapshot
  ) {
    return current;
  }

  return { token, routeKey, snapshot };
}

export function resolveActivePublicHeadSnapshot(
  registration: PublicHeadRegistration | null,
  routeKey: string,
): PublicHeadSnapshot | null {
  return registration?.routeKey === routeKey ? registration.snapshot : null;
}

/**
 * 详情页只提交 head + JSON-LD 快照，不直接写 DOM；离开或数据失效后自动回到路由基线。
 */
export function usePublicHeadSnapshot(snapshot: PublicHeadSnapshot | null): void {
  const lifecycle = useContext(PublicHeadLifecycleContext);
  const tokenRef = useRef<symbol>(Symbol('public-head-snapshot'));

  useEffect(() => {
    if (!lifecycle || !snapshot) {
      return;
    }

    const token = tokenRef.current;
    lifecycle.updateRegistration(token, lifecycle.routeKey, snapshot);
    return () => {
      lifecycle.updateRegistration(token, lifecycle.routeKey, null);
    };
  }, [lifecycle, snapshot]);
}
