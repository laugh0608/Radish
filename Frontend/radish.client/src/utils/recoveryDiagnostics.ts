import { copyToClipboard } from './clipboard';

type DiagnosticValue = string | number | boolean | null | undefined;

interface RecoveryDiagnosticInput {
  module: string;
  stage: string;
  error?: unknown;
  target?: Record<string, DiagnosticValue>;
  context?: Record<string, DiagnosticValue>;
  path?: string;
}

function normalizeDiagnosticValue(value: DiagnosticValue): string {
  if (value === null || value === undefined || value === '') {
    return 'unknown';
  }

  return String(value).replace(/\s+/g, ' ').trim() || 'unknown';
}

function normalizeErrorMessage(error: unknown): string {
  if (error instanceof Error && error.message.trim()) {
    return error.message.trim();
  }

  if (typeof error === 'string' && error.trim()) {
    return error.trim();
  }

  return 'unknown';
}

function appendDiagnosticRecord(lines: string[], prefix: string, record?: Record<string, DiagnosticValue>) {
  if (!record) {
    return;
  }

  for (const [key, value] of Object.entries(record)) {
    lines.push(`${prefix}.${key}: ${normalizeDiagnosticValue(value)}`);
  }
}

export function getCurrentRecoveryPath(): string {
  if (typeof window === 'undefined') {
    return 'unknown';
  }

  return `${window.location.pathname}${window.location.search}${window.location.hash}`;
}

export function buildRecoveryDiagnostics(input: RecoveryDiagnosticInput): string {
  const lines = [
    'Radish recovery diagnostic',
    `module: ${normalizeDiagnosticValue(input.module)}`,
    `stage: ${normalizeDiagnosticValue(input.stage)}`,
    `path: ${normalizeDiagnosticValue(input.path ?? getCurrentRecoveryPath())}`,
    `error: ${normalizeErrorMessage(input.error)}`,
    `createdAt: ${new Date().toISOString()}`,
  ];

  appendDiagnosticRecord(lines, 'target', input.target);
  appendDiagnosticRecord(lines, 'context', input.context);

  return lines.join('\n');
}

export async function copyRecoveryDiagnostics(input: RecoveryDiagnosticInput): Promise<void> {
  await copyToClipboard(buildRecoveryDiagnostics(input));
}
