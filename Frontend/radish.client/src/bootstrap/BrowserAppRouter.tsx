import { lazy, startTransition, useEffect, useState, type ElementType } from 'react';
import { resolveBrowserEntryKind, type BrowserEntryKind } from '@/bootstrap/entryRoute';

const OidcCallbackPage = lazy(() => import('@/auth/OidcCallbackPage').then((module) => ({ default: module.OidcCallbackPage })));
const CircleEntry = lazy(() => import('@/circle/CircleEntry').then((module) => ({ default: module.CircleEntry })));
const MeEntry = lazy(() => import('@/me/MeEntry').then((module) => ({ default: module.MeEntry })));
const MessagesEntry = lazy(() => import('@/messages/MessagesEntry').then((module) => ({ default: module.MessagesEntry })));
const NotificationsEntry = lazy(() => import('@/notifications/NotificationsEntry').then((module) => ({ default: module.NotificationsEntry })));
const PetEntry = lazy(() => import('@/pet/PetEntry').then((module) => ({ default: module.PetEntry })));
const ShopEntry = lazy(() => import('@/shop/ShopEntry').then((module) => ({ default: module.ShopEntry })));
const DocsAuthorEntry = lazy(() => import('@/docs/DocsAuthorEntry').then((module) => ({ default: module.DocsAuthorEntry })));
const WorkbenchEntry = lazy(() => import('@/workbench/WorkbenchEntry').then((module) => ({ default: module.WorkbenchEntry })));
const RootEntry = lazy(() => import('@/desktop/RootEntry').then((module) => ({ default: module.RootEntry })));
const PublicEntry = lazy(() => import('@/public/PublicEntry').then((module) => ({ default: module.PublicEntry })));

function getCurrentEntryKind(): BrowserEntryKind {
  return resolveBrowserEntryKind(window.location.pathname);
}

function resolveEntryComponent(entryKind: BrowserEntryKind): ElementType {
  switch (entryKind) {
    case 'oidc':
      return OidcCallbackPage;
    case 'messages':
      return MessagesEntry;
    case 'notifications':
      return NotificationsEntry;
    case 'pet':
      return PetEntry;
    case 'me':
      return MeEntry;
    case 'circle':
      return CircleEntry;
    case 'shop':
      return ShopEntry;
    case 'docs-author':
      return DocsAuthorEntry;
    case 'workbench':
      return WorkbenchEntry;
    case 'public':
      return PublicEntry;
    default:
      return RootEntry;
  }
}

export function BrowserAppRouter() {
  const [entryKind, setEntryKind] = useState<BrowserEntryKind>(() => getCurrentEntryKind());

  useEffect(() => {
    const handlePopState = () => {
      startTransition(() => {
        setEntryKind(getCurrentEntryKind());
      });
    };

    window.addEventListener('popstate', handlePopState);
    return () => {
      window.removeEventListener('popstate', handlePopState);
    };
  }, []);

  const Page = resolveEntryComponent(entryKind);
  return <Page />;
}
