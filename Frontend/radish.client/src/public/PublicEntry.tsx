import { useEffect, useState } from 'react';
import { PublicForumApp } from './forum/PublicForumApp';

export interface PublicForumRoute {
  kind: 'list' | 'detail';
  postId?: number;
}

function normalizePositiveInteger(value: string | undefined): number | undefined {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return undefined;
  }

  return parsed;
}

function parsePublicForumRoute(pathname: string): PublicForumRoute {
  if (pathname === '/forum' || pathname === '/forum/') {
    return { kind: 'list' };
  }

  const matched = pathname.match(/^\/forum\/post\/(\d+)\/?$/);
  const postId = normalizePositiveInteger(matched?.[1]);
  if (postId) {
    return { kind: 'detail', postId };
  }

  return { kind: 'list' };
}

function buildPublicForumPath(route: PublicForumRoute): string {
  if (route.kind === 'detail' && route.postId) {
    return `/forum/post/${route.postId}`;
  }

  return '/forum';
}

export const PublicEntry = () => {
  const [route, setRoute] = useState<PublicForumRoute>(() => parsePublicForumRoute(window.location.pathname));

  useEffect(() => {
    const handlePopState = () => {
      setRoute(parsePublicForumRoute(window.location.pathname));
    };

    window.addEventListener('popstate', handlePopState);
    return () => {
      window.removeEventListener('popstate', handlePopState);
    };
  }, []);

  const navigateToRoute = (nextRoute: PublicForumRoute, options?: { replace?: boolean }) => {
    const nextPath = buildPublicForumPath(nextRoute);
    if (options?.replace) {
      window.history.replaceState({}, '', nextPath);
    } else if (window.location.pathname !== nextPath) {
      window.history.pushState({}, '', nextPath);
    }

    setRoute(nextRoute);
  };

  return (
    <PublicForumApp
      route={route}
      onNavigate={navigateToRoute}
    />
  );
};
