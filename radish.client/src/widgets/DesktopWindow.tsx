import { Rnd } from 'react-rnd';
import { useWindowStore } from '@/stores/windowStore';
import { getAppById } from '@/desktop/AppRegistry';
import type { WindowState } from '@/desktop/types';
import styles from './DesktopWindow.module.css';

export interface DesktopWindowProps {
  /** 窗口状态 */
  window: WindowState;
}

/**
 * 桌面窗口组件
 *
 * 支持拖拽和调整大小
 */
export const DesktopWindow = ({ window }: DesktopWindowProps) => {
  const { closeWindow, minimizeWindow, focusWindow, updateWindowPosition, updateWindowSize, maximizeWindow, unmaximizeWindow } = useWindowStore();
  const app = getAppById(window.appId);

  if (!app) {
    return null;
  }

  const AppComponent = app.component;
  const defaultWidth = app.defaultSize?.width || 800;
  const defaultHeight = app.defaultSize?.height || 600;

  return (
    <Rnd
      default={{
        x: 100 + (window.zIndex - 1) * 30,
        y: 80 + (window.zIndex - 1) * 30,
        width: defaultWidth,
        height: defaultHeight
      }}
      size={window.size}
      position={window.position}
      minWidth={400}
      minHeight={300}
      bounds="parent"
      dragHandleClassName="window-drag-handle"
      style={{ zIndex: window.zIndex }}
      onMouseDown={() => focusWindow(window.id)}
      disableDragging={window.isMaximized}
      enableResizing={!window.isMaximized}
      onDragStop={(_e, d) => {
        updateWindowPosition(window.id, { x: d.x, y: d.y });
      }}
      onResizeStop={(_e, _direction, ref, _delta, position) => {
        updateWindowSize(window.id, {
          width: parseInt(ref.style.width),
          height: parseInt(ref.style.height)
        });
        updateWindowPosition(window.id, position);
      }}
    >
      <div className={styles.window}>
        {/* 窗口标题栏 */}
        <div className={`${styles.titleBar} window-drag-handle`}>
          <span className={styles.title}>{app.name}</span>
          <div className={styles.controls}>
            <button
              className={`${styles.controlBtn} ${styles.minimizeBtn}`}
              onClick={() => minimizeWindow(window.id)}
              title="最小化"
            />
            <button
              className={`${styles.controlBtn} ${styles.maximizeBtn}`}
              onClick={() => {
                const parentWidth = window.innerWidth || 0;
                const parentHeight = window.innerHeight || 0;
                const dockHeight = 80; // Dock 高度（Dock.module.css 中为 80px）
                const padding = 16; // 与边缘留一点距离，避免贴边

                if (window.isMaximized) {
                  unmaximizeWindow(window.id);
                  return;
                }

                maximizeWindow(window.id, {
                  width: parentWidth - padding * 2,
                  height: parentHeight - dockHeight - padding * 2,
                  x: padding,
                  y: padding
                });
              }}
              title={window.isMaximized ? '还原' : '最大化'}
            />
            <button
              className={`${styles.controlBtn} ${styles.closeBtn}`}
              onClick={() => closeWindow(window.id)}
              title="关闭"
            />
          </div>
        </div>

        {/* 窗口内容区 */}
        <div className={styles.content}>
          <AppComponent />
        </div>
      </div>
    </Rnd>
  );
};
