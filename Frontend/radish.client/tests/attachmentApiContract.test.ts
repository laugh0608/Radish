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
  event: 'load' | 'error' | 'abort' | 'timeout';
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
    queueMicrotask(() => this.listeners.get(this.scenario.event)?.());
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
};

const harness = {
  config: {
    baseUrl: 'https://gateway.example',
    timeout: 3456,
    getToken: () => 'access-token',
    getLanguage: () => 'en-US',
    translateMessage: (key: string) => defaultTranslations[key],
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
    code?: string;
    statusCode?: number;
    traceId?: string;
  }, translate: (key: string) => string): ParsedResponse<T> => {
    const localized = response.messageKey ? translate(response.messageKey) : undefined;
    return response.isSuccess
      ? {
          ok: true,
          data: response.responseData,
          messageInfo: response.messageInfo,
          messageKey: response.messageKey,
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
  uploadImage: (options: { file: File }, t: (key: string) => string) => Promise<unknown>;
  getMyAttachments: () => Promise<unknown>;
  deleteAttachment: (id: string, t: (key: string) => string) => Promise<void>;
};

function messageModel(options: {
  success: boolean;
  status: number;
  data?: unknown;
  message?: string;
  messageKey?: string;
  code?: string;
  traceId?: string;
}): string {
  return JSON.stringify({
    isSuccess: options.success,
    statusCode: options.status,
    responseData: options.data,
    messageInfo: options.message,
    messageKey: options.messageKey,
    code: options.code,
    traceId: options.traceId,
  });
}

function t(key: string): string {
  return defaultTranslations[key] ?? key;
}

async function withoutRetryDelay<T>(action: () => Promise<T>): Promise<T> {
  const originalSetTimeout = globalThis.setTimeout;
  globalThis.setTimeout = ((callback: (...args: unknown[]) => void) => {
    queueMicrotask(callback);
    return 0 as unknown as ReturnType<typeof setTimeout>;
  }) as typeof setTimeout;

  try {
    return await action();
  } finally {
    globalThis.setTimeout = originalSetTimeout;
  }
}

beforeEach(() => {
  MockXmlHttpRequest.scenarios = [];
  MockXmlHttpRequest.instances = [];
  Object.assign(harness.config, {
    baseUrl: 'https://gateway.example',
    timeout: 3456,
    getToken: () => 'access-token',
    getLanguage: () => 'en-US',
    translateMessage: (key: string) => defaultTranslations[key],
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

test('网络失败与明确可恢复 5xx 才重试，业务 4xx 和非 JSON 响应不重试', async () => {
  MockXmlHttpRequest.scenarios = [
    { event: 'error' },
    {
      event: 'load',
      status: 503,
      responseText: messageModel({ success: false, status: 503, message: 'temporarily unavailable' }),
    },
    {
      event: 'load',
      status: 200,
      responseText: messageModel({ success: true, status: 200, data: { voId: '99' } }),
    },
  ];

  const result = await withoutRetryDelay(() => attachmentApi.uploadImage({
    file: new File(['retry'], 'retry.png'),
  }, t));
  assert.deepEqual(result, { voId: '99' });
  assert.equal(MockXmlHttpRequest.instances.length, 3);

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
