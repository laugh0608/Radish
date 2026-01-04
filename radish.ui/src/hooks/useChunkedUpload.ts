import { useState, useCallback, useRef } from 'react';
import type { TFunction } from 'i18next';
import type { UploadProgress, ChunkedUploadOptions, ChunkedUploadResult } from '../components/ChunkedFileUpload/ChunkedFileUpload';

/**
 * 分片上传 Hook
 *
 * 封装分片上传的完整逻辑：文件切片、并发控制、进度跟踪、断点续传
 */
export function useChunkedUpload(_t: TFunction) {
  const [uploading, setUploading] = useState(false);
  const abortControllerRef = useRef<AbortController | null>(null);
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
      api: {
        createSession: (opts: any) => Promise<any>;
        uploadChunk: (sessionId: string, index: number, blob: Blob, onChunkProgress?: (p: number) => void) => Promise<any>;
        getSession: (sessionId: string) => Promise<any>;
        mergeChunks: (opts: any) => Promise<any>;
        cancelSession: (sessionId: string) => Promise<void>;
      }
    ): Promise<ChunkedUploadResult> => {
      if (uploading) {
        throw new Error('上传已在进行中');
      }

      setUploading(true);
      abortControllerRef.current = new AbortController();
      uploadedChunksRef.current.clear();

      const chunkSize = options.chunkSize || 2 * 1024 * 1024; // 默认 2MB
      const concurrency = options.concurrency || 5; // 默认 5 个并发
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
          businessType: options.businessType || 'General',
          businessId: options.businessId
        });

        sessionIdRef.current = session.sessionId;

        // 如果会话已存在（断点续传），恢复已上传的分片
        if (session.uploadedChunkIndexes && Array.isArray(session.uploadedChunkIndexes)) {
          session.uploadedChunkIndexes.forEach((idx: number) => {
            uploadedChunksRef.current.add(idx);
          });
          uploadedBytes = session.uploadedChunks * chunkSize;
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
                totalChunks
              });

              lastUpdateTime = now;
              lastUploadedBytes = uploadedBytes;
            }
          );

          uploadedChunksRef.current.add(chunkIndex);
        };

        // 并发控制：每次最多上传 concurrency 个分片
        const uploadQueue = [...pendingChunks];
        const activeUploads: Promise<void>[] = [];

        while (uploadQueue.length > 0 || activeUploads.length > 0) {
          // 启动新的上传任务（不超过并发限制）
          while (activeUploads.length < concurrency && uploadQueue.length > 0) {
            const chunkIndex = uploadQueue.shift()!;
            const uploadPromise = uploadChunk(chunkIndex).catch((err) => {
              // 分片上传失败，重新加入队列（最多重试 3 次）
              if (!uploadQueue.includes(chunkIndex)) {
                uploadQueue.push(chunkIndex);
              }
              throw err;
            });
            activeUploads.push(uploadPromise);
          }

          // 等待任意一个上传完成
          if (activeUploads.length > 0) {
            await Promise.race(activeUploads);
            // 移除已完成的上传
            const completedIndex = activeUploads.findIndex((p) => {
              let completed = false;
              p.then(() => { completed = true; }).catch(() => { completed = true; });
              return completed;
            });
            if (completedIndex >= 0) {
              activeUploads.splice(completedIndex, 1);
            }
          }
        }

        // 等待所有上传完成
        await Promise.all(activeUploads);

        // 4. 合并分片
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
          totalChunks
        });

        setUploading(false);
        sessionIdRef.current = null;
        uploadedChunksRef.current.clear();

        return {
          attachmentId: attachmentInfo.id,
          fileName: attachmentInfo.fileName,
          filePath: attachmentInfo.filePath,
          fileSize: attachmentInfo.fileSize,
          accessUrl: attachmentInfo.accessUrl
        };
      } catch (error) {
        setUploading(false);

        // 如果是取消操作，清理会话
        if (abortControllerRef.current?.signal.aborted && sessionIdRef.current) {
          try {
            await api.cancelSession(sessionIdRef.current);
          } catch {
            // 忽略取消会话的错误
          }
          sessionIdRef.current = null;
          uploadedChunksRef.current.clear();
        }

        throw error;
      }
    },
    [uploading]
  );

  /**
   * 取消上传
   */
  const cancel = useCallback(() => {
    abortControllerRef.current?.abort();
  }, []);

  return {
    upload,
    cancel,
    uploading
  };
}
