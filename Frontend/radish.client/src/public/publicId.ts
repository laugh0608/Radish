interface PublicPostIdentifierSource {
  voId: string | number;
  voPublicId?: string | null;
}

interface PublicUserIdentifierSource {
  voUserId?: string | number | null;
  voPublicId?: string | null;
}

export function normalizePublicPostId(value: string | null | undefined): string | null {
  if (typeof value !== 'string') {
    return null;
  }

  const normalized = value.trim().toLowerCase();
  return /^pst_[0-9a-f]{32}$/.test(normalized) ? normalized : null;
}

export function normalizePublicUserId(value: string | null | undefined): string | null {
  if (typeof value !== 'string') {
    return null;
  }

  const normalized = value.trim().toLowerCase();
  return /^usr_[0-9a-f]{32}$/.test(normalized) ? normalized : null;
}

export function resolvePublicPostRouteIdentifier(post: PublicPostIdentifierSource): string {
  return normalizePublicPostId(post.voPublicId) ?? String(post.voId);
}

export function resolvePublicUserRouteIdentifier(
  user: PublicUserIdentifierSource | null,
  fallbackUserId?: string | number | null,
): string | null {
  const publicId = normalizePublicUserId(user?.voPublicId);
  if (publicId) {
    return publicId;
  }

  const rawUserId = user?.voUserId ?? fallbackUserId;
  const normalizedUserId = String(rawUserId ?? '').trim();
  return /^[1-9]\d*$/.test(normalizedUserId) ? normalizedUserId : null;
}
