import { create } from 'zustand';
import type { WindowState } from '@/desktop/types';

interface WindowStore {
  /** 打开的窗口列表 */
  openWindows: WindowState[];

  /** 打开应用 */
  openApp: (appId: string) => void;

  /** 关闭窗口 */
  closeWindow: (windowId: string) => void;

  /** 最小化窗口 */
  minimizeWindow: (windowId: string) => void;

  /** 恢复窗口 */
  restoreWindow: (windowId: string) => void;

  /** 聚焦窗口 */
  focusWindow: (windowId: string) => void;

  /** 更新窗口位置 */
  updateWindowPosition: (windowId: string, position: { x: number; y: number }) => void;

  /** 更新窗口大小 */
  updateWindowSize: (windowId: string, size: { width: number; height: number }) => void;

  /** 最大化窗口 */
  maximizeWindow: (windowId: string, payload: { width: number; height: number; x: number; y: number }) => void;

  /** 退出最大化 */
  unmaximizeWindow: (windowId: string) => void;
}

export const useWindowStore = create<WindowStore>((set, get) => ({
  openWindows: [],

  openApp: (appId: string) => {
    const { openWindows } = get();

    // 如果应用已打开，聚焦窗口
    const existingWindow = openWindows.find(w => w.appId === appId);
    if (existingWindow) {
      get().focusWindow(existingWindow.id);
      // 如果是最小化状态，恢复窗口
      if (existingWindow.isMinimized) {
        get().restoreWindow(existingWindow.id);
      }
      return;
    }

    // 创建新窗口
    const maxZIndex = Math.max(0, ...openWindows.map(w => w.zIndex));
    const newWindow: WindowState = {
      id: `win-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
      appId,
      zIndex: maxZIndex + 1,
      isMinimized: false,
      isMaximized: false
    };

    set({ openWindows: [...openWindows, newWindow] });
  },

  closeWindow: (windowId: string) => {
    set(state => ({
      openWindows: state.openWindows.filter(w => w.id !== windowId)
    }));
  },

  minimizeWindow: (windowId: string) => {
    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId ? { ...w, isMinimized: true } : w
      )
    }));
  },

  restoreWindow: (windowId: string) => {
    const { openWindows } = get();
    const maxZIndex = Math.max(0, ...openWindows.map(w => w.zIndex));

    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId
          ? { ...w, isMinimized: false, zIndex: maxZIndex + 1 }
          : w
      )
    }));
  },

  focusWindow: (windowId: string) => {
    const { openWindows } = get();
    const maxZIndex = Math.max(0, ...openWindows.map(w => w.zIndex));
    const targetWindow = openWindows.find(w => w.id === windowId);

    // 如果已经是最前面的窗口，不需要更新
    if (targetWindow && targetWindow.zIndex === maxZIndex) {
      return;
    }

    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId ? { ...w, zIndex: maxZIndex + 1 } : w
      )
    }));
  },

  updateWindowPosition: (windowId: string, position: { x: number; y: number }) => {
    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId ? { ...w, position } : w
      )
    }));
  },

  updateWindowSize: (windowId: string, size: { width: number; height: number }) => {
    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId ? { ...w, size } : w
      )
    }));
  },

  maximizeWindow: (windowId: string, payload: { width: number; height: number; x: number; y: number }) => {
    const { openWindows } = get();
    const maxZIndex = Math.max(0, ...openWindows.map(w => w.zIndex));

    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId
          ? {
              ...w,
              isMinimized: false,
              isMaximized: true,
              prevPosition: w.position,
              prevSize: w.size,
              position: { x: payload.x, y: payload.y },
              size: { width: payload.width, height: payload.height },
              zIndex: maxZIndex + 1
            }
          : w
      )
    }));
  },

  unmaximizeWindow: (windowId: string) => {
    const { openWindows } = get();
    const maxZIndex = Math.max(0, ...openWindows.map(w => w.zIndex));

    set(state => ({
      openWindows: state.openWindows.map(w =>
        w.id === windowId
          ? {
              ...w,
              isMaximized: false,
              position: w.prevPosition ?? w.position,
              size: w.prevSize ?? w.size,
              zIndex: maxZIndex + 1
            }
          : w
      )
    }));
  }
}));
