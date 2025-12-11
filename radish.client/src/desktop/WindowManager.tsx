import { useWindowStore } from '@/stores/windowStore';
import { DesktopWindow } from '@/widgets/DesktopWindow';

/**
 * 窗口管理器
 *
 * 负责渲染所有打开的窗口
 */
export const WindowManager = () => {
  const { openWindows } = useWindowStore();

  return (
    <>
      {openWindows.map(window => {
        // 不渲染最小化的窗口
        if (window.isMinimized) {
          return null;
        }

        return (
          <DesktopWindow
            key={window.id}
            window={window}
          />
        );
      })}
    </>
  );
};
