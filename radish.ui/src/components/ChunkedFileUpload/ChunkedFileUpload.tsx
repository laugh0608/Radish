import React, { useState, useCallback, useRef } from 'react';
import { Button } from '../Button/Button';
import { Icon } from '../Icon/Icon';
import './ChunkedFileUpload.module.css';

/**
 * 分片上传选项
 */
export interface ChunkedUploadOptions {
  /**
   * 分片大小（字节，默认 2MB）
   */
  chunkSize?: number;

  /**
   * 并发上传数（默认 5）
   */
  concurrency?: number;

  /**
   * 业务类型
   */
  businessType?: 'General' | 'Post' | 'Comment' | 'Avatar' | 'Document';

  /**
   * 业务 ID
   */
  businessId?: number | string;

  /**
   * 是否生成缩略图
   */
  generateThumbnail?: boolean;

  /**
   * 是否生成多尺寸
   */
  generateMultipleSizes?: boolean;

  /**
   * 是否添加水印
   */
  addWatermark?: boolean;

  /**
   * 水印文本
   */
  watermarkText?: string;

  /**
   * 是否移除 EXIF
   */
  removeExif?: boolean;
}

/**
 * 分片上传结果
 */
export interface ChunkedUploadResult {
  /**
   * 附件 ID
   */
  attachmentId: number;

  /**
   * 文件名
   */  fileName: string;

  /**
   * 文件路径
   */
  filePath: string;

  /**
   * 文件大小
   */
  fileSize: number;

  /**
   * 访问 URL
   */
  accessUrl: string;
}

/**
 * 上传进度信息
 */
export interface UploadProgress {
  /**
   * 已上传字节数
   */
  uploadedBytes: number;

  /**
   * 总字节数
   */
  totalBytes: number;

  /**
   * 进度百分比 (0-100)
   */
  percentage: number;

  /**
   * 上传速度 (字节/秒)
   */
  speed: number;

  /**
   * 剩余时间 (秒)
   */
  remainingTime: number;

  /**
   * 已上传分片数
   */
  uploadedChunks: number;

  /**
   * 总分片数
   */
  totalChunks: number;
}

/**
 * ChunkedFileUpload 组件 Props
 */
export interface ChunkedFileUploadProps {
  /**
   * 文件对象
   */
  file: File;

  /**
   * 上传选项
   */
  options?: ChunkedUploadOptions;

  /**
   * 上传函数（由外部提供，调用 API）
   */
  onUpload: (
    file: File,
    options: ChunkedUploadOptions,
    onProgress: (progress: UploadProgress) => void
  ) => Promise<ChunkedUploadResult>;

  /**
   * 上传完成回调
   */
  onComplete?: (result: ChunkedUploadResult) => void;

  /**
   * 上传失败回调
   */
  onError?: (error: Error) => void;

  /**
   * 取消上传回调
   */
  onCancel?: () => void;

  /**
   * 是否自动开始上传
   */
  autoStart?: boolean;

  /**
   * 显示详细进度信息
   */
  showDetailedProgress?: boolean;
}

/**
 * 格式化文件大小
 */
function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${(bytes / Math.pow(k, i)).toFixed(2)} ${sizes[i]}`;
}

/**
 * 格式化时间
 */
function formatTime(seconds: number): string {
  if (!isFinite(seconds) || seconds < 0) return '--:--';
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = Math.floor(seconds % 60);
  if (h > 0) {
    return `${h}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  }
  return `${m}:${s.toString().padStart(2, '0')}`;
}

/**
 * 分片文件上传组件
 *
 * 支持大文件分片上传、并发控制、暂停/恢复、进度显示
 */
export const ChunkedFileUpload: React.FC<ChunkedFileUploadProps> = ({
  file,
  options = {},
  onUpload,
  onComplete,
  onError,
  onCancel,
  autoStart = false,
  showDetailedProgress = true
}) => {
  const [status, setStatus] = useState<'idle' | 'uploading' | 'paused' | 'completed' | 'error'>('idle');
  const [progress, setProgress] = useState<UploadProgress>({
    uploadedBytes: 0,
    totalBytes: file.size,
    percentage: 0,
    speed: 0,
    remainingTime: 0,
    uploadedChunks: 0,
    totalChunks: Math.ceil(file.size / (options.chunkSize || 2 * 1024 * 1024))
  });
  const [error, setError] = useState<string | null>(null);

  const abortControllerRef = useRef<AbortController | null>(null);
  const isPausedRef = useRef(false);

  /**
   * 开始上传
   */
  const handleStart = useCallback(async () => {
    if (status === 'uploading' || status === 'completed') return;

    setStatus('uploading');
    setError(null);
    isPausedRef.current = false;
    abortControllerRef.current = new AbortController();

    try {
      const uploadResult = await onUpload(file, options, (prog) => {
        if (!isPausedRef.current) {
          setProgress(prog);
        }
      });

      setStatus('completed');
      onComplete?.(uploadResult);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : '上传失败';
      setError(errorMessage);
      setStatus('error');
      onError?.(err instanceof Error ? err : new Error(errorMessage));
    }
  }, [file, options, onUpload, onComplete, onError, status]);

  /**
   * 暂停上传
   */
  const handlePause = useCallback(() => {
    if (status !== 'uploading') return;
    isPausedRef.current = true;
    abortControllerRef.current?.abort();
    setStatus('paused');
  }, [status]);

  /**
   * 恢复上传
   */
  const handleResume = useCallback(() => {
    if (status !== 'paused') return;
    handleStart();
  }, [status, handleStart]);

  /**
   * 取消上传
   */
  const handleCancel = useCallback(() => {
    isPausedRef.current = true;
    abortControllerRef.current?.abort();
    setStatus('idle');
    setProgress({
      uploadedBytes: 0,
      totalBytes: file.size,
      percentage: 0,
      speed: 0,
      remainingTime: 0,
      uploadedChunks: 0,
      totalChunks: Math.ceil(file.size / (options.chunkSize || 2 * 1024 * 1024))
    });
    setError(null);
    onCancel?.();
  }, [file.size, options.chunkSize, onCancel]);

  /**
   * 重试上传
   */
  const handleRetry = useCallback(() => {
    setError(null);
    handleStart();
  }, [handleStart]);

  // 自动开始上传
  React.useEffect(() => {
    if (autoStart && status === 'idle') {
      handleStart();
    }
  }, [autoStart, status, handleStart]);

  return (
    <div className="radish-chunked-upload">
      {/* 文件信息 */}
      <div className="radish-chunked-upload__file-info">
        <Icon icon="mdi:file-outline" size={24} />
        <div className="radish-chunked-upload__file-details">
          <div className="radish-chunked-upload__file-name">{file.name}</div>
          <div className="radish-chunked-upload__file-size">{formatFileSize(file.size)}</div>
        </div>
      </div>

      {/* 进度条 */}
      <div className="radish-chunked-upload__progress-bar">
        <div
          className="radish-chunked-upload__progress-fill"
          style={{ width: `${progress.percentage}%` }}
        />
      </div>

      {/* 进度信息 */}
      <div className="radish-chunked-upload__progress-info">
        <span className="radish-chunked-upload__percentage">
          {progress.percentage.toFixed(1)}%
        </span>
        {showDetailedProgress && status === 'uploading' && (
          <>
            <span className="radish-chunked-upload__speed">
              {formatFileSize(progress.speed)}/s
            </span>
            <span className="radish-chunked-upload__remaining">
              剩余 {formatTime(progress.remainingTime)}
            </span>
            <span className="radish-chunked-upload__chunks">
              {progress.uploadedChunks}/{progress.totalChunks} 分片
            </span>
          </>
        )}
      </div>

      {/* 状态信息 */}
      {status === 'completed' && (
        <div className="radish-chunked-upload__success">
          <Icon icon="mdi:check-circle" color="green" />
          <span>上传完成</span>
        </div>
      )}

      {status === 'error' && error && (
        <div className="radish-chunked-upload__error">
          <Icon icon="mdi:close-circle" color="red" />
          <span>{error}</span>
        </div>
      )}

      {status === 'paused' && (
        <div className="radish-chunked-upload__paused">
          <Icon icon="mdi:pause-circle" color="orange" />
          <span>已暂停</span>
        </div>
      )}

      {/* 操作按钮 */}
      <div className="radish-chunked-upload__actions">
        {status === 'idle' && (
          <Button onClick={handleStart} variant="primary">
            <Icon icon="mdi:upload" size={16} />
            开始上传
          </Button>
        )}

        {status === 'uploading' && (
          <Button onClick={handlePause} variant="secondary">
            <Icon icon="mdi:pause" size={16} />
            暂停
          </Button>
        )}

        {status === 'paused' && (
          <>
            <Button onClick={handleResume} variant="primary">
              <Icon icon="mdi:play" size={16} />
              继续
            </Button>
            <Button onClick={handleCancel} variant="secondary">
              <Icon icon="mdi:close" size={16} />
              取消
            </Button>
          </>
        )}

        {status === 'error' && (
          <>
            <Button onClick={handleRetry} variant="primary">
              <Icon icon="mdi:refresh" size={16} />
              重试
            </Button>
            <Button onClick={handleCancel} variant="secondary">
              <Icon icon="mdi:close" size={16} />
              取消
            </Button>
          </>
        )}

        {(status === 'uploading' || status === 'paused') && (
          <Button onClick={handleCancel} variant="danger">
            <Icon icon="mdi:close" size={16} />
            取消上传
          </Button>
        )}
      </div>
    </div>
  );
};
