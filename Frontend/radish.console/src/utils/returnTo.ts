export function normalizeConsoleReturnTo(value?: string | null): string | undefined {
  const normalized = value?.trim();
  if (!normalized) {
    return undefined;
  }

  if (!normalized.startsWith('/') || normalized.startsWith('//') || normalized.includes('\\')) {
    return undefined;
  }

  return normalized;
}
