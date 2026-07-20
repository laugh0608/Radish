import { useState, useCallback, useRef } from 'react';
import type { TFunction } from 'i18next';
import type { UploadProgress, ChunkedUploadOptions, ChunkedUploadResult } from '../components/ChunkedFileUpload/ChunkedFileUpload';
import { runChunkUploadWorkers } from './chunkedUploadWorkers';

type ChunkedUploadBusinessType = NonNullable<ChunkedUploadOptions['businessType']>;

interface CreateChunkedUploadSessionOptions {
  fileName: string;
  totalSize: number;
  mimeType?: string;
  chunkSize?: number;
  businessType?: ChunkedUploadBusinessType;
}

interface ChunkedUploadSession {
  sessionId: string;
  uploadedChunks: number;
  uploadedChunkIndexes: number[];
}

interface MergeChunkedUploadOptions {
  sessionId: string;
  generateThumbnail?: boolean;
  generateMultipleSizes?: boolean;
  addWatermark?: boolean;
  watermarkText?: string;
  removeExif?: boolean;
}

interface ChunkedUploadAttachmentInfo {
  attachmentId: string;
  fileName: string;
  fileSize: number;
  accessUrl: string;
}

interface ChunkedUploadApi {
  createSession: (opts: CreateChunkedUploadSessionOptions) => Promise<ChunkedUploadSession>;
  uploadChunk: (
    sessionId: string,
    index: number,
    blob: Blob,
    onChunkProgress?: (progress: number) => void,
    signal?: AbortSignal,
  ) => Promise<void>;
  getSession: (sessionId: string) => Promise<ChunkedUploadSession>;
  mergeChunks: (opts: MergeChunkedUploadOptions) => Promise<ChunkedUploadAttachmentInfo>;
  cancelSession: (sessionId: string) => Promise<void>;
}

/**
 * 分片上传 Hook
 *
 * 封装分片上传的完整逻辑：文件切片、并发控制、进度跟踪与失败会话回收。
 */
export function useChunkedUpload(t: TFunction) {
  void t;
  const [uploading, setUploading] = useState(false);
  const abortControllerRef = useRef<AbortController | null>(null);
  const finalizingRef = useRef(false);
  const sessionIdRef = useRef<string | null>(null);
  const uploadedChunksRef = useRef<Set<number>>(new Set());

  /**
   * 执行分片上传
   */
  const upload = useCallback(
    async (
      file: File,
      options: ChunkedUploadOptions,
      onProgress: (progress: UploadProgress) => void,
      // API 函数由外部传入（避免循环依赖）
      api: ChunkedUploadApi
    ): Promise<ChunkedUploadResult> => {
      if (uploading) {
        throw new Error('上传已在进行中');
      }

      setUploading(true);
      finalizingRef.current = false;
      abortControllerRef.current = new AbortController();
      uploadedChunksRef.current.clear();

      const chunkSize = options.chunkSize || 2 * 1024 * 1024; // 默认 2MB
      const concurrency = Math.max(1, Math.floor(options.concurrency || 5));
      const totalChunks = Math.ceil(file.size / chunkSize);

      let uploadedBytes = 0;
      const startTime = Date.now();
      let lastUpdateTime = startTime;
      let lastUploadedBytes = 0;

      try {
        // 1. 创建上传会话
        const session = await api.createSession({
          fileName: file.name,
          totalSize: file.size,
          mimeType: file.type,
          chunkSize,
          businessType: options.businessType || 'General'
        });

        sessionIdRef.current = session.sessionId;

        // 尊重服务端返回的已上传索引；当前公开入口每次都会创建新会话，尚不承诺跨请求续传。
        if (session.uploadedChunkIndexes && Array.isArray(session.uploadedChunkIndexes)) {
          session.uploadedChunkIndexes.forEach((idx: number) => {
            uploadedChunksRef.current.add(idx);
          });
          uploadedBytes = session.uploadedChunkIndexes.reduce((total, chunkIndex) => {
            const chunkStart = chunkIndex * chunkSize;
            return total + Math.max(0, Math.min(chunkSize, file.size - chunkStart));
          }, 0);
        }

        // 2. 准备待上传的分片队列
        const pendingChunks: number[] = [];
        for (let i = 0; i < totalChunks; i++) {
          if (!uploadedChunksRef.current.has(i)) {
            pendingChunks.push(i);
          }
        }

        // 3. 并发上传分片
        const uploadChunk = async (chunkIndex: number) => {
          if (abortControllerRef.current?.signal.aborted) {
            throw new Error('上传已取消');
          }

          const start = chunkIndex * chunkSize;
          const end = Math.min(start + chunkSize, file.size);
          const chunkBlob = file.slice(start, end);

          const chunkUploadedBytes = { value: 0 };

          await api.uploadChunk(
            sessionIdRef.current!,
            chunkIndex,
            chunkBlob,
            (chunkProgress) => {
              // 更新当前分片的已上传字节数
              const previousBytes = chunkUploadedBytes.value;
              chunkUploadedBytes.value = Math.floor((chunkProgress / 100) * (end - start));
              uploadedBytes += chunkUploadedBytes.value - previousBytes;

              // 计算整体进度
              const now = Date.now();
              const timeSinceLastUpdate = (now - lastUpdateTime) / 1000;
              const bytesSinceLastUpdate = uploadedBytes - lastUploadedBytes;

              const percentage = (uploadedBytes / file.size) * 100;
              const speed = timeSinceLastUpdate > 0 ? bytesSinceLastUpdate / timeSinceLastUpdate : 0;
              const remainingBytes = file.size - uploadedBytes;
              const remainingTime = speed > 0 ? remainingBytes / speed : 0;

              onProgress({
                uploadedBytes,
                totalBytes: file.size,
                percentage: Math.min(percentage, 99), // 合并前不显示 100%
                speed,
                remainingTime,
                uploadedChunks: uploadedChunksRef.current.size,
                totalChunks,
                isFinalizing: false
              });

              lastUpdateTime = now;
              lastUploadedBytes = uploadedBytes;
            },
            abortControllerRef.current?.signal,
          );

          uploadedChunksRef.current.add(chunkIndex);
        };

        await runChunkUploadWorkers(
          pendingChunks,
          concurrency,
          () => abortControllerRef.current?.signal.aborted === true,
          uploadChunk,
        );

        if (abortControllerRef.current?.signal.aborted) {
          throw new Error('上传已取消');
        }

        // 4. 合并分片。服务端最终化开始后不能可靠撤销，不再暴露伪取消。
        finalizingRef.current = true;
        abortControllerRef.current = null;
        onProgress({
          uploadedBytes: file.size,
          totalBytes: file.size,
          percentage: 99,
          speed: 0,
          remainingTime: 0,
          uploadedChunks: totalChunks,
          totalChunks,
          isFinalizing: true
        });
        const attachmentInfo = await api.mergeChunks({
          sessionId: sessionIdRef.current!,
          generateThumbnail: options.generateThumbnail ?? true,
          generateMultipleSizes: options.generateMultipleSizes ?? false,
          addWatermark: options.addWatermark ?? false,
          watermarkText: options.watermarkText || 'Radish',
          removeExif: options.removeExif ?? true
        });

        // 5. 上传完成
        onProgress({
          uploadedBytes: file.size,
          totalBytes: file.size,
          percentage: 100,
          speed: 0,
          remainingTime: 0,
          uploadedChunks: totalChunks,
          totalChunks,
          isFinalizing: false
        });

        setUploading(false);
        finalizingRef.current = false;
        abortControllerRef.current = null;
        sessionIdRef.current = null;
        uploadedChunksRef.current.clear();

        return {
          attachmentId: attachmentInfo.attachmentId,
          fileName: attachmentInfo.fileName,
          fileSize: attachmentInfo.fileSize,
          accessUrl: attachmentInfo.accessUrl
        };
      } catch (error) {
        setUploading(false);
        finalizingRef.current = false;

        // 任何终止错误都回收服务端会话，避免旧会话与配额预留一直占用到自然过期。
        const sessionId = sessionIdRef.current;
        if (sessionId) {
          try {
            await api.cancelSession(sessionId);
          } catch {
            // 原始错误优先；服务端仍会通过会话过期清理兜底。
          }
        }

        sessionIdRef.current = null;
        uploadedChunksRef.current.clear();
        abortControllerRef.current = null;

        throw error;
      }
    },
    [uploading]
  );

  /**
   * 取消上传
   */
  const cancel = useCallback(() => {
    if (finalizingRef.current || !abortControllerRef.current) {
      return false;
    }

    abortControllerRef.current.abort();
    return true;
  }, []);

  return {
    upload,
    cancel,
    uploading
  };
}
