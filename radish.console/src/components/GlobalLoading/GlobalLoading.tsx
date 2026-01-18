import { useLoading } from '../../contexts/LoadingContext';
import './GlobalLoading.css';

/**
 * 全局加载指示器
 *
 * 显示全屏加载状态
 */
export function GlobalLoading() {
  const { loading, loadingText } = useLoading();

  if (!loading) {
    return null;
  }

  return (
    <div className="global-loading-overlay">
      <div className="global-loading-content">
        <div className="global-loading-spinner" />
        <div className="global-loading-text">{loadingText}</div>
      </div>
    </div>
  );
}
