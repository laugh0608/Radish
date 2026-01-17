import { Component, type ReactNode, type ErrorInfo } from 'react';
import { log } from '@/utils/logger';
import { AntButton, Result } from '@radish/ui';

interface ErrorBoundaryProps {
  children: ReactNode;
  /**
   * 自定义错误回退 UI
   */
  fallback?: (error: Error, errorInfo: ErrorInfo, reset: () => void) => ReactNode;
  /**
   * 错误回调
   */
  onError?: (error: Error, errorInfo: ErrorInfo) => void;
}

interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
  errorInfo: ErrorInfo | null;
}

/**
 * 错误边界组件
 *
 * 捕获子组件树中的 JavaScript 错误，记录错误并显示备用 UI
 *
 * @example
 * ```tsx
 * <ErrorBoundary>
 *   <YourApp />
 * </ErrorBoundary>
 * ```
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
      errorInfo: null,
    };
  }

  static getDerivedStateFromError(error: Error): Partial<ErrorBoundaryState> {
    return {
      hasError: true,
      error,
    };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    // 记录错误信息
    log.error('ErrorBoundary caught an error:', error, errorInfo);

    // 更新状态
    this.setState({
      error,
      errorInfo,
    });

    // 调用错误回调
    this.props.onError?.(error, errorInfo);

    // TODO: 可以在这里上报错误到监控系统
    // reportErrorToService(error, errorInfo);
  }

  handleReset = (): void => {
    this.setState({
      hasError: false,
      error: null,
      errorInfo: null,
    });
  };

  render(): ReactNode {
    const { hasError, error, errorInfo } = this.state;
    const { children, fallback } = this.props;

    if (hasError && error && errorInfo) {
      // 如果提供了自定义 fallback，使用自定义 UI
      if (fallback) {
        return fallback(error, errorInfo, this.handleReset);
      }

      // 默认错误 UI
      return (
        <div style={{ padding: '48px', maxWidth: '600px', margin: '0 auto' }}>
          <Result
            status="error"
            title="页面出错了"
            subTitle="抱歉，页面遇到了一些问题。您可以尝试刷新页面或返回首页。"
            extra={[
              <AntButton type="primary" key="home" onClick={() => window.location.href = '/'}>
                返回首页
              </AntButton>,
              <AntButton key="reload" onClick={() => window.location.reload()}>
                刷新页面
              </AntButton>,
              <AntButton key="reset" onClick={this.handleReset}>
                重试
              </AntButton>,
            ]}
          />

          {/* 开发环境显示错误详情 */}
          {import.meta.env.DEV && (
            <details style={{ marginTop: '24px', whiteSpace: 'pre-wrap' }}>
              <summary style={{ cursor: 'pointer', fontWeight: 'bold', marginBottom: '8px' }}>
                错误详情 (仅开发环境显示)
              </summary>
              <div style={{
                background: '#f5f5f5',
                padding: '16px',
                borderRadius: '4px',
                fontSize: '12px',
                fontFamily: 'monospace',
              }}>
                <div style={{ marginBottom: '12px' }}>
                  <strong>错误信息:</strong>
                  <div style={{ color: '#ff4d4f' }}>{error.toString()}</div>
                </div>
                <div>
                  <strong>组件堆栈:</strong>
                  <div style={{ color: '#666' }}>{errorInfo.componentStack}</div>
                </div>
              </div>
            </details>
          )}
        </div>
      );
    }

    return children;
  }
}
