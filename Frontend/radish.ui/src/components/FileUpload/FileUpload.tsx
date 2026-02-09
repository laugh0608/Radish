import { useState, useRef } from 'react';
import type { DragEvent, ChangeEvent } from 'react';
import { Icon } from '../Icon/Icon';
import styles from './FileUpload.module.css';

export interface FileUploadProps {
  /**
   * 接受的文件类型（MIME 类型或文件扩展名）
   * @example "image/*" or ".jpg,.png,.gif"
   */
  accept?: string;

  /**
   * 最大文件大小（字节）
   * @default 5 * 1024 * 1024 (5MB)
   */
  maxSize?: number;

  /**
   * 是否支持多文件上传
   * @default false
   */
  multiple?: boolean;

  /**
   * 普通上传回调（小文件）
   */
  onUpload?: (file: File, options?: UploadOptions) => Promise<UploadResult>;

  /**
   * 分片上传回调（大文件）
   * 返回 Promise<UploadResult>
   */
  onChunkedUpload?: (file: File, options?: UploadOptions, onProgress?: (progress: ChunkedUploadProgress) => void) => Promise<UploadResult>;

  /**
   * 分片上传阈值（字节）
   * 文件大小超过此值时使用分片上传
   * @default 10 * 1024 * 1024 (10MB)
   */
  chunkedUploadThreshold?: number;

  /**
   * 上传成功后的回调
   */
  onSuccess?: (result: UploadResult) => void;

  /**
   * 上传失败回调
   */
  onError?: (error: Error) => void;

  /**
   * 是否禁用
   * @default false
   */
  disabled?: boolean;

  /**
   * 自定义类名
   */
  className?: string;

  /**
   * 提示文本
   */
  placeholder?: string;

  /**
   * 是否显示预览
   * @default true
   */
  showPreview?: boolean;

  /**
   * 是否显示水印选项（仅图片）
   * @default false
   */
  showWatermarkOption?: boolean;

  /**
   * 是否显示多尺寸选项（仅图片）
   * @default false
   */
  showMultipleSizesOption?: boolean;

  /**
   * 默认水印文本
   * @default "Radish"
   */
  defaultWatermarkText?: string;
}

/**
 * 分片上传进度信息
 */
export interface ChunkedUploadProgress {
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
  speed?: number;

  /**
   * 剩余时间 (秒)
   */
  remainingTime?: number;

  /**
   * 已上传分片数
   */
  uploadedChunks?: number;

  /**
   * 总分片数
   */
  totalChunks?: number;
}

export interface UploadOptions {
  /**
   * 是否添加水印
   */
  addWatermark?: boolean;

  /**
   * 水印文本
   */
  watermarkText?: string;

  /**
   * 是否生成多尺寸
   */
  generateMultipleSizes?: boolean;

  /**
   * 是否生成缩略图
   */
  generateThumbnail?: boolean;

  /**
   * 是否移除 EXIF
   */
  removeExif?: boolean;
}

export interface UploadResult {
  /**
   * 文件 ID
   */
  id: number | string;

  /**
   * 原始文件名
   */
  originalName: string;

  /**
   * 访问 URL
   */
  url: string;

  /**
   * 缩略图 URL（可选）
   */
  thumbnailUrl?: string;

  /**
   * 文件大小（字节）
   */
  fileSize?: number;

  /**
   * MIME 类型
   */
  mimeType?: string;
}

interface UploadState {
  file: File | null;
  previewUrl: string | null;
  uploading: boolean;
  progress: number;
  error: string | null;
  result: UploadResult | null;
  // 上传选项
  addWatermark: boolean;
  watermarkText: string;
  generateMultipleSizes: boolean;
  // 分片上传相关
  isChunkedUpload: boolean;
  chunkedProgress?: ChunkedUploadProgress;
}

export const FileUpload = ({
  accept = 'image/*',
  maxSize = 5 * 1024 * 1024, // 5MB
  chunkedUploadThreshold = 10 * 1024 * 1024, // 10MB
  multiple = false,
  onUpload,
  onChunkedUpload,
  onSuccess,
  onError,
  disabled = false,
  className = '',
  placeholder = '点击或拖拽文件到此处上传',
  showPreview = true,
  showWatermarkOption = false,
  showMultipleSizesOption = false,
  defaultWatermarkText = 'Radish'
}: FileUploadProps) => {
  const [state, setState] = useState<UploadState>({
    file: null,
    previewUrl: null,
    uploading: false,
    progress: 0,
    error: null,
    result: null,
    addWatermark: false,
    watermarkText: defaultWatermarkText,
    generateMultipleSizes: false,
    isChunkedUpload: false,
    chunkedProgress: undefined
  });

  const [isDragging, setIsDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // 格式化文件大小
  const formatFileSize = (bytes: number): string => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`;
  };

  // 格式化速度
  const formatSpeed = (bytesPerSecond: number): string => {
    if (bytesPerSecond === 0) return '0 B/s';
    const k = 1024;
    const sizes = ['B/s', 'KB/s', 'MB/s', 'GB/s'];
    const i = Math.floor(Math.log(bytesPerSecond) / Math.log(k));
    return `${parseFloat((bytesPerSecond / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`;
  };

  // 格式化时间
  const formatTime = (seconds: number): string => {
    if (!isFinite(seconds) || seconds < 0) return '--';
    if (seconds < 60) return `${Math.round(seconds)} 秒`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)} 分 ${Math.round(seconds % 60)} 秒`;
    return `${Math.floor(seconds / 3600)} 小时 ${Math.floor((seconds % 3600) / 60)} 分`;
  };

  // 验证文件
  const validateFile = (file: File): string | null => {
    // 验证文件大小
    if (file.size > maxSize) {
      return `文件大小超过限制（最大 ${formatFileSize(maxSize)}）`;
    }

    // 验证文件类型
    if (accept && accept !== '*') {
      const acceptTypes = accept.split(',').map(t => t.trim());
      const isValid = acceptTypes.some(type => {
        if (type.startsWith('.')) {
          // 扩展名匹配
          return file.name.toLowerCase().endsWith(type.toLowerCase());
        } else if (type.endsWith('/*')) {
          // MIME 类型通配符匹配（如 image/*）
          const prefix = type.split('/')[0];
          return file.type.startsWith(prefix + '/');
        } else {
          // 完整 MIME 类型匹配
          return file.type === type;
        }
      });

      if (!isValid) {
        return `不支持的文件类型（仅支持 ${accept}）`;
      }
    }

    return null;
  };

  // 生成预览 URL
  const generatePreview = (file: File): string | null => {
    if (file.type.startsWith('image/')) {
      return URL.createObjectURL(file);
    }
    return null;
  };

  // 处理文件选择
  const handleFileSelect = async (file: File) => {
    // 验证文件
    const error = validateFile(file);
    if (error) {
      setState(prev => ({ ...prev, error, file: null, previewUrl: null }));
      if (onError) {
        onError(new Error(error));
      }
      return;
    }

    // 判断是否使用分片上传
    const useChunkedUpload = file.size >= chunkedUploadThreshold && !!onChunkedUpload;

    // 生成预览
    const previewUrl = showPreview ? generatePreview(file) : null;

    setState(prev => ({
      ...prev,
      file,
      previewUrl,
      error: null,
      uploading: false,
      progress: 0,
      result: null,
      isChunkedUpload: useChunkedUpload,
      chunkedProgress: undefined
    }));

    // 如果提供了上传函数，自动开始上传
    if (useChunkedUpload) {
      await startChunkedUpload(file);
    } else if (onUpload) {
      await startUpload(file);
    }
  };

  // 开始上传
  const startUpload = async (file: File) => {
    if (!onUpload) return;

    setState(prev => ({ ...prev, uploading: true, progress: 0, error: null }));

    try {
      // 模拟进度更新（实际项目中应使用 XMLHttpRequest 或 axios 的 onUploadProgress）
      const progressInterval = setInterval(() => {
        setState(prev => {
          if (prev.progress >= 90) {
            clearInterval(progressInterval);
            return prev;
          }
          return { ...prev, progress: prev.progress + 10 };
        });
      }, 200);

      // 构建上传选项
      const uploadOptions: UploadOptions = {
        addWatermark: state.addWatermark,
        watermarkText: state.watermarkText,
        generateMultipleSizes: state.generateMultipleSizes,
        generateThumbnail: true,
        removeExif: true
      };

      const result = await onUpload(file, uploadOptions);

      clearInterval(progressInterval);

      setState(prev => ({
        ...prev,
        uploading: false,
        progress: 100,
        result,
        error: null
      }));

      if (onSuccess) {
        onSuccess(result);
      }
    } catch (error) {
      setState(prev => ({
        ...prev,
        uploading: false,
        progress: 0,
        error: error instanceof Error ? error.message : '上传失败'
      }));

      if (onError) {
        onError(error instanceof Error ? error : new Error('上传失败'));
      }
    }
  };

  // 开始分片上传
  const startChunkedUpload = async (file: File) => {
    if (!onChunkedUpload) return;

    setState(prev => ({ ...prev, uploading: true, progress: 0, error: null }));

    try {
      // 构建上传选项
      const uploadOptions: UploadOptions = {
        addWatermark: state.addWatermark,
        watermarkText: state.watermarkText,
        generateMultipleSizes: state.generateMultipleSizes,
        generateThumbnail: true,
        removeExif: true
      };

      const result = await onChunkedUpload(file, uploadOptions, (progress) => {
        setState(prev => ({
          ...prev,
          progress: progress.percentage,
          chunkedProgress: progress
        }));
      });

      setState(prev => ({
        ...prev,
        uploading: false,
        progress: 100,
        result,
        error: null
      }));

      if (onSuccess) {
        onSuccess(result);
      }
    } catch (error) {
      setState(prev => ({
        ...prev,
        uploading: false,
        progress: 0,
        error: error instanceof Error ? error.message : '上传失败'
      }));

      if (onError) {
        onError(error instanceof Error ? error : new Error('上传失败'));
      }
    }
  };

  // 点击上传区域
  const handleClick = () => {
    if (disabled || state.uploading) return;
    fileInputRef.current?.click();
  };

  // 文件输入变化
  const handleFileInputChange = (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      handleFileSelect(file);
    }
  };

  // 拖拽进入
  const handleDragEnter = (e: DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (!disabled && !state.uploading) {
      setIsDragging(true);
    }
  };

  // 拖拽悬停
  const handleDragOver = (e: DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
  };

  // 拖拽离开
  const handleDragLeave = (e: DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
  };

  // 文件放下
  const handleDrop = (e: DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);

    if (disabled || state.uploading) return;

    const file = e.dataTransfer.files?.[0];
    if (file) {
      handleFileSelect(file);
    }
  };

  // 清除文件
  const handleClear = () => {
    if (state.previewUrl) {
      URL.revokeObjectURL(state.previewUrl);
    }
    setState({
      file: null,
      previewUrl: null,
      uploading: false,
      progress: 0,
      error: null,
      result: null,
      addWatermark: false,
      watermarkText: defaultWatermarkText,
      generateMultipleSizes: false,
      isChunkedUpload: false,
      chunkedProgress: undefined
    });
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  // 重试上传
  const handleRetry = () => {
    if (state.file) {
      if (state.isChunkedUpload) {
        startChunkedUpload(state.file);
      } else {
        startUpload(state.file);
      }
    }
  };

  return (
    <div className={`${styles.container} ${className}`}>
      <input
        ref={fileInputRef}
        type="file"
        accept={accept}
        multiple={multiple}
        onChange={handleFileInputChange}
        className={styles.fileInput}
        disabled={disabled}
      />

      <div
        className={`
          ${styles.uploadArea}
          ${isDragging ? styles.dragging : ''}
          ${disabled ? styles.disabled : ''}
          ${state.uploading ? styles.uploading : ''}
          ${state.error ? styles.error : ''}
        `}
        onClick={handleClick}
        onDragEnter={handleDragEnter}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
      >
        {!state.file && !state.uploading && !state.result && (
          <div className={styles.placeholder}>
            <Icon icon="mdi:cloud-upload-outline" size={48} className={styles.uploadIcon} />
            <p className={styles.placeholderText}>{placeholder}</p>
            <p className={styles.placeholderHint}>
              支持 {accept}，最大 {formatFileSize(maxSize)}
            </p>
          </div>
        )}

        {state.file && !state.uploading && !state.result && (
          <div className={styles.fileInfo}>
            {state.previewUrl ? (
              <img src={state.previewUrl} alt="预览" className={styles.preview} />
            ) : (
              <Icon icon="mdi:file-outline" size={48} className={styles.fileIcon} />
            )}
            <div className={styles.fileDetails}>
              <p className={styles.fileName}>{state.file.name}</p>
              <p className={styles.fileSize}>{formatFileSize(state.file.size)}</p>

              {/* 图片上传选项 */}
              {state.file.type.startsWith('image/') && (showWatermarkOption || showMultipleSizesOption) && (
                <div className={styles.uploadOptions}>
                  {showWatermarkOption && (
                    <label className={styles.optionLabel}>
                      <input
                        type="checkbox"
                        checked={state.addWatermark}
                        onChange={(e) => {
                          e.stopPropagation();
                          setState(prev => ({ ...prev, addWatermark: e.target.checked }));
                        }}
                        className={styles.checkbox}
                      />
                      <span>添加水印</span>
                    </label>
                  )}

                  {showWatermarkOption && state.addWatermark && (
                    <input
                      type="text"
                      value={state.watermarkText}
                      onChange={(e) => {
                        e.stopPropagation();
                        setState(prev => ({ ...prev, watermarkText: e.target.value }));
                      }}
                      onClick={(e) => e.stopPropagation()}
                      placeholder="水印文本"
                      className={styles.watermarkInput}
                    />
                  )}

                  {showMultipleSizesOption && (
                    <label className={styles.optionLabel}>
                      <input
                        type="checkbox"
                        checked={state.generateMultipleSizes}
                        onChange={(e) => {
                          e.stopPropagation();
                          setState(prev => ({ ...prev, generateMultipleSizes: e.target.checked }));
                        }}
                        className={styles.checkbox}
                      />
                      <span>生成多尺寸</span>
                    </label>
                  )}
                </div>
              )}
            </div>
            <button
              type="button"
              className={styles.clearButton}
              onClick={(e) => {
                e.stopPropagation();
                handleClear();
              }}
              title="清除"
            >
              <Icon icon="mdi:close" size={20} />
            </button>
          </div>
        )}

        {state.uploading && (
          <div className={styles.uploadingState}>
            {state.previewUrl && (
              <img src={state.previewUrl} alt="上传中" className={styles.preview} />
            )}
            <div className={styles.progressWrapper}>
              <div className={styles.progressBar}>
                <div
                  className={styles.progressFill}
                  style={{ width: `${state.progress}%` }}
                />
              </div>
              <p className={styles.progressText}>
                上传中... {Math.round(state.progress)}%
              </p>
              {/* 分片上传详细进度 */}
              {state.isChunkedUpload && state.chunkedProgress && (
                <div className={styles.chunkedProgressDetails}>
                  {state.chunkedProgress.speed !== undefined && state.chunkedProgress.speed > 0 && (
                    <span className={styles.progressDetail}>
                      速度: {formatSpeed(state.chunkedProgress.speed)}
                    </span>
                  )}
                  {state.chunkedProgress.remainingTime !== undefined && state.chunkedProgress.remainingTime > 0 && (
                    <span className={styles.progressDetail}>
                      剩余: {formatTime(state.chunkedProgress.remainingTime)}
                    </span>
                  )}
                  {state.chunkedProgress.uploadedChunks !== undefined && state.chunkedProgress.totalChunks !== undefined && (
                    <span className={styles.progressDetail}>
                      分片: {state.chunkedProgress.uploadedChunks}/{state.chunkedProgress.totalChunks}
                    </span>
                  )}
                </div>
              )}
            </div>
          </div>
        )}

        {state.result && (
          <div className={styles.successState}>
            {state.result.thumbnailUrl || state.result.url ? (
              <img
                src={state.result.thumbnailUrl || state.result.url}
                alt={state.result.originalName}
                className={styles.preview}
              />
            ) : (
              <Icon icon="mdi:check-circle" size={48} className={styles.successIcon} />
            )}
            <div className={styles.fileDetails}>
              <p className={styles.fileName}>{state.result.originalName}</p>
              {state.result.fileSize && (
                <p className={styles.fileSize}>{formatFileSize(state.result.fileSize)}</p>
              )}
            </div>
            <button
              type="button"
              className={styles.clearButton}
              onClick={(e) => {
                e.stopPropagation();
                handleClear();
              }}
              title="清除"
            >
              <Icon icon="mdi:close" size={20} />
            </button>
          </div>
        )}
      </div>

      {state.error && (
        <div className={styles.errorMessage}>
          <Icon icon="mdi:alert-circle" size={16} />
          <span>{state.error}</span>
          {state.file && (
            <button
              type="button"
              className={styles.retryButton}
              onClick={handleRetry}
            >
              重试
            </button>
          )}
        </div>
      )}
    </div>
  );
};
