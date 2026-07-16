import assert from 'node:assert/strict';
import path from 'node:path';
import test, { after, beforeEach } from 'node:test';
import { fileURLToPath } from 'node:url';
import { rolldown } from 'rolldown';

interface ParsedResponse<T = unknown> {
  ok: boolean;
  data?: T;
  message?: string;
  code?: string;
  statusCode?: number;
  httpStatus?: number;
  traceId?: string;
}

interface XhrScenario {
  event: 'load' | 'error' | 'abort' | 'timeout' | 'pending';
  status?: number;
  responseText?: string;
}

class MockUploadTarget {
  addEventListener(): void {
    // 本组只验证请求生命周期，不模拟进度事件。
  }
}

class MockXmlHttpRequest {
  static scenarios: XhrScenario[] = [];
  static instances: MockXmlHttpRequest[] = [];

  readonly upload = new MockUploadTarget();
  readonly listeners = new Map<string, () => void>();
  readonly requestHeaders = new Map<string, string>();
  readonly scenario: XhrScenario;
  status = 0;
  responseText = '';
  timeout = 0;
  abortCount = 0;

  constructor() {
    const scenario = MockXmlHttpRequest.scenarios.shift();
    if (!scenario) throw new Error('Missing XMLHttpRequest scenario');
    this.scenario = scenario;
    this.status = scenario.status ?? 0;
    this.responseText = scenario.responseText ?? '';
    MockXmlHttpRequest.instances.push(this);
  }

  addEventListener(type: string, listener: () => void): void {
    this.listeners.set(type, listener);
  }

  open(): void {
    // URL 与 method 不是本组断言目标。
  }

  setRequestHeader(name: string, value: string): void {
    this.requestHeaders.set(name.toLowerCase(), value);
  }

  getResponseHeader(): string | null {
    return null;
  }

  send(): void {
    if (this.scenario.event !== 'pending') {
      queueMicrotask(() => this.listeners.get(this.scenario.event)?.());
    }
  }

  abort(): void {
    this.abortCount += 1;
    queueMicrotask(() => this.listeners.get('abort')?.());
  }
}

const harness = {
  XMLHttpRequest: MockXmlHttpRequest,
  config: {
    baseUrl: 'https://gateway.example',
    timeout: 4321,
    getToken: () => 'access-token',
    getLanguage: () => 'zh-CN',
    translateMessage: (key: string) => key,
  },
  apiGet: async (): Promise<ParsedResponse> => ({ ok: true, data: {} }),
  apiPost: async (): Promise<ParsedResponse> => ({ ok: true, data: {} }),
};

Object.assign(globalThis, {
  __RADISH_CHUNKED_UPLOAD_TEST_HARNESS__: harness,
});

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const entry = path.resolve(testDirectory, '../src/api/chunked-upload.ts');
const bundle = await rolldown({
  input: entry,
  plugins: [{
    name: 'chunked-upload-api-test-harness',
    transform(code, id) {
      if (id !== entry) return null;

      const transformedCode = code.replaceAll(
        'new XMLHttpRequest()',
        'new (globalThis.__RADISH_CHUNKED_UPLOAD_TEST_HARNESS__.XMLHttpRequest)()',
      );
      assert.notEqual(transformedCode, code, '未能为分片上传测试注入独立 XMLHttpRequest');
      return transformedCode;
    },
    resolveId(source) {
      if (source === '@radish/http') return '\0chunked-http';
      if (source === '@/config/env') return '\0chunked-env';
      if (source === '@/utils/longId') return '\0chunked-long-id';
      return null;
    },
    load(id) {
      if (id === '\0chunked-http') {
        return `
          const harness = globalThis.__RADISH_CHUNKED_UPLOAD_TEST_HARNESS__;
          export const apiGet = (...args) => harness.apiGet(...args);
          export const apiPost = (...args) => harness.apiPost(...args);
          export const configureApiClient = (next) => Object.assign(harness.config, next);
          export const getApiClientConfig = () => harness.config;
          export const createApiResponseError = (response, fallback) => {
            const error = new Error(response.message || fallback);
            Object.assign(error, response);
            return error;
          };
          export const parseApiResponseWithI18n = (response, translate) => response.isSuccess
            ? { ok: true, data: response.responseData, statusCode: response.statusCode }
            : {
                ok: false,
                message: response.messageKey
                  ? translate(response.messageKey, response.messageArguments)
                  : response.messageInfo,
                code: response.code,
                statusCode: response.statusCode,
              };
        `;
      }
      if (id === '\0chunked-env') {
        return `
          export const getApiBaseUrl = () => (
            globalThis.__RADISH_CHUNKED_UPLOAD_TEST_HARNESS__.config.baseUrl
          );
        `;
      }
      if (id === '\0chunked-long-id') {
        return `
          export const normalizePositiveLongIdKey = (value) => {
            if (typeof value === 'number') {
              return Number.isSafeInteger(value) && value > 0 ? String(value) : null;
            }
            if (typeof value !== 'string') return null;
            const trimmed = value.trim();
            return /^[1-9]\\d*$/.test(trimmed) ? trimmed : null;
          };
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
const chunkedApi = await import(
  `data:text/javascript;base64,${Buffer.from(chunk.code).toString('base64')}`
) as {
  createSession: (options: Record<string, unknown>) => Promise<Record<string, unknown>>;
  mergeChunks: (options: Record<string, unknown>) => Promise<Record<string, unknown>>;
  uploadChunk: (
    sessionId: string,
    chunkIndex: number,
    chunkBlob: Blob,
    onProgress?: (progress: number) => void,
    signal?: AbortSignal,
  ) => Promise<Record<string, unknown>>;
};

beforeEach(() => {
  MockXmlHttpRequest.scenarios = [];
  MockXmlHttpRequest.instances = [];
  harness.apiGet = async () => ({ ok: true, data: {} });
  harness.apiPost = async () => ({ ok: true, data: {} });
});

after(() => {
  Reflect.deleteProperty(globalThis, '__RADISH_CHUNKED_UPLOAD_TEST_HARNESS__');
});

test('raw UploadSessionVo 与 AttachmentVo 应映射为稳定领域模型和 LongId 字符串', async () => {
  harness.apiPost = async (url?: unknown) => String(url).includes('CreateSession')
    ? {
        ok: true,
        data: {
          voSessionId: '0123456789abcdef0123456789abcdef',
          voFileName: 'large.bin',
          voTotalSize: '4096',
          voChunkSize: 2048,
          voTotalChunks: 2,
          voUploadedChunks: 0,
          voUploadedChunkIndexes: [],
          voProgress: 0,
          voStatus: 'Uploading',
          voAttachmentId: '9223372036854775000',
          voExpiresAt: '2026-07-17T08:00:00Z',
          voCreateTime: '2026-07-16T08:00:00Z',
        },
      }
    : {
        ok: true,
        data: {
          voId: '9223372036854775001',
          voOriginalName: 'large.bin',
          voFileSize: '4096',
          voUrl: '/api/v1/Attachment/Download/9223372036854775001',
        },
      };

  const session = await chunkedApi.createSession({ fileName: 'large.bin', totalSize: 4096 });
  const attachment = await chunkedApi.mergeChunks({ sessionId: session.sessionId });

  assert.equal(session.totalSize, 4096);
  assert.equal(session.attachmentId, '9223372036854775000');
  assert.deepEqual(attachment, {
    attachmentId: '9223372036854775001',
    fileName: 'large.bin',
    fileSize: 4096,
    accessUrl: '/api/v1/Attachment/Download/9223372036854775001',
  });
});

test('XHR timeout 必须 settle 为拒绝并沿用统一超时配置', async () => {
  MockXmlHttpRequest.scenarios = [{ event: 'timeout' }];

  await assert.rejects(
    chunkedApi.uploadChunk('0123456789abcdef0123456789abcdef', 0, new Blob(['part'])),
    /上传分片超时/,
  );

  assert.equal(MockXmlHttpRequest.instances[0]?.timeout, 4321);
});

test('AbortSignal 必须中止 XHR 并 settle 为拒绝', async () => {
  MockXmlHttpRequest.scenarios = [{ event: 'pending' }];
  const controller = new AbortController();
  const uploadPromise = chunkedApi.uploadChunk(
    '0123456789abcdef0123456789abcdef',
    0,
    new Blob(['part']),
    undefined,
    controller.signal,
  );

  controller.abort();

  await assert.rejects(uploadPromise, /上传已取消/);
  assert.equal(MockXmlHttpRequest.instances[0]?.abortCount, 1);
});
