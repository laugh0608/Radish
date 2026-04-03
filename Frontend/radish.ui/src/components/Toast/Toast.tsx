import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import styles from './Toast.module.css';
import { toastManager } from './toastApi';

const EXIT_ANIMATION_DURATION = 300;

export interface ToastProps {
  /** 通知内容 */
  message: React.ReactNode;
  /** 通知类型 */
  type?: 'success' | 'error' | 'info' | 'warning';
  /** 显示时长（毫秒），0 表示不自动关闭 */
  duration?: number;
  /** 关闭回调 */
  onClose?: () => void;
  /** 自定义图标 */
  icon?: React.ReactNode;
}

/**
 * Toast 通知组件
 *
 * 轻量级通知提示，用于显示临时消息
 */
export const Toast = ({
  message,
  type = 'info',
  duration = 3000,
  onClose,
  icon
}: ToastProps) => {
  const [isClosing, setIsClosing] = useState(false);

  useEffect(() => {
    if (duration <= 0) {
      return undefined;
    }

    const timer = window.setTimeout(() => {
      setIsClosing(true);
    }, duration);

    return () => window.clearTimeout(timer);
  }, [duration]);

  useEffect(() => {
    if (!isClosing) {
      return undefined;
    }

    const timer = window.setTimeout(() => {
      onClose?.();
    }, EXIT_ANIMATION_DURATION);

    return () => window.clearTimeout(timer);
  }, [isClosing, onClose]);

  const getDefaultIcon = () => {
    switch (type) {
      case 'success':
        return '✓';
      case 'error':
        return '✕';
      case 'warning':
        return '⚠';
      default:
        return 'ℹ';
    }
  };

  return (
    <div
      className={`${styles.toast} ${styles[type]} ${isClosing ? styles.fadeOut : ''}`}
      role="status"
    >
      <span className={styles.icon}>
        {icon || getDefaultIcon()}
      </span>
      <span className={styles.message}>{message}</span>
      {duration > 0 && (
        <span className={styles.progressTrack} aria-hidden="true">
          <span
            className={styles.progressBar}
            style={{ animationDuration: `${duration}ms` }}
          />
        </span>
      )}
    </div>
  );
};

/**
 * Toast 容器组件
 *
 * 管理所有 Toast 的显示
 */
export const ToastContainer = () => {
  const [toasts, setToasts] = useState(() => toastManager.getToasts());

  useEffect(() => {
    const unsubscribe = toastManager.subscribe((nextToasts) => {
      setToasts(nextToasts);
    });
    return unsubscribe;
  }, []);

  return createPortal(
    <div className={styles.toastContainer}>
      {toasts.map(({ id, props }) => (
        <Toast key={id} {...props} />
      ))}
    </div>,
    document.body
  );
};
