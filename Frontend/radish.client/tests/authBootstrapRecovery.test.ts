import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import test, { afterEach, beforeEach, describe } from 'node:test';
import { rolldown } from 'rolldown';

const source = fileURLToPath(new URL('../src', import.meta.url));
const entry = '\0auth-recovery-test';
const stub = '\0auth-recovery-dependencies';
const dependencies = new Set([
  '@radish/http', '@/config/env', '@/i18n', '@/utils/logger', '@/services/tokenService',
  './browserWebSession', '@/services/chatHub', '@/services/commentHub', '@/services/notificationHub',
  './chatStore', './notificationStore',
]);

// 订阅真实的认证和用户 Store，记录页面在一次异步恢复中可以观察到的每次状态更新。
const bundle = await rolldown({
  input: entry,
  resolve: { alias: { '@': source } },
  plugins: [{
    name: 'auth-recovery-test',
    resolveId(id) {
      if (id === entry) return entry;
      return dependencies.has(id) ? stub : null;
    },
    load(id) {
      if (id === entry) return `
        export { hydrateAuthUser, revalidateAuthSession } from '@/services/authBootstrap';
        export { useUserStore } from '@/stores/userStore';
        export { useAuthStore } from '@/stores/authStore';
        export { tokenService } from '@/services/tokenService';
      `;
      if (id === stub) return `
        export const tokenService = {
          sessionMode: 'cookie', token: null, recovering: true,
          getValidAccessToken() { return Promise.resolve(this.token); },
          clearTokens() { this.token = null; },
          isSessionRecovering() { return this.recovering; },
          getUserIdentityFromAccessToken() { return { userId: '42', tenantId: '7', userName: 'member', roles: [] }; },
          getRolesFromAccessToken() { return []; },
        };
        export const browserWebSession = { generation: 0 };
        export const commentHub = { syncSessionRoute() {} };
        export const chatHub = {}, notificationHub = {}, TokenRefreshErrorType = {};
        export const configureApiClient = () => {}, configureTokenRefresh = () => {}, getAuthBaseUrl = () => '';
        export const useChatStore = { getState: () => ({ reset() {} }) }, useNotificationStore = useChatStore;
        export const apiFetch = async () => Response.json({ voUserId: '42', voTenantId: '7', voUserName: 'member' });
        export const parseApiResponse = data => ({ ok: true, data });
        export const log = { debug() {}, info() {}, warn() {}, error() {} };
        export default { language: 'zh' };
      `;
      return null;
    },
  }],
});
const { output } = await bundle.generate({ format: 'esm' });
await bundle.close();
const { hydrateAuthUser, revalidateAuthSession, useUserStore, useAuthStore, tokenService } =
  await import(`data:text/javascript;base64,${Buffer.from(output[0].code).toString('base64')}`);

const options = { apiBaseUrl: 'https://community.test' };
describe('普通 Web 会话恢复', () => {
  let previousWindow: PropertyDescriptor | undefined;
  beforeEach(() => {
    previousWindow = Object.getOwnPropertyDescriptor(globalThis, 'window');
    Object.defineProperty(globalThis, 'window', { configurable: true, value: { location: { origin: options.apiBaseUrl } } });
    tokenService.token = null;
    tokenService.recovering = true;
    useUserStore.getState().clearUser();
    useAuthStore.setState({ isAuthenticated: false, isSessionRecovering: false });
  });
  afterEach(() => {
    if (previousWindow) Object.defineProperty(globalThis, 'window', previousWindow);
    else Reflect.deleteProperty(globalThis, 'window');
  });

  for (const recover of [hydrateAuthUser, revalidateAuthSession]) {
    test(`${recover.name} 在恢复账号之前持续阻止受保护页跳转登录`, async t => {
      await hydrateAuthUser(options);
      assert.equal(useAuthStore.getState().isSessionRecovering, true);
      const redirectStates: boolean[] = [];
      const observe = () => {
        const auth = useAuthStore.getState();
        const loggedIn = auth.isAuthenticated && useUserStore.getState().isAuthenticated();
        redirectStates.push(!auth.isSessionRecovering && !loggedIn);
      };
      t.after(useAuthStore.subscribe(observe));
      t.after(useUserStore.subscribe(observe));
      tokenService.token = 'rotated';
      tokenService.recovering = false;

      const user = await recover(options);
      assert.equal(user.voUserId, '42');
      assert.equal(useAuthStore.getState().isAuthenticated, true);
      assert.equal(useAuthStore.getState().isSessionRecovering, false);
      assert.ok(redirectStates.length > 0);
      assert.ok(!redirectStates.includes(true), '恢复过程中发布了会触发登录的中间状态');
    });
  }

  test('服务端确认会话已到期后结束恢复，受保护页可进入登录', async () => {
    tokenService.token = 'previous';
    await hydrateAuthUser(options);
    assert.equal(useAuthStore.getState().isAuthenticated, true);
    tokenService.token = null;
    await hydrateAuthUser(options);
    assert.equal(useAuthStore.getState().isSessionRecovering, true);
    tokenService.recovering = false;
    assert.equal(await revalidateAuthSession(options), null);
    assert.equal(useAuthStore.getState().isSessionRecovering, false);
    assert.equal(useAuthStore.getState().isAuthenticated, false);
    assert.equal(useUserStore.getState().isAuthenticated(), false);
  });
});
