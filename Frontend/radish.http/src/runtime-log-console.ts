import { apiGet, apiPost } from './client';
import { createApiResponseError } from './api-response-error';
import type { ParsedApiResponse } from './types';

export interface RuntimeLogQuery {
  from?: string;
  to?: string;
  services?: string[];
  level?: string;
  eventCode?: string;
  traceId?: string;
  operationId?: string;
  keyword?: string;
  cursor?: string;
  limit?: number;
}

export interface RuntimeLogSummary {
  voEventId: string;
  voOccurredAtUtc: string;
  voService: string;
  voLevel: string;
  voEventCode: string;
  voTraceId?: string;
  voOperationId?: string;
}

export interface RuntimeLogPage {
  voItems: RuntimeLogSummary[];
  voNextCursor?: string;
  voSnapshotUtc: string;
}

export interface RuntimeLogDetail {
  voEvent: RuntimeLogSummary;
  voMessage: string;
  voRequestId?: string;
  voObservedAtUtc: string;
  voIngestedAtUtc: string;
  voProperties: Record<string, unknown>;
}

export interface RuntimeLogPipeline {
  voState: 'Unknown' | 'Faulted' | 'Healthy';
  voObservedAtUtc?: string;
  voActiveChunks?: number;
  voKnownCreateFailures?: string;
  voKnownWriteFailures?: string;
  voTailCountUnknown: boolean;
}

export interface RuntimeLogAlert {
  voId: string;
  voCode: string;
  voService: string;
  voEventId?: string;
  voState: 'Open' | 'Acknowledged' | 'Resolved';
  voVersion: number;
  voKnownFailures: string;
  voOpenedUtc: string;
  voUpdatedUtc: string;
}

export interface RuntimeLogAlerts {
  voItems: RuntimeLogAlert[];
  voOpenCount: string;
}

export interface RuntimeLogHistory {
  voId: string;
  voLocalTime: string;
  voSource: string;
  voCategory?: string;
  voSucceeded?: boolean;
}

const route = '/api/v1/RuntimeLogConsole/';

async function read<T>(response: Promise<ParsedApiResponse<T>>): Promise<T> {
  const result = await response;
  if (!result.ok || result.data == null) throw createApiResponseError(result, '日志服务暂不可用');
  return result.data;
}

export const runtimeLogConsole = {
  query: (query: RuntimeLogQuery, signal?: AbortSignal) =>
    read(apiPost<RuntimeLogPage>(route + 'Query', query, { withAuth: true, signal })),
  detail: (id: string, signal?: AbortSignal) =>
    read(apiGet<RuntimeLogDetail>(route + 'Detail?eventId=' + encodeURIComponent(id), { withAuth: true, signal })),
  export: (query: RuntimeLogQuery) =>
    read(apiPost<{ voCsv: string; voCount: number }>(route + 'Export', query, { withAuth: true })),
  pipeline: (signal?: AbortSignal) =>
    read(apiGet<RuntimeLogPipeline>(route + 'Pipeline', { withAuth: true, signal })),
  history: (audit: boolean, query: RuntimeLogQuery, signal?: AbortSignal) => {
    const params = new URLSearchParams();
    if (query.from) params.set('from', query.from);
    if (query.to) params.set('to', query.to);
    return read(apiGet<RuntimeLogHistory[]>(route + (audit ? 'Audit?' : 'History?') + params, { withAuth: true, signal }));
  },
  alerts: (signal?: AbortSignal) =>
    read(apiGet<RuntimeLogAlerts>(route + 'Alerts', { withAuth: true, signal })),
  transition: (item: RuntimeLogAlert, action: 'acknowledge' | 'resolve') =>
    read(apiPost<boolean>(route + 'Transition?id=' + encodeURIComponent(item.voId),
      { version: item.voVersion, action }, { withAuth: true })),
};
