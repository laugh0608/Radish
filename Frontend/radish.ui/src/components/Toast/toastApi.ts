import type { ToastProps } from './Toast';

export interface ToastEntry {
  id: number;
  props: ToastProps;
}

class ToastManager {
  private toasts: ToastEntry[] = [];
  private listeners: Array<(toasts: ToastEntry[]) => void> = [];
  private nextId = 1;

  show(props: ToastProps) {
    const id = this.nextId++;
    const toast: ToastEntry = {
      id,
      props: {
        ...props,
        onClose: () => {
          this.remove(id);
          props.onClose?.();
        }
      }
    };

    this.toasts = [...this.toasts, toast];
    this.notify();

    return id;
  }

  remove(id: number) {
    this.toasts = this.toasts.filter((toast) => toast.id !== id);
    this.notify();
  }

  subscribe(listener: (toasts: ToastEntry[]) => void) {
    this.listeners.push(listener);
    return () => {
      this.listeners = this.listeners.filter((currentListener) => currentListener !== listener);
    };
  }

  getToasts() {
    return this.toasts;
  }

  private notify() {
    this.listeners.forEach((listener) => listener(this.toasts));
  }
}

export const toastManager = new ToastManager();

export const toast = {
  success: (message: React.ReactNode, duration?: number) =>
    toastManager.show({ message, type: 'success', duration }),
  error: (message: React.ReactNode, duration?: number) =>
    toastManager.show({ message, type: 'error', duration }),
  info: (message: React.ReactNode, duration?: number) =>
    toastManager.show({ message, type: 'info', duration }),
  warning: (message: React.ReactNode, duration?: number) =>
    toastManager.show({ message, type: 'warning', duration }),
  custom: (props: ToastProps) => toastManager.show(props)
};
