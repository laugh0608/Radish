import {
  createApiResponseError,
  getApiClientConfig,
  parseApiResponse,
  type ApiClientConfig,
  type ApiResponse,
  type ParsedApiResponse,
} from '@radish/http';
import { buildAttachmentAssetUrl } from '@radish/ui/attachment-markdown';

export const consoleAttachmentBusinessTypes = [
  'CategoryCover',
  'CategoryIcon',
  'ProductCover',
  'ProductIcon',
  'SiteFavicon',
  'Sticker',
  'StickerCover',
] as const;

export type ConsoleAttachmentBusinessType = typeof consoleAttachmentBusinessTypes[number];

export interface UploadedAttachmentImage {
  attachmentId: string;
  url?: string;
  thumbnailUrl?: string;
}

export interface UploadAttachmentImageOptions {
  businessType: ConsoleAttachmentBusinessType;
  generateThumbnail?: boolean;
  removeExif?: boolean;
  signal?: AbortSignal;
}

const consoleAttachmentBusinessTypeSet = new Set<string>(consoleAttachmentBusinessTypes);

function toStringOrUndefined(value: unknown): string | undefined {
  if (typeof value === 'string' && value.trim()) {
    return value.trim();
  }

  return undefined;
}

function toNumberOrUndefined(value: unknown): number | undefined {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === 'string' && /^\d+$/.test(value.trim())) {
    return Number(value.trim());
  }

  return undefined;
}

function toAttachmentId(value: unknown): string | undefined {
  if (typeof value === 'string' && /^[1-9]\d*$/.test(value.trim())) {
    return value.trim();
  }

  if (typeof value === 'number' && Number.isSafeInteger(value) && value > 0) {
    return String(value);
  }

  return undefined;
}

function readEnvelopeValue(
  payload: Record<string, unknown>,
  camelCaseName: string,
  pascalCaseName: string,
): unknown {
  return payload[camelCaseName] ?? payload[pascalCaseName];
}

function resolveLocalizedMessage(
  config: Readonly<ApiClientConfig>,
  key: string,
  messageArguments?: readonly unknown[],
): string | undefined {
  const translated = config.translateMessage?.(key, messageArguments);
  return translated && translated !== key ? translated : undefined;
}

function localizeMessage(
  config: Readonly<ApiClientConfig>,
  key: string,
  messageArguments?: readonly unknown[],
): string {
  return resolveLocalizedMessage(config, key, messageArguments) ?? key;
}

function createLocalUploadError(
  config: Readonly<ApiClientConfig>,
  messageKey: string,
  options: {
    code: string;
    httpStatus?: number;
    traceId?: string;
  },
) {
  return createApiResponseError({
    ok: false,
    code: options.code,
    messageKey,
    statusCode: options.httpStatus,
    httpStatus: options.httpStatus,
    traceId: options.traceId,
    message: localizeMessage(config, messageKey),
  }, messageKey);
}

function isConsoleAttachmentBusinessType(value: string): value is ConsoleAttachmentBusinessType {
  return consoleAttachmentBusinessTypeSet.has(value);
}

function getXhrStatus(xhr: XMLHttpRequest): number | undefined {
  return xhr.status > 0 ? xhr.status : undefined;
}

function getXhrTraceId(xhr: XMLHttpRequest): string | undefined {
  return xhr.getResponseHeader('x-correlation-id')?.trim() || undefined;
}

function normalizeMessageEnvelope<T>(value: unknown): ApiResponse<T> | null {
  if (typeof value !== 'object' || value === null) {
    return null;
  }

  const payload = value as Record<string, unknown>;
  const isSuccess = readEnvelopeValue(payload, 'isSuccess', 'IsSuccess');
  if (typeof isSuccess !== 'boolean') {
    return null;
  }

  const messageArguments = readEnvelopeValue(payload, 'messageArguments', 'MessageArguments');
  const statusCode = toNumberOrUndefined(
    readEnvelopeValue(payload, 'statusCode', 'StatusCode')
      ?? readEnvelopeValue(payload, 'status', 'Status'),
  );

  return {
    isSuccess,
    statusCode: statusCode ?? 0,
    messageInfo: toStringOrUndefined(readEnvelopeValue(payload, 'messageInfo', 'MessageInfo')) ?? '',
    responseData: readEnvelopeValue(payload, 'responseData', 'ResponseData') as T | undefined,
    code: toStringOrUndefined(readEnvelopeValue(payload, 'code', 'Code')),
    messageKey: toStringOrUndefined(readEnvelopeValue(payload, 'messageKey', 'MessageKey')),
    messageArguments: Array.isArray(messageArguments) ? messageArguments : undefined,
    traceId: toStringOrUndefined(readEnvelopeValue(payload, 'traceId', 'TraceId')),
  };
}

function parseXhrResponse<T>(
  xhr: XMLHttpRequest,
  config: Readonly<ApiClientConfig>,
): ParsedApiResponse<T> | null {
  let value: unknown;
  try {
    value = JSON.parse(xhr.responseText) as unknown;
  } catch {
    return null;
  }

  const envelope = normalizeMessageEnvelope<T>(value);
  if (!envelope) {
    return null;
  }

  const parsed = parseApiResponse(envelope);
  const localizedMessage = parsed.messageKey
    ? resolveLocalizedMessage(config, parsed.messageKey, parsed.messageArguments)
    : undefined;

  return {
    ...parsed,
    message: localizedMessage ?? (envelope.messageInfo ? parsed.message : undefined),
    statusCode: parsed.statusCode || getXhrStatus(xhr),
    httpStatus: getXhrStatus(xhr),
    traceId: parsed.traceId || getXhrTraceId(xhr),
  };
}

export function uploadAttachmentImage(
  file: File,
  options: UploadAttachmentImageOptions,
  onProgress?: (percent: number) => void,
): Promise<UploadedAttachmentImage> {
  const config = getApiClientConfig();
  const normalizedBaseUrl = (config.baseUrl || '').trim().replace(/\/+$/, '');
  if (!normalizedBaseUrl) {
    return Promise.reject(createLocalUploadError(config, 'attachment.api.baseUrlMissing', {
      code: 'ATTACHMENT_API_BASE_URL_MISSING',
    }));
  }

  const businessType = options.businessType.trim();
  if (!isConsoleAttachmentBusinessType(businessType)) {
    return Promise.reject(createLocalUploadError(config, 'error.attachment.business_type_unsupported', {
      code: 'Attachment.BusinessTypeUnsupported',
    }));
  }

  if (options.signal?.aborted) {
    return Promise.reject(createLocalUploadError(config, 'attachment.api.uploadCancelled', {
      code: 'ATTACHMENT_UPLOAD_CANCELLED',
    }));
  }

  return new Promise((resolve, reject) => {
    const uploadUrl = `${normalizedBaseUrl}/api/v1/Attachment/UploadImage`;
    const xhr = new XMLHttpRequest();
    let settled = false;

    const cleanup = () => {
      options.signal?.removeEventListener('abort', handleSignalAbort);
    };
    const resolveOnce = (result: UploadedAttachmentImage) => {
      if (settled) {
        return;
      }

      settled = true;
      cleanup();
      resolve(result);
    };
    const rejectOnce = (error: Error) => {
      if (settled) {
        return;
      }

      settled = true;
      cleanup();
      reject(error);
    };
    function handleSignalAbort() {
      xhr.abort();
      rejectOnce(createLocalUploadError(config, 'attachment.api.uploadCancelled', {
        code: 'ATTACHMENT_UPLOAD_CANCELLED',
      }));
    }

    options.signal?.addEventListener('abort', handleSignalAbort, { once: true });

    xhr.upload.onprogress = (event) => {
      if (!event.lengthComputable || !onProgress) {
        return;
      }

      const percent = Math.round((event.loaded / event.total) * 100);
      onProgress(Math.min(100, Math.max(0, percent)));
    };

    xhr.onerror = () => {
      rejectOnce(createLocalUploadError(config, 'attachment.api.networkError', {
        code: 'ATTACHMENT_UPLOAD_NETWORK_ERROR',
      }));
    };

    xhr.ontimeout = () => {
      rejectOnce(createLocalUploadError(config, 'attachment.api.networkError', {
        code: 'ATTACHMENT_UPLOAD_NETWORK_ERROR',
      }));
    };

    xhr.onabort = () => {
      rejectOnce(createLocalUploadError(config, 'attachment.api.uploadCancelled', {
        code: 'ATTACHMENT_UPLOAD_CANCELLED',
      }));
    };

    xhr.onload = () => {
      const parsed = parseXhrResponse<Record<string, unknown>>(xhr, config);
      if (!parsed) {
        rejectOnce(createLocalUploadError(config, 'attachment.api.invalidResponse', {
          code: 'ATTACHMENT_UPLOAD_INVALID_RESPONSE',
          httpStatus: getXhrStatus(xhr),
          traceId: getXhrTraceId(xhr),
        }));
        return;
      }

      if (xhr.status < 200 || xhr.status >= 300 || !parsed.ok) {
        rejectOnce(createApiResponseError(
          xhr.status >= 200 && xhr.status < 300 ? parsed : { ...parsed, ok: false },
          localizeMessage(config, 'attachment.api.imageUploadFailed'),
        ));
        return;
      }

      const responseData = parsed.data;
      const attachmentId = responseData
        ? toAttachmentId(
          responseData.voId
          ?? responseData.VoId
          ?? responseData.id
          ?? responseData.Id,
        )
        : undefined;
      if (!responseData || !attachmentId) {
        rejectOnce(createLocalUploadError(config, 'attachment.api.missingAttachmentData', {
          code: 'ATTACHMENT_UPLOAD_DATA_MISSING',
          httpStatus: getXhrStatus(xhr),
          traceId: parsed.traceId,
        }));
        return;
      }

      resolveOnce({
        attachmentId,
        url: toStringOrUndefined(
          responseData.voUrl
          ?? responseData.VoUrl
          ?? responseData.url
          ?? responseData.Url,
        ) ?? buildAttachmentAssetUrl(attachmentId, 'original'),
        thumbnailUrl: toStringOrUndefined(
          responseData.voThumbnailUrl
          ?? responseData.VoThumbnailUrl
          ?? responseData.thumbnailUrl
          ?? responseData.ThumbnailUrl,
        ),
      });
    };

    const formData = new FormData();
    formData.append('file', file);
    formData.append('businessType', businessType);
    formData.append('generateThumbnail', String(options.generateThumbnail ?? true));
    formData.append('removeExif', String(options.removeExif ?? true));

    try {
      xhr.open('POST', uploadUrl, true);
      xhr.timeout = config.timeout;
      xhr.setRequestHeader('Accept', 'application/json');

      const language = config.getLanguage?.()?.trim();
      if (language) {
        xhr.setRequestHeader('Accept-Language', language);
      }

      const token = config.getToken?.();
      if (token) {
        xhr.setRequestHeader('Authorization', `Bearer ${token}`);
      }

      xhr.send(formData);
    } catch {
      rejectOnce(createLocalUploadError(config, 'attachment.api.networkError', {
        code: 'ATTACHMENT_UPLOAD_NETWORK_ERROR',
      }));
    }
  });
}
