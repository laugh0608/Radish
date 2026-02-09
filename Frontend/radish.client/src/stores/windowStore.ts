import { create } from 'zustand';
import type { WindowState } from '@/desktop/types';
import { getAppById } from '@/desktop/AppRegistry';

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

    // 获取应用定义
    const app = getAppById(appId);

    // 如果是外部链接类型，直接在新标签页打开
    if (app?.type === 'external' && app.externalUrl) {
      window.open(app.externalUrl, '_blank');
      return;
    }

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

    // 获取视口尺寸
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    // 获取应用定义以获取默认尺寸
    const defaultWidth = app?.defaultSize?.width || 800;
    const defaultHeight = app?.defaultSize?.height || 600;

    // 根据视口大小计算窗口尺寸
    // 窗口最大不超过视口的 80% 宽度和 85% 高度（留空间给状态栏和 Dock）
    const maxWidth = viewportWidth * 0.80;
    const maxHeight = viewportHeight * 0.85;

    const finalWidth = Math.min(defaultWidth, maxWidth);
    const finalHeight = Math.min(defaultHeight, maxHeight);

    // 创建新窗口，设置初始位置和大小
    const maxZIndex = Math.max(0, ...openWindows.map(w => w.zIndex));
    const offsetIndex = openWindows.length; // 用于错开窗口位置
    const newWindow: WindowState = {
      id: `win-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
      appId,
      zIndex: maxZIndex + 1,
      isMinimized: false,
      isMaximized: false,
      position: {
        x: 100 + offsetIndex * 30,
        y: 80 + offsetIndex * 30
      },
      size: {
        width: finalWidth,
        height: finalHeight
      }
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
