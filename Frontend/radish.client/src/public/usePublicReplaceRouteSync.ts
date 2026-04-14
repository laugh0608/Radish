import { useEffect } from 'react';

export function shouldReplacePublicRouteSync(currentRouteKey: string, nextRouteKey: string): boolean {
  return currentRouteKey !== nextRouteKey;
}

export function usePublicReplaceRouteSync<TRoute>({
  currentRouteKey,
  nextRoute,
  nextRouteKey,
  onRouteStateChange
}: {
  currentRouteKey: string;
  nextRoute: TRoute;
  nextRouteKey: string;
  onRouteStateChange: (route: TRoute, options?: { replace?: boolean }) => void;
}) {
  useEffect(() => {
    if (!shouldReplacePublicRouteSync(currentRouteKey, nextRouteKey)) {
      return;
    }

    onRouteStateChange(nextRoute, { replace: true });
  }, [currentRouteKey, nextRoute, nextRouteKey, onRouteStateChange]);
}
