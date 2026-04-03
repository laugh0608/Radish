export type JwtPayload = Record<string, unknown>;

const legacyNameClaim = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';

function parseStringClaim(value: unknown): string | null {
  if (typeof value !== 'string') {
    return null;
  }

  const normalized = value.trim();
  return normalized ? normalized : null;
}

export function getUserNameFromTokenPayload(payload: JwtPayload): string | null {
  return parseStringClaim(payload.name)
    ?? parseStringClaim(payload.preferred_username)
    ?? parseStringClaim(payload.nickname)
    ?? parseStringClaim(payload.unique_name)
    ?? parseStringClaim(payload[legacyNameClaim]);
}
