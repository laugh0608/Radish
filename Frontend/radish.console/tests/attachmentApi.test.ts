import assert from 'node:assert/strict';
import { registerHooks } from 'node:module';
import test from 'node:test';

registerHooks({
  resolve(specifier, context, nextResolve) {
    try {
      return nextResolve(specifier, context);
    } catch (error) {
      const isExtensionlessRelativeImport = specifier.startsWith('.')
        && !/\.[a-z\d]+$/i.test(specifier);
      if (!isExtensionlessRelativeImport) {
        throw error;
      }

      return nextResolve(`${specifier}.ts`, context);
    }
  },
});

const {
  ApiResponseError,
  configureApiClient,
} = await import('@radish/http');
const { uploadAttachmentImage } = await import('../src/api/attachmentApi.ts');

class MockXmlHttpRequest {
  static instances: MockXmlHttpRequest[] = [];

  readonly headers = new Map<string, string>();
  readonly responseHeaders = new Map<string, string>();
  readonly upload: { onprogress: ((event: ProgressEvent) => void) | null } = { onprogress: null };

  method = '';
  url = '';
  async = true;
  timeout = 0;
  status = 0;
  responseText = '';
  sendCount = 0;
  abortCount = 0;
  requestBody: Document | XMLHttpRequestBodyInit | null = null;
  onerror: ((event: ProgressEvent) => void) | null = null;
  ontimeout: ((event: ProgressEvent) => void) | null = null;
  onabort: ((event: ProgressEvent) => void) | null = null;
  onload: ((event: ProgressEvent) => void) | null = null;

  constructor() {
    MockXmlHttpRequest.instances.push(this);
  }

  open(method: string, url: string, async = true) {
    this.method = method;
    this.url = url;
    this.async = async;
  }

  setRequestHeader(name: string, value: string) {
    this.headers.set(name.toLowerCase(), value);
  }

  getResponseHeader(name: string): string | null {
    return this.responseHeaders.get(name.toLowerCase()) ?? null;
  }

  send(body: Document | XMLHttpRequestBodyInit | null) {
    this.sendCount += 1;
    this.requestBody = body;
  }

  abort() {
    this.abortCount += 1;
    this.onabort?.({} as ProgressEvent);
  }

  respond(status: number, body: unknown, headers: Record<string, string> = {}) {
    this.status = status;
    this.responseText = typeof body === 'string' ? body : JSON.stringify(body);
    Object.entries(headers).forEach(([name, value]) => {
      this.responseHeaders.set(name.toLowerCase(), value);
    });
    this.onload?.({} as ProgressEvent);
  }

  failNetwork() {
    this.onerror?.({} as ProgressEvent);
  }
}

const originalXmlHttpRequest = globalThis.XMLHttpRequest;

test.beforeEach(() => {
  MockXmlHttpRequest.instances.length = 0;
  Object.defineProperty(globalThis, 'XMLHttpRequest', {
    configurable: true,
    value: MockXmlHttpRequest,
    writable: true,
  });
});

test.after(() => {
  Object.defineProperty(globalThis, 'XMLHttpRequest', {
    configurable: true,
    value: originalXmlHttpRequest,
    writable: true,
  });
});

function createImageFile(): File {
  return new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'icon.png', {
    type: 'image/png',
  });
}

function configureEnglishClient() {
  configureApiClient({
    baseUrl: 'https://radish.test/',
    timeout: 12_000,
    getToken: () => 'console-token',
    getLanguage: () => 'en-US',
    translateMessage: (key, messageArguments) => {
      if (key === 'error.attachment.upload_frequency_limit_reached') {
        return `Uploaded ${messageArguments?.[0]} of ${messageArguments?.[1]} files this minute.`;
      }

      const messages: Record<string, string> = {
        'attachment.api.imageUploadFailed': 'Failed to upload the image',
        'attachment.api.invalidResponse': 'Unable to parse the upload response',
        'attachment.api.missingAttachmentData': 'The upload succeeded but no attachment data was returned',
        'attachment.api.networkError': 'A network error interrupted the upload',
        'attachment.api.uploadCancelled': 'Upload cancelled',
        'error.attachment.business_type_unsupported': 'This attachment business type is not supported.',
      };
      return messages[key];
    },
  });
}

test('Console 图片上传应发送统一请求头并解析成功附件数据', async () => {
  configureEnglishClient();

  const upload = uploadAttachmentImage(createImageFile(), {
    businessType: 'ProductIcon',
  });
  const xhr = MockXmlHttpRequest.instances[0];
  assert.ok(xhr);
  assert.equal(xhr.method, 'POST');
  assert.equal(xhr.url, 'https://radish.test/api/v1/Attachment/UploadImage');
  assert.equal(xhr.headers.get('accept'), 'application/json');
  assert.equal(xhr.headers.get('accept-language'), 'en-US');
  assert.equal(xhr.headers.get('authorization'), 'Bearer console-token');
  assert.equal(xhr.timeout, 12_000);
  assert.equal(xhr.sendCount, 1);

  xhr.respond(200, {
    IsSuccess: true,
    StatusCode: 200,
    MessageInfo: '上传成功',
    ResponseData: {
      VoId: '9007199254740993',
      VoUrl: '/uploads/icon.png',
    },
  });

  assert.deepEqual(await upload, {
    attachmentId: '9007199254740993',
    url: '/uploads/icon.png',
    thumbnailUrl: undefined,
  });
});

test('Console 图片上传应本地化英文动态参数并保留结构化错误字段', async () => {
  configureEnglishClient();

  const upload = uploadAttachmentImage(createImageFile(), {
    businessType: 'Sticker',
  });
  const xhr = MockXmlHttpRequest.instances[0];
  assert.ok(xhr);
  xhr.respond(429, {
    IsSuccess: false,
    Status: 429,
    MessageInfo: '本分钟上传次数已达上限',
    Code: 'Attachment.UploadFrequencyLimitReached',
    MessageKey: 'error.attachment.upload_frequency_limit_reached',
    MessageArguments: [5, 5],
    TraceId: 'trace-upload-1',
  });

  await assert.rejects(upload, (error: unknown) => {
    assert.ok(error instanceof ApiResponseError);
    assert.equal(error.message, 'Uploaded 5 of 5 files this minute.');
    assert.equal(error.code, 'Attachment.UploadFrequencyLimitReached');
    assert.equal(error.messageKey, 'error.attachment.upload_frequency_limit_reached');
    assert.deepEqual(error.messageArguments, [5, 5]);
    assert.equal(error.messageInfo, '本分钟上传次数已达上限');
    assert.equal(error.statusCode, 429);
    assert.equal(error.httpStatus, 429);
    assert.equal(error.traceId, 'trace-upload-1');
    return true;
  });
});

test('AbortSignal 应取消当前上传并返回稳定的结构化取消错误', async () => {
  configureEnglishClient();
  const controller = new AbortController();

  const upload = uploadAttachmentImage(createImageFile(), {
    businessType: 'SiteFavicon',
    signal: controller.signal,
  });
  const xhr = MockXmlHttpRequest.instances[0];
  assert.ok(xhr);
  controller.abort();

  await assert.rejects(upload, (error: unknown) => {
    assert.ok(error instanceof ApiResponseError);
    assert.equal(error.message, 'Upload cancelled');
    assert.equal(error.code, 'ATTACHMENT_UPLOAD_CANCELLED');
    assert.equal(error.messageKey, 'attachment.api.uploadCancelled');
    return true;
  });
  assert.equal(xhr.abortCount, 1);
  assert.equal(xhr.sendCount, 1);
  assert.equal(MockXmlHttpRequest.instances.length, 1);
});

test('网络失败应保持单次请求且不自动重试', async () => {
  configureEnglishClient();

  const upload = uploadAttachmentImage(createImageFile(), {
    businessType: 'CategoryCover',
  });
  const xhr = MockXmlHttpRequest.instances[0];
  assert.ok(xhr);
  xhr.failNetwork();

  await assert.rejects(upload, (error: unknown) => {
    assert.ok(error instanceof ApiResponseError);
    assert.equal(error.message, 'A network error interrupted the upload');
    assert.equal(error.code, 'ATTACHMENT_UPLOAD_NETWORK_ERROR');
    return true;
  });
  assert.equal(xhr.sendCount, 1);
  assert.equal(MockXmlHttpRequest.instances.length, 1);
});

test('不安全的 number 型附件 ID 应被拒绝以避免 LongId 精度丢失', async () => {
  configureEnglishClient();

  const upload = uploadAttachmentImage(createImageFile(), {
    businessType: 'ProductCover',
  });
  const xhr = MockXmlHttpRequest.instances[0];
  assert.ok(xhr);
  xhr.respond(200, {
    IsSuccess: true,
    StatusCode: 200,
    ResponseData: {
      VoId: 9_007_199_254_740_992,
      VoUrl: '/uploads/unsafe-id.png',
    },
  });

  await assert.rejects(upload, (error: unknown) => {
    assert.ok(error instanceof ApiResponseError);
    assert.equal(error.code, 'ATTACHMENT_UPLOAD_DATA_MISSING');
    assert.equal(error.message, 'The upload succeeded but no attachment data was returned');
    return true;
  });
});
