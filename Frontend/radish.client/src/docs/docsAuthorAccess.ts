interface WikiSourceLike {
  voSourceType?: string | null;
}

export function canUseDocsAuthorTools(userId: string): boolean {
  return userId.trim().length > 0;
}

export function isBuiltInWikiDocument(document: WikiSourceLike | null | undefined): boolean {
  return (document?.voSourceType || '').trim().toLowerCase() === 'builtin';
}
