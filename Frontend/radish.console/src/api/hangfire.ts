import { apiFetch, apiPost, createApiResponseError } from '@radish/http';

export interface HangfireSession {
  voDashboardPath: string;
  voExpiresAtUtc: string;
}

export async function createHangfireSession(signal: AbortSignal): Promise<HangfireSession> {
  const response = await apiPost<HangfireSession>('/api/v1/HangfireSession/Create', undefined, {
    withAuth: true,
    credentials: 'include',
    signal,
  });
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '无法建立任务看板会话');
  }

  // iframe 的 load 事件无法区分 200 / 401，先验证 Cookie 确实可访问宿主页面。
  const dashboard = await apiFetch(response.data.voDashboardPath, {
    credentials: 'include',
    signal,
    redirect: 'error',
  });
  if (!dashboard.ok) {
    throw new Error(`Hangfire HTTP ${dashboard.status}`);
  }
  await dashboard.body?.cancel();
  return response.data;
}
