export interface AccessTokenIdentity {
  userId: number;
  userName: string;
  tenantId: number;
  roles: string[];
}

export type JwtPayload = Record<string, unknown>;

const legacyNameIdentifierClaim = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';
const legacyNameClaim = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';
const legacyRoleClaim = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

function parseNumericClaim(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return Math.trunc(value);
  }

  if (typeof value === 'string') {
    const parsed = Number.parseInt(value, 10);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}

function parseStringClaim(value: unknown): string | null {
  if (typeof value !== 'string') {
    return null;
  }

  const normalized = value.trim();
  return normalized ? normalized : null;
}

function parseRoleClaim(claim: unknown): string[] {
  if (typeof claim === 'string') {
    return claim
      .split(',')
      .map((item) => item.trim())
      .filter(Boolean);
  }

  if (Array.isArray(claim)) {
    return claim
      .flatMap((item) => (typeof item === 'string' ? item.split(',') : []))
      .map((item) => item.trim())
      .filter(Boolean);
  }

  return [];
}

function deduplicateRoles(roles: string[]): string[] {
  const uniqueRoles: string[] = [];
  const roleSet = new Set<string>();

  roles.forEach((role) => {
    const normalizedRole = role.trim();
    if (!normalizedRole) {
      return;
    }

    const roleKey = normalizedRole.toLowerCase();
    if (roleSet.has(roleKey)) {
      return;
    }

    roleSet.add(roleKey);
    uniqueRoles.push(normalizedRole);
  });

  return uniqueRoles;
}

export function getRolesFromTokenPayload(payload: JwtPayload): string[] {
  const standardRoles = deduplicateRoles([
    ...parseRoleClaim(payload.role),
    ...parseRoleClaim(payload.roles),
  ]);

  if (standardRoles.length > 0) {
    return standardRoles;
  }

  return deduplicateRoles(parseRoleClaim(payload[legacyRoleClaim]));
}

export function getUserIdentityFromTokenPayload(payload: JwtPayload): AccessTokenIdentity | null {
  const userId = parseNumericClaim(payload.sub ?? payload[legacyNameIdentifierClaim]);
  if (!userId || userId <= 0) {
    return null;
  }

  const userName = parseStringClaim(payload.preferred_username)
    ?? parseStringClaim(payload.name)
    ?? parseStringClaim(payload[legacyNameClaim])
    ?? String(userId);

  const tenantId = parseNumericClaim(payload.tenant_id ?? payload.TenantId) ?? 0;

  return {
    userId,
    userName,
    tenantId,
    roles: getRolesFromTokenPayload(payload),
  };
}
