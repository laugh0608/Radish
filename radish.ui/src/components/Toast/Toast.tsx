import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import styles from './Toast.module.css';

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
  const [visible, setVisible] = useState(true);

  useEffect(() => {
    if (duration > 0) {
      const timer = setTimeout(() => {
        setVisible(false);
        setTimeout(() => {
          onClose?.();
        }, 300); // 等待动画结束
      }, duration);

      return () => clearTimeout(timer);
    }
  }, [duration, onClose]);

  if (!visible) {
    return null;
  }

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
    <div className={`${styles.toast} ${styles[type]} ${!visible ? styles.fadeOut : ''}`}>
      <span className={styles.icon}>
        {icon || getDefaultIcon()}
      </span>
      <span className={styles.message}>{message}</span>
    </div>
  );
};

// Toast 管理器
class ToastManager {
  private toasts: Array<{
    id: number;
    props: ToastProps;
  }> = [];
  private listeners: Array<(toasts: typeof this.toasts) => void> = [];
  private nextId = 1;

  show(props: ToastProps) {
    const id = this.nextId++;
    const toast = {
      id,
      props: {
        ...props,
        onClose: () => {
          this.remove(id);
          props.onClose?.();
        }
      }
    };

    // 创建新数组引用，确保 React 能检测到变化
    this.toasts = [...this.toasts, toast];
    this.notify();

    return id;
  }

  remove(id: number) {
    this.toasts = this.toasts.filter(t => t.id !== id);
    this.notify();
  }

  subscribe(listener: (toasts: typeof this.toasts) => void) {
    this.listeners.push(listener);
    return () => {
      this.listeners = this.listeners.filter(l => l !== listener);
    };
  }

  getToasts() {
    return this.toasts;
  }

  private notify() {
    this.listeners.forEach(listener => listener(this.toasts));
  }
}

export const toastManager = new ToastManager();

/**
 * Toast 容器组件
 *
 * 管理所有 Toast 的显示
 */
export const ToastContainer = () => {
  const [toasts, setToasts] = useState(toastManager.getToasts());

  useEffect(() => {
    const unsubscribe = toastManager.subscribe(setToasts);
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

/**
 * Toast 快捷方法
 */
export const toast = {
  success: (message: React.ReactNode, duration?: number) =>
    toastManager.show({ message, type: 'success', duration }),
  error: (message: React.ReactNode, duration?: number) =>
    toastManager.show({ message, type: 'error', duration }),
  info: (message: React.ReactNode, duration?: number) =>
    toastManager.show({ message, type: 'info', duration }),
  warning: (message: React.ReactNode, duration?: number) =>
    toastManager.show({ message, type: 'warning', duration }),
  custom: (props: ToastProps) =>
    toastManager.show(props)
};
