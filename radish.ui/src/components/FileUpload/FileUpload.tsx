import { useState, useRef, DragEvent, ChangeEvent } from 'react';
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
   * 上传成功回调
   */
  onUpload?: (file: File, options?: UploadOptions) => Promise<UploadResult>;

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
   * 文件名
   */
  fileName: string;

  /**
   * 文件 URL
   */
  fileUrl: string;

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
}

export const FileUpload = ({
  accept = 'image/*',
  maxSize = 5 * 1024 * 1024, // 5MB
  multiple = false,
  onUpload,
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
    generateMultipleSizes: false
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

    // 生成预览
    const previewUrl = showPreview ? generatePreview(file) : null;

    setState(prev => ({
      ...prev,
      file,
      previewUrl,
      error: null,
      uploading: false,
      progress: 0,
      result: null
    }));

    // 如果提供了上传函数，自动开始上传
    if (onUpload) {
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
      result: null
    });
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  // 重试上传
  const handleRetry = () => {
    if (state.file) {
      startUpload(state.file);
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
              <p className={styles.progressText}>上传中... {state.progress}%</p>
            </div>
          </div>
        )}

        {state.result && (
          <div className={styles.successState}>
            {state.result.thumbnailUrl || state.result.fileUrl ? (
              <img
                src={state.result.thumbnailUrl || state.result.fileUrl}
                alt={state.result.fileName}
                className={styles.preview}
              />
            ) : (
              <Icon icon="mdi:check-circle" size={48} className={styles.successIcon} />
            )}
            <div className={styles.fileDetails}>
              <p className={styles.fileName}>{state.result.fileName}</p>
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
