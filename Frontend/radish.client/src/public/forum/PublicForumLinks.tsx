import type { ReactNode } from 'react';
import { buildPublicForumPath, type PublicForumRoute } from '../forumRouteState';
import { handlePublicForumLinkClick } from './publicForumLinkHandlers';
import styles from './PublicForumApp.module.css';

interface PublicForumRouteLinkProps {
  className: string;
  route: PublicForumRoute;
  onNavigate?: () => void;
  children: ReactNode;
  ariaCurrent?: 'page' | undefined;
}

export function PublicForumRouteLink({
  className,
  route,
  onNavigate,
  children,
  ariaCurrent
}: PublicForumRouteLinkProps) {
  return (
    <a
      className={className}
      href={buildPublicForumPath(route)}
      aria-current={ariaCurrent}
      onClick={(event) => handlePublicForumLinkClick(event, onNavigate)}
    >
      {children}
    </a>
  );
}

interface PublicForumPaginationProps<T extends PublicForumRoute> {
  currentPage: number;
  totalPages: number;
  visiblePages: number[];
  buildRoute: (page: number) => T;
  onPageChange: (page: number) => void;
}

export function PublicForumPagination<T extends PublicForumRoute>({
  currentPage,
  totalPages,
  visiblePages,
  buildRoute,
  onPageChange
}: PublicForumPaginationProps<T>) {
  if (totalPages <= 1) {
    return null;
  }

  return (
    <div className={styles.pagination}>
      {currentPage > 1 ? (
        <PublicForumRouteLink
          className={styles.paginationButton}
          route={buildRoute(currentPage - 1)}
          onNavigate={() => onPageChange(currentPage - 1)}
        >
          ‹
        </PublicForumRouteLink>
      ) : (
        <button type="button" className={styles.paginationButton} disabled>
          ‹
        </button>
      )}
      {visiblePages.map((page) => (
        <PublicForumRouteLink
          key={page}
          className={`${styles.paginationButton} ${page === currentPage ? styles.paginationButtonActive : ''}`}
          route={buildRoute(page)}
          ariaCurrent={page === currentPage ? 'page' : undefined}
          onNavigate={() => onPageChange(page)}
        >
          {page}
        </PublicForumRouteLink>
      ))}
      {currentPage < totalPages ? (
        <PublicForumRouteLink
          className={styles.paginationButton}
          route={buildRoute(currentPage + 1)}
          onNavigate={() => onPageChange(currentPage + 1)}
        >
          ›
        </PublicForumRouteLink>
      ) : (
        <button type="button" className={styles.paginationButton} disabled>
          ›
        </button>
      )}
    </div>
  );
}
