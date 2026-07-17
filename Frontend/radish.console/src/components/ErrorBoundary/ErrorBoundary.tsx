import { Component, type ReactNode, type ErrorInfo } from 'react';
import { log } from '@/utils/logger';
import { AntButton, Result } from '@radish/ui';
import i18n from '@/i18n';

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
    i18n.t('console.errorBoundary.diagnosticId', { value: diagnosticId }),
    i18n.t('console.errorBoundary.occurredAt', { value: new Date().toISOString() }),
    i18n.t('console.errorBoundary.currentPath', { value: getCurrentRouteSnapshot() }),
    i18n.t('console.errorBoundary.errorName', { value: error.name }),
    i18n.t('console.errorBoundary.errorMessageLine', { value: error.message }),
    i18n.t('console.errorBoundary.componentStackLine'),
    errorInfo.componentStack || i18n.t('console.errorBoundary.componentStackMissing'),
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
            title={i18n.t('console.errorBoundary.title')}
            subTitle={diagnosticId
              ? i18n.t('console.errorBoundary.descriptionWithId', { diagnosticId })
              : i18n.t('console.errorBoundary.description')}
            extra={[
              <AntButton type="primary" key="home" onClick={() => window.location.href = '/console/'}>
                {i18n.t('console.errorBoundary.home')}
              </AntButton>,
              <AntButton key="reload" onClick={() => window.location.reload()}>
                {i18n.t('console.errorBoundary.reload')}
              </AntButton>,
              <AntButton key="reset" onClick={this.handleReset}>
                {i18n.t('console.errorBoundary.retry')}
              </AntButton>,
              <AntButton key="copy" onClick={() => void this.handleCopyDiagnostic()}>
                {diagnosticCopied
                  ? i18n.t('console.errorBoundary.copied')
                  : i18n.t('console.errorBoundary.copy')}
              </AntButton>,
            ]}
          />

          {/* 开发环境显示错误详情 */}
          {import.meta.env.DEV && (
            <details style={{ marginTop: '24px', whiteSpace: 'pre-wrap' }}>
              <summary style={{ cursor: 'pointer', fontWeight: 'bold', marginBottom: '8px' }}>
                {i18n.t('console.errorBoundary.devDetails')}
              </summary>
              <div style={{
                background: '#f5f5f5',
                padding: '16px',
                borderRadius: '4px',
                fontSize: '12px',
                fontFamily: 'monospace',
              }}>
                <div style={{ marginBottom: '12px' }}>
                  <strong>{i18n.t('console.errorBoundary.errorMessage')}</strong>
                  <div style={{ color: '#ff4d4f' }}>{error.toString()}</div>
                </div>
                <div>
                  <strong>{i18n.t('console.errorBoundary.componentStack')}</strong>
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
