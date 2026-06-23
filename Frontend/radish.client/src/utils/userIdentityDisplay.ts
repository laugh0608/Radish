interface UserIdentityDisplaySource {
  voDisplayName?: string | null;
  voDisplayHandle?: string | null;
  voPublicIndex?: string | number | null;
  voUserName?: string | null;
}

function normalizeText(value: string | number | null | undefined): string | null {
  const normalized = String(value ?? '').trim();
  return normalized.length > 0 ? normalized : null;
}

export function isInternalPublicUserName(value: string | null | undefined): boolean {
  const normalized = normalizeText(value);
  return normalized ? /^usr_[0-9a-f]{32}$/i.test(normalized) : false;
}

export function resolveVisibleUserDisplayName(
  source: UserIdentityDisplaySource,
  fallback: string
): string {
  const displayName = normalizeText(source.voDisplayName);
  if (displayName) {
    return displayName;
  }

  const legacyUserName = normalizeText(source.voUserName);
  if (legacyUserName && !isInternalPublicUserName(legacyUserName)) {
    return legacyUserName;
  }

  return fallback;
}

export function resolveVisibleUserHandle(
  source: UserIdentityDisplaySource,
  displayName: string
): string | null {
  const displayHandle = normalizeText(source.voDisplayHandle);
  if (displayHandle) {
    return displayHandle;
  }

  const publicIndex = normalizeText(source.voPublicIndex);
  return publicIndex ? `${displayName}#${publicIndex}` : null;
}
