export const WORKBENCH_ENTRY_PATH = '/workbench';

export function isWorkbenchPathname(pathname: string): boolean {
  return pathname === WORKBENCH_ENTRY_PATH || pathname === `${WORKBENCH_ENTRY_PATH}/`;
}
