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
  diagnosticId: string | null;
  diagnosticCopied: boolean;
}

function createDiagnosticId(): string {
  return `CONSOLE-${Date.now().toString(36).toUpperCase()}-${Math.random().toString(36).slice(2, 7).toUpperCase()}`;
}

function getCurrentRouteSnapshot(): string {
  if (typeof window === 'undefined') {
    return 'unknown';
  }

  return `${window.location.pathname}${window.location.search}${window.location.hash}`;
}

function buildDiagnosticText(error: Error, errorInfo: ErrorInfo, diagnosticId: string): string {
  return [
    `诊断编号：${diagnosticId}`,
    `发生时间：${new Date().toISOString()}`,
    `当前路径：${getCurrentRouteSnapshot()}`,
    `错误名称：${error.name}`,
    `错误信息：${error.message}`,
    '组件堆栈：',
    errorInfo.componentStack || '未提供组件堆栈',
  ].join('\n');
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
      diagnosticId: null,
      diagnosticCopied: false,
    };
  }

  static getDerivedStateFromError(error: Error): Partial<ErrorBoundaryState> {
    return {
      hasError: true,
      error,
    };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    const diagnosticId = createDiagnosticId();
    log.error('ErrorBoundary', 'Console 页面异常', {
      diagnosticId,
      route: getCurrentRouteSnapshot(),
      error,
      errorInfo,
    });

    this.setState({
      error,
      errorInfo,
      diagnosticId,
      diagnosticCopied: false,
    });

    this.props.onError?.(error, errorInfo);
  }

  handleReset = (): void => {
    this.setState({
      hasError: false,
      error: null,
      errorInfo: null,
      diagnosticId: null,
      diagnosticCopied: false,
    });
  };

  handleCopyDiagnostic = async (): Promise<void> => {
    const { error, errorInfo, diagnosticId } = this.state;
    if (!error || !errorInfo || !diagnosticId) {
      return;
    }

    try {
      await navigator.clipboard.writeText(buildDiagnosticText(error, errorInfo, diagnosticId));
      this.setState({ diagnosticCopied: true });
    } catch (copyError) {
      this.setState({ diagnosticCopied: false });
      log.warn('ErrorBoundary', '复制 Console 异常诊断信息失败', copyError);
    }
  };

  render(): ReactNode {
    const { hasError, error, errorInfo, diagnosticId, diagnosticCopied } = this.state;
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
            subTitle={diagnosticId
              ? `页面遇到异常。请先尝试重试或刷新；如需反馈，请附上诊断编号 ${diagnosticId}。`
              : '页面遇到异常。请先尝试重试或刷新；如需反馈，请附上当前路径与操作时间。'}
            extra={[
              <AntButton type="primary" key="home" onClick={() => window.location.href = '/console/'}>
                返回首页
              </AntButton>,
              <AntButton key="reload" onClick={() => window.location.reload()}>
                刷新页面
              </AntButton>,
              <AntButton key="reset" onClick={this.handleReset}>
                重试
              </AntButton>,
              <AntButton key="copy" onClick={() => void this.handleCopyDiagnostic()}>
                {diagnosticCopied ? '诊断信息已复制' : '复制诊断信息'}
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
