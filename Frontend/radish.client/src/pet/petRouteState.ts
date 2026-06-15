export const PET_ENTRY_PATH = '/pet';

export function buildPetPath(): string {
  return PET_ENTRY_PATH;
}

export function isPetPathname(pathname: string): boolean {
  return pathname === PET_ENTRY_PATH;
}
