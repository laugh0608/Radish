export const ME_ENTRY_PATH = '/me';

export function buildMePath(): string {
  return ME_ENTRY_PATH;
}

export function isMePathname(pathname: string): boolean {
  return pathname === ME_ENTRY_PATH || pathname === `${ME_ENTRY_PATH}/`;
}
