import assert from 'node:assert/strict';
import path from 'node:path';
import test, { after, beforeEach } from 'node:test';
import { fileURLToPath } from 'node:url';
import { rolldown } from 'rolldown';

interface ParsedResponse<T = unknown> {
  ok: boolean;
  data?: T;
  message?: string;
  messageInfo?: string;
  messageKey?: string;
  messageArguments?: unknown[];
  code?: string;
  statusCode?: number;
  httpStatus?: number;
  traceId?: string;
}

class TestApiResponseError extends Error {
  readonly code?: string;
  readonly messageKey?: string;
  readonly messageInfo?: string;
  readonly statusCode?: number;
  readonly httpStatus?: number;
  readonly traceId?: string;

  constructor(response: ParsedResponse, fallbackMessage: string) {
    super(response.message || fallbackMessage);
    this.name = 'ApiResponseError';
    this.code = response.code;
    this.messageKey = response.messageKey;
    this.messageInfo = response.messageInfo;
    this.statusCode = response.statusCode;
    this.httpStatus = response.httpStatus;
    this.traceId = response.traceId;
  }
}

interface XhrScenario {
  event: 'load' | 'error' | 'abort' | 'timeout' | 'pending';
  status?: number;
  responseText?: string;
  responseHeaders?: Record<string, string>;
}

class MockUploadTarget {
  addEventListener(): void {
    // 上传进度并非本组契约的断言目标，只需保留浏览器接口形状。
  }
}

class MockXmlHttpRequest {
  static scenarios: XhrScenario[] = [];
  static instances: MockXmlHttpRequest[] = [];

  readonly upload = new MockUploadTarget();
  readonly requestHeaders = new Map<string, string>();
  readonly listeners = new Map<string, () => void>();
  readonly scenario: XhrScenario;
  status = 0;
  responseText = '';
  timeout = 0;
  method = '';
  url = '';
  sendCount = 0;

  constructor() {
    const scenario = MockXmlHttpRequest.scenarios.shift();
    if (!scenario) {
      throw new Error('Missing XMLHttpRequest test scenario');
    }

    this.scenario = scenario;
    this.status = scenario.status ?? 0;
    this.responseText = scenario.responseText ?? '';
    MockXmlHttpRequest.instances.push(this);
  }

  addEventListener(type: string, listener: () => void): void {
    this.listeners.set(type, listener);
  }

  open(method: string, url: string): void {
    this.method = method;
    this.url = url;
  }

  setRequestHeader(name: string, value: string): void {
    this.requestHeaders.set(name.toLowerCase(), value);
  }

  getResponseHeader(name: string): string | null {
    const expectedName = name.toLowerCase();
    const entry = Object.entries(this.scenario.responseHeaders ?? {})
      .find(([headerName]) => headerName.toLowerCase() === expectedName);
    return entry?.[1] ?? null;
  }

  send(): void {
    this.sendCount += 1;
    if (this.scenario.event !== 'pending') {
      queueMicrotask(() => this.listeners.get(this.scenario.event)?.());
    }
  }

  abort(): void {
    queueMicrotask(() => this.listeners.get('abort')?.());
  }
}

const defaultTranslations: Record<string, string> = {
  'attachment.api.imageUploadFailed': 'Image upload failed',
  'attachment.api.documentUploadFailed': 'Document upload failed',
  'attachment.api.invalidResponse': 'The server returned an invalid response',
  'attachment.api.networkError': 'Network request failed',
  'attachment.api.uploadCancelled': 'Upload cancelled',
  'attachment.api.deleteFailed': 'Delete failed',
  'profile.attachments.loadFailed': 'Failed to load attachments',
  'profile.avatar.setAvatarFailed': 'Failed to set avatar',
  'profile.avatar.removeAvatarFailed': 'Failed to remove avatar',
  'error.attachment.upload_forbidden': 'You cannot use this attachment as your avatar.',
  'error.attachment.file_too_large': 'The selected file exceeds the {{0}} limit.',
};

function translateMessage(key: string, messageArguments?: readonly unknown[]): string | undefined {
  const template = defaultTranslations[key];
  return template?.replace(/\{\{(\d+)\}\}/g, (_, index: string) => (
    String(messageArguments?.[Number(index)] ?? '')
  ));
}

const harness = {
  config: {
    baseUrl: 'https://gateway.example',
    timeout: 3456,
    getToken: () => 'access-token',
    getLanguage: () => 'en-US',
    translateMessage,
  },
  apiGet: async (): Promise<ParsedResponse> => ({ ok: true, data: {} }),
  apiDelete: async (): Promise<ParsedResponse> => ({ ok: true }),
  apiPost: async (): Promise<ParsedResponse> => ({ ok: true }),
  apiPut: async (): Promise<ParsedResponse> => ({ ok: true }),
  ApiResponseError: TestApiResponseError,
  createApiResponseError: (response: ParsedResponse, fallbackMessage: string) => (
    new TestApiResponseError(response, fallbackMessage)
  ),
  parseApiResponseWithI18n: <T>(response: {
    isSuccess: boolean;
    responseData?: T;
    messageInfo?: string;
    messageKey?: string;
    messageArguments?: unknown[];
    code?: string;
    statusCode?: number;
    traceId?: string;
  }, translate: (key: string, messageArguments?: readonly unknown[]) => string): ParsedResponse<T> => {
    const localized = response.messageKey
      ? translate(response.messageKey, response.messageArguments)
      : undefined;
    return response.isSuccess
      ? {
          ok: true,
          data: response.responseData,
          messageInfo: response.messageInfo,
          messageKey: response.messageKey,
          messageArguments: response.messageArguments,
          statusCode: response.statusCode,
          traceId: response.traceId,
        }
      : {
          ok: false,
          message: localized && localized !== response.messageKey
            ? localized
            : response.messageInfo,
          messageInfo: response.messageInfo,
          messageKey: response.messageKey,
          messageArguments: response.messageArguments,
          code: response.code,
          statusCode: response.statusCode,
          traceId: response.traceId,
        };
  },
};

const originalXmlHttpRequest = globalThis.XMLHttpRequest;
Object.assign(globalThis, {
  __RADISH_ATTACHMENT_TEST_HARNESS__: harness,
  XMLHttpRequest: MockXmlHttpRequest,
});

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const attachmentEntry = path.resolve(testDirectory, '../src/api/attachment.ts');
const bundle = await rolldown({
  input: attachmentEntry,
  plugins: [{
    name: 'attachment-api-test-harness',
    resolveId(source) {
      if (source === '@radish/http') return '\0attachment-http';
      if (source === '@/utils/logger') return '\0attachment-logger';
      if (source === '@/config/env') return '\0attachment-env';
      return null;
    },
    load(id) {
      if (id === '\0attachment-http') {
        return `
          const harness = globalThis.__RADISH_ATTACHMENT_TEST_HARNESS__;
          export const ApiResponseError = harness.ApiResponseError;
          export const apiDelete = (...args) => harness.apiDelete(...args);
          export const apiGet = (...args) => harness.apiGet(...args);
          export const apiPost = (...args) => harness.apiPost(...args);
          export const apiPut = (...args) => harness.apiPut(...args);
          export const configureApiClient = (next) => Object.assign(harness.config, next);
          export const createApiResponseError = harness.createApiResponseError;
          export const getApiClientConfig = () => harness.config;
          export const parseApiResponseWithI18n = harness.parseApiResponseWithI18n;
        `;
      }
      if (id === '\0attachment-logger') {
        return 'export const log = { warn() {} };';
      }
      if (id === '\0attachment-env') {
        return `
          export const getApiBaseUrl = () => (
            globalThis.__RADISH_ATTACHMENT_TEST_HARNESS__.config.baseUrl
          );
        `;
      }
      return null;
    },
  }],
});
const generated = await bundle.generate({ format: 'esm' });
await bundle.close();
const chunk = generated.output.find((item) => item.type === 'chunk');
assert.ok(chunk && chunk.type === 'chunk');
const attachmentApi = await import(`data:text/javascript;base64,${Buffer.from(chunk.code).toString('base64')}`) as {
  uploadImage: (options: { file: File; signal?: AbortSignal }, t: (key: string) => string) => Promise<unknown>;
  getMyAttachments: () => Promise<unknown>;
  deleteAttachment: (id: string, t: (key: string) => string) => Promise<void>;
  setMyAvatar: (
    id: string | null,
    t: (key: string) => string,
    signal?: AbortSignal,
  ) => Promise<void>;
};

function messageModel(options: {
  success: boolean;
  status: number;
  data?: unknown;
  message?: string;
  messageKey?: string;
  messageArguments?: unknown[];
  code?: string;
  traceId?: string;
}): string {
  return JSON.stringify({
    isSuccess: options.success,
    statusCode: options.status,
    responseData: options.data,
    messageInfo: options.message,
    messageKey: options.messageKey,
    messageArguments: options.messageArguments,
    code: options.code,
    traceId: options.traceId,
  });
}

function t(key: string): string {
  return defaultTranslations[key] ?? key;
}

beforeEach(() => {
  MockXmlHttpRequest.scenarios = [];
  MockXmlHttpRequest.instances = [];
  Object.assign(harness.config, {
    baseUrl: 'https://gateway.example',
    timeout: 3456,
    getToken: () => 'access-token',
    getLanguage: () => 'en-US',
    translateMessage,
  });
  harness.apiGet = async () => ({ ok: true, data: {} });
  harness.apiDelete = async () => ({ ok: true });
  harness.apiPost = async () => ({ ok: true });
  harness.apiPut = async () => ({ ok: true });
});

after(() => {
  globalThis.XMLHttpRequest = originalXmlHttpRequest;
  Reflect.deleteProperty(globalThis, '__RADISH_ATTACHMENT_TEST_HARNESS__');
});

test('XHR 上传应从统一配置注入认证、语言、超时与 JSON accept 头', async () => {
  const attachment = { voId: '42', voOriginalName: 'image.png' };
  MockXmlHttpRequest.scenarios = [{
    event: 'load',
    status: 200,
    responseText: messageModel({ success: true, status: 200, data: attachment }),
  }];

  const result = await attachmentApi.uploadImage({
    file: new File(['image'], 'image.png', { type: 'image/png' }),
  }, t);

  assert.deepEqual(result, attachment);
  const [request] = MockXmlHttpRequest.instances;
  assert.ok(request);
  assert.equal(request.method, 'POST');
  assert.equal(request.url, 'https://gateway.example/api/v1/Attachment/UploadImage');
  assert.equal(request.timeout, 3456);
  assert.equal(request.requestHeaders.get('authorization'), 'Bearer access-token');
  assert.equal(request.requestHeaders.get('accept-language'), 'en-US');
  assert.equal(request.requestHeaders.get('accept'), 'application/json');
  assert.equal(request.requestHeaders.has('content-type'), false);
});

test('400 MessageModel 应保留结构化错误且不得触发重试', async () => {
  harness.config.translateMessage = (key: string) => (
    key === 'errors.attachment.invalid_file' ? 'Unsupported image type' : defaultTranslations[key]
  );
  MockXmlHttpRequest.scenarios = [{
    event: 'load',
    status: 400,
    responseText: messageModel({
      success: false,
      status: 400,
      message: 'server fallback',
      messageKey: 'errors.attachment.invalid_file',
      code: 'ATTACHMENT_INVALID_FILE',
    }),
    responseHeaders: { 'X-Correlation-ID': 'correlation-42' },
  }];

  await assert.rejects(
    attachmentApi.uploadImage({ file: new File(['bad'], 'bad.exe') }, t),
    (error: unknown) => {
      assert.ok(error instanceof TestApiResponseError);
      assert.equal(error.message, 'Unsupported image type');
      assert.equal(error.code, 'ATTACHMENT_INVALID_FILE');
      assert.equal(error.messageKey, 'errors.attachment.invalid_file');
      assert.equal(error.messageInfo, 'server fallback');
      assert.equal(error.statusCode, 400);
      assert.equal(error.httpStatus, 400);
      assert.equal(error.traceId, 'correlation-42');
      return true;
    },
  );
  assert.equal(MockXmlHttpRequest.instances.length, 1);
});

test('网络、5xx、4xx 与非 JSON 失败均只提交一次并交由用户手动重试', async () => {
  MockXmlHttpRequest.scenarios = [{ event: 'error' }];
  await assert.rejects(
    attachmentApi.uploadImage({ file: new File(['network'], 'network.png') }, t),
    (error: unknown) => error instanceof Error && error.message === 'Network request failed',
  );
  assert.equal(MockXmlHttpRequest.instances.length, 1);
  assert.equal(MockXmlHttpRequest.instances[0]?.sendCount, 1);

  MockXmlHttpRequest.scenarios = [{
    event: 'load',
    status: 503,
    responseText: messageModel({ success: false, status: 503, message: 'temporarily unavailable' }),
  }];
  MockXmlHttpRequest.instances = [];
  await assert.rejects(
    attachmentApi.uploadImage({ file: new File(['server'], 'server.png') }, t),
    (error: unknown) => error instanceof TestApiResponseError && error.httpStatus === 503,
  );
  assert.equal(MockXmlHttpRequest.instances.length, 1);
  assert.equal(MockXmlHttpRequest.instances[0]?.sendCount, 1);

  MockXmlHttpRequest.scenarios = [{ event: 'load', status: 400, responseText: '<html>bad request</html>' }];
  MockXmlHttpRequest.instances = [];
  await assert.rejects(
    attachmentApi.uploadImage({ file: new File(['bad'], 'bad.png') }, t),
    (error: unknown) => {
      assert.ok(error instanceof TestApiResponseError);
      assert.equal(error.message, 'The server returned an invalid response');
      assert.equal(error.httpStatus, 400);
      return true;
    },
  );
  assert.equal(MockXmlHttpRequest.instances.length, 1);
  assert.equal(MockXmlHttpRequest.instances[0]?.sendCount, 1);
});

test('上传应保留动态消息参数并交给本地化函数格式化', async () => {
  MockXmlHttpRequest.scenarios = [{
    event: 'load',
    status: 413,
    responseText: messageModel({
      success: false,
      status: 413,
      message: 'server fallback',
      messageKey: 'error.attachment.file_too_large',
      messageArguments: ['5 MB'],
      code: 'Attachment.FileTooLarge',
    }),
  }];

  await assert.rejects(
    attachmentApi.uploadImage({ file: new File(['large'], 'large.png') }, t),
    (error: unknown) => {
      assert.ok(error instanceof TestApiResponseError);
      assert.equal(error.message, 'The selected file exceeds the 5 MB limit.');
      return true;
    },
  );
  assert.equal(MockXmlHttpRequest.instances.length, 1);
});

test('AbortSignal 在发送前与发送中都应取消上传且不产生重试', async () => {
  const preAborted = new AbortController();
  preAborted.abort();
  MockXmlHttpRequest.scenarios = [{ event: 'pending' }];

  await assert.rejects(
    attachmentApi.uploadImage({
      file: new File(['pre'], 'pre.png'),
      signal: preAborted.signal,
    }, t),
    (error: unknown) => error instanceof Error && error.message === 'Upload cancelled',
  );
  assert.equal(MockXmlHttpRequest.instances.length, 1);
  assert.equal(MockXmlHttpRequest.instances[0]?.sendCount, 0);

  const active = new AbortController();
  MockXmlHttpRequest.scenarios = [{ event: 'pending' }];
  MockXmlHttpRequest.instances = [];
  const uploadPromise = attachmentApi.uploadImage({
    file: new File(['active'], 'active.png'),
    signal: active.signal,
  }, t);
  active.abort();

  await assert.rejects(
    uploadPromise,
    (error: unknown) => error instanceof Error && error.message === 'Upload cancelled',
  );
  assert.equal(MockXmlHttpRequest.instances.length, 1);
  assert.equal(MockXmlHttpRequest.instances[0]?.sendCount, 1);
});

test('取消上传应使用调用方本地化 fallback 且不重试', async () => {
  MockXmlHttpRequest.scenarios = [{ event: 'abort' }];

  await assert.rejects(
    attachmentApi.uploadImage({ file: new File(['cancel'], 'cancel.png') }, t),
    (error: unknown) => {
      assert.ok(error instanceof Error);
      assert.equal(error.message, 'Upload cancelled');
      return true;
    },
  );
  assert.equal(MockXmlHttpRequest.instances.length, 1);
});

test('附件列表和删除 API 应将解析后的响应直接提升为 ApiResponseError', async () => {
  harness.apiGet = async () => ({
    ok: false,
    message: 'server list fallback',
    messageKey: 'errors.attachment.list_denied',
    code: 'ATTACHMENT_LIST_DENIED',
    statusCode: 403,
    httpStatus: 403,
    traceId: 'trace-list',
  });

  await assert.rejects(attachmentApi.getMyAttachments(), (error: unknown) => {
    assert.ok(error instanceof TestApiResponseError);
    assert.equal(error.code, 'ATTACHMENT_LIST_DENIED');
    assert.equal(error.httpStatus, 403);
    assert.equal(error.traceId, 'trace-list');
    return true;
  });

  harness.apiDelete = async () => ({
    ok: false,
    message: 'server delete fallback',
    code: 'ATTACHMENT_DELETE_DENIED',
    statusCode: 409,
    httpStatus: 409,
  });
  await assert.rejects(attachmentApi.deleteAttachment('42', t), (error: unknown) => {
    assert.ok(error instanceof TestApiResponseError);
    assert.equal(error.code, 'ATTACHMENT_DELETE_DENIED');
    assert.equal(error.httpStatus, 409);
    return true;
  });
});

test('头像绑定应复用统一客户端并保留结构化错误与取消信号', async () => {
  const controller = new AbortController();
  let requestArguments: unknown[] = [];
  harness.apiPost = async (...args: unknown[]) => {
    requestArguments = args;
    return { ok: true };
  };

  await attachmentApi.setMyAvatar('9223372036854775001', t, controller.signal);

  assert.equal(requestArguments[0], '/api/v1/User/SetMyAvatar');
  assert.deepEqual(requestArguments[1], { attachmentId: '9223372036854775001' });
  assert.deepEqual(requestArguments[2], { withAuth: true, signal: controller.signal });

  harness.apiPost = async () => ({
    ok: false,
    message: 'You cannot use this attachment as your avatar.',
    messageInfo: '无权设置该附件为头像',
    messageKey: 'error.attachment.upload_forbidden',
    code: 'Attachment.UploadForbidden',
    statusCode: 403,
    httpStatus: 403,
    traceId: 'trace-avatar',
  });

  await assert.rejects(attachmentApi.setMyAvatar(null, t), (error: unknown) => {
    assert.ok(error instanceof TestApiResponseError);
    assert.equal(error.code, 'Attachment.UploadForbidden');
    assert.equal(error.messageKey, 'error.attachment.upload_forbidden');
    assert.equal(error.httpStatus, 403);
    assert.equal(error.traceId, 'trace-avatar');
    return true;
  });
});
