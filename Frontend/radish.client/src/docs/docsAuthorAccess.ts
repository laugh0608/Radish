interface WikiSourceLike {
  voSourceType?: string | null;
}

export function canUseDocsAuthorTools(roles: string[]): boolean {
  return roles.some((role) => {
    const normalized = role.trim().toLowerCase();
    return normalized === 'admin' || normalized === 'system';
  });
}

export function isBuiltInWikiDocument(document: WikiSourceLike | null | undefined): boolean {
  return (document?.voSourceType || '').trim().toLowerCase() === 'builtin';
}
